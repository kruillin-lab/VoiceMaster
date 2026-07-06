using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using VoiceMaster.Enums;

namespace VoiceMaster.DataClasses;

/// <summary>
/// Represents a voice message to be processed and played
/// </summary>
public class VoiceMessage
{
    public IGameObject? SpeakerObj { get; set; }
    public IGameObject? SpeakerFollowObj { get; set; }
    public TextSource Source { get; set; }
    public NpcMapData Speaker { get; set; } = null!;
    public string Text { get; set; } = string.Empty;
    public string OriginalText { get; set; } = string.Empty;
    public Dalamud.Game.ClientLanguage Language { get; set; }
    public Guid EventId { get; set; }
    public bool OnlyRequest { get; set; }
    public float Volume { get; set; } = 1.0f;
    public bool IsLastInDialogue { get; set; }
    public bool Is3D { get; set; }
    public Stream? Stream { get; set; }
    public Guid StreamId { get; set; }
    public bool LoadedLocally { get; set; }

    public string GetDebugInfo()
    {
        return $"VoiceMessage[Source={Source}, Speaker={Speaker.Name}, Text={Text[..Math.Min(50, Text.Length)]}..., Is3D={Is3D}, Volume={Volume}]";
    }
}

/// <summary>
/// NPC mapping data for voice assignment
/// </summary>
public class NpcMapData
{
    public ObjectKind ObjectKind { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RaceStr { get; set; } = string.Empty;
    public int Race { get; set; }
    public int Gender { get; set; }
    public bool IsChild { get; set; }
    public bool HasBubbles { get; set; }
    public float Volume { get; set; } = 1.0f;
    public float VolumeBubble { get; set; } = 1.0f;
    public bool IsEnabled { get; set; } = true;
    public bool IsEnabledBubble { get; set; } = true;
    public EchokrautVoice? Voice { get; set; }

    public NpcMapData(ObjectKind objectKind)
    {
        ObjectKind = objectKind;
    }

    public override string ToString()
    {
        return $"NpcMapData[Name={Name}, Race={RaceStr}, Gender={Gender}, Voice={Voice?.VoiceName ?? "null"}]";
    }
}

/// <summary>
/// Voice configuration for an NPC
/// </summary>
public class EchokrautVoice
{
    public string BackendVoice { get; set; } = string.Empty;
    public string VoiceName { get; set; } = string.Empty;
    public float Volume { get; set; } = 1.0f;
    public List<Genders> AllowedGenders { get; set; } = new();
    public List<NpcRaces> AllowedRaces { get; set; } = new();
    public bool IsDefault { get; set; }
    public bool UseAsRandom { get; set; }
    public bool IsChildVoice { get; set; }

    public bool FitsNpcData(int gender, int race, bool isChild, bool strict = false)
    {
        if (IsChildVoice != isChild && strict)
            return false;

        var npcRace = (NpcRaces)race;
        var npcGender = (Genders)gender;

        var raceMatches = AllowedRaces.Count == 0 || AllowedRaces.Contains(npcRace);
        var genderMatches = AllowedGenders.Count == 0 || AllowedGenders.Contains(npcGender);

        return raceMatches && genderMatches;
    }

    public override string ToString()
    {
        return $"EchokrautVoice[Name={VoiceName}, Backend={BackendVoice}, IsDefault={IsDefault}]";
    }
}

/// <summary>
/// Gender enumeration
/// </summary>
public enum Genders
{
    Male = 0,
    Female = 1
}

/// <summary>
/// NPC race enumeration
/// </summary>
public enum NpcRaces
{
    Hyur = 1,
    Elezen = 2,
    Lalafell = 3,
    Miqote = 4,
    Roegadyn = 5,
    AuRa = 6,
    Hrothgar = 7,
    Viera = 8
}
