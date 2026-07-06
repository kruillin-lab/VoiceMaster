using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, string> _displayNameToVoiceId = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _voiceIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly NpcVoiceProfileStore _profileStore;
        private readonly ImmersionEngine _immersion;

        public InworldAIBackend()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
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
        // Drains the NDJSON response (each line's decoded chunk is raw PCM with the
        // WAV header stripped) into a seekable MemoryStream, then returns it so the
        // audio can be both played and saved to the local cache.
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

                // Buffer-then-play: fully drain the NDJSON response into a seekable
                // MemoryStream of raw PCM. A live one-shot pipe stream cannot be re-read,
                // so it could never be saved to the local cache; a seekable buffer can be
                // both played AND written to disk (see AudioFileHelper/PlayingHelper cache).
                var pcmBuffer = new MemoryStream();
                int totalBytes = 0;
                int chunkCount = 0;

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
                            // contains a complete WAV header. We need raw PCM, so strip the
                            // WAV header from every chunk (matches the blocking path, which
                            // returns headerless 24kHz/16-bit/mono PCM the engine plays with
                            // its default format).
                            var pcmBytes = StripWavHeader(chunkBytes);

                            if (pcmBytes == null || pcmBytes.Length == 0)
                                continue;

                            chunkCount++;
                            totalBytes += pcmBytes.Length;
                            pcmBuffer.Write(pcmBytes, 0, pcmBytes.Length);
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

                if (totalBytes == 0)
                {
                    pcmBuffer.Dispose();
                    return null;
                }

                // Rewind so both the audio engine and the local-cache saver read from the start.
                pcmBuffer.Position = 0;
                return pcmBuffer;
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
                    $"Response Status: {response.StatusCode}, Content-Length: {responseContent.Length} chars", eventId);

                if (!response.IsSuccessStatusCode)
                {
                    LogHelper.Error(MethodBase.GetCurrentMethod().Name,
                        $"TTS request failed: {response.StatusCode} - {responseContent[..Math.Min(300, responseContent.Length)]}", eventId);
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

        public async Task<List<string>> GetAvailableVoices(EKEventId eventId, bool englishOnly = true)
        {
            var voices = new List<string>();
            try
            {
                var url = "https://api.inworld.ai/voices/v1/voices";
                _displayNameToVoiceId.Clear();
                _voiceIds.Clear();

                string pageToken = "";
                do
                {
                    var queryUrl = url;
                    var separator = "?";
                    
                    if (englishOnly)
                    {
                        queryUrl += "?filter=lang_code%20%3D%20%22en%22";
                        separator = "&";
                    }
                    
                    if (!string.IsNullOrEmpty(pageToken))
                    {
                        queryUrl += $"{separator}pageToken={Uri.EscapeDataString(pageToken)}";
                    }
                    
                    var request = new HttpRequestMessage(HttpMethod.Get, queryUrl);
                    request.Headers.Authorization = GetBasicAuthHeader();

                    var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        LogHelper.Error(MethodBase.GetCurrentMethod().Name,
                            $"Voices API returned {response.StatusCode}", eventId);
                        return voices;
                    }

                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var json = JObject.Parse(content);

                    if (json["voices"] != null)
                    {
                        foreach (var voiceNode in json["voices"])
                        {
                            var id = voiceNode["voiceId"]?.ToString();
                            if (string.IsNullOrWhiteSpace(id))
                                continue;

                            _voiceIds.Add(id);

                            var displayName = voiceNode["displayName"]?.ToString() ?? id;
                            _displayNameToVoiceId[displayName] = id;
                            voices.Add(displayName);
                        }
                    }

                    pageToken = json["nextPageToken"]?.ToString() ?? "";
                }
                while (!string.IsNullOrEmpty(pageToken));
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
                 var voices = await GetAvailableVoices(eventId, englishOnly: false);
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
                await GetAvailableVoices(eventId, englishOnly: false);

            if (_displayNameToVoiceId.TryGetValue(selectedVoice, out var mappedId))
                return mappedId;

            if (_voiceIds.Contains(selectedVoice))
                return selectedVoice;

            // Backward/forward compatibility: if value is unknown, pass through as raw voiceId.
            return selectedVoice;
        }
    }
}
