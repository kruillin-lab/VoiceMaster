# VoiceMaster Crash Stability Implementation

Date: 2026-07-06
Stage: 03-implement

## Scope

Executed the top-three crash/load-order/runtime-init fixes from `wargames/voicemaster-crash-battle-plan.md`.

## Changes

- `VoiceMaster/VoiceMaster.csproj`: made `bass.dll` an explicit content item and restored a guarded dev-plugin deploy copy so the native BASS DLL travels beside `VoiceMaster.dll`.
- `VoiceMaster/Helper/Functional/Live3DAudioEngine.cs`: changed BASS initialization from throwing to a latched audio-disabled state, with a single error log.
- `VoiceMaster/Helper/API/BackendHelper.cs`: guarded eager `PlayingHelper.Setup()` so audio init cannot abort plugin construction.
- `VoiceMaster/Helper/Functional/PlayingHelper.cs`: handled `Guid.Empty` from disabled audio by logging and returning without marking playback active.
- `VoiceMaster/Helper/API/JsonLoaderHelper.cs` and `VoiceMaster/Plugin.cs`: wrapped startup data initialization and observed task faults so remote data failures do not become unobserved task failures.

## Verification

- Baseline build before edits: `0 Error(s)`, `442 Warning(s)`.
- Post-change build: `0 Error(s)`; full compile showed existing warning volume, up-to-date verification showed only the pre-existing OtterGui SDK-version warnings.
- `VoiceMaster/bin/Release/bass.dll` now exists beside `VoiceMaster/bin/Release/VoiceMaster.dll`.
- No test project exists in the relocated source repo; stale test references remain only in carried `Implementation/` planning docs.
- `pwsh` is not installed, so no PowerShell domain evals were run.

## Manual Checks Remaining

- Launch FFXIV/XIVLauncher with the rebuilt plugin and confirm no `DllNotFoundException` for `bass.dll`.
- Trigger TTS playback and confirm either audio plays or one BASS init error is logged while the plugin remains loaded.
- Test startup with GitHub raw content unavailable and confirm plugin load survives with data-load errors only.
