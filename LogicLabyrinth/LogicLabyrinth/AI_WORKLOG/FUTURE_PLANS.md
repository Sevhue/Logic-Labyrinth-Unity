# Future Plans

## Immediate validation (after selective Level1-6 merge from `origin/67`)
- Open `Level1` to `Level6` in Unity one-by-one and verify scene loads without missing references.
- In each level, verify gate spawns are reachable and not clipped.
- Verify candle objects/collect flow still behave as intended in levels where candle diffs existed.
- Confirm puzzle tables still open and render correctly in levels with puzzle interactions.
- Run quick smoke test for death/resume flow in each merged level.
- If all good, create commit with only the six staged scene files.

## Branch audit follow-up (`origin/67`)
- If needed, compare `origin/67` vs `origin/main` scene-by-scene before merging anything.
- Safe candidate to cherry-pick selectively: SpawnPoint transform changes in `Level7.unity` and `Level8.unity` only, if those are desired.
- Review candle scene edits separately in `Level1`, `Level2`, `Level3`, `Level5`, `Level6` before merging; those are not isolated SpawnPoint-only changes.
- Review `Level1` wall-related diff manually in Unity Editor before merging; deeper CLI check shows wall prefab instance reparent/serialization churn (same local values appearing as remove/add), not an obvious intentional wall reposition patch.
- Keep UI asset changes (`Level4.prefab`, `UITable.prefab`, `Main.unity`) out of any SpawnPoint-only merge unless explicitly wanted.

## Immediate validation (manual close should keep attempts)
- Open puzzle and submit one wrong answer so attempts show `2/3`.
- Press `Esc` (or click `X`) to close.
- Reopen the same table immediately.
- Confirm attempts still show `2/3` (not reset to `3/3`).
- Fail until game over and confirm next fresh open starts at `3/3` as expected.

## Immediate validation (wrong submit shake visibility)
- Open puzzle and submit a wrong answer while attempts remain.
- Confirm the camera shake is visible immediately (not only on close/game-over).
- Submit another wrong answer and confirm shake repeats consistently.
- On final wrong answer, confirm shake still triggers before game-over flow.

## Immediate validation (puzzle 0/3 should kill player)
- In puzzle table, fail submissions until attempts reach `0/3`.
- Confirm `GAME OVER` feedback is shown first (about 3 seconds).
- Confirm puzzle closes and then normal player death flow starts (`YOU DIED` + respawn prompt).
- Confirm no gate pickup is possible during death overlay (dead-state interaction lock still active).

## Immediate validation (death-state interaction lock)
- Die while carrying gates so they drop at death location.
- During death/game-over overlay, spam `E` and confirm no gate is collected.
- Press Space to respawn and confirm `E` interactions work normally again.

## Immediate validation (correct level-entry respawn anchor)
- Enter Level 2 from normal gameplay flow and note the entry point.
- Die once and confirm respawn returns to that same level-entry point.
- Confirm respawn does not jump to gate markers (`SpawnPoint1..SpawnPoint10`).
- If a dedicated player marker is present in a scene, verify respawn uses that marker.

## Immediate validation (all-level spawn marker rule)
- Repeat death/respawn test in Levels 1 through 8.
- Verify no level uses generic gate `SpawnPoint*` markers for player rescue/respawn.
- Verify fallback behavior remains stable in levels without explicit player markers (uses level-entry anchor).

## Immediate validation (game over + question rotation + respawn)
- In a multi-question level (e.g., Level2/Level4), fail puzzle submission 3 times.
- Re-open table and verify it loads a different remaining question instead of repeating the failed one.
- Verify dropped/placed gates are cleared after game over close (slots reset visually and functionally).
- Die in-level and confirm respawn no longer uses gate marker names (`SpawnPoint1..N`).
- Confirm respawn uses explicit player marker if present; otherwise uses level-start position.

## Team consistency hardening (machine-local scale)
- Keep ADR scale source-of-truth in code defaults + sane range guard (already applied).
- If teammate sees bad ADR size, clear only local keys: `LL_ADR_HAND_SCALE_X`, `LL_ADR_HAND_SCALE_Y`, `LL_ADR_HAND_SCALE_Z`.
- Optionally add a small in-game debug action to reset ADR pose prefs for QA builds.
- Re-verify that no additional gameplay systems persist object scale via PlayerPrefs.

## Immediate validation (ADR size consistency across machines)
- On friend machine, pull latest and enter any gameplay level.
- Select ADR slot and equip (without manually saving pose): bottle should appear at normal handheld size (not giant).
- Press `P` once to save preferred ADR pose on that machine.
- If ADR still appears huge on friend machine from old local prefs, clear ADR PlayerPrefs keys (`LL_ADR_HAND_SCALE_X/Y/Z`) or use fresh prefs, then retest.

## Immediate validation (SpawnPoint-only merge for Level7/Level8)
- Open Level 7 and Level 8 scenes and verify SpawnPoint gizmo positions match Evan's intended layout.
- Confirm each SpawnPoint has the reduced scale copied from Evan branch (non-`1,1,1`).
- Run gate spawn flow in both levels and verify spawned gates appear at valid, reachable locations.
- Confirm no other scene systems regressed (torches/traps/puzzle table unchanged by this selective merge).
- If any SpawnPoint is invalid, adjust only that SpawnPoint transform and keep the merge scope strictly transform-only.

## Immediate validation (restart confirmation)
- In any level, open Pause menu and click `Restart`.
- Confirm dialog appears with themed title/message and buttons: `Yes, Restart` / `No`.
- Click `No`: dialog closes and pause menu remains open.
- Click `Yes, Restart`: level restarts and existing restart behavior (inventory/gate/session reset) still works.
- Press `Esc` while restart confirmation is open: dialog should close and return to pause panel.

## Immediate retest (after 2026-04-09 minimal collector fix)
- Retest gate pickup in the exact cabinet/partial-embed case for AND and OR gates.
- Retest a normal open-area pickup case for AND, OR, and NOT to confirm no regressions.
- Retest a far-doorway case to confirm mesh-distance still blocks remote collection.

## Only if still failing
- Next simplest check: add temporary gate-type debug logs in `SimpleGateCollector` to confirm target acquisition and distance values for AND/OR at runtime.
- If logs show no target on AND/OR only, inspect scene-level collider/layer overrides on spawned gate instances (not prefab assets) before any structural script changes.

## Near-term validation
- Playtest Level2 puzzle repeatedly until all questions (`Q1` to `Q5`) appear correctly.
- Playtest Level4 puzzle repeatedly for the same panel-selection validation.
- Quick sanity check Level3 puzzle still opens correctly.

## Stability improvements (optional)
- Add a compact debug log with selected UI path:
  - `LevelX/Background/QY`
- Add safe fallback behavior if selected `QY` is missing:
  - show clear warning in UI,
  - or auto-switch to `Q1` only within the same level root.

## Process rule
- Keep this file updated whenever we agree on next steps so future sessions can continue without losing context.

