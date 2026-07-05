using Dalamud.Plugin.Services;
using VoiceMaster.Backend;
using VoiceMaster.DataClasses;
using VoiceMaster.Enums;
using VoiceMaster.Helper.Data;
using VoiceMaster.Helper.Functional;
using VoiceMaster.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace VoiceMaster.Helper.API
{
    public static class BackendHelper
    {
        static Random Rand { get; set; }
        static ITTSBackend? Backend { get; set; }

        public static void Initialize(TTSBackends backendType)
        {
            Rand = new Random(Guid.NewGuid().GetHashCode());
            SetBackendType(backendType);
            PlayingHelper.Setup();
        }

        public static async void SetBackendType(TTSBackends backendType)
        {
            if (backendType == TTSBackends.Alltalk)
            {
                if (Plugin.Configuration.Alltalk.RemoteInstance ||
                    (Plugin.Configuration.Alltalk.LocalInstance && AlltalkInstanceHelper.InstanceRunning))
                {
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Creating backend instance: {backendType}",
                                   new EKEventId(0, TextSource.None));
                    Backend = new AlltalkBackend();
                    await GetAndMapVoices(new EKEventId(0, TextSource.None));
                }
                else
                {
                    LogHelper.Error(MethodBase.GetCurrentMethod().Name, "Alltalk selected but no instance configured (Remote or Local must be enabled)",
                                    new EKEventId(0, TextSource.None));
                }
            }
            else if (backendType == TTSBackends.InworldAI)
            {
                if (!Plugin.Configuration.InworldAI.Enabled)
                {
                    LogHelper.Error(MethodBase.GetCurrentMethod().Name, "InworldAI selected but not enabled in configuration",
                                    new EKEventId(0, TextSource.None));
                    return;
                }
                if (string.IsNullOrWhiteSpace(Plugin.Configuration.InworldAI.ApiKey) ||
                    string.IsNullOrWhiteSpace(Plugin.Configuration.InworldAI.ApiSecret))
                {
                    LogHelper.Error(MethodBase.GetCurrentMethod().Name, "InworldAI selected but API Key or Secret is missing",
                                    new EKEventId(0, TextSource.None));
                    return;
                }

                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Creating backend instance: {backendType}",
                               new EKEventId(0, TextSource.None));
                Backend = new InworldAIBackend();
                await GetAndMapVoices(new EKEventId(0, TextSource.None));
            }
        }

        public static async Task RefreshVoicesAsync(EKEventId eventId)
        {
            if (Backend == null)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, "Cannot refresh voices: backend not initialized", eventId);
                return;
            }
            await GetAndMapVoices(eventId);
        }

        public static async Task<bool> ReloadService(string reloadModel, EKEventId eventId)
        {
            if (Backend == null)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, "Cannot reload service: backend not initialized", eventId);
                return false;
            }
            return await Backend.ReloadService(reloadModel, eventId).ConfigureAwait(false);
        }

        public static bool IsBackendAvailable()
        {
            switch (Plugin.Configuration.BackendSelection)
            {
                case TTSBackends.Alltalk:
                    if (Plugin.Configuration.Alltalk.LocalInstance && Plugin.Configuration.Alltalk.LocalInstall &&
                        AlltalkInstanceHelper.InstanceRunning)
                        return true;

                    if (Plugin.Configuration.Alltalk.RemoteInstance &&
                        !string.IsNullOrWhiteSpace(Plugin.Configuration.Alltalk.BaseUrl))
                        return true;
                    break;
                case TTSBackends.InworldAI:
                    // Basic check if configured
                    if (!string.IsNullOrEmpty(Plugin.Configuration.InworldAI.ApiKey) &&
                        !string.IsNullOrEmpty(Plugin.Configuration.InworldAI.ApiSecret))
                        return true;
                    break;
            }

            return false;
        }

        // Pre-filter to skip non-speech text (ASCII art, emoticons, mostly punctuation)
        static bool IsSpeakableText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            // Count letters vs non-letters
            int letterCount = 0;
            int nonLetterCount = 0;

            foreach (char c in text)
            {
                if (char.IsLetter(c))
                    letterCount++;
                else if (!char.IsWhiteSpace(c))
                    nonLetterCount++;
            }

            // Require at least some letters, and letters should be majority
            return letterCount > 0 && letterCount >= nonLetterCount;
        }

        public static void OnSay(VoiceMessage voiceMessage)
        {
            var eventId = voiceMessage.EventId;
            LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Starting voice inference: {voiceMessage.Language}", eventId);
            LogHelper.Info(MethodBase.GetCurrentMethod().Name, voiceMessage.Text.ToString(), eventId);

            // Skip non-speech text (ASCII art, emoticons, etc.)
            if (!IsSpeakableText(voiceMessage.Text))
            {
                LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Skipping non-speech text: {voiceMessage.Text}", eventId);
                return;
            }

            // Optional: suppress NPCs that are already voiced by the game (user-maintained ignore list)
            // Applies only to NPC-facing sources (not chat/bubble).
            if (voiceMessage.Source is TextSource.AddonTalk or TextSource.AddonBattleTalk
                or TextSource.AddonSelectString or TextSource.AddonCutsceneSelectString)
            {
                var speakerName = voiceMessage.Speaker?.Name;
                if (Plugin.ShouldIgnoreNpcSpeaker(speakerName))
                {
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Ignored voiced NPC: {speakerName}", eventId);
                    return;
                }
            }

            switch (voiceMessage.Source)
            {
                case TextSource.Chat:
                case TextSource.AddonBubble:
                    PlayingHelper.AddRequestBubbleToQueue(voiceMessage);
                    break;
                case TextSource.AddonTalk:
                case TextSource.AddonBattleTalk:
                case TextSource.AddonCutsceneSelectString:
                case TextSource.AddonSelectString:
                case TextSource.VoiceTest:
                    var messageList = new List<string>();
                    if (Plugin.Configuration.GenerateBySentence)
                    {
                        //var messageArr = voiceMessage.Text.Split(Constants.SENTENCESEPARATORS);
                        var messageArr = TalkTextHelper.SplitKeepLeft(voiceMessage.Text, Constants.SENTENCESEPARATORS);
                        messageList = messageArr.ToList().FindAll(p => !string.IsNullOrWhiteSpace(p.Trim()));
                    }
                    else 
                        messageList.Add(voiceMessage.Text);

                    foreach (var message in messageList)
                    {
                        var trimmedMessage = message.Trim();
                        var cleanText = Plugin.Configuration.RemovePunctuation ? TalkTextHelper.RemovePunctuation(trimmedMessage) : trimmedMessage;
                        var messageObj = new VoiceMessage()
                        {
                            Text = cleanText,
                            ChatType = voiceMessage.ChatType,
                            Language = voiceMessage.Language,
                            LoadedLocally = voiceMessage.LoadedLocally,
                            SpeakerObj = voiceMessage.SpeakerObj,
                            SpeakerFollowObj = voiceMessage.SpeakerFollowObj,
                            Source = voiceMessage.Source,
                            Speaker = voiceMessage.Speaker,
                            EventId = voiceMessage.EventId,
                            Volume = voiceMessage.Volume
                        };
                
                        if (message == messageList.Last())
                            messageObj.IsLastInDialogue = true;
                
                        PlayingHelper.AddRequestToQueue(messageObj);
                    }
                    break;
            }
        }

        public static void OnCancelAll()
        {
            LogHelper.Info(MethodBase.GetCurrentMethod().Name, "Stopping VoiceMaster", new EKEventId(0, TextSource.None));
            LogHelper.Info(MethodBase.GetCurrentMethod().Name, "Stopping VoiceMaster", new EKEventId(0, TextSource.AddonTalk));
            LogHelper.Info(MethodBase.GetCurrentMethod().Name, "Stopping VoiceMaster", new EKEventId(0, TextSource.AddonBattleTalk));
            LogHelper.Info(MethodBase.GetCurrentMethod().Name, "Stopping VoiceMaster", new EKEventId(0, TextSource.AddonCutsceneSelectString));
            LogHelper.Info(MethodBase.GetCurrentMethod().Name, "Stopping VoiceMaster", new EKEventId(0, TextSource.AddonSelectString));
            LogHelper.Info(MethodBase.GetCurrentMethod().Name, "Stopping VoiceMaster", new EKEventId(0, TextSource.AddonBubble));
            LogHelper.Info(MethodBase.GetCurrentMethod().Name, "Stopping VoiceMaster", new EKEventId(0, TextSource.Chat));
            LogHelper.Info(MethodBase.GetCurrentMethod().Name, "Stopping VoiceMaster", new EKEventId(0, TextSource.Backend));
            PlayingHelper.ClearRequestingQueue();
            PlayingHelper.ClearRequestedQueue();
            PlayingHelper.ClearPlayingQueue();
        }

        public static void OnCancel(VoiceMessage message)
        {
            if (PlayingHelper.Playing)
            {
                PlayingHelper.StopPlaying(message);
            }
        }

        public static void OnPause(VoiceMessage message)
        {
            PlayingHelper.PausePlaying(message);
        }

        public static void OnResume(VoiceMessage message)
        {
            PlayingHelper.ResumePlaying(message);
        }

        static async Task GetAndMapVoices(EKEventId eventId)
        {
            if (Backend == null)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, "Cannot get and map voices: backend not initialized", eventId);
                return;
            }

            LogHelper.Info(MethodBase.GetCurrentMethod().Name, "Loading and mapping voices", eventId);
            var englishOnly = Plugin.Configuration.InworldEnglishOnly;
            var backendVoices = await Backend.GetAvailableVoices(eventId, englishOnly);

            
            // Guard: if the backend returns no voices (backend offline / endpoint misconfigured / transient failure),
            // do NOT mutate or save the local voice list. Wiping VoiceMasterVoices here causes user mappings to "disappear".
            if (backendVoices == null || backendVoices.Count == 0)
            {
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, "Backend returned 0 voices; keeping existing configured voices.", eventId);
                NpcDataHelper.RefreshSelectables(Plugin.Configuration.VoiceMasterVoices);
                ConfigWindow.UpdateDataVoices = true;
                return;
            }

            var newVoices = backendVoices.FindAll(p => Plugin.Configuration.VoiceMasterVoices.Find(f => f.BackendVoice == p) == null);

            if (newVoices.Count > 0)
            {
                LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Adding {newVoices.Count} new Voices", eventId);
                foreach (var newVoice in newVoices)
                {
                    var voiceName = Path.GetFileNameWithoutExtension(newVoice);
                    var newEkVoice = new VoiceMasterVoice()
                    {
                        BackendVoice = newVoice,
                        VoiceName = voiceName,
                        Volume = 1,
                        AllowedGenders = new List<Genders>(),
                        AllowedRaces = new List<NpcRaces>(),
                        IsDefault = newVoice.Equals(Constants.NARRATORVOICE, StringComparison.OrdinalIgnoreCase),
                        UseAsRandom = voiceName.Contains("NPC")
                    };

                    NpcDataHelper.ReSetVoiceGenders(newEkVoice, eventId);
                    NpcDataHelper.ReSetVoiceRaces(newEkVoice, eventId);

                    Plugin.Configuration.VoiceMasterVoices.Add(newEkVoice);
                    LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Added {newEkVoice}", eventId);
                }

                Plugin.Configuration.Save();
            }

            var oldVoices =
                Plugin.Configuration.VoiceMasterVoices.FindAll(p => backendVoices.Find(f => f == p.BackendVoice) == null);
            
            if (oldVoices.Count > 0)
            {
                LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Replacing {oldVoices.Count} old Voices", eventId);
                foreach (var oldVoice in oldVoices)
                {
                    VoiceMasterVoice? newEkVoice = null;

                    if (oldVoice.BackendVoice.Contains("NPC"))
                    {
                        if (oldVoice.AllowedRaces.Count > 0 && NpcDataHelper.IsGenderedRace(oldVoice.AllowedRaces[0]))
                        {
                            var newEkVoices = Plugin.Configuration.VoiceMasterVoices.FindAll(
                                f => !oldVoices.Contains(f) &&
                                     f.VoiceName.Contains("NPC") &&
                                     f.IsChildVoice == oldVoice.IsChildVoice &&
                                     !oldVoice.AllowedGenders.Except(f.AllowedGenders).Any() &&
                                     !oldVoice.AllowedRaces.Except(f.AllowedRaces).Any()
                            );
                            
                            newEkVoice = newEkVoices.Count > 0 ? newEkVoices[Rand.Next(0, newEkVoices.Count)] : null;
                        }
                        else
                        {
                            var newEkVoices = Plugin.Configuration.VoiceMasterVoices.FindAll(
                                f => !oldVoices.Contains(f) &&
                                     f.VoiceName.Contains("NPC") &&
                                     f.IsChildVoice == oldVoice.IsChildVoice &&
                                     !oldVoice.AllowedRaces.Except(f.AllowedRaces).Any()
                            );
                            
                            newEkVoice = newEkVoices.Count > 0 ? newEkVoices[Rand.Next(0, newEkVoices.Count)] : null;
                        }
                    }
                    else
                    {
                        newEkVoice = Plugin.Configuration.VoiceMasterVoices.Find(
                            f => !oldVoices.Contains(f) &&
                                 f.VoiceName == oldVoice.VoiceName);
                    }

                    NpcDataHelper.MigrateOldData(oldVoice, newEkVoice);
                    Plugin.Configuration.VoiceMasterVoices.Remove(oldVoice);
                    if (newEkVoice != null)
                    {
                        LogHelper.Debug(MethodBase.GetCurrentMethod().Name,
                                        $"Replaced {oldVoice} with {newEkVoice}", eventId);
                        continue;
                    }

                    LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Failed to replace {oldVoice}", eventId);
                }

                Plugin.Configuration.Save();
            }

            NpcDataHelper.MigrateOldData();

            NpcDataHelper.RefreshSelectables(Plugin.Configuration.VoiceMasterVoices);
            ConfigWindow.UpdateDataVoices = true;

            LogHelper.Info(MethodBase.GetCurrentMethod().Name, "Success", eventId);
        }

        public static async Task<bool> GenerateVoice(VoiceMessage message)
        {
            var eventId = message.EventId;
            LogHelper.Info(MethodBase.GetCurrentMethod().Name, "Generating...", eventId);
            try
            {
                if (Backend == null)
                {
                    LogHelper.Error(MethodBase.GetCurrentMethod().Name, "Cannot generate voice: backend not initialized", eventId);
                    return false;
                }

                if (PlayingHelper.RequestedQueue.Contains(message))
                {
                    var voice = GetVoice(eventId, message.Speaker);
                    var language = message.Language;

                    Stream responseStream = null;
                    var i = 0;
                    while (i < 10 && responseStream == null)
                    {
                        try
                        {
                            responseStream = await Backend.GenerateAudioStreamFromVoice(eventId, message, voice, language);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.Error(MethodBase.GetCurrentMethod().Name, ex.ToString(), eventId);
                        }

                        i++;
                    }
                
                    if (responseStream != null)
                    {
                        message.Stream = responseStream;
                        PlayingHelper.PlayingQueue.Add(message);
                        return true;
                    }
                    else
                    {
                        LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Failed to generate audio for: {message.Text}. Removing from queue.", eventId);
                        PlayingHelper.RequestedQueue.Remove(message);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, ex.ToString(), eventId);
                LogHelper.End(MethodBase.GetCurrentMethod().Name, eventId);
            }

            return false;
        }

        public static async Task<string> CheckReady(EKEventId eventId)
        {
            if (Backend == null)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, "Cannot check ready: backend not initialized", eventId);
                return string.Empty;
            }
            return await Backend.CheckReady(eventId);
        }

        public static void GetVoiceOrRandom(EKEventId eventId, NpcMapData npcData)
        {
            LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Searching voice: {npcData.Voice?.VoiceName ?? ""} for NPC: {npcData.Name}", eventId);
            var voiceItem = npcData.Voice;
            var isChild = npcData.IsChild;
            var mappedList = npcData.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Pc ? Plugin.Configuration.MappedPlayers : Plugin.Configuration.MappedNpcs;

            if (voiceItem == null || voiceItem == Plugin.Configuration.VoiceMasterVoices.Find(p => p.IsDefault))
            {
                var npcName = npcData.Name;

                var voiceItems = Plugin.Configuration.VoiceMasterVoices.FindAll(p => p.VoiceName.Contains(npcName, StringComparison.OrdinalIgnoreCase));
                if (voiceItems.Count > 0)
                {
                    voiceItem = voiceItems[0];
                }

                if (voiceItem == null)
                {
                    var isGenderedRace = NpcDataHelper.IsGenderedRace(npcData.Race);
                        voiceItems = Plugin.Configuration.VoiceMasterVoices.FindAll(p => p.FitsNpcData(npcData.Gender, npcData.Race, isChild, isGenderedRace));
                        
                    if (voiceItems.Count > 0)
                    {
                        var randomVoice = voiceItems[Rand.Next(0, voiceItems.Count)];
                        voiceItem = randomVoice;
                    }
                }

                if (voiceItem == null)
                    voiceItem = Plugin.Configuration.VoiceMasterVoices.Find(p => p.IsDefault);

                if (voiceItem != npcData.Voice)
                {
                    if (npcData.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Pc)
                    {
                        LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Chose voice: {voiceItem} for Player: {npcName}", eventId);
                    }
                    else
                    {
                        LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Chose voice: {voiceItem} for NPC: {npcName}", eventId);
                    }
                    npcData.Voice = voiceItem;
                    Plugin.Configuration.Save();
                }
            }

            if (voiceItem != null)
                LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Found voice: {voiceItem} for NPC: {npcData.Name}", eventId);
            else
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Couldn't find voice for NPC: {npcData.Name}", eventId);
        }

        private static string GetVoice(EKEventId eventId, NpcMapData npcData)
        {
            GetVoiceOrRandom(eventId, npcData);

            LogHelper.Info(MethodBase.GetCurrentMethod().Name, string.Format("Loaded voice: {0} for NPC: {1}", npcData.Voice.BackendVoice, npcData.Name), eventId);
            return npcData.Voice.BackendVoice;
        }
    }
}
