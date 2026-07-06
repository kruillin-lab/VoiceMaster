namespace VoiceMaster.DataClasses
{
    /// <summary>
    /// Fantasy-trope starter personalities for Inworld TTS-2 natural-language steering.
    /// These are editable templates: picking one fills the per-NPC "voice direction" text,
    /// which the user can then tweak. The stored value is always the free-text descriptor
    /// (see <see cref="NpcMapData.Personality"/>), not the preset name.
    /// </summary>
    public static class PersonalityPresets
    {
        /// <summary>(display name, steering descriptor) pairs. Descriptors are kept lowercase
        /// and punctuation-light per Inworld guidance; empty descriptor = clear.</summary>
        public static readonly (string Name, string Descriptor)[] Presets =
        {
            ("Grizzled Veteran",    "a grizzled old warrior, low and gravelly, weary and battle-worn"),
            ("Noble Aristocrat",    "a refined aristocrat, crisp articulate diction, haughty and composed"),
            ("Cheerful Merchant",   "a cheerful merchant, warm and bright, friendly and upbeat"),
            ("Sinister Villain",    "a menacing villain, cold and deliberate, quietly threatening"),
            ("Wise Elder",          "a wise old sage, slow and measured, calm gravitas"),
            ("Timid Youth",         "a timid young person, soft and hesitant, slightly nervous"),
            ("Boisterous Warrior",  "a boisterous warrior, loud and hearty, full of bravado"),
            ("Mysterious Stranger", "a mysterious stranger, hushed and enigmatic, guarded and low"),
            ("Regal Monarch",       "a regal monarch, commanding and dignified, resonant authority"),
            ("Cackling Witch",      "a wicked witch, shrill and cackling, sly and malicious"),
            ("Stoic Knight",        "a stoic knight, steady and disciplined, resolute and formal"),
            ("Childlike Sprite",    "a playful sprite, high and light, giddy and mischievous"),
        };

        /// <summary>Combo menu labels: an inert prompt at index 0, the presets, then a clear entry.</summary>
        public static string[] MenuLabels()
        {
            var labels = new string[Presets.Length + 2];
            labels[0] = "— apply a preset —";
            for (var i = 0; i < Presets.Length; i++)
                labels[i + 1] = Presets[i].Name;
            labels[^1] = "Clear (no personality)";
            return labels;
        }

        /// <summary>
        /// Maps a combo menu index to the descriptor to store. Returns null for index 0
        /// (the inert prompt — caller does nothing), "" for the trailing Clear entry.
        /// </summary>
        public static string? DescriptorForMenuIndex(int menuIndex)
        {
            if (menuIndex <= 0)
                return null;
            if (menuIndex == Presets.Length + 1)
                return string.Empty;
            return Presets[menuIndex - 1].Descriptor;
        }
    }
}
