---
tags:
  - type/battle-plan
  - project/voicemaster
  - status/active
  - workflow/wargame
type: battle-plan
project: voicemaster
status: active
aliases: []
---

# VoiceMaster Crash Battle Plan

> **This is a route, not a report.** It was wargamed on paper against read-only
> recon. A mid-tier executor follows it move by move to fix the top 3 crash /
> load-order / runtime-init defects. Every move states what you should see,
> what most likely goes wrong, and where to fork. Do not improvise past a
> `TRIGGER`. Do not proceed past an `ABORT` condition.

---

## 0. Theatre map (what recon settled, so you don't re-fight it)

**The mission brief names the repo as `/home/kruillin/Projects/Projects/VoiceMaster`.
That directory is a docs/planning shell — it has no plugin source.** It is not
even its own git repo (`git rev-parse --show-toplevel` there returns the parent
`/home/kruillin/Projects/Projects`). It holds AGENTS.md, ICM stages,
`Implementation/` (reference snippets that were never the live build), and this
`wargames/` folder.

**The real, buildable plugin source lives in a separate git repo:**

- Repo root: `/home/kruillin/Projects/Projects/output/VoiceMaster`
- Active branch (post-hardening): `claude/wonderful-khorana-1847d9`
- The `VoiceMaster/` project folder and the sibling `OtterGui/` project are both
  under that root. `Plugin.cs`, the backends, the audio engine, and config live
  there.

> `RECON NEEDED [R0]` — **Confirm the source root before any edit.**
> Check: `ls /home/kruillin/Projects/Projects/output/VoiceMaster/VoiceMaster/Plugin.cs`.
> - If present → that is your working tree; all file paths below are relative to
>   `/home/kruillin/Projects/Projects/output/VoiceMaster/`.
> - If **absent** → the repo may have been moved to
>   `/home/kruillin/Projects/Projects/VoiceMaster/` (a folder move was pending at
>   plan time). Re-locate with
>   `find /home/kruillin/Projects/Projects -name Plugin.cs -path '*VoiceMaster*' -not -path '*/obj/*' 2>/dev/null`
>   and rebase every path in this plan onto the directory that contains
>   `VoiceMaster/Plugin.cs`. Do not edit any copy under `Implementation/`.

### Toolchain facts (the machine is Linux; the game runs under Wine via XIVLauncher.Core)

- **.NET SDK is NOT on `PATH` as `dotnet`.** It lives at `~/.dotnet/dotnet`
  (v10.0.301 at recon). Every dotnet command below uses that absolute path.
- **The build needs the Dalamud assemblies.** They live at
  `~/.xlcore/dalamud/Hooks/dev`. Export `DALAMUD_HOME` for every build.
- **Canonical build command** (green at plan time — 0 errors, 442 warnings):
  ```bash
  DALAMUD_HOME=~/.xlcore/dalamud/Hooks/dev ~/.dotnet/dotnet build \
    /home/kruillin/Projects/Projects/output/VoiceMaster/VoiceMaster/VoiceMaster.csproj -c Release
  ```
- **Live plugin config** (do not mutate without the backup move): `~/.xlcore/pluginConfigs/VoiceMaster.json`
  (there is also a stale `NpcVoiceMaster.json` beside it — leave it alone).
- **`bass.dll` is a Windows native DLL** loaded under Wine by ManagedBass P/Invoke.
  It is git-tracked at `VoiceMaster/bass.dll` (~160 KB).

### The three targets (each cited, one-sentence scenario, severity)

