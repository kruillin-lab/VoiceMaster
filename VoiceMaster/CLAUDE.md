# VoiceMaster Project

## Project Overview

**VoiceMaster** is a Dalamud plugin for Final Fantasy XIV (FFXIV) that provides Text-to-Speech (TTS) for NPC dialogue. It's a complete rebrand of the "Echokraut" plugin.

- **Location**: `C:\Users\kruil\Documents\Projects\output\VoiceMaster`
- **Framework**: .NET 8 / Dalamud SDK 14
- **Audio Engine**: ManagedBass (Live3DAudioEngine with bass.dll)
- **License**: AGPL-3.0-or-later

## Tech Stack

- **Language**: C# (.NET 8)
- **Framework**: Dalamud Plugin API v14
- **Audio Processing**: ManagedBass (BASS audio library)
- **UI**: ImGui via Dalamud
- **TTS Backends**:
  - **Alltalk**: Local/remote self-hosted TTS
  - **Inworld AI**: Cloud-based character TTS

## Project Structure

```
VoiceMaster/
├── Backends/               # TTS Backend implementations
│   ├── ITTSBackend.cs      # Backend interface
│   ├── AlltalkBackend.cs   # Alltalk TTS integration
│   └── InworldAIBackend.cs # Inworld AI integration
├── DataClasses/            # Data models and configuration
│   ├── Configuration.cs    # Plugin settings
│   ├── VoiceMessage.cs     # Voice line data
│   ├── AlltalkData.cs      # Alltalk config
│   ├── InworldAIData.cs    # Inworld AI config
│   └── ...
├── Enums/                  # Enumerations
│   ├── TTSBackends.cs
│   ├── TextSource.cs
│   ├── EventType.cs
│   └── ...
├── Helper/                 # Helper utilities
│   ├── Addons/             # UI addon helpers
│   ├── DataHelper/         # Data utilities
│   ├── API/                # API utilities
│   └── Functional/         # Functional utilities
├── Windows/                # UI Windows
│   ├── ConfigWindow.cs
│   ├── AlltalkInstanceWindow.cs
│   ├── FirstTimeWindow.cs
│   └── DialogExtraOptionsWindow.cs
├── Resources/              # Static resources
├── Plugin.cs               # Main plugin entry point
├── VoiceMaster.csproj      # Project file
├── VoiceMaster.json        # Plugin manifest
└── bass.dll                # Audio engine native lib
```

## Build Commands

```bash
# Build Release
dotnet build VoiceMaster.csproj -c Release

# Build Debug
dotnet build VoiceMaster.csproj -c Debug

# Restore packages
dotnet restore VoiceMaster.csproj
```

