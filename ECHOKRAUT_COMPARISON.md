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
# Echokraut vs VoiceMaster - Improvement Analysis

## Overview

This document compares the current Echokraut codebase (as of March 2026) with the VoiceMaster fork to identify improvements that can be integrated.

## Major Architectural Improvements in Echokraut

### 1. **Dependency Injection Container** ⭐ HIGH PRIORITY
**File:** `Services/ServiceContainer.cs`

Echokraut has implemented a proper DI container with:
- `ServiceContainer` - Simple DI container with factory pattern
- `ServiceBuilder` - Centralized service registration
- Interface-based service contracts (e.g., `ILogService`, `IVoiceMessageProcessor`)

**Benefits:**
- Testable code (interfaces allow mocking)
- Decoupled components
- Easier maintenance and extension
- Lifecycle management

**Integration Strategy:**
- Gradually introduce interfaces for existing services
- Create `ServiceContainer` and `ServiceBuilder`
- Migrate services one at a time

---

### 2. **Voice Message Queue System** ⭐ HIGH PRIORITY
**Files:** 
- `Services/Queue/VoiceMessageQueue.cs`
- `Services/Queue/VoiceMessageEntry.cs`
- `Services/Queue/VoiceMessageState.cs`
- `Services/Queue/IVoiceMessageQueue.cs`

**Key Improvements:**
- Thread-safe concurrent queue using `ConcurrentQueue` and `ConcurrentDictionary`
- State machine for voice messages (Pending → Generating → Ready → Playing → Completed/Failed)
- Priority queue support (dialogue gets priority over bubbles)
- Queue statistics and monitoring
- Proper cancellation support

**State Machine:**
```
PendingGeneration → Generating → ReadyToPlay → Playing → Completed
                                      ↓              ↓
                                  Cancelled      Cancelled/Failed
```

**Integration Strategy:**
- Create queue infrastructure first
- Migrate `AudioPlaybackService` to use the queue
- Update `BackendService` for generation loop

---

### 3. **Async/Await Architecture** ⭐ HIGH PRIORITY
**Files:** 
- `Services/BackendService.cs` (GenerationLoopAsync)
- `Services/AudioPlaybackService.cs` (PlaybackLoopAsync)
- `Services/VoiceMessageProcessor.cs` (ProcessSpeechAsync)

**Key Improvements:**
- Async processing loops for generation and playback
- Proper cancellation token handling
- Non-blocking TTS generation
- Concurrent processing capability

**Pattern Example:**
```csharp
private async Task GenerationLoopAsync(CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        if (_queue.TryDequeuePendingGeneration(out var entry))
            await ProcessGenerationAsync(entry, cancellationToken);
        await Task.Delay(100, cancellationToken);
    }
}
```

---

### 4. **VoiceMessageProcessor Service** ⭐ HIGH PRIORITY
**File:** `Services/VoiceMessageProcessor.cs`

Replaces the massive `Plugin.Say()` method with a clean pipeline:

**Pipeline Steps:**
1. Backend availability check
2. Text cleaning and normalization
3. NPC data retrieval/creation
4. Mute/volume checks
5. Voice assignment
6. Volume calculation
7. Voice message building
8. Muted dialogue check
9. Backend processing

**Benefits:**
- Clean separation of concerns
- Testable individual steps
- Consistent error handling
- Easier to extend

---

### 5. **Live 3D Audio Engine** ⭐ MEDIUM PRIORITY
**File:** `Services/Live3DAudioEngine.cs`

Improved 3D audio positioning:
- Continuous position updates via `SetSourcePoller`
- Proper listener state from camera matrix
- Distance-based volume attenuation

**Key Features:**
```csharp
_audioEngine.SetSourcePoller(message.StreamId, () => new Vector3D(
    message.SpeakerFollowObj?.Position.X ?? 0,
    message.SpeakerFollowObj?.Position.Y ?? 0,
    message.SpeakerFollowObj?.Position.Z ?? 0));
```

---

### 6. **Enhanced Logging System**
**Files:** `Echotools.Logging` namespace (external library)

**Features:**
- Structured logging with `EKEventId`
- Text source tracking (AddonTalk, Bubble, Chat, etc.)
- Log levels (Debug, Info, Warning, Error)
- Event correlation via EventId

**Pattern:**
```csharp
_log.Debug(nameof(MethodName), "Message", eventId);
_log.Start(nameof(MethodName), textSource);
_log.End(nameof(MethodName), eventId);
```

---

### 7. **Configuration Management**
**File:** `DataClasses/Configuration.cs`

**New Options Added:**
- `GoogleDriveRequestVoiceLine` - Request voice lines from Google Drive
- `GoogleDriveDownloadPeriodically` - Periodic sync
- `UseNativeUI` - Toggle between ImGui and Native UI
- `HideUiInCutscenes` - Better cutscene handling
- More granular chat channel controls

---

### 8. **Native UI Support (KamiToolKit)**
**Files:** `Windows/Native/` folder

