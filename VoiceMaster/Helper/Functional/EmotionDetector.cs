using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using VoiceMaster.Enums;

namespace VoiceMaster.Helper.Functional
{
    /// <summary>
    /// Stateless emotion detector. Scans dialogue text for keyword patterns and returns
    /// the highest-priority matching EmotionState, or Neutral if no match found.
    /// </summary>
    public static class EmotionDetector
    {
        // Priority order (index 0 = highest priority): Angry, Fearful, Excited, Happy, Sad, Solemn
        private static readonly (EmotionState Emotion, string[] Keywords)[] PriorityMap =
        {
            (EmotionState.Angry,   new[] { "rage", "furious", "damn", "fool", "enough", "insolent", "impudent", "wretched", "despicable", "loathe" }),
            (EmotionState.Fearful, new[] { "afraid", "danger", "run", "flee", "terror", "horrified", "dread", "peril", "escape", "threat" }),
            (EmotionState.Excited, new[] { "amazing", "incredible", "finally", "unbelievable", "magnificent", "extraordinary", "astounding", "cannot believe", "at last" }),
            (EmotionState.Happy,   new[] { "joy", "wonderful", "laugh", "glad", "celebrate", "delighted", "pleased", "grateful", "thank you", "rejoice" }),
            (EmotionState.Sad,     new[] { "tears", "grieve", "lost", "sorry", "miss", "mourn", "weep", "heartbroken", "regret", "sorrow" }),
            (EmotionState.Solemn,  new[] { "honor", "duty", "sacrifice", "must", "vow", "pledge", "resolve", "swear", "burden", "responsibility" }),
        };

        // Pre-compiled regexes keyed by emotion, built once on static initialization
        private static readonly Dictionary<EmotionState, Regex> _regexCache = BuildRegexCache();

        private static Dictionary<EmotionState, Regex> BuildRegexCache()
        {
            var cache = new Dictionary<EmotionState, Regex>();
            foreach (var (emotion, keywords) in PriorityMap)
            {
                // Word-boundary match for each keyword, case-insensitive
                var pattern = @"\b(" + string.Join("|", Array.ConvertAll(keywords, Regex.Escape)) + @")\b";
                cache[emotion] = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
            return cache;
        }

        /// <summary>
        /// Scans the full text for all emotion keyword matches, then returns the
        /// highest-priority match. Returns EmotionState.Neutral if no keywords match.
        /// Priority order: Angry > Fearful > Excited > Happy > Sad > Solemn > Neutral.
        /// </summary>
        public static EmotionState Detect(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return EmotionState.Neutral;

            // Iterate in priority order; return first emotion whose regex matches
            foreach (var (emotion, _) in PriorityMap)
            {
                if (_regexCache[emotion].IsMatch(text))
                    return emotion;
            }

            return EmotionState.Neutral;
        }
    }
}
