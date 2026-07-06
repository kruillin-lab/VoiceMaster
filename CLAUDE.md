---
tags:
  - type/doc
  - project/voicemaster
  - status/active
type: doc
project: voicemaster
status: active
aliases: []
---
# VoiceMaster

Dalamud plugin for FFXIV TTS of NPC dialogue. Rebrand of Echokraut. Dalamud API 14 / .NET 10.

## Build

```bash
dotnet build VoiceMaster.csproj -c Release
```
PostBuild auto-copies to `%APPDATA%\XIVLauncher\devPlugins\VoiceMaster\`. If MSB3030 on bass.dll, copy manually.

## Non-obvious Gotchas

### Dialogue Gating
- NPC Talk fires on `PostDraw` every frame — gate on `(Speaker, NormalizedText)` tuple, speak only on change
- `SelectString` fires on `PreFinalize` — always speaks, no suppression
- Normalize text BEFORE state comparison
- `IsSpeakableText()`: letters must outnumber symbols (skips ASCII art)

### Audio
- 3D audio (BASS) only for `AddonBubble`. Dialogue/Talk = 2D
- `Set3DFactors(1, 1, 1)` — NOT `(1, audibleRange, 1)`. Wrong rolloff arg = underwater sound
- Strip WAV header before passing PCM to BASS
- Check `Playing` flag in `WorkPlayingQueue()` to prevent stream overlap
- `PlayAudio()` waits for game voice lines to finish before TTS

### NPC Ignore
- Two levels: `IgnoredNpcSession` (until reload) and `IgnoredNpcInstance` (until zone change)
- Both are case-insensitive `HashSet<string>`

## Commands
`/vm` config · `/vmtalk` · `/vmbtalk` · `/vmbubble` · `/vmchat`

## Credits
Original: Echokraut by Ren Nagasaki (AGPL-3.0)

- **DOX file contracts:** see AGENTS.md for local traversal/update rules (subordinate to AgentBrain).

