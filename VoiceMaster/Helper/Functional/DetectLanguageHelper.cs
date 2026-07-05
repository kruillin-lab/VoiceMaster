using Dalamud.Game;
using VoiceMaster.DataClasses;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using VoiceMaster.Helper.Data;
using Dalamud.Plugin.Services;

namespace VoiceMaster.Helper.Functional
{
    public static class DetectLanguageHelper
    {

        private static HttpClient? httpClient;

        public static void Initialize()
        {
            httpClient?.Dispose();
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                MaxConnectionsPerServer = 10,
                EnableMultipleHttp2Connections = true,
            };
            httpClient = new HttpClient(handler);
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "VoiceMaster/1.0");
        }

        public static void Dispose()
        {
            httpClient?.Dispose();
            httpClient = null;
        }

        public static async Task<ClientLanguage> GetTextLanguage(string text, EKEventId eventId)
        {
            var languageString = "en";
            if (Plugin.Configuration.VoiceChatLanguageAPIKey.Length == 32)
            {
                try
                {
                    if (httpClient == null)
                        Initialize();

                    var detectLanguagesApiKey = Plugin.Configuration.VoiceChatLanguageAPIKey;
                    var uriBuilder = new UriBuilder(@"https://ws.detectlanguage.com/0.2/") { Path = "/0.2/detect" };
                    var detectData = new Dictionary<string, string>();
                    detectData.Add("q", text);
                    var httpRequestMessage = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = uriBuilder.Uri,

                        Headers = {
                            { HttpRequestHeader.Authorization.ToString(), $"Bearer {detectLanguagesApiKey}" },
                            { HttpRequestHeader.Accept.ToString(), "application/json" }
                        },
                        Content = new FormUrlEncodedContent(detectData)
                    };
                    var response = await httpClient!.SendAsync(httpRequestMessage).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    var jsonResult = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    dynamic resultObj = JObject.Parse(jsonResult);

                    if (resultObj.data.detections.Count > 0)
                        languageString = resultObj.data.detections[0].language;
                    else
                        languageString = "en";
                }
                catch (HttpRequestException ex)
                {
                    var innerMsg = ex.InnerException?.Message ?? "No inner exception";
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Language detection HTTP error ({innerMsg}). Using client language.", eventId);
                    return Plugin.ClientState.ClientLanguage;
                }
                catch (TaskCanceledException ex)
                {
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Language detection timed out/canceled. Using client language. Exception: {ex.Message}", eventId);
                    return Plugin.ClientState.ClientLanguage;
                }
                catch (OperationCanceledException ex)
                {
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Language detection canceled. Using client language. Exception: {ex.Message}", eventId);
                    return Plugin.ClientState.ClientLanguage;
                }
                catch (Exception ex)
                {
                    LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error while detecting language. Using client language. Exception: {ex}", eventId);
                    return Plugin.ClientState.ClientLanguage;
                }

                var language = ClientLanguage.English;
                switch (languageString)
                {
                    case "de":
                        language = ClientLanguage.German;
                        break;
                    case "en":
                        language = ClientLanguage.English;
                        break;
                    case "ja":
                        language = ClientLanguage.Japanese;
                        break;
                    case "fr":
                        language = ClientLanguage.French;
                        break;
                }

                LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Found language for chat: {languageString}/{language.ToString()}", eventId);
                return language;
            }
            else
            {
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Skipping language detection for chat. Using client language.", eventId);

                return Plugin.ClientState.ClientLanguage;
            }
        }
    }
}
