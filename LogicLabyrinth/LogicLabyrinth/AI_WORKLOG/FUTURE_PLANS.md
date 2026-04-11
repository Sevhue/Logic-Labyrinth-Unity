# Future Plans

## Immediate validation (Level6 Table + 7-slot inventory checks)
- Validate new Level7 truth-table input flow:
  - open Level7 and press E on TruthDoor,
  - verify board renders correctly (no overlap/invisible state),
  - verify left input panel appears with `?`, `1`, `0`,
  - verify right tutorial panel appears and does not overlap the board,
  - click a green `?` in the table and confirm left display selects it,
  - confirm selected cell blinks while active,
  - click `1` and verify selected `?` changes to `1`,
  - click `0` immediately after and verify same selected cell changes back to `0`,
  - click another green `?`, click `0`, verify it changes to `0`,
  - verify SUBMIT/X/Attempts still work as before.
- Validate table input lock on all levels:
  - open any puzzle table,
  - confirm `WASD`, jump, sprint, and mouse-look do nothing while the table UI is open,
  - confirm mouse cursor still works for dragging and dropping gates,
  - close the puzzle and confirm normal movement/look return immediately.
- Validate top-left labels are removed from the puzzle UI completely:
  - open the puzzle board and confirm `Question X/Y`, `F = ...`, and `Required: ...` no longer appear,
  - confirm submit, attempts, close button, and drag/drop still work,
  - if any of the three labels still appear, inspect the instantiated puzzle root for differently named TMP objects and hide them with the same force-hide pass.
- Validate Level6 opens intended prefab board:
  - press `E` on Table in Level6,
  - confirm displayed board matches `Assets/Prefabs/Table/Table/Level 6/Level6.prefab` visual layout,
  - confirm `Question X/5` panels show expected boxes (not blank background only).
- Validate drag/drop flow on Level6 board:
  - drag gates into Box1..Box7,
  - submit wrong and correct attempts,
  - verify attempts and slot reset behavior still works.
- Validate Level6 Table-only interaction fix:
  - in Level6, aim at object named `Table` and confirm `Press E to open Puzzle Table` appears,
  - confirm puzzle opens from `Table`,
  - confirm non-`Table` objects no longer open the puzzle,
  - check log: `[LevelManager] Level6 table-fix: enabled InteractiveTable on 'Table', disabled X non-Table InteractiveTable component(s).`
- Validate Level6 gate size after checkpoint/load:
  - restore/checkpoint in Level6,
  - confirm spawned gates appear at original prefab size (not SpawnPoint-scaled),
  - confirm this applies both on fresh load and restored layout load.
- Validate new Level6 anchor snap fallback:
  - enter Level6 and confirm player is snapped into playable area if initial spawn is off,
  - check for log: `[LevelManager] Level6 spawn-stabilizer: snapped player to SpawnPoint2 ...`.
- If still black/outside after this patch, capture 5-10 console lines beginning with `[LevelManager]` during Level6 load for next minimal targeted change.
- Validate Level6 entry after simplest override: Continue Game into Level6 should use scene spawn every time (no saved-position restore for Level6).
- In Console, confirm log appears once on load:
  `[LevelManager] OnSceneLoaded: Skipping saved-position restore for Level6; using scene spawn.`
- Confirm black/outside camera symptom no longer appears on first load and after one death/reload cycle.
- Validate Level6 entry from Continue Game with stale cloud save data: if saved position is invalid, player should stay at scene spawn (no outside camera load).
- Validate Level6 entry from valid mid-level save still restores correctly (guard should not block normal saves).
- Aim at the Table in Level6 and confirm `Press E to open Puzzle Table` prompt appears.
- Open puzzle and verify Q1-Q5 mappings match: Q1/Q3/Q4/Q5 = NOT NOT OR OR OR AND AND, Q2 = NOT NOT AND AND AND OR OR.
- Verify 7-slot questions allow collecting all 7 gates without inventory-cap blocks.
- If 7-cap still not showing, check if LevelManager reports correct level 6 before first gate collect.
- Check Levels 1-4 (reported 5-cap on 6-box questions): with the scene-name fallback fix from previous session, retest Level2/Level4 6-box scenarios.
- Future: check Levels 7-8 once user provides their box mappings.

## Immediate validation (Level5 Table + 6-slot inventory checks)
- In Level5, aim at the single `Table` object and confirm prompt shows `Press E to open Puzzle Table`.
- Press `E` and confirm puzzle UI opens with drag/drop behavior matching Levels 1-4.
- Run through Level5 Q1-Q5 and verify answer keys match latest mapping update.
- Validate 6-slot questions can be completed without inventory-cap blocks (no forced 5-slot cap).
- Recheck Levels 2 and 4 reports: when a 6-box question appears, confirm inventory capacity rises to 6 and collection is allowed.
- If any level still shows 5-cap on a 6-box question, log active scene name and current capacity in runtime for smallest targeted follow-up.

## Immediate workflow (Level5/Level6 box mapping input)
- User will provide per-question gate mapping in order (`Box1`, `Box2`, `Box3`, ...).
- Apply mappings exactly as provided (no reinterpretation) for the target question (`Q1`, `Q2`, ...).
- Use smallest possible edits in existing puzzle/table data only; avoid structural scene/UI refactors.
- After each mapping update:
  - run compile/error check on changed scripts/files,
  - verify mapping references exist,
  - report back exactly what was changed.
- If a provided mapping cannot be applied directly (missing slot/question), stop and ask for the smallest clarifying detail.

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

