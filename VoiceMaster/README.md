# VoiceMaster

A complete rebrand of Echokraut - A TTS Plugin for FFXIV that breaks the silence!

## What This Is

This is a **pure, complete copy of Echokraut** with all references changed from "Echokraut" to "VoiceMaster":
- All namespaces updated
- All class references updated  
- All commands changed from `/ekt*` to `/vm*`
- Project files renamed
- Assembly name updated

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

The compiled plugin will be in `bin/x64/Release/VoiceMaster.dll`

## Installation

1. Build the project
2. Copy `VoiceMaster.dll` and `VoiceMaster.json` to your Dalamud plugins folder
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
