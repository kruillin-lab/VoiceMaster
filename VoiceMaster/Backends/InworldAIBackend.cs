using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VoiceMaster.DataClasses;
using VoiceMaster.Helper.Data;
using VoiceMaster.Helper.Functional;
using Dalamud.Game;
using System.Reflection;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace VoiceMaster.Backend
{
    public class InworldAIBackend : ITTSBackend
    {
        private string _accessToken = "";
        private DateTime _tokenExpiration = DateTime.MinValue;
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, string> _displayNameToVoiceId = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _voiceIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly NpcVoiceProfileStore _profileStore;
        private readonly ImmersionEngine _immersion;

        public InworldAIBackend()
        {
            _httpClient = new HttpClient();
            var configDir = Plugin.PluginInterface.ConfigDirectory.FullName;
            _profileStore = new NpcVoiceProfileStore(configDir);
            _immersion = new ImmersionEngine(_profileStore);
        }

        private AuthenticationHeaderValue GetBasicAuthHeader()
        {
            var apiKey = Plugin.Configuration.InworldAI.ApiKey;
            var apiSecret = Plugin.Configuration.InworldAI.ApiSecret;
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
                throw new Exception("InworldAI API Key or Secret not configured.");

            var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{apiSecret}"));
            return new AuthenticationHeaderValue("Basic", authString);
        }

        private async Task EnsureAccessToken(EKEventId eventId)
        {
            await Task.CompletedTask;
        }

        public async Task<Stream> GenerateAudioStreamFromVoice(EKEventId eventId, VoiceMessage message, string voice, ClientLanguage language)
        {
            var selectedVoice = voice;
            if (string.IsNullOrWhiteSpace(selectedVoice))
                selectedVoice = Plugin.Configuration.InworldAI.CharacterId;

            var voiceId = await ResolveVoiceId(selectedVoice, eventId);
            if (string.IsNullOrWhiteSpace(voiceId))
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, "No Inworld voice configured/resolved.", eventId);
                return null;
            }

            if (Plugin.Configuration.InworldAI.StreamingEnabled)
                return await GenerateStreamingAsync(eventId, message, voiceId);
            else
                return await GenerateBlockingAsync(eventId, message, voiceId);
        }

        // ---------------------------------------------------------------------------
        // Streaming path: POST /tts/v1/voice:stream
        // Returns a Pipe reader stream that audio engine can consume immediately.
        // Chunks arrive as NDJSON lines; each decoded chunk is raw PCM (WAV header
        // stripped) written into the pipe as it arrives.
        // ---------------------------------------------------------------------------
        private async Task<Stream> GenerateStreamingAsync(EKEventId eventId, VoiceMessage message, string voiceId)
        {
            var ttsParams = _immersion.Resolve(message);
            try
            {
                var url = "https://api.inworld.ai/tts/v1/voice:stream";
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = GetBasicAuthHeader();

                var payload = new JObject
                {
                    ["text"] = message.Text,
                    ["voiceId"] = voiceId,
                    ["modelId"] = ttsParams.ModelId,
                    ["audioConfig"] = new JObject
                    {
                        ["audioEncoding"] = "LINEAR16",
                        ["sampleRateHertz"] = 24000,
                        ["speakingRate"] = ttsParams.SpeakingRate,
                        ["temperature"] = ttsParams.Temperature
                    }
                };

                request.Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

                // ResponseHeadersRead: return control as soon as headers arrive,
                // before the body has been received.
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                                               .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    LogHelper.Error(MethodBase.GetCurrentMethod().Name,
                        $"Streaming TTS request failed: {response.StatusCode} - {errorBody}", eventId);
                    response.Dispose();
                    return null;
                }

                LogHelper.Info(MethodBase.GetCurrentMethod().Name,
                    $"Streaming TTS started for: {message.Text[..Math.Min(40, message.Text.Length)]}", eventId);

                // Create a pipe. We write decoded PCM into Writer; caller reads from Reader.
                var pipe = new Pipe();

                // Background task: read NDJSON lines and write PCM chunks to pipe.
                _ = Task.Run(async () =>
                {
                    bool firstChunk = true;
                    int totalBytes = 0;
                    int chunkCount = 0;

                    try
                    {
                        using (response)
                        await using (var bodyStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var reader = new StreamReader(bodyStream, Encoding.UTF8))
                        {
                            string line;
                            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                            {
                                if (string.IsNullOrWhiteSpace(line))
                                    continue;

                                try
                                {
                                    var json = JObject.Parse(line);
                                    var audioBase64 = json["result"]?["audioContent"]?.ToString();

                                    if (string.IsNullOrEmpty(audioBase64))
                                        continue;

                                    var chunkBytes = Convert.FromBase64String(audioBase64);

                                    // Per Inworld API docs: with LINEAR16 streaming, EVERY chunk
                                    // contains a complete WAV header. We need raw PCM for BASS,
                                    // so strip the WAV header from every chunk.
                                    var pcmBytes = StripWavHeader(chunkBytes);

                                    if (pcmBytes == null || pcmBytes.Length == 0)
                                        continue;

                                    // On the very first chunk, write a synthetic WAV header so
                                    // the audio engine knows the format (24kHz, 16-bit mono PCM).
                                    if (firstChunk)
                                    {
                                        firstChunk = false;
                                        // We return raw PCM without a header to match the existing
                                        // engine behaviour (same as the blocking path which strips
                                        // the WAV header before returning).
                                    }

                                    chunkCount++;
                                    totalBytes += pcmBytes.Length;

                                    var memory = pipe.Writer.GetMemory(pcmBytes.Length);
                                    pcmBytes.AsSpan().CopyTo(memory.Span);
                                    pipe.Writer.Advance(pcmBytes.Length);

                                    var flushResult = await pipe.Writer.FlushAsync().ConfigureAwait(false);
                                    if (flushResult.IsCompleted || flushResult.IsCanceled)
                                        break;
                                }
                                catch (Exception lineEx)
                                {
                                    LogHelper.Error(MethodBase.GetCurrentMethod().Name,
                                        $"Error processing NDJSON line: {lineEx.Message}", eventId);
                                }
                            }
                        }

                        LogHelper.Info(MethodBase.GetCurrentMethod().Name,
                            $"Streaming TTS complete: {chunkCount} chunks, {totalBytes} bytes PCM", eventId);
                        await pipe.Writer.CompleteAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error(MethodBase.GetCurrentMethod().Name,
                            $"Streaming TTS background task error: {ex}", eventId);
                        await pipe.Writer.CompleteAsync(ex).ConfigureAwait(false);
                    }
                });

                // Return the reader side immediately — audio engine starts consuming
                // as soon as the first PCM bytes are written above.
                return pipe.Reader.AsStream();
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, ex.ToString(), eventId);
                return null;
            }
        }

        // ---------------------------------------------------------------------------
        // Blocking fallback path: POST /tts/v1/voice (original behaviour)
        // Buffers the entire response before returning.
        // ---------------------------------------------------------------------------
        private async Task<Stream> GenerateBlockingAsync(EKEventId eventId, VoiceMessage message, string voiceId)
        {
            var ttsParams = _immersion.Resolve(message);
            try
            {
                var url = "https://api.inworld.ai/tts/v1/voice";
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = GetBasicAuthHeader();

                var payload = new JObject
                {
                    ["text"] = message.Text,
                    ["voiceId"] = voiceId,
                    ["audioConfig"] = new JObject
                    {
                        ["audioEncoding"] = "LINEAR16",
                        ["sampleRateHertz"] = 24000,
                        ["speakingRate"] = ttsParams.SpeakingRate,
                        ["temperature"] = ttsParams.Temperature
                    },
                    ["modelId"] = ttsParams.ModelId,
                };

                request.Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                LogHelper.Debug(MethodBase.GetCurrentMethod().Name,
                    $"Response Status: {response.StatusCode}, Body: {responseContent[..Math.Min(500, responseContent.Length)]}", eventId);

                if (!response.IsSuccessStatusCode)
                {
                    LogHelper.Error(MethodBase.GetCurrentMethod().Name,
                        $"TTS request failed: {response.StatusCode} - {responseContent}", eventId);
                    return null;
                }

                var json = JObject.Parse(responseContent);
                var audioBase64 = json["audioContent"]?.ToString();
                LogHelper.Debug(MethodBase.GetCurrentMethod().Name,
                    $"AudioContent length: {(audioBase64?.Length ?? 0)} chars", eventId);

                if (!string.IsNullOrEmpty(audioBase64))
                {
                    var audioBytes = Convert.FromBase64String(audioBase64);
                    var pcm = StripWavHeader(audioBytes) ?? audioBytes;
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name,
                        $"Blocking path: returning {pcm.Length} bytes PCM", eventId);
                    return new MemoryStream(pcm);
                }

                LogHelper.Error(MethodBase.GetCurrentMethod().Name, "No audioContent in response.", eventId);
                return null;
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, ex.ToString(), eventId);
                return null;
            }
        }

        // ---------------------------------------------------------------------------
        // Shared helper: strip RIFF/WAV header, return raw PCM bytes.
        // Returns null if the input is not a WAV file (caller should use bytes as-is).
        // ---------------------------------------------------------------------------
        private static byte[] StripWavHeader(byte[] audioBytes)
        {
            if (audioBytes == null || audioBytes.Length <= 12)
                return null;

            // Check RIFF....WAVE signature
            if (audioBytes[0] != 'R' || audioBytes[1] != 'I' || audioBytes[2] != 'F' || audioBytes[3] != 'F' ||
                audioBytes[8] != 'W' || audioBytes[9] != 'A' || audioBytes[10] != 'V' || audioBytes[11] != 'E')
                return null; // not a WAV — caller decides what to do

            // Scan chunks to find "data"
            int pos = 12;
            while (pos + 8 <= audioBytes.Length)
            {
                var chunkId = Encoding.ASCII.GetString(audioBytes, pos, 4);
                int chunkLen = BitConverter.ToInt32(audioBytes, pos + 4);

                if (chunkId == "data")
                {
                    int start = pos + 8;
                    int count = Math.Min(chunkLen, audioBytes.Length - start);
                    var pcm = new byte[count];
                    Buffer.BlockCopy(audioBytes, start, pcm, 0, count);
                    return pcm;
                }

                // Skip to next chunk (chunks are word-aligned)
                pos += 8 + chunkLen;
                if (chunkLen % 2 != 0) pos++; // padding byte
            }

            return null; // data chunk not found
        }

        public async Task<List<string>> GetAvailableVoices(EKEventId eventId)
        {
            var voices = new List<string>();
            try
            {
                var url = "https://api.inworld.ai/tts/v1/voices";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = GetBasicAuthHeader();

                var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                     return voices;
                }

                var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var json = JObject.Parse(content);
                _displayNameToVoiceId.Clear();
                _voiceIds.Clear();

                if (json["voices"] != null)
                {
                    foreach (var voiceNode in json["voices"])
                    {
                        var id = voiceNode["voiceId"]?.ToString() ?? voiceNode["id"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            _voiceIds.Add(id);

                            var displayName =
                                voiceNode["displayName"]?.ToString() ??
                                voiceNode["name"]?.ToString() ??
                                id;

                            if (!string.IsNullOrWhiteSpace(displayName))
                            {
                                _displayNameToVoiceId[displayName] = id;
                                voices.Add(displayName);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, ex.ToString(), eventId);
            }
            return voices.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public async Task<string> CheckReady(EKEventId eventId)
        {
            try
            {
                 var voices = await GetAvailableVoices(eventId);
                 return voices.Count > 0 ? "Ready" : "NotReady (No characters found or Auth failed)";
            }
            catch
            {
                return "NotReady";
            }
        }

        public async Task<bool> ReloadService(string reloadModel, EKEventId eventId)
        {
            return await Task.FromResult(true);
        }

        public void StopGenerating(EKEventId eventId)
        {
        }

        private async Task<string> ResolveVoiceId(string selectedVoice, EKEventId eventId)
        {
            if (string.IsNullOrWhiteSpace(selectedVoice))
                return string.Empty;

            if (_displayNameToVoiceId.Count == 0 && _voiceIds.Count == 0)
                await GetAvailableVoices(eventId);

            if (_displayNameToVoiceId.TryGetValue(selectedVoice, out var mappedId))
                return mappedId;

            if (_voiceIds.Contains(selectedVoice))
                return selectedVoice;

            // Backward/forward compatibility: if value is unknown, pass through as raw voiceId.
            return selectedVoice;
        }
    }
}
