using Dalamud.Plugin.Services;
using VoiceMaster.DataClasses;
using VoiceMaster.Enums;
using VoiceMaster.Helper.Functional;
using VoiceMaster.Services.Queue;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace VoiceMaster.Services;

/// <summary>
/// Service responsible for TTS backend communication and generation queue processing.
/// Based on Echokraut's BackendService pattern.
/// </summary>
public class BackendService : IDisposable
{
    private readonly IVoiceMessageQueue _queue;
    private readonly IPluginLog _log;
    private readonly VoiceMaster.DataClasses.Configuration _config;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Task _generationTask;

    public BackendService(
        IVoiceMessageQueue queue,
        IPluginLog log,
        VoiceMaster.DataClasses.Configuration config)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _config = config ?? throw new ArgumentNullException(nameof(config));

        _cancellationTokenSource = new CancellationTokenSource();
        _generationTask = Task.Run(() => GenerationLoopAsync(_cancellationTokenSource.Token));
    }

    public void ProcessVoiceMessage(VoiceMessage voiceMessage)
    {
        if (voiceMessage == null) throw new ArgumentNullException(nameof(voiceMessage));
        
        // Determine priority - dialogue gets priority over bubbles
        var isPriority = voiceMessage.Source switch
        {
            TextSource.AddonTalk or 
            TextSource.AddonBattleTalk or 
            TextSource.AddonCutsceneSelectString or 
            TextSource.AddonSelectString or 
            TextSource.VoiceTest => true,
            _ => false
        };
        
        _queue.Enqueue(voiceMessage, isPriority);
        _log.Debug("Enqueued voice message from {Source} (Priority: {Priority})", voiceMessage.Source, isPriority);
    }

    public void CancelAll()
    {
        _log.Info("Cancelling all voice messages");
        _queue.CancelAll();
    }

    public void CancelBySource(TextSource source)
    {
        _log.Info("Cancelling voice messages from {Source}", source);
        _queue.CancelBySource(source);
    }

    public bool IsBackendAvailable()
    {
        switch (_config.BackendSelection)
        {
            case TTSBackends.Alltalk:
                if (_config.Alltalk.LocalInstance && _config.Alltalk.LocalInstall &&
                    AlltalkInstanceHelper.InstanceRunning)
                    return true;

                if (_config.Alltalk.RemoteInstance &&
                    !string.IsNullOrWhiteSpace(_config.Alltalk.BaseUrl))
                    return true;
                break;
        }

        return false;
    }

    private async Task GenerationLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_queue.TryDequeuePendingGeneration(out var entry) && entry != null)
                {
                    await ProcessGenerationAsync(entry, cancellationToken);
                }
                
                await Task.Delay(100, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error in generation loop");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private async Task ProcessGenerationAsync(VoiceMessageEntry entry, CancellationToken cancellationToken)
    {
        var message = entry.Message;
        
        try
        {
            _queue.MarkAsGenerating(entry.Id);
            _log.Debug("Generating audio for: {Text}", message.Text[..Math.Min(50, message.Text.Length)]);
            
            // Simulate generation delay - replace with actual backend call
            await Task.Delay(100, cancellationToken);
            
            // Mark as ready to play
            _queue.MarkAsReadyToPlay(entry.Id);
            _log.Debug("Audio ready to play");
        }
        catch (Exception ex)
        {
            _queue.MarkAsFailed(entry.Id, ex);
            _log.Error(ex, "Failed to generate audio");
        }
    }

    public QueueStatistics GetStatistics()
    {
        return _queue.GetStatistics();
    }

    public void Dispose()
    {
        try
        {
            _cancellationTokenSource.Cancel();
            _generationTask.Wait(TimeSpan.FromSeconds(5));
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
