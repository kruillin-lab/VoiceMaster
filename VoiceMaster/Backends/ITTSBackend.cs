using Dalamud.Game;
using Dalamud.Plugin.Services;
using VoiceMaster.DataClasses;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace VoiceMaster.Backend
{
    public interface ITTSBackend
    {
        Task<List<string>> GetAvailableVoices(EKEventId eventId, bool englishOnly = true);
        Task<Stream> GenerateAudioStreamFromVoice(EKEventId eventId, VoiceMessage voiceLine, string voice, ClientLanguage language);
        Task<string> CheckReady(EKEventId eventId);
        Task<bool> ReloadService(string reloadModel, EKEventId eventId);
        void StopGenerating(EKEventId eventId);
    }
}
