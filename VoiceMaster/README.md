# VoiceMaster

[![Dalamud API](https://img.shields.io/badge/Dalamud%20API-14-blue)](https://dalamud.dev/)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-AGPL--3.0-orange)](LICENSE)

**A TTS Plugin for FFXIV that breaks the silence!**

VoiceMaster is a Dalamud plugin that provides text-to-speech (TTS) for Final Fantasy XIV dialogue, battle text, chat messages, and speech bubbles. It supports multiple TTS backends including Alltalk and Inworld AI.

> **Note:** VoiceMaster is a rebrand of [Echokraut](https://github.com/RenNagasaki/Echokraut) by Ren Nagasaki, with added Inworld AI integration.

## ✨ Features

### 🔊 TTS Backends

| Backend | Description | Status |
|---------|-------------|--------|
| **Alltalk** | Self-hosted TTS server | ✅ Supported |
| **Inworld AI** | AI-powered voice synthesis with characters | ✅ Supported |

### 💬 Dialogue Sources

| Source | Description | Behavior |
|--------|-------------|----------|
| **NPC Dialogue** | Talk window conversations | Speak-on-change, no repeats |
| **Battle Talk** | Combat dialogue & announcements | Real-time TTS |
| **Player Choices** | SelectString dialogs | Fully repeatable |
| **Cutscene Choices** | Cutscene player selections | Fully repeatable |
| **Speech Bubbles** | Overhead character dialogue | Configurable trigger |
| **Chat Messages** | Customizable chat TTS | Channel filtering |

### 🤖 Inworld AI Integration

Advanced AI-powered TTS with character voices:
- **Endpoint:** `v1/voice` (Non-Streaming) for maximum stability
- **Format:** MP3 audio for clean playback without static or truncation
- **Features:**
  - API Key/Secret authentication
  - Workspace ID selection
  - Dynamic character/voice fetching
  - NPC-to-character mapping in UI
  - Clean playback via Live3DAudioEngine (BASS)

### 🎯 Smart Dialogue Gating

VoiceMaster inherits Echokraut's proven dialogue gating system:

**NPC Dialogue (No Repeats):**
- Fires on `PostDraw` while window visible
- State comparison: `(Speaker, NormalizedText)`
- Only speaks when content changes
- Resets when Talk window closes

**Player Dialogue (Always Speaks):**
- Fires on `PreFinalize` (when selection confirmed)
- Fully repeatable (same choice can be reselected)
- No suppression between selections

**Text Normalization:**
- Punctuation normalization (emdashes, ellipsis, etc.)
- Applied before state comparison
- Consistent across all dialogue types

## 📦 Installation

### Requirements
- Final Fantasy XIV with Dalamud installed
- XIVLauncher
- For Alltalk: Self-hosted Alltalk TTS server
- For Inworld AI: Inworld AI account and API credentials

### Dev Build

1. Clone the repository:
```bash
git clone https://github.com/kruillin-lab/VoiceMaster.git
```

2. Build the project:
```bash
cd VoiceMaster
dotnet build VoiceMaster.csproj -c Release
```

3. Copy files to devPlugins:
```bash
# Copy to Dalamud dev plugins folder
cp bin/Release/net8.0-windows/VoiceMaster.dll %APPDATA%\XIVLauncher\devPlugins\VoiceMaster\
cp VoiceMaster.json %APPDATA%\XIVLauncher\devPlugins\VoiceMaster\
cp bass.dll %APPDATA%\XIVLauncher\devPlugins\VoiceMaster\
```

4. In FFXIV: `/xlplugins` → Dev Tools → Scan for dev plugins

## 🎮 Usage

### Commands

| Command | Description |
|---------|-------------|
| `/vm` | Open main configuration |
| `/vmtalk` | Talk window TTS settings |
| `/vmbtalk` | Battle talk TTS settings |
| `/vmbubble` | Speech bubble TTS settings |
| `/vmchat` | Chat TTS settings |

### Configuration

1. Open the config window (`/vm`)
2. Select your TTS backend:
   - **Alltalk:** Configure server URL
   - **Inworld AI:** Enter API Key/Secret and Workspace ID
3. Map voices to NPCs (optional)
4. Adjust volume, speed, and other settings
5. Enable desired dialogue sources

### Backend Setup

#### Alltalk
1. Install [Alltalk TTS](https://github.com/erew123/alltalk_tts) on your server
2. Note the server URL (e.g., `http://localhost:7851`)
3. Enter URL in VoiceMaster config

#### Inworld AI
1. Create account at [Inworld AI](https://www.inworld.ai/)
2. Generate API Key and Secret
3. Create a workspace and note the Workspace ID
4. Enter credentials in VoiceMaster config
5. Fetch available characters/voices

## 🛠️ Building from Source

### Requirements
- Visual Studio 2022+ (or `dotnet` CLI)
- .NET 8 SDK
- Dalamud.NET.Sdk 14.0.0

### Dependencies
- **ManagedBass** - Audio playback library
- **Humanizer** - Text normalization
- **R3** - Reactive extensions
- **Reloaded.Memory** - Memory utilities
- **OtterGui** - UI components

### Build
```bash
dotnet build VoiceMaster.sln -c Release
```

Output: `VoiceMaster/bin/x64/Release/VoiceMaster.dll`

## 🏗️ Project Structure

```
VoiceMaster/
├── Plugin.cs                    # Main plugin entry
├── VoiceMaster.csproj           # Project file
├── VoiceMaster.json             # Plugin manifest
├── Backends/                    # TTS backends
│   ├── AlltalkBackend.cs
│   ├── InworldAIBackend.cs
│   └── ITTSBackend.cs
├── Helper/
│   ├── Addons/                  # Dialogue handlers
│   │   ├── AddonTalkHelper.cs
│   │   ├── AddonBattleTalkHelper.cs
│   │   ├── AddonSelectStringHelper.cs
│   │   ├── AddonBubbleHelper.cs
│   │   └── ChatTalkHelper.cs
│   ├── API/                     # Backend clients
│   └── Functional/              # Core functionality
├── DataClasses/                 # Data structures
├── Enums/                       # Enumerations
└── Windows/                     # UI windows
    ├── ConfigWindow.cs
    ├── AlltalkInstanceWindow.cs
    └── DialogExtraOptionsWindow.cs
```

## 🔧 Configuration Options

### TTS Settings
- **Backend Selection** - Alltalk or Inworld AI
- **Voice Mapping** - Assign voices to NPCs
- **Volume** - Master volume control
- **Speed** - Speech rate adjustment
- **Device** - Output audio device

### Dialogue Sources
- Enable/disable per source:
  - Talk window
  - Battle talk
  - Speech bubbles
  - Chat messages
  
### Chat Filtering
- Enable TTS for specific channels:
  - Say, Tell, Party, Alliance
  - Free Company, Linkshells
  - Custom channels

## 🤝 Contributing

Contributions welcome! Areas for improvement:
- Additional TTS backends
- Voice effects and modulation
- Better NPC voice detection
- Performance optimizations

Please:
1. Fork the repository
2. Create a feature branch
3. Follow existing code style
4. Test in-game before submitting
5. Submit PR with clear description

## 📚 Resources

- Original Project: [Echokraut](https://github.com/RenNagasaki/Echokraut)
- [Dalamud Documentation](https://dalamud.dev/)
- [Inworld AI Docs](https://docs.inworld.ai/)
- [Alltalk TTS](https://github.com/erew123/alltalk_tts)

## 📝 License

AGPL-3.0-or-later

See [LICENSE](LICENSE) for full text.

## 🙏 Credits

- **Original Author:** Ren Nagasaki ([Echokraut](https://github.com/RenNagasaki/Echokraut))
- **VoiceMaster Maintainer:** kruillin-lab
- **Audio Engine:** [BASS](https://www.un4seen.com/)
- **UI Components:** [OtterGui](https://github.com/Ottermandias/OtterGui)

---

**Version:** 0.15.0.1  
**Dalamud API:** 14 (FFXIV Patch 7.x)  
**License:** AGPL-3.0-or-later
