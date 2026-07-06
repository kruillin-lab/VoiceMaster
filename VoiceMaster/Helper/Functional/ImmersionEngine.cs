using System;
using System.Collections.Generic;
using VoiceMaster.DataClasses;
using VoiceMaster.Enums;

namespace VoiceMaster.Helper.Functional
{
    /// <summary>
    /// Orchestrates NpcVoiceProfileStore + EmotionDetector to produce final Inworld TTS
    /// parameters for a given VoiceMessage. Emotion modifiers are applied to a copy of
    /// the saved profile; the persisted profile is never mutated by emotion detection.
    /// </summary>
    public class ImmersionEngine
    {
        private const string DefaultModelId = "inworld-tts-2";

        // Clamp bounds per spec
        private const double MinRate = 0.5;
        private const double MaxRate = 1.5;
        private const double MinTemp = 0.0;
        private const double MaxTemp = 2.0;

        // Emotion modifier deltas indexed by EmotionState enum value:
        // Neutral=0, Happy=1, Angry=2, Sad=3, Fearful=4, Excited=5, Solemn=6
        private static readonly (double RateDelta, double TempDelta)[] EmotionDeltas =
        {
            /* Neutral = 0 */ ( 0.00,  0.00),
            /* Happy   = 1 */ ( 0.05,  0.15),
            /* Angry   = 2 */ ( 0.10,  0.30),
            /* Sad     = 3 */ (-0.08, -0.10),
            /* Fearful = 4 */ ( 0.12,  0.25),
            /* Excited = 5 */ ( 0.15,  0.35),
            /* Solemn  = 6 */ (-0.05, -0.15),
        };

        // Per-line delivery phrases for TTS-2 natural-language steering, indexed by EmotionState.
        // Neutral contributes no phrase. Kept lowercase/punctuation-light per Inworld guidance.
        private static readonly string[] EmotionSteer =
        {
            /* Neutral = 0 */ "",
            /* Happy   = 1 */ "cheerfully",
            /* Angry   = 2 */ "with sharp anger",
            /* Sad     = 3 */ "sadly in a low heavy tone",
            /* Fearful = 4 */ "fearfully tense and shaky",
            /* Excited = 5 */ "with bright excited energy",
            /* Solemn  = 6 */ "solemnly slow and grave",
        };

        /// <summary>
        /// Composes a single bracketed TTS-2 steering instruction from the persistent
        /// per-NPC personality (Layer 1) and the per-line detected emotion (Layer 2).
        /// Returns "" when neither contributes, so plain text is sent unchanged.
        /// </summary>
        public static string BuildSteering(string personality, EmotionState emotion)
        {
            var persona = (personality ?? string.Empty).Trim();
            var emo = EmotionSteer[(int)emotion];

            if (persona.Length == 0 && emo.Length == 0)
                return string.Empty;

            var parts = new List<string>();
            parts.Add(persona.Length > 0 ? "speak as " + persona : "speak");
            if (emo.Length > 0)
                parts.Add(emo);

            // One instruction, at the start of the text, lowercase per Inworld best practice.
            return "[" + string.Join(", ", parts).ToLowerInvariant() + "] ";
        }

        private readonly NpcVoiceProfileStore _store;

        public ImmersionEngine(NpcVoiceProfileStore store)
        {
            _store = store;
        }

        /// <summary>
        /// Resolves the final TTS parameters for a VoiceMessage.
        /// Returns default params (rate=1.0, temp=0.60) if Speaker is null.
        /// </summary>
        public InworldTtsParams Resolve(VoiceMessage message)
        {
            if (message?.Speaker == null)
                return new InworldTtsParams(1.0, 0.60, DefaultModelId);

            var speaker = message.Speaker;
            var profile  = _store.GetOrCreate(speaker.Name, speaker.Race, speaker.Gender);
            var emotion  = EmotionDetector.Detect(message.Text);

            var (rateDelta, tempDelta) = EmotionDeltas[(int)emotion];

            var finalRate = Math.Clamp(profile.SpeakingRate + rateDelta, MinRate, MaxRate);
            var finalTemp = Math.Clamp(profile.Temperature  + tempDelta, MinTemp, MaxTemp);

            var steering = BuildSteering(speaker.Personality, emotion);

            return new InworldTtsParams(finalRate, finalTemp, DefaultModelId, steering);
        }
    }
}
