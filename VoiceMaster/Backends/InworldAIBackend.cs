using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using VoiceMaster.DataClasses;
using VoiceMaster.Helper.Data;
using Dalamud.Game;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ManagedBass;
using System.Runtime.InteropServices;
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

        public InworldAIBackend()
        {
            _httpClient = new HttpClient();
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

            try
            {
                var url = "https://api.inworld.ai/tts/v1/voice"; 
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = GetBasicAuthHeader();

                // Build 15: Request LINEAR16 24000Hz (Engine Default).
                // We will STRIP the WAV header and return Raw PCM to match Engine's default fallback.
                var payload = new JObject
                {
                    ["text"] = message.Text,
                    ["voiceId"] = voiceId,
                    ["audioConfig"] = new JObject
                    {
                        ["audioEncoding"] = "LINEAR16", 
                        ["sampleRateHertz"] = 24000, 
                        ["speakingRate"] = 1.0,
                        ["pitch"] = 0.0
                    },
                    ["modelId"] = "inworld-tts-1.5-max",
                };

                request.Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
                
                var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Response Status: {response.StatusCode}, Body: {responseContent.Substring(0, Math.Min(500, responseContent.Length))}", eventId);

                if (!response.IsSuccessStatusCode)
                {
                     LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"TTS request failed: {response.StatusCode} - {responseContent}", eventId);
                     return null;
                }

                var json = JObject.Parse(responseContent);
                var audioBase64 = json["audioContent"]?.ToString();
                LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"AudioContent length: {(audioBase64?.Length ?? 0)} chars", eventId); 
                
                if (!string.IsNullOrEmpty(audioBase64))
                {
                    var audioBytes = Convert.FromBase64String(audioBase64);
                    
                    // Check for RIFF header
                    if (audioBytes.Length > 12 && 
                        audioBytes[0] == 'R' && audioBytes[1] == 'I' && audioBytes[2] == 'F' && audioBytes[3] == 'F' &&
                        audioBytes[8] == 'W' && audioBytes[9] == 'A' && audioBytes[10] == 'V' && audioBytes[11] == 'E')
                    {
                        // Find data chunk and strip header
                        int dataPos = 12;
                        while (dataPos + 8 < audioBytes.Length)
                        {
                            var id = Encoding.ASCII.GetString(audioBytes, dataPos, 4);
                            int len = BitConverter.ToInt32(audioBytes, dataPos + 4);
                            if (id == "data")
                            {
                                int start = dataPos + 8;
                                int count = Math.Min(len, audioBytes.Length - start);
                                var pcm = new byte[count];
                                Buffer.BlockCopy(audioBytes, start, pcm, 0, count);
                                
                                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Build 15: Stripped WAV Header. Returning {count} bytes Raw PCM (24k).", eventId);
                                return new MemoryStream(pcm);
                            }
                            dataPos += 8 + len;
                        }
                    }
                    
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Build 15: No Header Found. Returning {audioBytes.Length} bytes Raw PCM (24k).", eventId);
                    return new MemoryStream(audioBytes);
                }
                else
                {
                     LogHelper.Error(MethodBase.GetCurrentMethod().Name, "No audioContent in response.", eventId);
                     return null;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, ex.ToString(), eventId);
                return null;
            }
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
