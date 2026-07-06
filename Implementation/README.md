---
tags:
  - type/readme
  - project/voicemaster
  - status/active
type: readme
project: voicemaster
status: active
aliases: []
---
# VoiceMaster - Echokraut Integration Summary

## Overview
This directory contains the analysis and implementation resources for integrating improvements from Echokraut (upstream) into VoiceMaster (fork).

## Files Delivered

### Documentation
- **`ECHOKRAUT_COMPARISON.md`** - Comprehensive comparison of Echokraut improvements
- **`INTEGRATION_GUIDE.md`** - Step-by-step integration instructions
- **`README.md`** - This file

### Implementation Files (`Implementation/`)

#### Core Services
- `Services/ServiceContainer.cs` - Dependency injection container
- `Services/Queue/VoiceMessageQueue.cs` - Thread-safe message queue  
- `Services/Queue/VoiceMessageEntry.cs` - Queue entry with state tracking
- `Services/Queue/IVoiceMessageQueue.cs` - Queue interface

#### Data Classes
- `DataClasses/VoiceMessage.cs` - Voice message and NPC data classes

#### Enums
- `Enums/VoiceMessageState.cs` - Message state enumeration
- `Enums/TextSource.cs` - Text source enumeration

#### Tests
- `Tests/VoiceMessageQueueTests.cs` - xUnit tests for queue

## Quick Start

1. **Read the Comparison** → `ECHOKRAUT_COMPARISON.md`
2. **Follow Integration Guide** → `INTEGRATION_GUIDE.md`
3. **Copy Implementation Files** → From `Implementation/` to your project
4. **Run Tests** → `dotnet test`

## Key Improvements Identified

1. **Dependency Injection Container** - `ServiceContainer` pattern
2. **Voice Message Queue** - Thread-safe concurrent queue with state machine
3. **Async/Await Architecture** - Non-blocking TTS generation
4. **VoiceMessageProcessor** - Clean pipeline replacing `Plugin.Say()`
5. **Live 3D Audio Engine** - Improved spatial audio
6. **Enhanced Logging** - Structured logging with event IDs
7. **Native UI Support** - KamiToolKit integration (optional)
8. **Unit Testing** - xUnit test coverage
9. **AllTalk Instance Management** - Automated local instance handling
10. **Configuration Management** - New options for sync and UI

## Architecture Comparison

### Before (VoiceMaster Pattern)
```
Addon Event → Plugin.Say() → Direct Backend Call → Audio Playback
```

### After (Echokraut Pattern)
```
Addon Event → VoiceMessageProcessor → Queue → Async Generation → Audio Playback
                                          ↓
                                    State Machine Tracking
```

## Integration Priority

### Phase 1: Foundation (Safe)
- [ ] Copy infrastructure files
- [ ] Set up DI container
- [ ] Add queue infrastructure

### Phase 2: Core Refactoring (Medium Risk)
- [ ] Migrate to async pattern
- [ ] Integrate queue with backend
- [ ] Add VoiceMessageProcessor

### Phase 3: Enhanced Features (Higher Risk)
- [ ] 3D audio improvements
- [ ] Native UI (optional)
- [ ] Configuration expansion

### Phase 4: Polish
- [ ] Logging enhancements
- [ ] Performance optimization
- [ ] Complete test coverage

## Testing

```bash
# Run unit tests
dotnet test Implementation/Tests/VoiceMessageQueueTests.cs

# Manual testing checklist in INTEGRATION_GUIDE.md
```

## Backwards Compatibility

All changes maintain backwards compatibility:
- Existing config format preserved
- Old code paths kept as fallback
- Feature flags for new functionality
- Gradual migration approach

## Version Information

- **Echokraut Version Analyzed**: 0.18.0.2 (March 2026)
- **Dalamud API Level**: 14
- **Target Framework**: .NET 10.0

## Credits

- Original Echokraut by Ren Nagasaki: https://github.com/RenNagasaki/Echokraut
- VoiceMaster fork improvements based on Echokraut patterns

## License

AGPL-3.0-or-later (same as Echokraut)
