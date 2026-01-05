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
                var oldPlayerMapData = Plugin.Configuration.MappedPlayers.FindAll(p => p.voiceItem != null);
                var oldNpcMapData = Plugin.Configuration.MappedNpcs.FindAll(p => p.voiceItem != null);

                if (oldPlayerMapData.Count > 0 || oldNpcMapData.Count > 0)
                {
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Migrating old npcdata",
                                   new EKEventId(0, TextSource.None));

                    foreach (var player in oldPlayerMapData)
                    {
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

            // ---- Stable player mapping (Option B) ----
            // Player names can vary by context ("You", cutscene proxies, cross-world formatting, etc.).
            // For ObjectKind.Player, use ContentId as the primary key when available.
            if (data.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player && data.ContentId != 0)
            {
                // First, try to find an existing mapping by ContentId.
                var existingByContentId = datas.Find(p => p.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player && p.ContentId == data.ContentId && p.ContentId != 0);
                if (existingByContentId != null)
                {
                    // Keep the existing mapping, but refresh basic identity fields in case they were unknown.
                    if (!string.IsNullOrWhiteSpace(data.Name) && !data.Name.Equals(existingByContentId.Name, StringComparison.OrdinalIgnoreCase))
                        existingByContentId.Name = data.Name;
                    if (data.Race != NpcRaces.Unknown && existingByContentId.Race == NpcRaces.Unknown)
                        existingByContentId.Race = data.Race;
                    if (!string.IsNullOrWhiteSpace(data.RaceStr) && string.IsNullOrWhiteSpace(existingByContentId.RaceStr))
                        existingByContentId.RaceStr = data.RaceStr;
                    if (data.Gender != 0 && existingByContentId.Gender == 0)
                        existingByContentId.Gender = data.Gender;

                    return existingByContentId;
                }

                // Otherwise, attempt a one-time migration from legacy player mappings that predate ContentId.
                // If we find a matching legacy player entry by name (best-effort), we attach the ContentId to it.
                var legacyByName = datas.Find(p => p.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player
                                                   && p.ContentId == 0
                                                   && !string.IsNullOrWhiteSpace(p.Name)
                                                   && !string.IsNullOrWhiteSpace(data.Name)
                                                   && p.Name.Equals(data.Name, StringComparison.OrdinalIgnoreCase));
                if (legacyByName != null)
                {
                    legacyByName.ContentId = data.ContentId;
                    if (data.Race != NpcRaces.Unknown)
                        legacyByName.Race = data.Race;
                    if (!string.IsNullOrWhiteSpace(data.RaceStr))
                        legacyByName.RaceStr = data.RaceStr;
                    if (data.Gender != 0)
                        legacyByName.Gender = data.Gender;
                    if (!string.IsNullOrWhiteSpace(data.Name))
                        legacyByName.Name = data.Name;

                    return legacyByName;
                }
            }

            if (data.Race == NpcRaces.Unknown)
            {
                var oldResult = datas.Find(p => p.ToString() == data.ToString());
                result = datas.Find(p => p.Name == data.Name && p.Race != NpcRaces.Unknown);

                if (result != null)
                    datas.Remove(oldResult);
            }
            else if (data.Race != NpcRaces.Unknown)
            {
                result = datas.Find(p => p.Name == data.Name && p.Race == NpcRaces.Unknown);

                if (result != null)
                {
                    data.Voice = result.Voice;
                    datas.Remove(result);
                    result = null;
                }
            }

            if (result == null)
            {
                result = datas.Find(p => p.ToString() == data.ToString());

                if (result == null)
                {
                    datas.Add(data);
                    data.Voices = Plugin.Configuration.VoiceMasterVoices;
                    data.RefreshSelectable();
                    BackendHelper.GetVoiceOrRandom(eventId, data);
                    ConfigWindow.UpdateDataNpcs = true;
                    ConfigWindow.UpdateDataBubbles = true;
                    ConfigWindow.UpdateDataPlayers = true;
                    var mapping = data.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player ? "player" : "npc";
                    LogHelper.Debug(MethodBase.GetCurrentMethod()!.Name, $"Added new {mapping} to mapping: {data.ToString()}", eventId);

                    result = data;
                }
                else
                    LogHelper.Debug(MethodBase.GetCurrentMethod()!.Name, $"Found existing mapping for: {data.ToString()} result: {result.ToString()}", eventId);
            }
            else
                LogHelper.Debug(MethodBase.GetCurrentMethod()!.Name, $"Found existing mapping for: {data.ToString()} result: {result.ToString()}", eventId);

            return result;
        }
    }
}
