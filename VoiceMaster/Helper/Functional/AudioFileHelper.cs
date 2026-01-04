using VoiceMaster.DataClasses;
using VoiceMaster.Helper.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace VoiceMaster.Helper.Functional
{
    public static class AudioFileHelper
    {
        public static Dictionary<DateTime, string> SavedFiles = new();

        public static bool LoadLocalAudio(EKEventId eventId, string localSaveLocation, VoiceMessage voiceMessage)
        {
            try
            {
                var filePath = GetLocalAudioPath(localSaveLocation, voiceMessage);

                if (File.Exists(filePath))
                {
                    voiceMessage.LoadedLocally = true;

                    // IMPORTANT: Do NOT dispose here. Playback owns the stream lifetime.
                    var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    voiceMessage.Stream = fs;

                    PlayingHelper.PlayingQueue.Add(voiceMessage);
                    LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Local file found. Location: {filePath}", eventId);
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error while loading local audio: {ex}", eventId);
            }

            return false;
        }

        public static string GetLocalAudioPath(string localSaveLocation, VoiceMessage voiceMessage)
        {
            var speakerKey = GetSpeakerKey(voiceMessage);
            var speakerFolder = GetSpeakerAudioPath(localSaveLocation, speakerKey);
            var fileName = VoiceMessageToFileName(voiceMessage?.Text ?? string.Empty);
            return Path.Combine(speakerFolder, fileName + ".wav");
        }

        public static string GetSpeakerAudioPath(string localSaveLocation, string speaker)
        {
            var basePath = string.IsNullOrWhiteSpace(localSaveLocation) ? "." : localSaveLocation;
            var safeSpeaker = SanitizePathSegment(string.IsNullOrWhiteSpace(speaker) ? "NOPERSON" : speaker);
            return Path.Combine(basePath, safeSpeaker);
        }

        private static string GetSpeakerKey(VoiceMessage voiceMessage)
        {
            try
            {
                var s = voiceMessage?.Speaker;
                if (s == null)
                    return "NOPERSON";

                // Prefer the selected backend voice identifier (stable across renames).
                if (!string.IsNullOrWhiteSpace(s.voice))
                    return s.voice;

                // Fallback to NPC name.
                if (!string.IsNullOrWhiteSpace(s.Name))
                    return s.Name;
            }
            catch
            {
                // ignored
            }

            return "NOPERSON";
        }

        public static string VoiceMessageToFileName(string voiceMessage)
        {
            voiceMessage ??= string.Empty;

            var fileName = voiceMessage.Trim();
            if (fileName.Length == 0)
                fileName = "empty";

            // Strip invalid filename characters and common punctuation that explodes filenames.
            var temp = fileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries);
            fileName = string.Join("", temp)
                .ToLowerInvariant()
                .Replace(".", "")
                .Replace("?", "")
                .Replace("!", "")
                .Replace(",", "")
                .Replace("-", "")
                .Replace("_", "");

            // Keep it reasonable for Windows path limits.
            if (fileName.Length > 120)
                fileName = fileName.Substring(0, 120);

            if (fileName.Length == 0)
                fileName = "empty";

            return fileName;
        }

        public static bool WriteStreamToFile(EKEventId eventId, string filePath, Stream stream)
        {
            LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Saving audio locally: {filePath}", eventId);

            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                // Copy to a private buffer so the caller can dispose their stream safely later.
                using var buffer = CopyToMemory(stream);

                // If it already looks like a WAV, write as-is.
                if (LooksLikeWav(buffer))
                {
                    buffer.Position = 0;
                    using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                    buffer.CopyTo(fs);
                }
                else
                {
                    buffer.Position = 0;
                    // Treat as raw PCM and wrap into WAV using plugin defaults (24kHz mono 16-bit).
                    RawPcmToWav.CreateWaveFileAsync(filePath, buffer, sampleRate: 24000, bitsPerSample: 16, channels: 1)
                        .GetAwaiter().GetResult();
                }

                SavedFiles[DateTime.Now] = filePath;
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error while saving audio locally: {ex}", eventId);
                return false;
            }
        }

        private static MemoryStream CopyToMemory(Stream stream)
        {
            var ms = new MemoryStream();
            if (stream == null)
                return ms;

            try
            {
                if (stream.CanSeek)
                    stream.Seek(0, SeekOrigin.Begin);

                stream.CopyTo(ms);
                ms.Position = 0;
                return ms;
            }
            catch
            {
                ms.Dispose();
                throw;
            }
        }

        private static bool LooksLikeWav(Stream s)
        {
            if (s == null || !s.CanRead)
                return false;

            long pos = 0;
            try
            {
                if (!s.CanSeek)
                    return false;

                pos = s.Position;
                if (s.Length < 12)
                    return false;

                Span<byte> header = stackalloc byte[12];
                s.Position = 0;
                var read = s.Read(header);
                if (read < 12)
                    return false;

                // "RIFF" .... "WAVE"
                return header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F'
                       && header[8] == (byte)'W' && header[9] == (byte)'A' && header[10] == (byte)'V' && header[11] == (byte)'E';
            }
            finally
            {
                if (s.CanSeek)
                    s.Position = pos;
            }
        }

        private static string SanitizePathSegment(string input)
        {
            input ??= string.Empty;
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(input.Where(c => !invalid.Contains(c)).ToArray()).Trim();
            return cleaned.Length == 0 ? "NOPERSON" : cleaned;
        }

        public static int DeleteLastNFiles(int nFilesToDelete = 10)
        {
            var timeStamps = SavedFiles.Keys.ToList();
            var file = "";
            var deletedFiles = 0;

            for (var i = 0; i < nFilesToDelete && timeStamps.Count > 0; i++)
            {
                try
                {
                    file = SavedFiles[timeStamps[0]];
                    File.Delete(file);
                    deletedFiles++;
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Deleted local saved file: {file}",
                        new EKEventId(0, Enums.TextSource.None));
                }
                catch (FileNotFoundException)
                { }
                catch (Exception ex)
                {
                    LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error deleting local file: {file} - {ex}",
                        new EKEventId(0, Enums.TextSource.None));
                }

                SavedFiles.Remove(timeStamps[0]);
                timeStamps.RemoveAt(0);
            }

            return deletedFiles;
        }

        public static int DeleteLastNMinutesFiles(int nMinutesFilesToDelete = 10)
        {
            var timeStamps = SavedFiles.Keys.ToList().FindAll(p => p >= DateTime.Now.AddMinutes(-nMinutesFilesToDelete));
            var file = "";
            var deletedFiles = 0;

            foreach (var timeStamp in timeStamps)
            {
                if (SavedFiles.Count <= 0)
                    break;

                try
                {
                    file = SavedFiles[timeStamp];
                    File.Delete(file);
                    deletedFiles++;
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Deleted local saved file: {file}",
                        new EKEventId(0, Enums.TextSource.None));
                }
                catch (FileNotFoundException)
                { }
                catch (Exception ex)
                {
                    LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error deleting local file: {file} - {ex}",
                        new EKEventId(0, Enums.TextSource.None));
                }

                SavedFiles.Remove(timeStamp);
            }

            return deletedFiles;
        }

        

        // Backwards-compatible alias used by ConfigWindow.
        // Removes all locally saved audio files for the given speaker (NPC/player) by deleting the speaker folder.
        public static bool RemoveSavedNpcFiles(string localSaveLocation, string speaker)
        {
            return DeleteSpeakerFiles(localSaveLocation, speaker);
        }

public static bool DeleteSpeakerFiles(string localSaveLocation, string speaker)
        {
            if (!Directory.Exists(localSaveLocation))
                return false;

            var speakerFolderPath = GetSpeakerAudioPath(localSaveLocation, speaker);

            if (!Directory.Exists(speakerFolderPath))
                return false;

            try
            {
                Directory.Delete(speakerFolderPath, true);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name,
                    $"Error deleting speaker directory: {speakerFolderPath} - {ex}",
                    new EKEventId(0, Enums.TextSource.None));
            }

            return false;
        }
    }
}
