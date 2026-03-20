using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using VoiceMaster.DataClasses;
using VoiceMaster.Enums;
using VoiceMaster.Helper.Functional;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace VoiceMaster.Services;

/// <summary>
/// Orchestrates the complete voice message processing pipeline.
/// Replaces the monolithic Plugin.Say() method with clean, testable architecture.
/// Based on Echokraut's VoiceMessageProcessor pattern.
/// </summary>
public class VoiceMessageProcessor
{
    private readonly IPluginLog _log;
    private readonly BackendService _backend;
    private readonly Configuration _config;
    private readonly IClientState _clientState;

    public VoiceMessageProcessor(
        IPluginLog log,
        BackendService backend,
        Configuration config,
        IClientState clientState)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
    }

    public async Task ProcessSpeechAsync(Guid eventId, IGameObject? speaker, SeString speakerName, string textValue, TextSource source)
    {
        try
        {
            // Step 1: Check backend availability
            if (!_backend.IsBackendAvailable())
            {
                _log.Debug("Backend not available, skipping");
                return;
            }

            _log.Debug("Preparing for inference: {Speaker} - {Text} - {Source}", 
                speakerName.TextValue, textValue, source);

            // Step 2: Clean and normalize text
            var cleanText = CleanText(textValue);
            
            if (string.IsNullOrWhiteSpace(cleanText))
            {
                _log.Debug("Text not speakable after cleaning");
                return;
            }

            // Step 3: Create NPC data
            var npcData = CreateNpcData(speaker, speakerName.TextValue, source);

            // Step 4: Check if NPC/voice is muted
            if (IsNpcMuted(npcData, source))
            {
                _log.Debug("NPC is muted: {Npc}", npcData.Name);
                return;
            }

            // Step 5: Calculate volume
            var finalVolume = CalculateVolume(npcData, source);
            
            if (finalVolume <= 0)
            {
                _log.Debug("Volume is 0, skipping");
                return;
            }

            // Step 6: Build voice message
            var is3d = ShouldUse3DAudio(source);
            var voiceMessage = new VoiceMessage
            {
                SpeakerObj = speaker,
                SpeakerFollowObj = is3d && speaker != null ? speaker : null,
                Source = source,
                Speaker = npcData,
                Text = cleanText,
                OriginalText = textValue,
                Language = _clientState.ClientLanguage,
                EventId = new EKEventId(eventId.GetHashCode(), source),
                Volume = finalVolume,
                IsLastInDialogue = true,
                Is3D = is3d
            };

            _log.Debug("Processing voice message: {Info}", voiceMessage.GetDebugInfo());

            // Step 7: Process through backend
            _backend.ProcessVoiceMessage(voiceMessage);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error processing speech");
        }
    }

    private string CleanText(string text)
    {
        // Apply text transformations
        var cleanText = TalkTextHelper.StripAngleBracketedText(text);
        cleanText = TalkTextHelper.ReplaceSsmlTokens(cleanText);
        cleanText = TalkTextHelper.NormalizePunctuation(cleanText);
        cleanText = _config.RemoveStutters ? TalkTextHelper.RemoveStutters(cleanText) : cleanText;
        
        return cleanText.Trim();
    }

    private NpcMapData CreateNpcData(IGameObject? speaker, string speakerName, TextSource source)
    {
        var objectKind = speaker?.ObjectKind ?? ObjectKind.None;
        var npcData = new NpcMapData(objectKind)
        {
            Name = speakerName,
            ObjectKind = objectKind
        };

        return npcData;
    }

    private bool IsNpcMuted(NpcMapData npcData, TextSource source)
    {
        return source switch
        {
            TextSource.AddonBubble => !npcData.IsEnabledBubble || npcData.VolumeBubble == 0f,
            _ => !npcData.IsEnabled || npcData.Volume == 0f
        };
    }

    private float CalculateVolume(NpcMapData npcData, TextSource source)
    {
        var npcVolume = source == TextSource.AddonBubble ? npcData.VolumeBubble : npcData.Volume;
        // Use VolumeHelper to get the base voice volume from game settings
        var baseVolume = VolumeHelper.GetVoiceVolume(new EKEventId(0, source));
        return npcVolume * baseVolume;
    }

    private bool ShouldUse3DAudio(TextSource source)
    {
        return source switch
        {
            TextSource.AddonBubble => true,
            TextSource.AddonTalk => _config.VoiceDialogueIn3D,
            TextSource.Chat => _config.VoiceChatIn3D,
            _ => false
        };
    }
}
