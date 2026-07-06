# VoiceMaster Crash Wargame Prompt

WARGAME ORDER. You are not executing this mission. You are wargaming it. A cheaper executor runs the brief below later. Your job is to produce the route it will follow.

Recon first, read-only:

- Read `/home/kruillin/Projects/Projects/VoiceMaster/AGENTS.md`.
- Read `icm/CONTEXT.md` if the task touches existing staged workflow.
- Read `Implementation/README.md` if it explains runtime architecture.
- Inspect plugin manifest/config files, startup code, audio/voice initialization, Dalamud lifecycle hooks, and any native dependency loading.
- If live `.xlcore` context is needed, inspect it read-only first and require backups before any config mutation.

Then fight the mission on paper, move by move, and write the battle plan to `wargames/voicemaster-crash-battle-plan.md`:

- Every move states its expected observation, exactly what the executor should see if it worked.
- Every move carries its most likely failure, the cause it signals, and the counter-move.
- Every fork gets a trigger: if the executor observes X, take route B.
- Assumptions recon could not settle get marked `RECON NEEDED` with the exact check that settles it.
- End with abort conditions and verification runs, including what pass looks like for each.
- Keep the plan executable by a mid-tier coding model without asking follow-up questions.

=== THE MISSION BRIEF (the executor's orders, not yours) ===

Repository: `/home/kruillin/Projects/Projects/VoiceMaster`.

Goal: identify and fix the top 3 real crash, load-order, or runtime-initialization defects in VoiceMaster, with special focus on Dalamud plugin startup, native/audio dependency loading, config migration, and teardown safety.

Before touching anything, trace the plugin lifecycle from load through UI/config initialization, audio/voice service creation, runtime event subscriptions, and disposal. If recent crashpacks or `.xlcore` plugin config are involved, copy evidence into the report but do not mutate live config until the plan calls for an explicit backup.

Rules:

- No style nits.
- No broad rewrite.
- No replacing the project identity or behavior with unrelated code.
- Do not overwrite user changes.
- Fix only the top 3 evidence-backed findings.
- Each finding must cite file and line, explain the failure scenario in one sentence, rate severity, and include proof from a failing test, reproduction command, log/crash trace, build diagnostic, or concrete trace through the code.
- Any live Dalamud or game launch step must be marked `MANUAL VERIFY` with exact expected behavior and rollback instructions.

Required verification:

- Run the repo's documented build command if discoverable.
- Run any focused tests or static checks that exist.
- Verify startup and disposal paths by code trace even if the game cannot be launched.
- If build/test cannot run on this machine, capture the exact error and provide the smallest next command for the user to run in the right environment.
- Final report must separate: fixed findings, unfixed evidence-backed findings, verification results, manual game checks, rollback notes, and residual risk.
