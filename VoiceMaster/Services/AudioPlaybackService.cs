using Dalamud.Plugin.Services;
using VoiceMaster.DataClasses;
using VoiceMaster.Enums;
using VoiceMaster.Helper.Functional;
using VoiceMaster.Services.Queue;
using ManagedBass;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace VoiceMaster.Services;

/// <summary>
/// Service responsible for audio playback with queue integration.
/// Based on Echokraut's AudioPlaybackService pattern.
/// </summary>
public class AudioPlaybackService : IDisposable
{
    private readonly IVoiceMessageQueue _queue;
    private readonly IPluginLog _log;
    private readonly VoiceMaster.DataClasses.Configuration _config;
    private readonly IFramework _framework;
    private readonly ILipSyncHelper _lipSync;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _playbackTask;
    private readonly Dictionary<Guid, VoiceMessage> _currentlyPlayingDictionary = new();

    private bool _isPlaying;

    public event Action<EKEventId>? AutoAdvanceRequested;
    public event Action<VoiceMessage?>? CurrentMessageChanged;

    public bool IsPlaying => _isPlaying;

    public AudioPlaybackService(
        IVoiceMessageQueue queue,
        IPluginLog log,
        VoiceMaster.DataClasses.Configuration config,
        IFramework framework,
        ILipSyncHelper lipSync)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _framework = framework ?? throw new ArgumentNullException(nameof(framework));
        _lipSync = lipSync ?? throw new ArgumentNullException(nameof(lipSync));

        _cancellationTokenSource = new CancellationTokenSource();
        _playbackTask = Task.Run(() => PlaybackLoopAsync(_cancellationTokenSource.Token));
    }

    public void UpdateListenerState(Vector3 position, float frX, float frY, float frZ, float toX, float toY, float toZ)
    {
        // Configure the listener position and orientation
        PlayingHelper.AudioEngine.ConfigureListener(
            new Vector3D(position.X, position.Y, position.Z),
            new Vector3D(frX, frY, frZ),
            new Vector3D(toX, toY, toZ));
    }

    public void StopPlaying(VoiceMessage? message)
    {
        if (message == null) return;
        _isPlaying = false;
        _log.Info("Stopping voice inference");
        if (PlayingHelper.AudioEngine.GetState(message.StreamId) != VoiceMaster.Helper.Functional.PlaybackState.Stopped)
            PlayingHelper.AudioEngine.Stop(message.StreamId);
    }

    public void ClearQueue(TextSource textSource = TextSource.None)
    {
        if (textSource == TextSource.None)
            _queue.CancelAll();
        else
            _queue.CancelBySource(textSource);
    }

    private async Task PlaybackLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!_isPlaying && _queue.TryDequeueReadyToPlay(out var entry) && entry != null)
                {
                    await PlayAudioAsync(entry, cancellationToken);
                }

                await Task.Delay(100, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error in playback loop");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private async Task PlayAudioAsync(VoiceMessageEntry entry, CancellationToken cancellationToken)
    {
        var message = entry.Message;
        
        try
        {
            _log.Info("Playing audio: {Text}", message.Text[..Math.Min(50, message.Text.Length)]);
            _queue.MarkAsPlaying(entry.Id);

            // Add to currently playing dictionary
            _currentlyPlayingDictionary[message.StreamId] = message;

            // Trigger lip sync
            _ = _lipSync.TryLipSync(message);

            _isPlaying = true;

            // Simulate playback - replace with actual audio playback
            await Task.Delay(1000, cancellationToken);

            _queue.MarkAsCompleted(entry.Id);
            _isPlaying = false;

            _log.Info("Playback completed");
        }
        catch (OperationCanceledException)
        {
            _queue.MarkAsCancelled(entry.Id);
            _isPlaying = false;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error playing audio");
            _queue.MarkAsFailed(entry.Id, ex);
            _isPlaying = false;
        }
    }

    public void Dispose()
    {
        try
        {
            _cancellationTokenSource.Cancel();
            _playbackTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error during disposal");
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
        }
    }
}

/// <summary>
/// Interface for lip sync helper to avoid circular dependencies
/// </summary>
public interface ILipSyncHelper
{
    Task TryLipSync(VoiceMessage message);
}
