using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using Dalamud.Game;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using VoiceMaster.DataClasses;
using VoiceMaster.Enums;
using VoiceMaster.Helper.Data;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;

namespace VoiceMaster.Helper.Addons;

public class SoundHelper : IDisposable
{
    // Signature strings drawn from Anna Clemens's Sound Filter plugin -
    // https://git.anna.lgbt/ascclemens/SoundFilter/src/commit/3b8512b4cd2f3ea0a0d162db4fa251ccb61f7dc4/SoundFilter/Filter.cs#L12
    private const string LoadSoundFileSig = "E8 ?? ?? ?? ?? 48 85 C0 75 05 40 B7 F6";

    /// <summary>
    /// Event triggered when a voice line is detected and loaded.
    /// The string parameter contains the filename of the voice line.
    /// </summary>
    public event Action<string>? VoiceLineDetected;

    private const string PlaySpecificSoundSig =
        "48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 20 33 F6 8B DA 48 8B F9 0F BA E2 0F";

    private delegate nint LoadSoundFileDelegate(nint resourceHandlePtr, uint arg2);

    private delegate nint PlaySpecificSoundDelegate(nint soundPtr, int arg2);

    private readonly Hook<LoadSoundFileDelegate>? loadSoundFileHook;
    private readonly Hook<PlaySpecificSoundDelegate>? playSpecificSoundHook;

    private static readonly int ResourceDataOffset = Marshal.SizeOf<ResourceHandle>();
    private static readonly int SoundDataOffset = Marshal.SizeOf<nint>();

    private const string SoundContainerFileNameSuffix = ".scd";

    private static readonly Regex IgnoredSoundFileNameRegex = new(
        @"^(bgcommon|music|sound/(battle|foot|instruments|strm|vfx|voice/Vo_Emote|zingle))/");

    private static readonly Regex VoiceLineFileNameRegex = new(@"^cut/.*/(vo_|voice)");
    private static readonly Regex BattleVoiceLineFileNameRegex = new(@"^sound/.*/(Vo_Line)");
    private readonly HashSet<nint> knownVoiceLinePtrs = new();
    
    // Voice line collision avoidance: track active voice line playback
    private static readonly HashSet<nint> activeVoiceLinePtrs = new();
    private static readonly object voiceLineLock = new object();
    
    /// <summary>
    /// Returns true if any game voice line is currently playing.
    /// Used by TTS system to avoid overlapping with game audio.
    /// </summary>
    public static bool IsVoiceLinePlaying()
    {
        lock (voiceLineLock)
        {
            // Clean up any pointers that are no longer valid (disposed)
            var toRemove = new List<nint>();
            foreach (var ptr in activeVoiceLinePtrs)
            {
                try
                {
                    // Try to read a byte to check if memory is still accessible
                    _ = Marshal.ReadByte(ptr);
                }
                catch
                {
                    toRemove.Add(ptr);
                }
            }
            foreach (var ptr in toRemove)
                activeVoiceLinePtrs.Remove(ptr);
            
            return activeVoiceLinePtrs.Count > 0;
        }
    }
    
    /// <summary>
    /// Waits for any active voice lines to finish playing.
    /// Returns true if waited, false if no voice lines were playing.
    /// </summary>
    public static bool WaitForVoiceLineToFinish(TimeSpan timeout)
    {
        var startTime = DateTime.UtcNow;
        var didWait = false;
        
        while (IsVoiceLinePlaying())
        {
            didWait = true;
            if (DateTime.UtcNow - startTime > timeout)
                break;
            Thread.Sleep(50);
        }
        
        return didWait;
    }
    private readonly Dictionary<nint, string> knownVoiceLinesMap = new();
    public SoundHelper()
    {
        if (Plugin.SigScanner.TryScanText(LoadSoundFileSig, out var loadSoundFilePtr))
        {
            loadSoundFileHook =
                Plugin.GameInteropProvider.HookFromAddress<LoadSoundFileDelegate>(loadSoundFilePtr, LoadSoundFileDetour);
            loadSoundFileHook.Enable();
            LogHelper.Debug(MethodBase.GetCurrentMethod().Name, "Hooked into LoadSoundFile", new EKEventId(0, TextSource.AddonBattleTalk));
            LogHelper.Debug(MethodBase.GetCurrentMethod().Name, "Hooked into LoadSoundFile", new EKEventId(0, TextSource.AddonTalk));
        }
        else
        {
            LogHelper.Error(MethodBase.GetCurrentMethod().Name, "Failed to hook into LoadSoundFile", new EKEventId(0, TextSource.AddonBattleTalk));
            LogHelper.Error(MethodBase.GetCurrentMethod().Name, "Failed to hook into LoadSoundFile", new EKEventId(0, TextSource.AddonTalk));
        }

        if (Plugin.SigScanner.TryScanText(PlaySpecificSoundSig, out var playSpecificSoundPtr))
        {
            playSpecificSoundHook =
                Plugin.GameInteropProvider.HookFromAddress<PlaySpecificSoundDelegate>(playSpecificSoundPtr, PlaySpecificSoundDetour);
            playSpecificSoundHook.Enable();
            LogHelper.Debug(MethodBase.GetCurrentMethod().Name, "Hooked into PlaySpecificSound", new EKEventId(0, TextSource.AddonBattleTalk));
            LogHelper.Debug(MethodBase.GetCurrentMethod().Name, "Hooked into PlaySpecificSound", new EKEventId(0, TextSource.AddonTalk));
        }
        else
        {
            LogHelper.Error(MethodBase.GetCurrentMethod().Name, "Failed to hook into PlaySpecificSound", new EKEventId(0, TextSource.AddonBattleTalk));
            LogHelper.Error(MethodBase.GetCurrentMethod().Name, "Failed to hook into PlaySpecificSound", new EKEventId(0, TextSource.AddonTalk));
        }
    }

