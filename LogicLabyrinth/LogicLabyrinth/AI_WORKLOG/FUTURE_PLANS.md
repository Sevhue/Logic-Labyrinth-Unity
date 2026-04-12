# Future Plans

## Immediate validation (profile reset regression)
- Log in with the account that previously reverted profile data.
- Open Account Profile and confirm current values are correct.
- Make a small edit (for example Name suffix), press `SAVE`, and return to main menu.
- Fully stop play mode, start again, and log in through the same route used before (including fallback scenario if Firebase Auth fails).
- Reopen Account Profile and confirm values remain the latest saved values (no reset to older snapshot).
- If reset still appears, capture `AccountManager` logs around:
  - `Login fallback succeeded via DB password hash verification.`
  - `Restored local session snapshot.`
  - any `Cloud save skipped (offline/local mode)` warnings.

## Immediate validation (Level7 -> Level8 progression hotfix)
- Open `Level7` and solve the table question correctly.
- Confirm puzzle-complete flow triggers without requiring additional door-trigger interaction.
- Confirm scene transitions to `Level8`.
- Confirm player is controllable after load and no stuck fade/black overlay remains.
- If transition still fails, capture 10 console lines containing `[InteractiveTable]`, `[SuccessDoor]`, or `[LevelManager]`.

## Immediate validation (Level8 fall transition LEVEL 9 text)
- Open `Level8` and complete the puzzle successfully.
- Confirm door opens and player falls as intended (no-floor behavior unchanged).
- During the fall/transition overlay, confirm `LEVEL 9` text is visible.
- Confirm no stuck black screen during/after overlay.
- If `LEVEL 9` text is missing, capture 10 console lines containing `[SuccessDoor]`.

## Immediate validation (Level1 table dialogue overlap)
- Open `Level1`, interact with the puzzle table, and wait for post-dialogue board text.
- Confirm only one dialogue sentence is visible at a time (no doubled/stacked letters).
- Close and reopen the puzzle table once and confirm overlap does not return.
- If overlap still appears, capture 10 console lines containing `[Cutscene]`.

## Immediate validation (Level1 overlap 2nd-pass global cleanup)
- Restart play mode (fresh session), enter `Level1`, and open the table once.
- Confirm there is exactly one `PostDialogueBoardExtension` visual block (single text render).
- Open/close the table rapidly 2-3 times and confirm text remains single.
- If overlap still appears, include screenshot + 10 console lines with `[Cutscene]` and count of `PostDialogueBoardExtension` objects.

## Immediate validation (Level1 simplest single-label fix)
- Restart play mode and open Level1 puzzle table.
- Confirm first sentence appears, then is replaced by second sentence in the same location.
- Confirm there is no stacked/doubled text at any point.

## Immediate validation (cursor relock after X close)
- Open puzzle table, click `X` to close, and confirm mouse-look works immediately.
- Repeat for truth-table/other closeable overlays that unlock cursor.
- Confirm `TAB` toggle still works normally for manual cursor show/hide.
- If issue persists, capture 10 lines with `[MotionDebug] Cursor changed` from console.

## Immediate validation (cursor relock after X close - 2nd pass)
- Open puzzle table, click `X`, and confirm debug no longer reports `lock=None, visible=True` while gameplay is active.
- Confirm mouse-look starts immediately without any TAB press.
- Repeat open/close cycle 3 times and confirm behavior is consistent.

## Immediate validation (new Google profile Name caret)
- Create/login with a new Google account that triggers `COMPLETE THY PROFILE`.
- Confirm Name field is focused automatically and blinking `|` is visible without clicking.
- Confirm typing works immediately and existing prefilled name keeps caret at the end.

## Immediate validation (new Google profile Name caret - 2nd pass)
- Reopen the same popup in a fresh run and confirm blinking `|` is now visible in Name before any click.
- Confirm dropdowns still open normally after the popup-created EventSystem safeguard.

## Immediate validation (new Google profile Name caret - visual fallback)
- Reopen the popup and confirm a visible blinking `|` now appears directly after the current Name text.
- Type and backspace a few characters and confirm the blinking `|` follows the end of the text.