**Auto-deploy**: The csproj includes a PostBuild target that copies `VoiceMaster.dll` and `VoiceMaster.json` to `%APPDATA%\XIVLauncher\devPlugins\VoiceMaster\`.

## Installation

1. Build the project
2. Copy these files to your Dalamud plugins folder:
   - `bin/Release/net8.0-windows/VoiceMaster.dll`
   - `VoiceMaster.json`
   - `bass.dll`
3. Load in FFXIV

## Key Components

### Plugin Entry Point (`Plugin.cs`)
- Implements `IDalamudPlugin`
- Uses `[PluginService]` for DI (Dependency Injection)
- Registers commands: `/vm`, `/vmtalk`, `/vmbtalk`, `/vmbubble`, `/vmchat`
- Initializes all helper services

### TTS Backends

**Alltalk Backend** (`AlltalkBackend.cs`):
- Self-hosted TTS (local or remote)
- Supports multiple voice models
- HTTP API communication

**Inworld AI Backend** (`InworldAIBackend.cs`):
- Cloud-based AI character voices
- Endpoint: `v1/voice` (Non-Streaming)
- Format: MP3 audio
- Auth: API Key + Secret
- Fetches characters/voices dynamically

### Dialogue Gating System

**NPC Dialogue**:
- Triggers on `PostDraw` (every frame while visible)
- State comparison: `(Speaker, NormalizedText)`
- Only speaks when state changes
- Resets when Talk window closes

**Player Dialogue**:
- `SelectString`: Fires on `PreFinalize` (selection confirmed)
- `CutSceneSelectString`: Same for cutscenes
- Always speaks when selected

**Text Normalization**:
- Punctuation normalization (emdashes, ellipsis, etc.)
- Applied before state comparison

### Helpers

- **LipSyncHelper**: Lip sync animation control
- **SoundHelper**: Audio playback management, game voice line detection
- **AddonTalkHelper**: Talk window handling
- **AddonBattleTalkHelper**: Battle talk handling
- **AddonBubbleHelper**: Chat bubble handling
- **ChatTalkHelper**: Chat message handling
- **Various SelectString Helpers**: Player choice handling
- **Live3DAudioEngine**: 3D positional audio with BASS.dll
  - Fixed CS0120 compile error: `_engine.VoiceLineStarted?.Invoke(_id)` in nested Source class
  - Supports both 2D (dialogue) and 3D (bubbles) audio modes
  - WAV header stripping for raw PCM playback

## Configuration

Key settings in `Configuration.cs`:
- `BackendSelection`: Choose between Alltalk/Inworld AI
- `VoiceDialogue`: Enable/disable NPC dialogue TTS
- `VoiceBattleDialogue`: Enable/disable battle dialogue
- `VoicePlayerChoices`: Enable/disable player choice voicing
- `CancelSpeechOnTextAdvance`: Cancel speech when advancing text
- `Voice3DAudibleRange`: 3D audio range setting
- NPC/Player voice mapping lists

## Commands

- `/vm` - Open configuration
- `/vmtalk` - Talk window settings
- `/vmbtalk` - Battle talk settings
- `/vmbubble` - Bubble settings
- `/vmchat` - Chat settings

## Development Guidelines

### Audio Handling
- **MP3 Passthrough**: Inworld AI uses PCM format (LINEAR16 24kHz)
- **3D Audio**: Only enabled for `AddonBubble` sources; 2D playback for dialogue windows
- **3D Audio Fix**: Fixed incorrect rolloff factor that caused "underwater" sound
- **Voice Line Collision**: TTS waits for game voice lines to finish before playing
- **Sample Rate**: 48kHz for Inworld AI
- **Cancellation**: Support canceling speech on text advance

### Dialogue Processing
- Always normalize text before comparison
- Check state changes before triggering speech
- Handle window open/close events properly
- Support ignore lists for NPCs
- **Text Filtering**: `IsSpeakableText()` skips ASCII art and non-speech content (requires letters to outnumber symbols)

### Backend Implementation
- Implement `ITTSBackend` interface
- Support voice listing
- Support async audio generation
- Handle errors gracefully

## Dependencies

```xml
<PackageReference Include="Humanizer.Core" Version="2.14.1" />
<PackageReference Include="ManagedBass" Version="4.0.1" />
<PackageReference Include="R3" Version="1.1.13" />
<PackageReference Include="Reloaded.Memory" Version="7.1.0" />
```

## Environment Setup

```bash
# Prerequisites
- .NET 8 SDK
- Dalamud SDK (via NuGet)

# Clone and build
git clone <repository>
cd VoiceMaster
dotnet restore
dotnet build -c Release

# Deploy to dev plugins folder
cp bin/Release/net8.0-windows/VoiceMaster.dll %APPDATA%/XIVLauncher/devPlugins/VoiceMaster/
cp VoiceMaster.json %APPDATA%/XIVLauncher/devPlugins/VoiceMaster/
cp bass.dll %APPDATA%/XIVLauncher/devPlugins/VoiceMaster/
```

## Current Status

### Inworld AI Backend - ✅ Functional
The Inworld AI TTS backend is now working with the following fixes applied:

**Fixes Applied:**
1. Added `InworldAI.Enabled` validation in `BackendHelper.SetBackendType()` - prevents silent failures when backend not enabled
2. Added base64 credential parser in Config UI - paste `key:secret` base64 string to auto-fill API credentials
3. Added comprehensive debug logging in `InworldAIBackend.GenerateAudioStreamFromVoice()`
4. Fixed thread blocking issues by using proper async/await patterns throughout

**Configuration:**
- Uses REST API endpoint: `https://api.inworld.ai/tts/v1/voice`
- Authentication: Basic Auth with `ApiKey:ApiSecret` base64 encoded
- Audio Format: LINEAR16 24kHz PCM (WAV header stripped for BASS compatibility)
- Response parsing: Expects JSON with `audioContent` field containing base64 audio

