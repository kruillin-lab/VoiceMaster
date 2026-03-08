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
                var sheet = Plugin.DataManager.GetExcelSheet<TerritoryType>();
                if (sheet != null && sheet.TryGetRow(territoryRow, out var row))
                    Territory = row;
                else
                    Territory = null;
            }

            return Territory;
        }

        internal static ENpcBase? GetENpcBase(uint dataId, EKEventId eventId)
        {
            try
            {
                var sheet = Plugin.DataManager.GetExcelSheet<ENpcBase>();
                if (sheet == null)
                    return null;

                // Validate row exists before accessing it
                if (!sheet.TryGetRow(dataId, out var row))
                {
                    // Silently ignore - happens for transient/invalid NPCs when leaving instances
                    return null;
                }

                return row;
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod()?.Name ?? nameof(GetENpcBase),
                    $"Error while starting voice inference: {ex}", eventId);
            }

            return null;
        }

        internal static Race? GetRace(byte speakerRace, EKEventId eventId)
        {
            try
            {
                var sheet = Plugin.DataManager.GetExcelSheet<Race>();
                if (sheet == null)
                    return null;

                // Validate row exists before accessing it
                if (!sheet.TryGetRow(speakerRace, out var row))
                    return null;

                return row;
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod()?.Name ?? nameof(GetRace),
                    $"Error while starting voice inference: {ex}", eventId);
            }

            return null;
        }

        private static List<string>? WorldNamesCache;

        /// <summary>
        /// Returns world/server names for UI dropdowns. Includes a top "(Any)" option.
        /// Filters out clearly invalid/internal rows where possible, and de-dupes + sorts results.
        /// </summary>
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
                        if (string.IsNullOrWhiteSpace(name))
                            continue;

                        name = name.Trim();

                        // Filter out non-public/internal rows when flags exist.
                        // Reflection keeps this compatible across Lumina versions.
                        var rowType = row.GetType();
                        bool allow = true;

                        allow = allow && GetOptionalBool(rowType, row, "IsPublic", defaultValue: true);
                        allow = allow && GetOptionalBool(rowType, row, "UserActive", defaultValue: true);
                        allow = allow && GetOptionalBool(rowType, row, "IsActive", defaultValue: true);

                        if (!allow)
                            continue;

                        // Extra guard: drop obvious garbage if flags don't exist or leak through.
                        var lower = name.ToLowerInvariant();
                        if (lower.Contains("internal") || lower.Contains("dummy") || lower.Contains("unknown") ||
                            lower.Contains("dev") || lower.Contains("test"))
                            continue;

                        names.Add(name);
                    }
                }
            }
            catch
            {
                // ignore; will fall back to only (Any)
            }

            var distinct = names
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            WorldNamesCache = new List<string> { "(Any)" };
            WorldNamesCache.AddRange(distinct);
            return WorldNamesCache;
        }

        private static bool GetOptionalBool(Type rowType, object row, string propName, bool defaultValue)
        {
            try
            {
                var prop = rowType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.PropertyType == typeof(bool))
                    return (bool)prop.GetValue(row)!;
            }
            catch
            {
                // ignored
            }

            return defaultValue;
        }

        /// <summary>
        /// Attempts to extract a Home World name string from an arbitrary player object via reflection.
        /// Returns empty string if unavailable.
        /// </summary>
        public static string TryGetHomeWorldName(object? player)
        {
            if (player == null)
                return string.Empty;

            try
            {
                var t = player.GetType();

                object? hw = t.GetProperty("HomeWorld")?.GetValue(player);
                if (hw == null)
                    hw = t.GetProperty("HomeWorldId")?.GetValue(player);

                if (hw == null)
                    return string.Empty;

                if (hw is string s)
                    return s.Trim();

                var nameProp = hw.GetType().GetProperty("Name");
                if (nameProp != null)
                {
                    var nameVal = nameProp.GetValue(hw);
                    var nameStr = nameVal?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(nameStr))
                        return nameStr.Trim();
                }

                uint? id = null;
                if (hw is byte b) id = b;
                else if (hw is ushort us) id = us;
                else if (hw is uint ui) id = ui;
                else if (hw is int i) id = (uint)Math.Max(i, 0);
                else
                {
                    foreach (var propName in new[] { "RowId", "Value", "Id" })
                    {
                        var prop = hw.GetType().GetProperty(propName);
                        if (prop == null) continue;
                        var v = prop.GetValue(hw);
                        if (v is byte bb) { id = bb; break; }
                        if (v is ushort uus) { id = uus; break; }
                        if (v is uint uui) { id = uui; break; }
                        if (v is int ii) { id = (uint)Math.Max(ii, 0); break; }
                    }
                }

                if (id != null && id.Value > 0)
                {
                    var sheet = Plugin.DataManager.GetExcelSheet<World>();
                    var row = sheet?.GetRow(id.Value);
                    var rowName = row?.Name.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(rowName))
                        return rowName.Trim();
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
