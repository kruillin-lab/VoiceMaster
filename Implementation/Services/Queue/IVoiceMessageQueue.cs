using VoiceMaster.Enums;

namespace VoiceMaster.Services.Queue;

/// <summary>
/// Interface for the voice message queue
/// </summary>
public interface IVoiceMessageQueue : IDisposable
{
    void Enqueue(VoiceMessage message, bool isPriority = false);
    bool TryDequeuePendingGeneration(out VoiceMessageEntry? entry);
    bool TryDequeueReadyToPlay(out VoiceMessageEntry? entry);
    void MarkAsGenerating(Guid entryId);
    void MarkAsReadyToPlay(Guid entryId);
    void MarkAsPlaying(Guid entryId);
    void MarkAsPaused(Guid entryId);
    void MarkAsCompleted(Guid entryId);
    void MarkAsCancelled(Guid entryId);
    void MarkAsFailed(Guid entryId, Exception error);
    void CancelBySource(TextSource source);
    void CancelAll();
    VoiceMessageEntry? GetEntry(Guid entryId);
    VoiceMessageEntry? GetCurrentlyPlaying();
    IReadOnlyList<VoiceMessageEntry> GetEntriesByState(VoiceMessageState state);
    QueueStatistics GetStatistics();
}
