# VoiceMaster - Project Summary

## Overview

**VoiceMaster** is a Dalamud TTS (Text-to-Speech) plugin for Final Fantasy XIV that provides voice synthesis for in-game dialogue, battle text, chat, and speech bubbles. It is a rebrand of the Echokraut plugin with additional backends including Inworld AI.

| Property | Value |
|----------|-------|
| **Original Author** | Ren Nagasaki (Echokraut) |
| **Version** | 0.15.0.1 |
| **License** | AGPL-3.0-or-later |
| **Target Game** | FFXIV (Dalamud API 14) |
| **Framework** | .NET 8 / Dalamud SDK 14.0.0 |
| **Language** | C# |
| **Repository** | https://github.com/RenNagasaki/VoiceMaster |

## Description

A TTS plugin that "breaks the silence" by providing voice synthesis for FFXIV content. Supports self-hosted TTS services including Alltalk and Inworld AI backends.

## Key Features

### TTS Backends
- **Alltalk** - Self-hosted TTS server
- **Inworld AI** - AI-powered voice synthesis with character support
  - Uses `v1/voice` (Non-Streaming) endpoint
  - MP3 format for clean playback
  - API Key/Secret authentication
  - Dynamic character/voice fetching
  - NPC-to-character mapping in UI

### Dialogue Sources
- **NPC Dialogue** (Talk window) - Speak-on-change, no repeats
- **Battle Talk** - Combat dialogue and announcements
- **Player Choices** (SelectString) - Fully repeatable
- **Cutscene Choices** - Cutscene player selections
- **Speech Bubbles** - Overhead character dialogue
- **Chat Messages** - Customizable chat TTS

### Audio Engine
- **BASS Audio Library** (`bass.dll`) - Low-latency audio playback
- **Live3DAudioEngine** - Spatial audio support
- **Lip Sync** - Character lip synchronization

## Project Structure

```
output/
├── README.md                      # Build and installation guide
├── VoiceMaster.sln                # Visual Studio solution
├── deploy.ps1                     # Deployment script
├── VoiceMaster.zip                # Pre-built package
│
├── VoiceMaster/                   # Main plugin project
│   ├── README.md                  # Project documentation
│   ├── Plugin.cs                  # Main entry point (467 lines)
│   ├── VoiceMaster.csproj         # Project file
│   ├── VoiceMaster.json           # Plugin manifest
│   ├── packages.lock.json         # Package locks
│   ├── bass.dll                   # Audio library
│   ├── CLAUDE.md / gemini.md      # AI agent instructions
│   │
│   ├── Backends/                  # TTS backend implementations
│   │   ├── Alltalk/               # Alltalk TTS integration
│   │   └── Inworld/               # Inworld AI integration
│   │
│   ├── Helper/                    # Core functionality
│   │   ├── Addons/                # Dialogue handlers (Echokraut core)
│   │   │   ├── AddonTalkHelper.cs         # NPC dialogue (speak-on-change)
│   │   │   ├── AddonBattleTalkHelper.cs   # Battle dialogue
│   │   │   ├── AddonSelectStringHelper.cs # Player choices
│   │   │   ├── AddonCutSceneSelectStringHelper.cs # Cutscene choices
│   │   │   ├── AddonBubbleHelper.cs       # Speech bubbles
│   │   │   └── ...
│   │   ├── DataHelper/            # Data loading helpers
│   │   ├── API/                   # Backend API clients
│   │   └── Functional/            # Core functional helpers
│   │       ├── LipSyncHelper.cs
│   │       └── SoundHelper.cs
│   │
│   ├── DataClasses/               # Data structures
│   ├── Enums/                     # C# enumerations
│   ├── Exceptions/                # Custom exceptions
│   ├── Windows/                   # ImGui UI windows
│   │   ├── ConfigWindow.cs
│   │   ├── AlltalkInstanceWindow.cs
│   │   ├── FirstTimeWindow.cs
│   │   └── DialogExtraOptionsWindow.cs
│   ├── Resources/                 # Embedded resources
│   │   ├── VoiceMaster.png
│   │   ├── VoiceNamesEN.json
│   │   ├── VoiceNamesDE.json
│   │   └── VoiceNamesFR.json
│   ├── Properties/                # Assembly properties
│   └── bin/obj/                   # Build output
│
└── OtterGui/                      # UI dependency library
    ├── OtterGui.csproj
    ├── OtterGuiInternal/          # Low-level ImGui wrappers
    └── ...
```

## Commands

| Command | Description |
|---------|-------------|
| `/vm` | Open main configuration |
| `/vmtalk` | Talk window TTS settings |
| `/vmbtalk` | Battle talk TTS settings |
| `/vmbubble` | Speech bubble TTS settings |
| `/vmchat` | Chat TTS settings |

## Dialogue Gating System

VoiceMaster inherits Echokraut's proven dialogue gating architecture:

### NPC Dialogue (AddonTalkHelper)
- Fires on `PostDraw` (every frame while visible)
- State comparison: `(Speaker, NormalizedText)`
- Only speaks when state changes (no duplicates)
- Resets `lastValue` when Talk window closes
- Works in world and cutscenes

