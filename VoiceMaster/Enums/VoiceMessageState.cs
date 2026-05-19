namespace VoiceMaster.Enums;

/// <summary>
/// States for voice message lifecycle
/// </summary>
public enum VoiceMessageState
{
    PendingGeneration,
    Generating,
    ReadyToPlay,
    Playing,
    Paused,
    Completed,
    Cancelled,
    Failed
}
