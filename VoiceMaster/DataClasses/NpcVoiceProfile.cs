namespace VoiceMaster.DataClasses
{
    /// <summary>
    /// Persistent voice profile for a single NPC, derived from race/gender signals.
    /// Keyed by NPC display name in NpcVoiceProfileStore.
    /// </summary>
    public class NpcVoiceProfile
    {
        /// <summary>Speaking rate multiplier (0.5–1.5). Derived from race/gender lookup.</summary>
        public double SpeakingRate { get; set; } = 1.0;

        /// <summary>Temperature / expressiveness (0.0–2.0). Derived from gender.</summary>
        public double Temperature { get; set; } = 0.60;

        /// <summary>Stable emotion label — reserved for future personality baseline; not used in v1.</summary>
        public string BaseEmotion { get; set; } = "calm";

        /// <summary>Compound race+gender key used to regenerate profile if lookup table changes. E.g. "Elezen_Male".</summary>
        public string RaceGender { get; set; } = "Unknown_Unknown";

        /// <summary>If true, auto-recompute from race/gender is skipped; user has manually set this profile.</summary>
        public bool Overridden { get; set; } = false;
    }
}