**Features:**
- Alternative to ImGui using KamiToolKit
- Native FFXIV UI appearance
- Better integration with game UI
- Runtime UI mode switching

**Window Types:**
- `NativeConfigWindow`
- `NativeFirstTimeWindow`
- `NativeVoiceConfigWindow`
- `DialogTalkController`

---

### 9. **Unit Testing** ⭐ HIGH PRIORITY
**File:** `Echokraut.Tests/VoiceMessageQueueTests.cs`

Comprehensive test coverage for:
- Queue operations (enqueue, dequeue, priority)
- State transitions
- Cancellation behavior
- Statistics tracking

**Test Framework:** xUnit

---

### 10. **AllTalk Instance Management**
**File:** `Services/AlltalkInstanceService.cs`

Improved local AllTalk instance management:
- Automated local installation
- Instance lifecycle management
- Auto-start on plugin load
- Health monitoring

---

### 11. **Text Processing Pipeline**
**File:** `Services/TextProcessingService.cs`

Service wrapper around `TalkTextHelper`:
- Consistent interface for text transformations
- Better testability
- Language-aware processing

---

## Integration Recommendations

### Phase 1: Foundation (Safe, Low Risk)
1. **Create Interface Contracts**
   - Define interfaces for existing services
   - Keep implementations unchanged initially

2. **Add Service Container**
   - Create `ServiceContainer` class
   - Create `ServiceBuilder` class
   - Register services without changing logic

3. **Add Queue Infrastructure**
   - Create queue classes (can exist alongside current code)
   - Implement state machine

### Phase 2: Core Refactoring (Medium Risk)
1. **Migrate to Async Pattern**
   - Convert `BackendService` to async loop
   - Convert `AudioPlaybackService` to async loop
   - Add cancellation token support

2. **Introduce VoiceMessageProcessor**
   - Extract `Say()` logic into service
   - Gradually migrate call sites

3. **Add Unit Tests**
   - Test queue operations
   - Test state transitions

### Phase 3: Enhanced Features (Higher Risk)
1. **3D Audio Improvements**
   - Implement `Live3DAudioEngine`
   - Add source position polling

2. **Configuration Expansion**
   - Add new configuration options
   - Migration for existing configs

3. **Native UI (Optional)**
   - Add KamiToolKit integration if desired

### Phase 4: Polish
1. **Logging Enhancements**
   - Integrate structured logging
   - Add event correlation

2. **Performance Optimization**
   - Profile and optimize hot paths
   - Memory usage improvements

---

## Critical Files to Port

| Priority | File | Description |
|----------|------|-------------|
| High | `ServiceContainer.cs` | DI container |
| High | `ServiceBuilder.cs` | Service registration |
| High | `VoiceMessageQueue.cs` | Queue system |
| High | `VoiceMessageProcessor.cs` | Speech processing pipeline |
| High | `BackendService.cs` | Async backend |
| High | `AudioPlaybackService.cs` | Async playback |
| Medium | `Live3DAudioEngine.cs` | 3D audio |
| Medium | `AlltalkInstanceService.cs` | Instance management |
| Low | `Windows/Native/*.cs` | Native UI |

---

## Risk Assessment

| Change | Risk Level | Mitigation |
|--------|------------|------------|
| DI Container | Low | Add alongside existing code |
| Queue System | Medium | Feature flag, gradual rollout |
| Async Migration | Medium | Thorough testing, fallback |
| VoiceMessageProcessor | Low | Keep old method as fallback |
| Native UI | High | Keep ImGui as default |
| Config Changes | Low | Migration helper |

---

## Backwards Compatibility

All changes should maintain backwards compatibility:
1. Keep existing configuration format
2. Support both sync and async paths during transition
3. Feature flags for new functionality
4. Gradual migration with deprecation warnings

---

## Testing Checklist

- [ ] Queue operations work correctly
- [ ] State transitions are accurate
- [ ] Priority system gives dialogue precedence
- [ ] Cancellation works properly
- [ ] Audio playback continues to function
- [ ] 3D positioning is accurate
- [ ] Configuration loads/saves correctly
- [ ] All addon helpers work (Talk, BattleTalk, Bubble, Chat, SelectString)
- [ ] Lip sync continues to work
- [ ] No memory leaks

---

## Additional Notes

### Version Information
- Echokraut Version: 0.18.0.2 (latest)
- Dalamud API Level: 14
- Target Framework: .NET 10.0

### Key Dependencies
- ManagedBass 4.0.1 (audio)
- R3 1.1.13 (reactive extensions)
- KamiToolKit (optional, for native UI)
- Echotools.Logging (structured logging)

### Files NOT Changed Significantly
- `AddonBattleTalkHelper.cs` - Minor updates
- `AddonBubbleHelper.cs` - Minor updates
- `AddonSelectStringHelper.cs` - Minor updates
- `AddonCutSceneSelectStringHelper.cs` - Minor updates
- `ChatTalkHelper.cs` - Minor updates
- `LipSyncHelper.cs` - Minor updates
- `SoundHelper.cs` - Minor updates
