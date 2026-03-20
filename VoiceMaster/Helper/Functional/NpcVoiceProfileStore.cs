using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using VoiceMaster.DataClasses;
using VoiceMaster.Enums;
using VoiceMaster.Helper.Data;

namespace VoiceMaster.Helper.Functional
{
    /// <summary>
    /// Loads and saves per-NPC voice profiles to npc-voice-memory.json in the plugin
    /// config directory. Holds an in-memory dictionary between calls so JSON is not
    /// re-read on every TTS request.
    /// </summary>
    public class NpcVoiceProfileStore
    {
        private const string FileName = "npc-voice-memory.json";

        // Race lookup: (NpcRaces race, bool isFemale) → speakingRate
        private static readonly Dictionary<(NpcRaces, bool), double> RateTable =
            new Dictionary<(NpcRaces, bool), double>
            {
                { (NpcRaces.Hyur,     false), 1.00 }, { (NpcRaces.Hyur,     true),  1.02 },
                { (NpcRaces.Elezen,   false), 0.88 }, { (NpcRaces.Elezen,   true),  0.90 },
                { (NpcRaces.Lalafell, false), 1.18 }, { (NpcRaces.Lalafell, true),  1.20 },
                { (NpcRaces.Miqote,   false), 1.05 }, { (NpcRaces.Miqote,   true),  1.08 },
                { (NpcRaces.Roegadyn, false), 0.82 }, { (NpcRaces.Roegadyn, true),  0.85 },
                { (NpcRaces.AuRa,     false), 0.95 }, { (NpcRaces.AuRa,     true),  0.98 },
                { (NpcRaces.Hrothgar, false), 0.80 }, { (NpcRaces.Hrothgar, true),  0.80 },
                { (NpcRaces.Viera,    false), 0.93 }, { (NpcRaces.Viera,    true),  0.95 },
            };

        private readonly string _filePath;
        private readonly Dictionary<string, NpcVoiceProfile> _profiles;

        public NpcVoiceProfileStore(string pluginConfigDirectory)
        {
            _filePath = Path.Combine(pluginConfigDirectory, FileName);
            _profiles = Load();
        }

        /// <summary>
        /// Returns the saved profile for this NPC, creating and saving one if not found.
        /// If the profile exists (regardless of overridden flag), it is returned as-is
        /// to maintain stability — recompute only happens on first encounter.
        /// </summary>
        public NpcVoiceProfile GetOrCreate(string npcName, NpcRaces race, Genders gender)
        {
            if (_profiles.TryGetValue(npcName, out var existing))
                return existing;

            var profile = ComputeProfile(race, gender);
            _profiles[npcName] = profile;
            Save();
            return profile;
        }

        private NpcVoiceProfile ComputeProfile(NpcRaces race, Genders gender)
        {
            var isFemale = gender == Genders.Female;
            var key = (race, isFemale);

            double speakingRate = RateTable.TryGetValue(key, out var rate) ? rate : 1.00;
            double temperature = gender == Genders.Female ? 0.65 :
                                 gender == Genders.Male   ? 0.55 : 0.60;

            var raceStr   = race   == NpcRaces.Unknown ? "Unknown" : race.ToString();
            var genderStr = gender == Genders.Female   ? "Female"  :
                            gender == Genders.Male     ? "Male"    : "Unknown";

            return new NpcVoiceProfile
            {
                SpeakingRate = speakingRate,
                Temperature  = temperature,
                BaseEmotion  = "calm",
                RaceGender   = $"{raceStr}_{genderStr}",
                Overridden   = false
            };
        }

        private Dictionary<string, NpcVoiceProfile> Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return new Dictionary<string, NpcVoiceProfile>(StringComparer.OrdinalIgnoreCase);

                var json   = File.ReadAllText(_filePath);
                var result = JsonConvert.DeserializeObject<Dictionary<string, NpcVoiceProfile>>(json);
                return result ?? new Dictionary<string, NpcVoiceProfile>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name,
                    $"Failed to load {FileName}: {ex.Message}",
                    new EKEventId(0, TextSource.None));
                return new Dictionary<string, NpcVoiceProfile>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void Save()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_profiles, Formatting.Indented);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name,
                    $"Failed to save {FileName}: {ex.Message}",
                    new EKEventId(0, TextSource.None));
                // Swallow — never crash the audio pipeline on a save failure
            }
        }
    }
}