## Immediate validation (store offline fallback)
- Open pause menu store and click `Buy` for at least one item.
- With local Maya backend stopped/unreachable, confirm checkout enters local sandbox confirmation flow instead of hard-failing on backend error.
- Confirm status message indicates backend unavailable/local sandbox mode.
- Confirm `Verify Now` or auto-confirm path grants the purchased item and refreshes inventory UI.
- If backend is reachable with valid payload, confirm hosted checkout still opens normally.

## Immediate validation (death-drop gate size preservation)
- Pick up at least one visibly non-default-sized gate of each type used in level (`AND`/`OR`/`NOT`).
- Press `Q` once to confirm regular drop still keeps collected size.
- Pick those gates back up, then die while carrying them.
- From death camera/overlay perspective, confirm dropped gates keep the same collected size (no revert to prefab default scale).
- Respawn and re-collect one of the death-dropped gates, then drop again to confirm scale remains consistent across the full cycle.

## Immediate validation (adrenaline consume slot compaction)
- Ensure at least two items are visible in hotbar with `ADR` in an earlier slot and another item in a later slot.
- Consume one adrenaline charge using the normal flow (`select ADR` + `F`).
- If adrenaline is exhausted/removed, confirm remaining items shift left (example: old slot 2 becomes slot 1).
- Confirm no visual stale slot remains between filled slots.
- Confirm subsequent pickups/drops still rebuild hotbar correctly.

## Immediate validation (puzzle success consumes used gates)
- Enter a puzzle level with known starting gate counts (record AND/OR/NOT before opening table).
- Place a specific combination in slots (example: 2x AND, 1x OR, 1x NOT) and submit a correct answer.
- After success and puzzle close, confirm inventory counts decreased by exactly the placed combination.
- Reopen/continue gameplay and confirm no extra gate deduction occurs after the first successful submit.
- Smoke-check wrong attempt path: submit wrong once and confirm returned gates are still not consumed until a successful solve.

## Immediate validation (Level3 door interaction target)
- In `Level3`, look toward the exit-door area where both `Design` and `Success_Door` can be in range.
- Press `E` and confirm interaction resolves to `Success_Door` (not the `Design` tutorial door).
- Confirm prompt remains `Press E to open` near `Success_Door` and does not prefer `Design` when both are candidates.
- Smoke-check `Level1` tutorial flow still works: tutorial door can still be opened with `E` in its normal sequence.

## Immediate validation (Level1-8 progression behavior)
- Level1-4:
  - solve puzzle, collect success key, interact with `Door_Success` / `Success_Door` and confirm door unlock/open requires key.
  - verify locked message appears before key, and open succeeds after key pickup.
- Level5-6:
  - solve puzzle and confirm no key pickup is required.
  - confirm puzzle-complete flow appears and level auto-loads to next level.
- Level4/6/7/8 doorway progression:
  - after entering opened success doorway, confirm transition text shows target level and loads corresponding next level.
  - verify sequence mapping remains correct (4->5, 6->7, 7->8, 8->9 if available in build settings).
- Direct scene play sanity:
  - start Play directly in Level5 or Level6 and confirm solve -> auto-next uses correct current scene level (not Level1 fallback).

## Immediate validation (Level7/8 timing + Chapter3 target)
- Level7:
  - solve truth table, trigger success door open, and confirm transition starts automatically after ~3 seconds (without requiring doorway re-entry).
  - confirm destination is `Level8`.
- Level8:
  - solve truth table, trigger success door open, and confirm transition starts automatically after ~3 seconds.
  - confirm destination scene is `Chapter3` (not `Level9`).
- Verify fade/overlay flow still clears in destination scenes (no stuck black screen).

## Immediate validation (Q-drop gate scale)
- Pick up one gate of each type (`AND`, `OR`, `NOT`) in-level.
- Press `Q`, drop each type from inventory, and confirm dropped mesh size matches the picked-up gate size.
- For each type, pick up dropped gate again and re-drop once more to verify scale stays consistent across repeated cycles.
- Smoke-check that drop collision delay and pickup interactions still work normally after scale application.

