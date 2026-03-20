using VoiceMaster.DataClasses;
using VoiceMaster.Enums;

namespace VoiceMaster.Services.Queue;

/// <summary>
/// Represents a voice message entry in the queue with state tracking
/// </summary>
public class VoiceMessageEntry
{
    public Guid Id { get; }
    public VoiceMessage Message { get; }
    public VoiceMessageState State { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? StartedGeneratingAt { get; private set; }
    public DateTime? ReadyToPlayAt { get; private set; }
    public DateTime? StartedPlayingAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Exception? Error { get; set; }

    public VoiceMessageEntry(VoiceMessage message)
    {
        Id = Guid.NewGuid();
        Message = message ?? throw new ArgumentNullException(nameof(message));
        State = VoiceMessageState.PendingGeneration;
        CreatedAt = DateTime.UtcNow;
    }

    public void TransitionTo(VoiceMessageState newState)
    {
        State = newState;
        
        switch (newState)
        {
            case VoiceMessageState.Generating:
                StartedGeneratingAt = DateTime.UtcNow;
                break;
            case VoiceMessageState.ReadyToPlay:
                ReadyToPlayAt = DateTime.UtcNow;
                break;
            case VoiceMessageState.Playing:
                StartedPlayingAt = DateTime.UtcNow;
                break;
            case VoiceMessageState.Completed:
            case VoiceMessageState.Cancelled:
            case VoiceMessageState.Failed:
                CompletedAt = DateTime.UtcNow;
                break;
        }
    }

    public TimeSpan? GetTotalProcessingTime()
    {
        if (CompletedAt.HasValue)
            return CompletedAt.Value - CreatedAt;
        return null;
    }

    public TimeSpan? GetGenerationTime()
    {
        if (StartedGeneratingAt.HasValue && ReadyToPlayAt.HasValue)
            return ReadyToPlayAt.Value - StartedGeneratingAt.Value;
        return null;
    }
}
