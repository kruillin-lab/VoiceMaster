# VoiceMaster

A complete rebrand of Echokraut - A TTS Plugin for FFXIV that breaks the silence!

## What This Is

This is a **pure, complete copy of Echokraut** with all references changed from "Echokraut" to "VoiceMaster":
- All namespaces updated
- All class references updated  
- All commands changed from `/ekt*` to `/vm*`
- Project files renamed
- Assembly name updated

## Recent Updates (Inworld AI Integration)

We have successfully integrated **Inworld AI** as a TTS backend, alongside Alltalk.

### Inworld AI Backend
- **Endpoint:** Uses the `v1/voice` (Non-Streaming) endpoint for maximum stability.
- **Format:** Requests **MP3** audio to bypass decoding/header issues.
- **Engine:** Passes MP3 streams directly to the `Live3DAudioEngine` (BASS), ensuring clean playback without static, clicking, or truncation.
- **Features:**
    - Supports API Key/Secret authentication.
    - Allows selecting Workspace ID.
    - Fetches available Characters/Voices dynamically from Inworld.
    - Maps Inworld characters to FFXIV NPCs via the VoiceMaster UI.

## Key Changes from Echokraut

**Branding:**
- Plugin name: Echokraut → VoiceMaster
- Namespace: `Echokraut.*` → `VoiceMaster.*`
- Commands: `/ekt` → `/vm`, `/ekttalk` → `/vmtalk`, etc.
- Files: `Echokraut.csproj` → `VoiceMaster.csproj`, etc.

**Project Structure:**
- OtterGui project reference is commented out (was empty in source)
- All other functionality remains identical to Echokraut

## Dialogue Gating (The Gold Standard)

VoiceMaster inherits Echokraut's proven dialogue gating system:

**NPC Dialogue:**
- Fires on `PostDraw` (every frame while visible)
- State comparison: `(Speaker, NormalizedText)`
- Only speaks when state changes
- Resets `lastValue` when Talk window closes

**Player Dialogue:**
- SelectString: Fires on `PreFinalize` (when selection confirmed)
- CutSceneSelectString: Same pattern for cutscenes
- Always speaks when selected (fully repeatable)
- No suppression between selections

**Text Normalization:**
- Punctuation normalization (emdashes, ellipsis, etc.)
- Applied before state comparison
- Consistent across all dialogue types

## Building

```bash
dotnet build VoiceMaster.csproj -c Release
```

The compiled plugin will be in `bin/Release/net8.0-windows/VoiceMaster.dll`

## Installation

1. Build the project
2. Copy `VoiceMaster.dll`, `VoiceMaster.json`, and `bass.dll` to your Dalamud plugins folder (e.g. `%APPDATA%\XIVLauncher\devPlugins\VoiceMaster\`)
3. Load in FFXIV

## Commands

- `/vm` - Open configuration
- `/vmtalk` - Talk window settings
- `/vmbtalk` - Battle talk settings  
- `/vmbubble` - Bubble settings
- `/vmchat` - Chat settings

## Credits

This is a rebrand of **Echokraut** by Ren Nagasaki:
- Original: https://github.com/RenNagasaki/Echokraut
- License: AGPL-3.0-or-later

All core functionality, dialogue gating, and TTS integration is from the original Echokraut project.

## Next Steps

This is the **pure baseline** - add features as needed while maintaining the proven gating architecture.