## Known Issues

### MSB3030 (bass.dll copy error)
The PostBuild may fail to copy `bass.dll` with error MSB3030 if:
- The file is locked by another process
- NuGet package hasn't initialized

**Workaround**: Manually copy files if auto-deploy fails.

## Troubleshooting

### InworldAI Not Playing Audio

**Symptoms**: Plugin logs show TTS generation but no audio plays.

**Root Causes & Fixes**:
1. **InworldAI.Enabled = false** - Enable checkbox in config UI (`/vm` → Backend Settings)
2. **Missing API credentials** - Use the "Paste Base64 Credentials" field for easy key:secret entry
3. **Invalid voice ID** - Check available voices dropdown; if empty, verify API credentials

**Debug Steps**:
- Enable debug logging in Dalamud (`/xllog`)
- Look for `InworldAIBackend` messages showing API response status
- Verify `audioContent` is returned in the response

## Common Tasks

### Adding a New TTS Backend
1. Create new class in `Backends/` implementing `ITTSBackend`
2. Add backend enum value to `TTSBackends.cs`
3. Add configuration data class
4. Add UI controls in `ConfigWindow.cs`
5. Register in `Plugin.cs` backend selection

### Mapping NPC Voices
1. Interact with NPC in-game
2. Use VoiceMaster UI to assign voice
3. Mapped data stored in `Configuration.MappedNpcs`

### Debugging Audio Issues
1. Check `bass.dll` is present in plugin folder
2. Verify TTS backend is reachable
3. Check logs for backend errors
4. Test with different audio formats

## Notes

- **Inworld AI Backend**: ✅ **FUNCTIONAL** - REST API with base64 credential support (2025-03-07)
- **Alltalk Backend**: ✅ Functional - Local/remote self-hosted TTS
- **Inworld AI Strategy**: Uses REST API (not WebSocket) for TTS - simpler one-shot audio generation
- **WebSocket API**: The Realtime WebSocket API is for bidirectional character conversations, not suitable for TTS
- **Dialogue Gating**: State-based comparison prevents duplicate speech
- **3D Audio**: Optional spatial audio for dialogue positioning (bubbles only; 2D for dialogue/Talk windows)
- **Voice Chat**: Experimental feature for voicing chat messages

### Recent Fixes (March 2025)
- Fixed `InworldAI.Enabled` flag being ignored in `BackendHelper.SetBackendType()`
- Added base64 credential paste helper in Config UI
- Added comprehensive debug logging in `InworldAIBackend.GenerateAudioStreamFromVoice()`
- Fixed thread blocking with proper async/await patterns throughout
- Audio format: LINEAR16 24kHz PCM with WAV header stripped for BASS compatibility
- Fixed voice list wipe bug: `GetAndMapVoices()` now guards against empty backend responses
- Added `IsSpeakableText()` filter to skip ASCII art and non-speech text
- Fixed 3D audio bug: Changed `Bass.Set3DFactors(1, audibleRange, 1)` to `Set3DFactors(1, 1, 1)` - rolloff was incorrectly using distance as a factor
- Fixed audio overlap: Added `Playing` flag check in `WorkPlayingQueue()` to prevent multiple streams
- Added game voice line collision avoidance in `PlayAudio()` - waits for game voice to finish before TTS

## Troubleshooting Inworld AI

### No Audio Output
1. Check `/xllog` for InworldAI error messages
2. Verify `InworldAI.Enabled = true` in config
3. Confirm API credentials are valid (test with base64 decode)
4. Check voice ID matches available voices from `/tts/v1/voices` endpoint

### Debug Logging
Enable verbose logging to see:
- HTTP response status codes
- API response body (first 500 chars)
- Audio content length
- WAV header detection and stripping

## Credits

Original project: **Echokraut** by Ren Nagasaki
- Repository: https://github.com/RenNagasaki/Echokraut
- License: AGPL-3.0-or-later

VoiceMaster is a rebrand with Inworld AI integration added.