    public void Dispose()
    {
        loadSoundFileHook?.Dispose();
        playSpecificSoundHook?.Dispose();
        
        lock (voiceLineLock)
        {
            activeVoiceLinePtrs.Clear();
        }
    }

    private nint LoadSoundFileDetour(nint resourceHandlePtr, uint arg2)
    {
        var result = loadSoundFileHook!.Original(resourceHandlePtr, arg2);

        try
        {
            string fileName;
            unsafe
            {
                fileName = ((ResourceHandle*)resourceHandlePtr)->FileName.ToString();
            }

            if (fileName.EndsWith(SoundContainerFileNameSuffix))
            {
                var resourceDataPtr = Marshal.ReadIntPtr(resourceHandlePtr + ResourceDataOffset);
                if (resourceDataPtr != nint.Zero)
                {
                    var isVoiceLine = false;

                    if (!IgnoredSoundFileNameRegex.IsMatch(fileName))
                    {
                        if (VoiceLineFileNameRegex.IsMatch(fileName) || BattleVoiceLineFileNameRegex.IsMatch(fileName))
                        {
                            isVoiceLine = true;
                        }
                    }

                    if (isVoiceLine)
                    {
                        LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Discovered voice line at address {resourceDataPtr:x}", new EKEventId(0, TextSource.AddonBattleTalk));
                        LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Discovered voice line at address {resourceDataPtr:x}", new EKEventId(0, TextSource.AddonTalk));
                        knownVoiceLinePtrs.Add(resourceDataPtr);
                        knownVoiceLinesMap.Add(resourceDataPtr, fileName);
                        VoiceLineDetected?.Invoke(fileName);
                    }
                    else
                    {
                        // Addresses can be reused, so a non-voice-line sound may be loaded to an address previously
                        // occupied by a voice line.
                        if (knownVoiceLinePtrs.Remove(resourceDataPtr))
                        {
                            knownVoiceLinesMap.Remove(resourceDataPtr);
                            LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Cleared voice line from address {resourceDataPtr:x} (address reused by: {fileName})", new EKEventId(0, TextSource.AddonBattleTalk));
                            LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Cleared voice line from address {resourceDataPtr:x} (address reused by: {fileName})", new EKEventId(0, TextSource.AddonTalk));
                        }
                    }
                }
            }
        }
        catch (Exception exc)
        {
            LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error in LoadSoundFile detour: {exc}", new EKEventId(0, TextSource.AddonBattleTalk));
            LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error in LoadSoundFile detour: {exc}", new EKEventId(0, TextSource.AddonTalk));
        }

        return result;
    }

    private nint PlaySpecificSoundDetour(nint soundPtr, int arg2)
    {
        var result = playSpecificSoundHook!.Original(soundPtr, arg2);

        try
        {
            var soundDataPtr = Marshal.ReadIntPtr(soundPtr + SoundDataOffset);
            // Assume that a voice line will be played only once after it's loaded. Then the set can be pruned as voice
            // lines are played.
            if (knownVoiceLinePtrs.Remove(soundDataPtr))
            {
                knownVoiceLinesMap.TryGetValue(soundDataPtr, out var fileName);
                knownVoiceLinesMap.Remove(soundDataPtr);

                if (Path.GetFileNameWithoutExtension(fileName).Length == 10)
                {
                    LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Caught playback of known voice line at address {soundDataPtr:x}", new EKEventId(0, TextSource.AddonBattleTalk));
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Filename: {fileName}", new EKEventId(0, TextSource.AddonBattleTalk));
                    LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Caught playback of known voice line at address {soundDataPtr:x}", new EKEventId(0, TextSource.AddonBubble));
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Filename: {fileName}", new EKEventId(0, TextSource.AddonBubble));
                    Plugin.AddonBattleTalkHelper.nextIsVoice = true;
                    Plugin.AddonBattleTalkHelper.timeNextVoice = DateTime.Now;
                    Plugin.AddonBubbleHelper.nextIsVoice = true;
                    Plugin.AddonBubbleHelper.timeNextVoice = DateTime.Now;
                }
                else
                {
                    LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Caught playback of known voice line at address {soundDataPtr:x}", new EKEventId(0, TextSource.AddonTalk));
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Filename: {fileName}", new EKEventId(0, TextSource.AddonTalk));
                    Plugin.AddonTalkHelper.nextIsVoice = true;
                    Plugin.AddonTalkHelper.timeNextVoice = DateTime.Now;

                }
            }
        }
        catch (Exception exc)
        {
            LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error in PlaySpecificSound detour: {exc}", new EKEventId(0, TextSource.AddonBattleTalk));
            LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error in PlaySpecificSound detour: {exc}", new EKEventId(0, TextSource.AddonTalk));
        }

        return result;
    }
}