## Immediate validation (Account Profile stats panel)
- Open Account Profile and confirm `Puzzle Solved` equals completed puzzle count (not stuck at default `0` unless no puzzles solved).
- Confirm `Total Played` shows formatted recorded time (not `--:--` after completing timed levels).
- Open the best-campaign dropdown and confirm it lists recorded level best times (example: `Level 2   1:48`).
- Confirm dropdown gracefully shows `--:--` when no best times exist.
- Trigger `RefreshPlayerDataFromFirebase` path (load/refresh flow), then reopen Account Profile and confirm stats still persist correctly.
- Confirm right-side multiline labels are replaced in-place (no remaining prefab placeholders like `--.--` or `--:--` when data exists).
- Confirm dropdown list always shows `Level 2` to `Level 9` in order.
- Confirm levels without records show `--:--`, and recorded levels update live after save/refresh.
- Confirm dropdown is visible under the stats board every time Account Profile opens (no blank/missing control).
- Confirm dropdown visual style matches medieval panel (dark brown background, gold text, highlighted selected row), not default white UI style.
- Confirm dropdown control is directly below `Best Campaign` and width matches (or is smaller than) the Best Campaign stat block.
- Confirm dropdown no longer stretches across the panel and remains compact after reopening Account Profile.
- Confirm closed dropdown height is compact (~34px visual) and text appears smaller than stat labels.
- Confirm no duplicate white `Best Campaign` value text appears behind/under the dropdown.
- Compile guard: ensure no CS7036 remains for `ConfigureCampaignDropdownLayout` call sites.

## Immediate validation (Options volume adjuster visibility)
- Open gameplay, press `ESC`, then open `Settings`.
- Confirm two slider adjusters (`Music`, `SFX`) are visible directly under `VOLUME`.
- Drag `Music` slider to `0` and back up; confirm music mutes/unmutes live.
- Drag `SFX` slider to `0` and back up; confirm click/footstep SFX level follows slider.
- Close and reopen Settings 3-5 times; confirm sliders consistently render and remain interactive.

## Immediate validation (slider handle + crackle fix)
- Confirm the `|` slider handle appears visibly smaller than before.
- Drag SFX slider down/up quickly and confirm no crackly/harsh retrigger noise appears.
- Drag Music slider down/up and confirm smooth level change without repeated restart artifacts.

## Immediate validation (lower slider position + saved volume across levels)
- Open `ESC -> Settings` and confirm both `Music` and `SFX` sliders are slightly lower than before.
- Set `Music` and `SFX` to custom values (example: 0.35 and 0.62), close settings, reopen, and confirm values remain.
- Stop play mode, start again, open settings, and confirm values are still the same.
- Change level/scene, open settings again, and confirm saved values and slider positions are still consistent.

## Immediate validation (custom Box art visibility in puzzle runtime)
- Open Level5 table puzzle and confirm Box images keep custom brown art instead of gray `?` placeholders.
- Verify this also works in other level puzzle prefabs that use custom Box sprites.
- Confirm drag/drop still works and placed gate labels still appear when slot is filled.

## Immediate validation (no gray while drag-hover)
- Drag a gate over each empty custom box and confirm box art stays brown/gold (no gray tint while pointer is over slot).
- Move pointer out and back in several times to confirm color remains stable.

## Immediate validation (Level6 Table + 7-slot inventory checks)
- Validate Level7 answer-key submit flow:
  - open Level7, answer selected Q with an incorrect value and press SUBMIT,
  - confirm centered red `WRONG!` appears and attempts decrement by 1,
  - answer selected Q with all correct values and press SUBMIT,
  - confirm `CORRECT!` appears,
  - confirm TruthDoor rotates open,
  - confirm table does not reopen after solve,
  - confirm mismatch/fill guards: `Fill all boxes!` or `Box count mismatch.` when applicable.
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

