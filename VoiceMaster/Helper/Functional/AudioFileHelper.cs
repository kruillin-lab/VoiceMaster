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
        // Key = time added, Value = full file path on disk.
        public static Dictionary<DateTime, string> SavedFiles = new Dictionary<DateTime, string>();

        public static bool LoadLocalAudio(EKEventId eventId, string localSaveLocation, VoiceMessage voiceMessage)
        {
            try
            {
                var filePath = GetLocalAudioPath(localSaveLocation, voiceMessage);

                if (File.Exists(filePath))
                {
                    voiceMessage.LoadedLocally = true;

                    // IMPORTANT:
                    // Do NOT wrap this in a using/using var.
                    // The playback pipeline owns the stream lifetime and will dispose it after playback.
                    var mainOutputStream = new WavFileReader(filePath);
                    voiceMessage.Stream = mainOutputStream;

                    PlayingHelper.PlayingQueue.Add(voiceMessage);

                    LogHelper.Debug(MethodBase.GetCurrentMethod().Name,
                        $"Local file found. Location: {filePath}", eventId);

                    return true;
                }

                LogHelper.Debug(MethodBase.GetCurrentMethod().Name,
                    $"No local file found. Location searched: {filePath}", eventId);
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name,
                    $"Error while loading local audio: {ex}", eventId);
            }

            return false;
        }

        public static string GetLocalAudioPath(string localSaveLocation, VoiceMessage voiceMessage)
        {
            var speakerName = voiceMessage?.Speaker?.Name ?? "Unknown";
            var speakerFolder = GetSpeakerAudioPath(localSaveLocation, speakerName);
            var fileName = VoiceMessageToFileName(voiceMessage?.Text ?? string.Empty);

            return Path.Combine(speakerFolder, fileName);
        }

        public static string GetSpeakerAudioPath(string localSaveLocation, string speaker)
        {
            // Keep the folder name file-system safe.
            var safeSpeaker = string.Join("_", (speaker ?? "Unknown")
                .Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
                .Trim();

            return Path.Combine(localSaveLocation, safeSpeaker);
        }

        public static string VoiceMessageToFileName(string voiceMessage)
        {
            // Keep the filename file-system safe and reasonably bounded.
            var safe = string.Join("_", (voiceMessage ?? string.Empty)
                .Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
                .Trim();

            if (string.IsNullOrWhiteSpace(safe))
                safe = "Empty";

            // Avoid insane path lengths.
            const int max = 160;
            if (safe.Length > max)
                safe = safe.Substring(0, max);

            return safe + ".wav";
        }

        public static bool WriteStreamToFile(EKEventId eventId, string filePath, Stream stream)
        {
            try
            {
                LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Saving audio locally: {filePath}", eventId);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                if (stream.CanSeek)
                    stream.Seek(0, SeekOrigin.Begin);

                // IMPORTANT:
                // CreateWaveFileAsync writes the WAV header and then copies PCM bytes.
                // If we fire-and-forget this Task, playback may open the file before it is finished,
                // which can cause empty/partial reads and a BASS push-stream "Ended" failure.
                RawPcmToWav.CreateWaveFileAsync(filePath, stream, sampleRate: 24000, bitsPerSample: 16, channels: 1)
                    .GetAwaiter().GetResult();

                SavedFiles[DateTime.Now] = filePath;

                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name,
                    $"Error while saving audio locally: {ex}", eventId);
            }

            return false;
        }

        public static int DeleteLastNFiles(int nFilesToDelete = 10)
        {
            try
            {
                if (SavedFiles.Count == 0) return 0;

                int deleted = 0;

                foreach (var kv in SavedFiles.OrderByDescending(k => k.Key).Take(nFilesToDelete).ToList())
                {
                    var file = kv.Value;
                    try
                    {
                        if (File.Exists(file))
                        {
                            File.Delete(file);
                            deleted++;
                        }
                    }
                    catch
                    {
                        // Ignore per-file failures; continue cleanup.
                    }
                    finally
                    {
                        SavedFiles.Remove(kv.Key);
                    }
                }

                return deleted;
            }
            catch
            {
                return 0;
            }
        }

        public static int DeleteLastNMinutesFiles(int nMinutesFilesToDelete = 10)
        {
            try
            {
                if (SavedFiles.Count == 0) return 0;

                var cutoff = DateTime.Now.AddMinutes(-Math.Abs(nMinutesFilesToDelete));
                int deleted = 0;

                foreach (var kv in SavedFiles.Where(k => k.Key >= cutoff).OrderByDescending(k => k.Key).ToList())
                {
                    var file = kv.Value;
                    try
                    {
                        if (File.Exists(file))
                        {
                            File.Delete(file);
                            deleted++;
                        }
                    }
                    catch
                    {
                        // Ignore per-file failures; continue cleanup.
                    }
                    finally
                    {
                        SavedFiles.Remove(kv.Key);
                    }
                }

                return deleted;
            }
            catch
            {
                return 0;
            }
        }

        public static bool RemoveSavedNpcFiles(string localSaveLocation, string speaker)
        {
            try
            {
                var speakerFolderPath = GetSpeakerAudioPath(localSaveLocation, speaker);

                if (!Directory.Exists(speakerFolderPath))
                    return true;

                Directory.Delete(speakerFolderPath, true);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name,
                    $"Failed to remove saved NPC files for speaker '{speaker}': {ex}",
                    new EKEventId(0, Enums.TextSource.None));
                return false;
            }
        }
    }
}
