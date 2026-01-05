using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VoiceMaster.DataClasses;
using VoiceMaster.Helper.Data;

namespace VoiceMaster.Helper.DataHelper
{
    public static class LuminaHelper
    {
        private static ushort TerritoryRow;
        private static TerritoryType? Territory;

        public static TerritoryType? GetTerritory()
        {
            var territoryRow = Plugin.ClientState.TerritoryType;
            if (territoryRow != TerritoryRow)
            {
                TerritoryRow = territoryRow;
                Territory = Plugin.DataManager.GetExcelSheet<TerritoryType>()!.GetRow(territoryRow);
            }

            return Territory;
        }

        internal static ENpcBase? GetENpcBase(uint dataId, EKEventId eventId)
        {
            try
            {
                return Plugin.DataManager.GetExcelSheet<ENpcBase>()!.GetRow(dataId);
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error while starting voice inference: {ex}",
                                eventId);
            }

            return null;
        }

        internal static Race? GetRace(byte speakerRace, EKEventId eventId)
        {
            try
            {
                return Plugin.DataManager.GetExcelSheet<Race>()?.GetRow(speakerRace) ?? null;
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error while starting voice inference: {ex}", eventId);
            }

            return null;
        }

        private static List<string>? WorldNamesCache;

        public static List<string> GetWorldNames()
        {
            if (WorldNamesCache != null && WorldNamesCache.Count > 0)
                return WorldNamesCache;

            var names = new List<string>();
            try
            {
                var sheet = Plugin.DataManager.GetExcelSheet<World>();
                if (sheet != null)
                {
                    foreach (var row in sheet)
                    {
                        var name = row.Name.ToString();
                        if (!string.IsNullOrWhiteSpace(name))
                            names.Add(name);
                    }
                }
            }
            catch
            {
                // ignore and fall back to empty list
            }

            WorldNamesCache = names.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();
            // Add a top option so the UI can represent "no specific world" cleanly.
            WorldNamesCache.Insert(0, "(Any)");
            return WorldNamesCache;
        }

        public static string TryGetHomeWorldName(object? player)
        {
            if (player == null)
                return string.Empty;

            try
            {
                var t = player.GetType();

                // Common patterns: HomeWorld, HomeWorldId
                object? hw = t.GetProperty("HomeWorld")?.GetValue(player);
                if (hw == null)
                    hw = t.GetProperty("HomeWorldId")?.GetValue(player);

                if (hw == null)
                    return string.Empty;

                // If it's already a string-ish type, use it
                if (hw is string s)
                    return s;

                // Some APIs expose a World object with Name
                var nameProp = hw.GetType().GetProperty("Name");
                if (nameProp != null)
                {
                    var nameVal = nameProp.GetValue(hw);
                    var nameStr = nameVal?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(nameStr))
                        return nameStr;
                }

                // Try to get an ID (RowId/Value/Id) and look it up via Lumina sheet
                ulong? id = null;
                if (hw is byte b) id = b;
                else if (hw is ushort us) id = us;
                else if (hw is uint ui) id = ui;
                else if (hw is int i) id = (ulong)i;
                else if (hw is long l) id = (ulong)l;
                else if (hw is ulong ul) id = ul;
                else
                {
                    foreach (var propName in new[] { "RowId", "Value", "Id" })
                    {
                        var prop = hw.GetType().GetProperty(propName);
                        if (prop == null)
                            continue;
                        var v = prop.GetValue(hw);
                        if (v is byte bb) { id = bb; break; }
                        if (v is ushort uus) { id = uus; break; }
                        if (v is uint uui) { id = uui; break; }
                        if (v is int ii) { id = (ulong)ii; break; }
                        if (v is ulong uul) { id = uul; break; }
                    }
                }

                if (id != null && id.Value > 0)
                {
                    var sheet = Plugin.DataManager.GetExcelSheet<World>();
                    var row = sheet?.GetRow((uint)id.Value);
                    var rowName = row?.Name.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(rowName))
                        return rowName;
                }
            }
            catch
            {
                // ignored
            }

            return string.Empty;
        }

    }
}
