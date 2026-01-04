using Dalamud.Game.ClientState.Objects.Enums;
using OtterGui.Widgets;
using System;
using System.Collections.Generic;
using VoiceMaster.Enums;
using VoiceMaster.Helper;
using VoiceMaster.Helper.Data;

namespace VoiceMaster.DataClasses
{
    public class NpcMapData : IComparable
    {
        public string Name { get; set; } = string.Empty;
        public NpcRaces Race { get; set; }
        public string RaceStr { get; set; } = string.Empty;
        public Genders Gender { get; set; }

        public bool IsChild { get; set; }

        // NOTE: Keep this PUBLIC for backwards compatibility.
        // Multiple parts of the codebase (and existing configs) reference .voice directly.
        public string voice = string.Empty;

        internal VoiceMasterVoice? Voice
        {
            get => NpcDataHelper.GetVoiceByBackendVoice(voice);
            set => voice = value != null ? value.BackendVoice : string.Empty;
        }

        public BackendVoiceItem voiceItem { get; set; } = new();

        public bool DoNotDelete { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool IsEnabledBubble { get; set; } = true;
        public float Volume { get; set; } = 1f;
        public float VolumeBubble { get; set; } = 1f;
        public bool HasBubbles { get; set; }

        public ObjectKind ObjectKind { get; set; }

        internal ClippedSelectableCombo<VoiceMasterVoice> VoicesSelectable { get; set; } = null!;
        internal ClippedSelectableCombo<VoiceMasterVoice> VoicesSelectableDialogue { get; set; } = null!;

        internal List<VoiceMasterVoice> Voices { get; set; } = new();

        public NpcMapData(ObjectKind objectKind)
        {
            ObjectKind = objectKind;
        }

        public override string ToString()
        {
            var raceString = Race == NpcRaces.Unknown ? RaceStr : Race.ToString();
            return $"{Gender} - {raceString} - {Name}";
        }

        public override bool Equals(object obj)
        {
            var item = obj as NpcMapData;
            if (item == null)
                return false;

            return ToString().Equals(item.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public int CompareTo(object? obj)
        {
            var otherObj = (NpcMapData)obj!;
            return otherObj.ToString().ToLower().CompareTo(ToString().ToLower());
        }

        public void RefreshSelectable()
        {
            // Keep Voices updated and always non-null.
            Voices ??= new List<VoiceMasterVoice>();

            VoicesSelectable = new(
                $"##AllVoices{ToString()}",
                string.Empty,
                200,
                Voices.FindAll(f => f.IsSelectable(Name, Gender, Race, IsChild)),
                g => g.VoiceNameNote);

            VoicesSelectableDialogue = new(
                $"##AllVoices{ToString()}",
                string.Empty,
                200,
                Voices.FindAll(f => f.IsSelectable(Name, Gender, Race, IsChild)),
                g => g.VoiceNameNote);
        }
    }
}
