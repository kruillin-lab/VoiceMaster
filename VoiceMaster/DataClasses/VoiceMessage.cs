using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Types;
using VoiceMaster.Enums;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VoiceMaster.DataClasses
{
    /// <summary>
    /// Represents a voice message to be processed and played
    /// Enhanced with additional properties from Echokraut integration
    /// </summary>
    public class VoiceMessage
    {
        public string Text { get; set; } = string.Empty;
        public string OriginalText { get; set; } = string.Empty;

        public IGameObject? SpeakerObj { get; set; }
        public IGameObject? SpeakerFollowObj { get; set; }
        public NpcMapData Speaker { get; set; } = null!;
        public TextSource Source { get; set; }
        public int? ChatType { get; set; }
        public ClientLanguage Language { get; set; }
        public float Volume { get; set; } = 1f;

        public bool LoadedLocally { get; set; }
        public bool IsLastInDialogue { get; set; } = false;
        public bool Is3D { get; set; }
        public bool OnlyRequest { get; set; }

        public EKEventId EventId { get; set; } = null!;
        
        public Stream? Stream { get; set; }
        public Guid StreamId { get; set; }

        public string GetDebugInfo()
        {
            return $"SpeakerFollowObj: {SpeakerFollowObj}, SpeakerObj: {SpeakerObj}, Speaker: {Speaker}, IsLastInDialogue: {IsLastInDialogue}, LoadedLocally: {LoadedLocally}, Source: {Source}, ChatType: {ChatType}, Language: {Language}, Is3D: {Is3D}";
        }
    }
}