```csharp
private static AddonTalkState lastValue;

private void Mutate(AddonTalkState nextValue)
{
    if (lastValue.Equals(nextValue))
        return;  // Skip duplicate
    
    lastValue = nextValue;
    HandleChange(nextValue);
}
```

### Player Dialogue (SelectString Helpers)
- Fires on `PreFinalize` (when selection confirmed)
- Always speaks when selected
- Fully repeatable (same choice can be reselected)
- No suppression logic
- Separate pipeline from NPC dialogue

### Text Normalization
- Punctuation normalization (emdashes, ellipsis, etc.)
- Applied before state comparison
- Consistent across all dialogue types

## Build System

### Requirements
- Visual Studio 2022+ (or `dotnet` CLI)
- Dalamud SDK 14.0.0
- .NET 8 SDK

### Build Commands

```bash
# Restore packages
dotnet restore

# Build solution
dotnet build VoiceMaster.sln -c Release

# Build plugin only
dotnet build VoiceMaster/VoiceMaster.csproj -c Release
```

### Output
- Location: `VoiceMaster/bin/x64/Release/VoiceMaster.dll`
- Dependencies: `VoiceMaster.json`, `bass.dll`

### Post-Build Deployment
Build automatically deploys to:
```
%APPDATA%\XIVLauncher\devPlugins\VoiceMaster\
```

### Manual Installation
1. Copy to `%AppData%\XIVLauncher\devPlugins\VoiceMaster\`:
   - `VoiceMaster.dll`
   - `VoiceMaster.json`
   - `bass.dll`
2. In FFXIV: `/xlplugins` → Dev Tools → Scan for dev plugins

## Dependencies

### NuGet Packages
- **Humanizer.Core** (2.14.1) - Text normalization
- **ManagedBass** (4.0.1) - BASS audio wrapper
- **R3** (1.1.13) - Reactive extensions
- **Reloaded.Memory** (7.1.0) - Memory utilities

### External Libraries
- **bass.dll** - Un4seen audio library (included)
- **OtterGui** - UI utility library (project reference)

### Dalamud Services
```csharp
IDalamudPluginInterface    # Plugin interface
ITextureProvider           # Texture loading
ICommandManager            # Slash commands
IClientState               # Player state
IDataManager               # Game data
IPluginLog                 # Logging
IFramework                 # Game framework
ICondition                 # Player conditions
IObjectTable               # Game objects
IAddonLifecycle            # UI lifecycle
ISigScanner                # Signature scanning
IGameInteropProvider       # Game hooks
IGameGui                   # UI access
IGameConfig                # Game config
IChatGui                   # Chat interface
```

## Key Classes

### Plugin.cs
Main plugin class implementing `IDalamudPlugin`:
- Service injection
- Window system management
- Helper initialization
- Command registration

### Addon Helpers (Core)
Located in `Helper/Addons/`:

| Helper | Purpose |
|--------|---------|
| `AddonTalkHelper` | NPC dialogue gating |
| `AddonBattleTalkHelper` | Battle announcements |
| `AddonSelectStringHelper` | Player choice dialogs |
| `AddonCutSceneSelectStringHelper` | Cutscene choices |
| `AddonBubbleHelper` | Speech bubbles |
| `ChatTalkHelper` | Chat messages |

### Backend Classes
- `AlltalkInstance` - Alltalk TTS client
- `InworldVoiceBackend` - Inworld AI client

## Configuration

Configuration stored via `IDalamudPluginInterface`:
- Backend selection (Alltalk/Inworld)
- Voice mapping per NPC
- Volume and speed settings
- Dialogue source toggles
- Chat channel filters

## Troubleshooting

### Build Errors

**"OtterGui could not be found"**
- Ensure OtterGui folder is at same level as VoiceMaster
- Check OtterGuiInternal is inside OtterGui folder
- Verify project reference path

**"Properties namespace does not exist"**
- Ensure Properties folder exists
- Contains Resources.Designer.cs and Resources.resx

**Missing bass.dll**
- bass.dll included in VoiceMaster folder
- Should auto-copy to output on build

### Runtime Issues

**Plugin doesn't load**
- Check Dalamud log: `/xllog`
- Verify all DLL dependencies present
- Ensure using dev plugin path

**No TTS audio**
- Check backend configuration
- Verify audio device settings
- Check plugin config: `/vm`

## History

### Echokraut → VoiceMaster Rebrand
- All namespaces: `Echokraut.*` → `VoiceMaster.*`
- All commands: `/ekt*` → `/vm*`
- Project files renamed
- Zero functional changes to dialogue gating

### Inworld AI Integration
- Added as secondary TTS backend
- MP3 streaming for clean playback
- Character mapping system

## External Resources

- Original Echokraut: https://github.com/RenNagasaki/Echokraut
- OtterGui: https://github.com/Ottermandias/OtterGui
- BASS Audio: https://www.un4seen.com/

## License

AGPL-3.0-or-later

---

*Generated: March 2026*
*Original: Echokraut by Ren Nagasaki*
