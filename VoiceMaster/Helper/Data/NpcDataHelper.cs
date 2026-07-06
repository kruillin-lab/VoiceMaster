using Dalamud.Plugin.Services;
using VoiceMaster.DataClasses;
using VoiceMaster.Enums;
using VoiceMaster.Helper.API;
using VoiceMaster.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace VoiceMaster.Helper.Data
{
    public static class NpcDataHelper
    {
        public static bool IsGenderedRace(NpcRaces race)
        {
            if (((int)race > 0 && (int)race < 9) || JsonLoaderHelper.ModelGenderMap.Find(p => p.race == race) != null)
                return true;
                
            return false;
        }

        public static void ReSetVoiceRaces(VoiceMasterVoice voice, EKEventId? eventId = null)
        {
            if (eventId == null)
                eventId = new EKEventId(0, TextSource.None);
            
            voice.AllowedRaces.Clear();
            string[] splitVoice = voice.voiceName.Split('_');

            foreach (var split in splitVoice)
            {
                var racesStr = split;
                var raceStrArr = racesStr.Split('-');
                foreach (var raceStr in raceStrArr)
                {
                    if (Enum.TryParse(typeof(NpcRaces), raceStr, true, out object? race))
                    {
                        voice.AllowedRaces.Add((NpcRaces)race);
                        LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Found {race} race", eventId);
                    }
                    else if (raceStr.Equals("Child", StringComparison.InvariantCultureIgnoreCase))
                    {
                        voice.IsChildVoice = true;
                        LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Found Child option", eventId);
                    }
                    else if (raceStr.Equals("All", StringComparison.InvariantCultureIgnoreCase))
                    {
                        foreach (var raceObj in Constants.RACELIST)
                        {
                            voice.AllowedRaces.Add(raceObj);
                            LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Found {raceObj} race", eventId);
                        }
                    }
                    else
                        LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Did not Find race", eventId);
                }
            }
        }

        public static void ReSetVoiceGenders(VoiceMasterVoice voice, EKEventId? eventId = null)
        {
            if (eventId == null)
                eventId = new EKEventId(0, TextSource.None);
            
            voice.AllowedGenders.Clear();
            string[] splitVoice = voice.voiceName.Split('_');

            foreach (var split in splitVoice)
            {
                var genderStr = split;
                if (Enum.TryParse(typeof(Genders), genderStr, true, out object? gender))
                {
                    LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Found {gender} gender", eventId);
                    voice.AllowedGenders.Add((Genders)gender);
                }
            }
        }

        public static void MigrateOldData(VoiceMasterVoice? oldVoice = null, VoiceMasterVoice? newEkVoice = null)
        {
            if (oldVoice == null)
            {
                var oldPlayerMapData = Plugin.Configuration.MappedPlayers.FindAll(p => p.voiceItem?.Voice != null);
                var oldNpcMapData = Plugin.Configuration.MappedNpcs.FindAll(p => p.voiceItem?.Voice != null);

                if (oldPlayerMapData.Count > 0 || oldNpcMapData.Count > 0)
                {
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Migrating old npcdata",
                                   new EKEventId(0, TextSource.None));

                    foreach (var player in oldPlayerMapData)
                    {
                        if (player.voiceItem == null)
                            continue;

                        player.Voice =
                            Plugin.Configuration.VoiceMasterVoices.Find(p => p.BackendVoice == player.voiceItem.Voice);

                        LogHelper.Debug(MethodBase.GetCurrentMethod().Name,
                                        $"Migrated player {player.Name} from -> {player.voiceItem} to -> {player.Voice}",
                                        new EKEventId(0, TextSource.None));

                        if (player.Voice != null)
                            player.voiceItem = null;
                    }

                    foreach (var npc in oldNpcMapData)
                    {
                        if (npc.voiceItem == null)
                            continue;

                        npc.Voice = Plugin.Configuration.VoiceMasterVoices.Find(p => p.BackendVoice == npc.voiceItem.Voice);

                        LogHelper.Debug(MethodBase.GetCurrentMethod().Name,
                                        $"Migrated npc {npc.Name} from -> {npc.voiceItem} to -> {npc.Voice}",
                                        new EKEventId(0, TextSource.None));

                        if (npc.Voice != null)
                            npc.voiceItem = null;
                    }

                    Plugin.Configuration.Save();
                }
            }
            else 
            {
                var oldPlayerMapData = Plugin.Configuration.MappedPlayers.FindAll(p => p.Voice == oldVoice);
                var oldNpcMapData = Plugin.Configuration.MappedNpcs.FindAll(p => p.Voice == oldVoice);

                if (oldPlayerMapData.Count > 0 || oldNpcMapData.Count > 0)
                {
                    if (newEkVoice != null)
                    {
                        LogHelper.Info(MethodBase.GetCurrentMethod().Name,
                                       $"Migrating old npcdata from {oldVoice} to {newEkVoice}",
                                       new EKEventId(0, TextSource.None));

                        foreach (var player in oldPlayerMapData)
                        {
                            player.Voice = newEkVoice;

                            LogHelper.Debug(MethodBase.GetCurrentMethod().Name,
                                            $"Migrated player {player.Name} from -> {oldVoice} to -> {newEkVoice}",
                                            new EKEventId(0, TextSource.None));
                        }

                        foreach (var npc in oldNpcMapData)
                        {
                            npc.Voice = newEkVoice;

                            LogHelper.Debug(MethodBase.GetCurrentMethod().Name,
                                            $"Migrated npc {npc.Name} from -> {oldVoice} to -> {newEkVoice}",
                                            new EKEventId(0, TextSource.None));
                        }
                    }
                    else
                    {
                        LogHelper.Info(MethodBase.GetCurrentMethod().Name,
                                       $"Migrating old npcdata from {oldVoice} to NO VOICE",
                                       new EKEventId(0, TextSource.None));

                        foreach (var player in oldPlayerMapData)
                        {
                            player.Voice = null;

                            LogHelper.Debug(MethodBase.GetCurrentMethod().Name,
                                            $"Migrated player {player.Name} from -> {oldVoice} to -> NO VOICE",
                                            new EKEventId(0, TextSource.None));
                        }

                        foreach (var npc in oldNpcMapData)
                        {
                            npc.Voice = null;

                            LogHelper.Debug(MethodBase.GetCurrentMethod().Name,
                                            $"Migrated npc {npc.Name} from -> {oldVoice} to -> NO VOICE",
                                            new EKEventId(0, TextSource.None));
                        }
                    }

                    Plugin.Configuration.Save();
                }
            }
        }

        private static List<NpcMapData> GetCharacterMapDatas(EKEventId eventId)
        {
            switch (eventId.textSource)
            {
                case TextSource.AddonTalk:
                case TextSource.AddonBattleTalk:
                case TextSource.AddonBubble:
                    LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Found mapping: {Plugin.Configuration.MappedNpcs} count: {Plugin.Configuration.MappedNpcs.Count()}", eventId);
                    return Plugin.Configuration.MappedNpcs;
                case TextSource.AddonSelectString:
                case TextSource.AddonCutsceneSelectString:
                case TextSource.Chat:
                    LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Found mapping: {Plugin.Configuration.MappedPlayers} count: {Plugin.Configuration.MappedPlayers.Count()}", eventId);
                    return Plugin.Configuration.MappedPlayers;
            }

            LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Didn't find a mapping.", eventId);
            return new List<NpcMapData>();
        }

        public static VoiceMasterVoice GetVoiceByBackendVoice(string backendVoice)
        {
            return Plugin.Configuration.VoiceMasterVoices.Find(p => p.BackendVoice == backendVoice);
        }

        public static void RefreshSelectables(List<VoiceMasterVoice> voices)
        {
            try
            {
                Plugin.Configuration.MappedNpcs.ForEach(p =>
                {
                    p.Voices = voices;
                    p.RefreshSelectable();
                });
                Plugin.Configuration.MappedPlayers.ForEach(p =>
                {
                    p.Voices = voices;
                    p.RefreshSelectable();
                });
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error Exception: {ex}", new EKEventId(0, TextSource.None));
            }
        }

        public static NpcMapData GetAddCharacterMapData(NpcMapData data, EKEventId eventId)
        {
            NpcMapData? result = null;
            var datas = GetCharacterMapDatas(eventId);

            // ---- Stable player mapping (B+) ----
            // Persist player mappings by (Name + HomeWorld) so cross-world / out-of-zone chat does not create new blank mappings.
            if (data.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Pc)
            {
                // Normalize Name@World into Name + HomeWorld if needed.
                if (!string.IsNullOrWhiteSpace(data.Name) && data.Name.Contains('@'))
                {
                    var parts = data.Name.Split('@', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        data.Name = parts[0].Trim();
                        if (string.IsNullOrWhiteSpace(data.HomeWorld))
                            data.HomeWorld = parts[1].Trim();
                    }
                }

                // Prefer exact Name + HomeWorld match when HomeWorld is known.
                if (!string.IsNullOrWhiteSpace(data.HomeWorld))
                {
                    result = datas.Find(p => p.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Pc
                                          && string.Equals(p.Name, data.Name, StringComparison.OrdinalIgnoreCase)
                                          && string.Equals(p.HomeWorld, data.HomeWorld, StringComparison.OrdinalIgnoreCase));

                    // Migrate legacy entries (Name-only) to Name+HomeWorld if we found one.
                    if (result == null)
                    {
                        var legacy = datas.Find(p => p.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Pc
                                                && string.Equals(p.Name, data.Name, StringComparison.OrdinalIgnoreCase)
                                                && string.IsNullOrWhiteSpace(p.HomeWorld));
                        if (legacy != null)
                        {
                            legacy.HomeWorld = data.HomeWorld;
                            result = legacy;
                        }
                    }
                }

                // If HomeWorld is unknown, fall back to Name-only (prefer Name-only entries first).
                if (result == null)
                {
                    result = datas.Find(p => p.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Pc
                                          && string.Equals(p.Name, data.Name, StringComparison.OrdinalIgnoreCase)
                                          && string.IsNullOrWhiteSpace(p.HomeWorld))
                             ?? datas.Find(p => p.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Pc
                                          && string.Equals(p.Name, data.Name, StringComparison.OrdinalIgnoreCase));
                }
            }
            else
            {
                // NPC mapping: match by cleaned name (existing behavior).
                result = datas.Find(p => p.ObjectKind == data.ObjectKind
                                      && string.Equals(p.Name, data.Name, StringComparison.OrdinalIgnoreCase));
            }

            if (result != null)
            {
                // Refresh identity fields when the existing entry is missing info (do NOT clobber user settings).
                if (result.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Pc
                    && string.IsNullOrWhiteSpace(result.HomeWorld)
                    && !string.IsNullOrWhiteSpace(data.HomeWorld))
                    result.HomeWorld = data.HomeWorld;

                if (result.Race == NpcRaces.Unknown && data.Race != NpcRaces.Unknown)
                    result.Race = data.Race;
                if (result.Gender == Genders.None && data.Gender != Genders.None)
                    result.Gender = data.Gender;
                if (!result.IsChild && data.IsChild)
                    result.IsChild = true;

                result.Voices = Plugin.Configuration.VoiceMasterVoices;
                result.RefreshSelectable();
                return result;
            }

            // New mapping: add and assign a voice if needed.
            data.Voices = Plugin.Configuration.VoiceMasterVoices;
            data.RefreshSelectable();
            BackendHelper.GetVoiceOrRandom(eventId, data);
            datas.Add(data);

            ConfigWindow.UpdateDataNpcs = true;
            ConfigWindow.UpdateDataBubbles = true;
            ConfigWindow.UpdateDataPlayers = true;

            var mapping = data.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Pc ? "player" : "npc";
            LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Added new {mapping} to mapping: {data}", eventId);

            return data;
        }
    }
}