| # | Defect | File:line | One-sentence failure scenario | Severity |
|---|--------|-----------|-------------------------------|----------|
| F1 | `bass.dll` never deployed next to the plugin DLL | `VoiceMaster/VoiceMaster.csproj:57` | On a fresh dev-plugin deploy the DLL lands without `bass.dll`, so the first `Bass.Init` P/Invoke throws `DllNotFoundException` out of the constructor and Dalamud fails the whole plugin load. | HIGH |
| F2 | Audio init (`Bass.Init`) runs eagerly and unguarded inside the constructor, and its lazy path is swallowed by a bare `catch {}` | `VoiceMaster/Helper/Functional/Live3DAudioEngine.cs:216` reached from `VoiceMaster/Plugin.cs:158` → `VoiceMaster/Helper/API/BackendHelper.cs:27`; swallow at `VoiceMaster/Helper/Functional/PlayingHelper.cs:99-108` | If BASS can't initialize (no audio device under Wine, `Device3D` unsupported), the thrown `InvalidOperationException` either aborts construction (plugin won't load) or is silently eaten on the queue thread (audio dead all session, no signal). | HIGH |
| F3 | Startup NPC-data load is fire-and-forget over the network with no local fallback | fire-and-forget at `VoiceMaster/Plugin.cs:156`; blocking web calls at `VoiceMaster/Helper/API/JsonLoaderHelper.cs:17-23,31` | If `raw.githubusercontent.com` is unreachable/slow at login, race/gender/voice maps stay empty for the session and later NPC lookups degrade or null-deref, with a race where dialogue can fire before the maps finish loading. | MEDIUM (HIGH if it null-derefs — see R3) |

> These three are **orthogonal to the hardening pass already on this branch**
> (that pass touched `Dispose`, HttpClient lifetime, credential masking). You are
> extending a green tree, not undoing it. Read `git log --oneline -6` first so you
> recognize prior commits and don't revert them.

---

## 1. Move sequence

### MOVE 1 — Establish the baseline (read-only)

```bash
cd /home/kruillin/Projects/Projects/output/VoiceMaster
git status --short
git log --oneline -6
DALAMUD_HOME=~/.xlcore/dalamud/Hooks/dev ~/.dotnet/dotnet build VoiceMaster/VoiceMaster.csproj -c Release 2>&1 | tail -5
```

- **Expected observation:** working tree clean (or only your own new files);
  HEAD is a `fix:` commit from the hardening pass; build prints
  `Build succeeded.` with `0 Error(s)` and ~442 warnings.
- **Most likely failure:** `Build FAILED` on your very first run.
  - *Cause A:* `DALAMUD_HOME` not exported → errors about missing Dalamud types.
    **Counter:** re-run with the exact `DALAMUD_HOME=...` prefix shown.
  - *Cause B:* `~/.dotnet/dotnet` not found. **Counter:** `command -v dotnet ||
    ls ~/.dotnet/dotnet`; if neither exists, `ABORT-ENV` (see §3).
