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
# VoiceMaster Integration Guide

This guide provides step-by-step instructions for integrating the Echokraut improvements into VoiceMaster.

## What Has Been Delivered

### 1. Comparison Analysis (`ECHOKRAUT_COMPARISON.md`)
A comprehensive comparison between Echokraut and VoiceMaster, including:
- 11 major architectural improvements identified
- Risk assessment for each change
- Integration phases and priorities
- Testing checklist

### 2. Core Infrastructure Files (`Implementation/`)

#### Service Container Pattern
- **`Services/ServiceContainer.cs`** - Dependency injection container
- **`Services/Queue/VoiceMessageQueue.cs`** - Thread-safe message queue
- **`Services/Queue/VoiceMessageEntry.cs`** - Queue entry with state tracking
- **`Services/Queue/IVoiceMessageQueue.cs`** - Queue interface

#### Supporting Types
- **`Enums/VoiceMessageState.cs`** - Message state enumeration
- **`Enums/TextSource.cs`** - Text source enumeration
- **`DataClasses/VoiceMessage.cs`** - Message data classes

#### Unit Tests
- **`Tests/VoiceMessageQueueTests.cs`** - Comprehensive queue tests

## Integration Steps

### Phase 1: Foundation (Low Risk)

#### Step 1: Copy Infrastructure Files
Copy all files from `Implementation/` to your VoiceMaster project:
```
Implementation/Services/ServiceContainer.cs → VoiceMaster/Services/
Implementation/Services/Queue/*.cs → VoiceMaster/Services/Queue/
Implementation/Enums/*.cs → VoiceMaster/Enums/
Implementation/DataClasses/VoiceMessage.cs → VoiceMaster/DataClasses/
Implementation/Tests/*.cs → VoiceMaster.Tests/
```

#### Step 2: Update Existing DataClasses
Add the following to your existing `DataClasses/`:
- `VoiceMessage` class (or merge with existing)
- `NpcMapData` enhancements from `VoiceMessage.cs`
- `EchokrautVoice` class

#### Step 3: Add Enums
Add the new enums to your project:
- `VoiceMessageState`
- `TextSource` (if not already present)
- `Genders`
- `NpcRaces`

### Phase 2: Queue Integration (Medium Risk)

#### Step 4: Create Queue Instance
In your `Plugin.cs` or service initialization:
```csharp
// Add to Plugin constructor or service setup
_voiceMessageQueue = new VoiceMessageQueue();
```

#### Step 5: Integrate with Backend Service
Modify your backend/AllTalk service to use the queue:
```csharp
public class BackendService
{
    private readonly IVoiceMessageQueue _queue;
    
    public BackendService(IVoiceMessageQueue queue)
    {
        _queue = queue;
    }
    
    public void ProcessVoiceMessage(VoiceMessage message)
    {
        // Determine priority
        var isPriority = message.Source switch
        {
            TextSource.AddonTalk or 
            TextSource.AddonBattleTalk or 
            TextSource.AddonCutsceneSelectString or 
            TextSource.AddonSelectString => true,
            _ => false
        };
        
        _queue.Enqueue(message, isPriority);
    }
}
```

#### Step 6: Add Async Generation Loop
Create a background task for processing the queue:
```csharp
private async Task GenerationLoopAsync(CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        if (_queue.TryDequeuePendingGeneration(out var entry) && entry != null)
        {
            await ProcessGenerationAsync(entry, cancellationToken);
        }
        await Task.Delay(100, cancellationToken);
    }
}
```

### Phase 3: Service Refactoring (Higher Risk)

#### Step 7: Implement VoiceMessageProcessor
Create a new service to replace the `Plugin.Say()` method:
```csharp
public class VoiceMessageProcessor
{
    public async Task ProcessSpeechAsync(
        Guid eventId, 
        IGameObject? speaker, 
        SeString speakerName, 
        string textValue)
    {
        // Step 1: Check backend availability
        // Step 2: Clean and normalize text
        // Step 3: Get or create NPC data
        // Step 4: Check if NPC/voice is muted
        // Step 5: Assign voice if needed
        // Step 6: Calculate final volume
        // Step 7: Build voice message
        // Step 8: Process the voice message
    }
}
```

#### Step 8: Implement DI Container
Set up the service container in `Plugin.cs`:
```csharp
public class Plugin : IDalamudPlugin
{
    private readonly ServiceContainer _services;
    
    public Plugin(...)
    {
        _services = new ServiceContainer();
        RegisterServices();
    }
    
    private void RegisterServices()
    {
        _services.RegisterSingleton<IVoiceMessageQueue>(new VoiceMessageQueue());
        _services.RegisterFactory<IBackendService>(c => new BackendService(
            c.GetService<IVoiceMessageQueue>(),
            ...
        ));
        // Register other services...
    }
}
```

### Phase 4: Testing & Validation

#### Step 9: Run Unit Tests
```bash
dotnet test VoiceMaster.Tests.csproj
```

#### Step 10: Manual Testing Checklist
- [ ] Queue operations work correctly
- [ ] State transitions are accurate
- [ ] Priority system gives dialogue precedence over bubbles
- [ ] Cancellation works properly
- [ ] Audio playback continues to function
- [ ] 3D positioning is accurate
- [ ] Configuration loads/saves correctly
- [ ] All addon helpers work
- [ ] Lip sync continues to work
- [ ] No memory leaks

## Key Changes Summary

### New Architecture Benefits
1. **Thread-Safe Queue**: Concurrent collections prevent race conditions
2. **State Machine**: Clear message lifecycle tracking
3. **Priority System**: Dialogue gets precedence over bubbles
4. **Async Processing**: Non-blocking TTS generation
5. **DI Container**: Testable, decoupled services
6. **Unit Tests**: Verifiable correctness

### Breaking Changes to Avoid
- Keep existing configuration format
- Maintain backwards compatibility for voice assignments
- Don't change public API surface immediately
- Use feature flags for new functionality

## Rollback Strategy

If issues arise:
1. Keep old code paths available behind feature flags
2. Maintain `Plugin.Say()` as fallback
3. Version control your changes
4. Test incrementally

## Additional Resources

- Original Echokraut: https://github.com/RenNagasaki/Echokraut
- Comparison document: `ECHOKRAUT_COMPARISON.md`
- Unit test examples: `Implementation/Tests/`

## Support

For issues or questions:
1. Review the comparison document for context
2. Check unit tests for expected behavior
3. Compare with Echokraut source for reference implementation
