# VoiceMaster Project Context

## Overview
**VoiceMaster** is a Dalamud plugin for FFXIV, serving as a Text-to-Speech (TTS) engine for NPC dialogue. It is a complete rebrand/fork of the "Echokraut" plugin.

- **Framework:** .NET 8 (Windows)
- **Audio Engine:** ManagedBass (Live3DAudioEngine)
- **Backends:**
    - **Alltalk:** Local/Remote TTS.
    - **Inworld AI:** (New) Cloud-based Character TTS.

## Inworld AI Integration (Technical)

The Inworld AI backend (`InworldAIBackend.cs`) has been engineered to work around specific limitations of the playback engine.

### Endpoint & Protocol
- **URL:** `https://api.inworld.ai/tts/v1/voice` (Non-Streaming)
- **Method:** POST
- **Auth:** Basic Auth (ApiKey:ApiSecret)
- **Format:** Requests `audioEncoding: "MP3"` and `sampleRateHertz: 48000`.

### Audio Handling Strategy
We use the **Non-Streaming MP3 Pass-through** strategy (Build 14 logic):
1.  We request the full audio file as MP3 from Inworld.
2.  We receive a single JSON response containing the Base64 MP3.
3.  We decode the Base64 to a `byte[]`.
4.  We wrap it in a `MemoryStream` and return it directly to the engine.

### Why this strategy?
- **Static/Noise:** Caused by feeding MP3 bytes to a PCM-expecting Push Stream. Using file-based streams lets BASS auto-detect MP3.
- **Cut-offs/Speed:** Caused by manual decoding/resampling mismatches (e.g. 24k vs 48k). Letting Inworld send the file and BASS decode it natively solves this.
- **Clicks:** Caused by multiple WAV headers in the Streaming (`v1/voice:stream`) endpoint's NDJSON chunks. The Non-Streaming endpoint returns a clean, single file.

## Build & Deployment Notes

### Known Issue: MSB3030
The build process (`VoiceMaster.csproj`) attempts to copy `bass.dll` to the dev plugins folder but often fails with `MSB3030` (File not found) if the NuGet package hasn't initialized correctly or the file is locked.

**Workaround:**
1.  The `.csproj` has been modified to use `ContinueOnError="true"` for deployment tasks.
2.  If the automatic deployment fails, manually copy the files:
    ```bash
    cp bin/Release/net8.0-windows/VoiceMaster.dll %APPDATA%/XIVLauncher/devPlugins/VoiceMaster/
    ```

### Essential Files
- `VoiceMaster.dll`: The plugin logic.
- `bass.dll`: The audio engine (ManagedBass native lib). Must be present in the plugin folder.
- `VoiceMaster.json`: Plugin manifest.

## Current Status
- **Backend:** Inworld AI is fully functional using Build 14 logic.
- **UI:** Configuration window allows setting API Key, Secret, Workspace ID, and Default Character ID.
- **Voices:** The plugin fetches available voices/characters from Inworld dynamically.

## Future Maintenance
If switching back to Streaming (low latency) is required later:
- You must parse NDJSON chunks.
- You must STRIP the 44-byte WAV header from *every* chunk before concatenating.
- You must ensure the playback engine expects Raw PCM, or wrap the final result in a single valid WAV header.
- **Current recommendation:** Stick to Non-Streaming MP3 for stability.