- **TRIGGER — dirty tree with edits you did not make:** stop and run
  `git stash list` / `git diff`. Do **not** overwrite user changes (mission rule).
  If someone else's WIP is present, branch off it (`git switch -c
  wargame/crash-fixes`) before editing so nothing is clobbered.

---

### MOVE 2 — Fix F1: ship `bass.dll` with the plugin

**Proof to capture first (read-only):**
```bash
sed -n '44,58p' VoiceMaster/VoiceMaster.csproj      # the deploy target + the "Bass.dll copy removed" comment on line 57
grep -n 'CopyToOutputDirectory' VoiceMaster/VoiceMaster.csproj   # confirms bass.dll copies to BUILD output only
```
- **Expected observation:** the `DeployToDevPlugins` target copies `VoiceMaster.dll`
  and `VoiceMaster.json` to `$(APPDATA)\XIVLauncher\devPlugins\VoiceMaster` but
  line 57 is a comment saying the bass.dll copy was removed. `bass.dll` has
  `<CopyToOutputDirectory>Always</CopyToOutputDirectory>`, so it reaches
  `bin/Release/`, **but never the deployed plugin folder.**

> `RECON NEEDED [R1]` — **Does Dalamud package `bass.dll` on real installs?**
> The auto-deploy target only runs on Windows (`Condition="'$(APPDATA)' != ''"`),
> so on this Linux box it never fires and the question is how the plugin is
> actually installed. Check: is there a `.json` manifest listing extra files, or
> does the release path rely on `DalamudPackager` (note the csproj *removes*
> `DalamudPackager`)? Run:
> `grep -rn 'DalamudPackager\|bass' VoiceMaster/VoiceMaster.csproj` and
> `ls ~/.xlcore/installedPlugins/*VoiceMaster*/**/bass.dll 2>/dev/null`.
> - If a deployed/installed copy already sits next to a shipped `bass.dll` →
>   F1 is latent (only bites dev deploys); **still fix it** but downgrade the
>   MANUAL VERIFY urgency.
> - If no `bass.dll` ships anywhere → F1 is the live crash; proceed.

**The fix (executable, minimal):** make `bass.dll` travel with the DLL in both
the build output *and* any deploy copy. Two independent, additive edits:

1. In `VoiceMaster.csproj`, inside the `DeployToDevPlugins` target, restore a
   guarded copy right after the `VoiceMaster.json` copy (line ~56):
   ```xml
   <Copy SourceFiles="$(TargetDir)bass.dll" DestinationFolder="$(DevPluginPath)"
         Condition="Exists('$(TargetDir)bass.dll')" ContinueOnError="true" />
   ```
   The `Exists(...)` guard is what prevents the historical `MSB3030` build
   failure the comment was working around — so you get the copy without the break.

- **Expected observation after edit:** rebuild (Move 1 command) still prints
  `Build succeeded.`; `ls bin/Release/bass.dll` exists.
- **Most likely failure:** build breaks with `MSB3030 bass.dll ... being used by
  another process` or `could not find`. **Cause:** unguarded/locked copy.
  **Counter:** confirm the `Condition="Exists(...)"` guard is present; if a lock,
  it's a Windows-only concern and won't occur on this Linux build.
- **TRIGGER — R1 showed the real install path is elsewhere (e.g. a packaged zip
  target):** route to **MOVE 2B**.

**MOVE 2B (fork, only if R1 says packaging is the real channel):** ensure
`bass.dll` is an `<None Include>` with `Pack`/packaging metadata appropriate to
whatever packager the release uses, rather than (or in addition to) the deploy
target. Keep the change additive; do not delete existing item groups.

---

### MOVE 3 — Fix F2: isolate audio init so BASS failure degrades instead of crashing

**Proof to capture first (read-only):**
```bash
sed -n '205,236p' VoiceMaster/Helper/Functional/Live3DAudioEngine.cs   # EnsureInit throws on Bass.Init failure (line ~216)
sed -n '38,44p'   VoiceMaster/Helper/Functional/PlayingHelper.cs        # Setup() -> ConfigureListener -> EnsureInit
sed -n '95,110p'  VoiceMaster/Helper/Functional/PlayingHelper.cs        # WorkPlayingQueues while-loop wrapped in bare catch {}
grep -n 'PlayingHelper.Setup'    VoiceMaster/Helper/API/BackendHelper.cs # line ~27
grep -n 'BackendHelper.Initialize' VoiceMaster/Plugin.cs                 # line ~158, NOT in a try/catch
```
- **Expected observation:** the call chain **Plugin ctor (Plugin.cs:158) →
  BackendHelper.Initialize → PlayingHelper.Setup (BackendHelper.cs:27) →
  ConfigureListener → EnsureInit → Bass.Init** runs synchronously during
  construction, and nothing on that path catches. Separately, once the queue
  thread is running, `WorkPlayingQueues` wraps its whole `while` loop in
  `try { … } catch { }` — so a later `EnsureInit` throw from `PlayStream` is
  swallowed and the loop exits permanently (silent audio death).

**The fix (two small, surgical changes — do NOT rewrite the engine):**

1. **Make BASS init non-fatal and remembered.** In `Live3DAudioEngine.cs`, give
   the engine a failure latch so a dead device disables audio instead of throwing
   on every call. Change `EnsureInit()` so the `Bass.Init` failure path sets a
   `bool _initFailed` and returns `false` (make `EnsureInit` return `bool`), and
   have callers bail when it returns false:
   - `EnsureInit`: on the `!Bass.Init(...) && LastError != Already` branch, log
     once, set `_initFailed = true`, `return false;` instead of `throw`. Top of
     method: `if (_initFailed) return false; if (_inited) return true;`.
   - `PlayStream` (line ~175) and `ConfigureListener` (line ~57): `if
     (!EnsureInit()) return …;` (return a sentinel/`Guid.Empty` for PlayStream;
     just `return;` for ConfigureListener).
2. **Guard the eager startup path.** In `BackendHelper.Initialize` (around line
   27) wrap `PlayingHelper.Setup();` in `try { … } catch (Exception ex) {
   LogHelper.Error(...); }` so a construction-time BASS failure can never abort
   the Plugin constructor.

- **Expected observation after edit:** `Build succeeded.`; by code trace, a forced
  BASS failure now logs one error and leaves the plugin loaded with audio
  disabled, rather than throwing out of the constructor or silently killing the
  queue thread.
- **Most likely failure:** compile errors from changing `EnsureInit`'s return
  type (callers still treat it as `void`). **Counter:** update *both* call sites
  (lines ~57 and ~175); re-grep `grep -n 'EnsureInit' VoiceMaster/Helper/Functional/Live3DAudioEngine.cs`
  to be sure you caught them all before building.
- **TRIGGER — you discover a third `EnsureInit` caller or another `throw` inside
  the audio init path:** treat it the same way (return-false + guard); do not
  leave a mixed throw/return design.

> `RECON NEEDED [R2]` — **Does BASS actually init under this Wine setup?**
> This decides whether F2 is a live crash or a latent guard. Non-destructive
> check: `ls ~/.xlcore/dalamud/Hooks/dev` is irrelevant here; instead search the
> Dalamud log after a real launch for `Bass.Init` / `DllNotFound` (see MANUAL
> VERIFY M2). If BASS inits fine in the field, F2 is defense-in-depth (still
> correct to land). If it fails, F2 is what turns a hard crash into a soft
> degrade.

---

### MOVE 4 — Fix F3: make startup NPC-data load resilient (no fire-and-forget crash, no permanent empty maps)

**Proof to capture first (read-only):**
```bash
sed -n '15,35p' VoiceMaster/Helper/API/JsonLoaderHelper.cs   # remote raw.githubusercontent URLs + blocking WebRequest, 5s timeout
grep -n 'JsonLoaderHelper.Initialize' VoiceMaster/Plugin.cs  # line ~156: `_ = Task.Run(() => JsonLoaderHelper.Initialize(...))`
grep -rn 'ModelsToRaceMap\|VoiceMaps\|ModelGenderMap' VoiceMaster/Helper/Data/NpcDataHelper.cs | head
```
- **Expected observation:** `Initialize` is launched fire-and-forget on a
  `Task.Run` at `Plugin.cs:156`; it performs **synchronous** `WebRequest`s to
  `raw.githubusercontent.com/RenNagasaki/Echokraut/...` with a 5s timeout each;
  the maps it fills are read later by `NpcDataHelper` with no guarantee they are
  populated (race), and there is no on-disk fallback (the local `Resources/*.json`
  were confirmed dead and removed by the hardening pass — they were never read at
  runtime, so their removal changed nothing, but it also means remote is the
  *only* source).

> `RECON NEEDED [R3]` — **Do the map consumers null-check?** This sets F3's
> severity. Check every read of `ModelsToRaceMap`, `ModelGenderMap`, `VoiceMaps`
> in `NpcDataHelper.cs` and `TalkTextHelper.cs`:
> `grep -rn 'ModelsToRaceMap\|ModelGenderMap\|VoiceMaps' VoiceMaster --include='*.cs' | grep -v obj/`.
> - If a consumer indexes/`.First()`s without a guard → F3 is **HIGH** (empty map
>   → exception on first NPC line). Fix both the loader (below) *and* add the
>   guard at that specific consumer line.
> - If all consumers already tolerate empty → F3 is **MEDIUM** (silent wrong
>   voices only); the loader fix alone suffices.

**The fix (minimal, additive — keep it a background load, just a safe one):**

1. In `JsonLoaderHelper.Initialize`, wrap the whole body in `try/catch` that logs
   and leaves the (already-initialized-empty) collections intact — a network
   failure must never surface as an unobserved task exception.
2. At the `Plugin.cs:156` call site, observe the task's faults instead of
   discarding them: attach a `.ContinueWith(t => LogHelper.Error(...),
   TaskContinuationOptions.OnlyOnFaulted)` (mirror the pattern the hardening pass
   already used in `InworldAIBackend`/`PlayingHelper`).
3. If R3 came back HIGH: add the smallest guard at the offending consumer line
   (e.g. `if (JsonLoaderHelper.ModelsToRaceMap.Count == 0) return <default>;`).

- **Expected observation after edit:** `Build succeeded.`; by trace, a login with
  no network logs one error and yields default/empty maps without an unobserved
  exception or a hard failure on the first NPC line.
- **Most likely failure:** you over-reach and try to add a local-file fallback by
  resurrecting the deleted `Resources/*.json`. **Counter:** do **not** — the
  mission says no broad rewrite; the resilient-empty-map path is the scoped fix.
  Re-adding data files is out of scope (log it under residual risk instead).
- **TRIGGER — R3 shows a null-deref consumer AND it's on the hot dialogue path:**
  the guard at that consumer is mandatory, not optional; land it in this move.

---

### MOVE 5 — Verify (see §4 for pass criteria), then stage and commit

```bash
DALAMUD_HOME=~/.xlcore/dalamud/Hooks/dev ~/.dotnet/dotnet build VoiceMaster/VoiceMaster.csproj -c Release 2>&1 | tail -5
git -C /home/kruillin/Projects/Projects/output/VoiceMaster diff --stat
```
- **Expected observation:** `Build succeeded.`, `0 Error(s)`, warning count **≤ 442**
  (you should not have added warnings; a new `async`/`await` or unobserved-task
  edit that raises the count means recheck Move 4).
- Commit only the files you touched (`VoiceMaster.csproj`, `Live3DAudioEngine.cs`,
  `PlayingHelper.cs`, `BackendHelper.cs`, `JsonLoaderHelper.cs`, `Plugin.cs`, and
  any single guarded consumer). One commit, message scoped to
  `fix: crash/load-order hardening (bass deploy, BASS init isolation, startup data resilience)`.

---

## 2. Fork map (quick reference)

- `TRIGGER` Move 1 dirty tree with foreign edits → branch off it first, never overwrite.
- `TRIGGER` R1 says packaging (not the deploy target) is the real channel → **Move 2B**.
- `TRIGGER` R2 says BASS inits fine in the field → F2 lands as defense-in-depth, keep it.
- `TRIGGER` R3 says a consumer null-derefs on empty maps → add the consumer guard in Move 4 (F3 becomes HIGH).
- `TRIGGER` any move raises the warning count or breaks the build → revert *that move only* (`git checkout -- <file>`) and re-approach; never batch-revert prior green moves.

---

## 3. Abort conditions

- **ABORT-ENV:** `~/.dotnet/dotnet` missing AND `dotnet` not on PATH, or
  `~/.xlcore/dalamud/Hooks/dev` missing. You cannot build or trust a build.
  Capture `which dotnet; ls ~/.dotnet; ls ~/.xlcore/dalamud/Hooks/dev` and hand
  back to the user with the smallest next command (install .NET SDK / point
  `DALAMUD_HOME`). Do not "fix" code you can't compile.
- **ABORT-SCOPE:** you find the fix requires editing more than the files named in
  §1, or requires touching backend/audio *behavior* (not just init safety). Stop;
  this exceeds "top 3, no broad rewrite." Record it as an unfixed evidence-backed
  finding.
- **ABORT-SOURCE:** R0 cannot locate a tree containing `VoiceMaster/Plugin.cs`.
  Do not edit the `Implementation/` reference snippets as a substitute. Report
  that the buildable source could not be located.
- **ABORT-CONFIG:** any move tempts you to mutate `~/.xlcore/pluginConfigs/VoiceMaster.json`.
  Only the backup move (below) may touch live config, and only after copying it.

---

## 4. Verification runs (with pass criteria)

**V1 — Build gate (must run).**
`DALAMUD_HOME=~/.xlcore/dalamud/Hooks/dev ~/.dotnet/dotnet build VoiceMaster/VoiceMaster.csproj -c Release`
→ **PASS:** `Build succeeded.`, `0 Error(s)`, warnings ≤ 442.

**V2 — bass.dll travels (F1).**
`ls bin/Release/bass.dll` and, if the deploy target ran, `ls "$DevPluginPath/bass.dll"`.
→ **PASS:** `bass.dll` sits beside `VoiceMaster.dll` in every output that
contains the plugin DLL.

**V3 — Init isolation by code trace (F2).** Re-read the edited `EnsureInit`,
`PlayStream`, `ConfigureListener`, and `BackendHelper.Initialize`.
→ **PASS:** there is no reachable `throw` on the construction path, every
`EnsureInit` caller honors the `false` return, and `PlayingHelper.Setup()` is
inside a `try/catch`.

**V4 — Startup-data resilience by code trace (F3).** Re-read `JsonLoaderHelper.Initialize`
and the `Plugin.cs:156` call site.
→ **PASS:** the loader body is wrapped in `try/catch`, the task's faults are
observed, and (if R3=HIGH) the hot-path consumer guards an empty map.

**V5 — No regression of the hardening pass.**
`git diff 5a6d9d8..HEAD -- VoiceMaster/ | grep -c '^-'` sanity, and eyeball that
you did not revert credential masking, HttpClient sharing, or the Dispose fixes.
→ **PASS:** your diff only adds the F1–F3 changes.

---

## 5. MANUAL VERIFY (game launch — user runs these; you cannot)

> The executor cannot launch FFXIV. Mark these for the user with exact expected
> behavior and rollback.

**Backup first (the only sanctioned live-config touch):**
```bash
cp ~/.xlcore/pluginConfigs/VoiceMaster.json ~/.xlcore/pluginConfigs/VoiceMaster.json.bak.$(date +%s)
```

- **M1 (F1):** Deploy the freshly built plugin, launch FFXIV via XIVLauncher.Core,
  enable VoiceMaster. **Expected:** plugin loads with no red error in `/xllog`;
  no `DllNotFoundException: bass` / `Unable to load bass`. **Rollback:** disable
  the plugin; restore the previous plugin folder.
- **M2 (F2):** With the plugin loaded, trigger an NPC dialogue line. **Expected:**
  either TTS plays, or (if the audio device is unavailable) `/xllog` shows a
  single `Bass.Init failed` / audio-disabled log line and the game keeps running —
  **never** a plugin crash or a load failure. Search the log for `Bass.Init`,
  `Device3D`, `DllNotFound`. **Rollback:** disable plugin.
- **M3 (F3):** Log in with networking blocked (or firewall
  `raw.githubusercontent.com`). **Expected:** plugin still loads; `/xllog` shows a
  single data-load error; first NPC line does not throw (voices may be generic).
  Restore networking, `/xllog` should show maps loading on the next trigger or
  relog. **Rollback:** disable plugin; restore `VoiceMaster.json.bak.*` if config
  changed.

---

## 6. Report skeleton the executor must fill (mission requires these sections)

1. **Fixed findings** — F1/F2/F3 as landed, each with file:line, the one-sentence
   scenario, severity, and the proof (build diagnostic, code trace, or log).
2. **Unfixed evidence-backed findings** — anything hit by ABORT-SCOPE, plus the
   `RECON NEEDED` items that came back but were out of scope.
3. **Verification results** — V1–V5 pass/fail with the actual build tail.
4. **Manual game checks** — M1–M3 handed to the user, expected behavior stated.
5. **Rollback notes** — the config backup path, per-move `git checkout -- <file>`.
6. **Residual risk** — remote-only NPC data (no local fallback by design after the
   hardening pass), Wine/BASS device variability, and the R1 packaging question if
   still open.

---

## 7. Residual risk noted at plan time (so the executor doesn't rediscover it)

- **F3 is scoped to resilience, not restoration.** The plugin fetches all NPC
  race/gender/voice data from a third party's GitHub `master` (RenNagasaki/Echokraut)
  at every login. Even fixed, an upstream repo rename or file move silently
  degrades voices. A durable fix (bundle the JSON as an embedded resource fallback)
  is a *feature*, out of this mission's top-3 scope — flag it, don't build it here.
- **F2's soft-degrade hides a dead audio device.** After the fix, a user with no
  working BASS device gets silence + one log line, which is correct but quiet.
  Consider a one-time UI notice as a follow-up (out of scope).
- **The deploy target only runs on Windows** (`Condition="'$(APPDATA)' != ''"`).
  On this Linux dev box F1's copy never fires, so V2 must be checked against the
  actual install channel R1 identifies — not just the deploy target.
