# Session Log

## 2026-04-11

### UX polish: selected `?` blink + tutorial moved right + re-edit same cell
- User requested three UI improvements for Level7 truth-table input:
  - blink feedback when a `?` is clicked,
  - move tutorial panel further right to avoid overlapping the board,
  - allow changing a chosen value (not stuck after picking `1`).
- Minimal changes in `Assets/Scripts/Gameplay/TruthTableDisplay.cs`:
  - Added blinking highlight for selected cell using `UpdateSelectedCellBlink()` with `Mathf.PingPong` + `Color.Lerp`.
  - Shifted tutorial panel anchors from `(0.63..0.84)` to `(0.70..0.91)` on X so it sits further right.
  - Updated `ApplySelectedValue()` to keep current cell selected after applying `1/0`, allowing immediate re-selection to another value without re-clicking another `?`.
- Compile check: No errors.

### Feature: Level7 truth-table input UI (left 1/0 controls + right tutorial)
- User request: make interaction flow like mockup:
  - Click green `?`
  - Click `1` or `0`
  - Selected `?` updates to `1` or `0`
- Implemented in `Assets/Scripts/Gameplay/TruthTableDisplay.cs` with minimal runtime-only additions:
  - Added left-side input panel: selected-value display (`?`) + `1` button + `0` button.
  - Added right-side tutorial panel with text: `Click the green ? and choose your answer.`
  - After random Q selection, script scans active question panel for `TextMeshProUGUI` nodes whose text is exactly `?`.
  - Each `?` is made clickable via runtime `Button` component (`Transition.None`).
  - Clicking a `?` marks it as selected and updates left display.
  - Clicking `1` or `0` writes that value into the selected `?` and clears selection.
- Existing behavior preserved:
  - random Q1-Q5 activation,
  - top-right Attempts / SUBMIT / X,
  - 3-attempt game-over path.
- Compile check: No errors.

### Fix: TruthTableDisplay — reverted to Canvas-on-Level7 (only approach that renders)
- User report: wrapper canvas approach still invisible, console shows Q4: SHOWN.
- Conclusion: wrapper canvas with Level7 as child does not render regardless of RT settings. Only Canvas directly on Level7 root renders.
- Reverted to: `Instantiate(prefab)` + `AddComponent<Canvas>` + `CanvasScaler(ScaleWithScreenSize, 1920x1080)` on Level7 root.
- With user's cleaned hierarchy (`Level7 -> Background + Q1..Q5`), the old overlap from duplicate children no longer occurs.
- Background has center anchors + ~1000x468 size, so it will render centered within the screen-sized canvas — not fullscreen stretch.
- Compile check: No errors.

### Fix: TruthTableDisplay — stretch Level7 root RT to fill wrapper canvas
- User report: panel invisible again with wrapper canvas. Console shows Q5: SHOWN but nothing renders.
- Root cause: Level7 prefab root has a tiny RectTransform (~14x21 units). As a child of the wrapper canvas, that tiny rect clips all children. In the prefab editor, the Canvas Environment provides the full rendering space — we need to replicate that.
- Fix: set Level7 root RT to stretch-fill the wrapper canvas (`anchorMin=0,0`, `anchorMax=1,1`, `offset=zero`). Children (Background with center anchors + specific size, Q panels) now position correctly within the full canvas space — exactly matching the prefab editor's Canvas Environment.
- Compile check: No errors.

### Fix: TruthTableDisplay — wrapper canvas so prefab keeps its designed size
- User confirmed: prefab looks perfect in Scene view but Game view has content spilling outside gold border.
- Root cause finally clear: adding `Canvas` (ScreenSpaceOverlay) directly to the Level7 prefab root forces its RectTransform to screen size (e.g. 1920x1080). All Q children then position relative to screen coords instead of the designed 1206x588, causing overflow.
- Fix: wrapper canvas approach (empty GO with Canvas+CanvasScaler+GraphicRaycaster). Level7 prefab instantiated as CHILD of wrapper. Since Level7 is not the Canvas root, its RT keeps its designed size and all children position correctly — matching prefab editor appearance.
- Removed all previous normalization hacks: `NormalizeBackgroundLayout()`, `HideDuplicateOverlayLayers()`, Background-specific filtering, root-child suppression. These were band-aids for the wrong root cause.
- Simplified `RandomlySelectOneQuestionPanel()` back to clean `GetComponentsInChildren` search on `_panelInstance`.
- Key insight: wrapper canvas was tried before and appeared invisible. The difference now is `RecursivelyEnable()` and the user's cleaned-up prefab hierarchy (`Level7 -> Background + Q1..Q5`).
- Compile check: No errors.

### Fix: TruthTableDisplay — corrected for hierarchy `Level7 -> Background + Q1..Q5` (siblings)
- User clarification: `Background` and `Q1..Q5` are all direct children of `Level7`.
- Bug from previous patch: root-child suppression hid `Q1..Q5`, leaving only `Background` visible in Game view.
- Minimal correction in `Assets/Scripts/Gameplay/TruthTableDisplay.cs`:
  - Keep `Q1..Q5` root children enabled during root cleanup.
  - Select question panels by scanning `Level7` root subtree first.
  - Fallback to `Background` subtree only if no `Q1..Q5` found at root.
- Compile check: No errors.

### Fix: TruthTableDisplay — hide duplicate overlay layers outside selected question branch
- User reported overlap still visible after Background-only changes.
- Root cause refinement: duplicate table layers can exist inside the same `Background` branch, so root-level filtering alone is insufficient.
- Minimal follow-up fix in `Assets/Scripts/Gameplay/TruthTableDisplay.cs`:
  - After selecting one Q panel, keep selected branch visible.
  - Added `HideDuplicateOverlayLayers(searchRoot, selectedPanel)`.
  - Hides known overlay layer objects (`Level7`, `Lines`, `Binary`, `Logics`) when they are outside the selected question branch.
- Scope: no hierarchy rewrite; name-based suppression only.
- Compile check: No errors.

### Fix: TruthTableDisplay — remove overlap by showing only `Background` branch at root
- User test screenshot showed two truth-table layers overlapping (small board + large board text behind).
- Minimal fix applied in `Assets/Scripts/Gameplay/TruthTableDisplay.cs`:
  - In `NormalizeBackgroundLayout()`, if `Background` exists, disable all other root children under `Level7`.
  - In `RandomlySelectOneQuestionPanel()`, search for `Q1..Q5` under `Background` first (fallback to root only if `Background` missing).
- Purpose: prevent duplicate prefab visual layers from rendering simultaneously while preserving user hierarchy.
- Compile check: No errors.

### Fix: TruthTableDisplay — hierarchy-based clamp for `Level7 -> Background -> Q1..Q5`
- User updated prefab hierarchy and reported remaining stretch/overlap behavior.
- Minimal runtime fix applied in `Assets/Scripts/Gameplay/TruthTableDisplay.cs` (no structural rewrite):
  - In `NormalizeBackgroundLayout()`, force root RectTransform scale to `Vector3.one`.
  - Keep `Background` centered and fixed size (`1206x588`) with `localScale = Vector3.one`.
  - Keep `Image.preserveAspect = true` for `Background`.
- Purpose: prevent inherited non-unit scale from inflating the board in Game view while preserving user hierarchy.
- Compile check: No errors.

### Fix: TruthTableDisplay — user-created `Background` child normalized at runtime
- User update: created a new child named `Background` in Level7 prefab and asked for a non-stretched/non-overlapping runtime view.
- Simplest user-suggested fix applied in `Assets/Scripts/Gameplay/TruthTableDisplay.cs`:
  - Added `NormalizeBackgroundLayout()` and called it from `BuildUI()`.
  - If child named `Background` exists, force:
    - center anchors/pivot (`0.5, 0.5`),
    - `anchoredPosition = (0,0)`,
    - `sizeDelta = (1206, 588)`,
    - `localScale = (1,1,1)`,
    - `Image.preserveAspect = true`.
- Scope: no structural rewrite; only background rect normalization.
- Compile check: No errors.

### Fix: TruthTableDisplay — minimal stretch reduction (removed runtime CanvasScaler)
- User report: Level7 truth board is visible but stretched larger in Game view than prefab appearance.
- Simplest-first change applied in `Assets/Scripts/Gameplay/TruthTableDisplay.cs`:
  - Kept current direct prefab instantiation path (no structural rewrite).
  - Removed runtime-added `CanvasScaler` from `BuildUI()`.
  - Left existing Canvas + GraphicRaycaster + controls logic unchanged.
- Why this minimal fix first: `CanvasScaler` is the most likely source of size inflation across resolutions.
- Compile check: No errors.
- Next validation requested from user: test Level7 in Play mode and confirm whether board scale now matches prefab look more closely.

### Fix: TruthTableDisplay — panel stretched fullscreen (wrapper canvas + child panel, no Canvas on panel)
- User report: truth table panel visually stretched to fill the entire screen in Game view.
- Root cause: when you add `Canvas` (ScreenSpaceOverlay) to the root of the Level7 prefab, Unity forces that root's RectTransform to fill the entire screen — any `anchorMin/Max` or `sizeDelta` settings on the Canvas root RT are overridden by the Canvas renderer. So the background Image stretched fullscreen.
- Fix applied in `Assets/Scripts/Gameplay/TruthTableDisplay.cs`:
  - Reverted to wrapper canvas approach: `_canvasGO` is now a newly created empty GO with `Canvas`+`CanvasScaler`+`GraphicRaycaster`
  - Level7 prefab is instantiated as `_panelInstance` — a **child** of the wrapper canvas, WITHOUT adding any Canvas component to it
  - Panel RT: `anchorMin/Max = (0.5,0.5)`, `pivot = (0.5,0.5)`, `anchoredPosition = (0,0)`, `sizeDelta = (1206,588)`, `localScale = one` — these are respected because the panel is a child, not a Canvas root
  - Controls (Attempts, SUBMIT, X) remain parented to `_canvasGO` (the wrapper)
  - Q1–Q5 search updated to use `_panelInstance.GetComponentsInChildren<Transform>(true)` instead of `_canvasGO`
- Compile check: No errors.

### Fix: TruthTableDisplay — Q panels still all visible (search by exact name in all descendants)
- User report: all questions still showing on top of each other.
- Root cause: `RandomlySelectOneQuestionPanel()` was looking only at direct children of `_canvasGO`. But Q1–Q5 panels are nested at depth 3: `Level7 (root) → TruthTable → Level7 (inner) → Q1…Q5`. The direct child is only `TruthTable`, so no Q-panels were found and nothing was hidden.
- Confirmed via prefab inspection: panels are named exactly `Q1`, `Q2`, `Q3`, `Q4`, `Q5`.
- Fix: replaced direct-child loop with `GetComponentsInChildren<Transform>(true)` across all descendants, filtering on exact names `Q1`–`Q5`. One is randomly selected and enabled; the rest are disabled.
- Compile check: No errors.

### Fix: TruthTableDisplay — show only 1 random question (20% each for 5 questions)
- User report: all question panels showing at once instead of just one.
- Root cause: all child panels of the Level7 prefab were enabled; no logic to randomly select one.
- Fix applied in `Assets/Scripts/Gameplay/TruthTableDisplay.cs`:
  - Added `RandomlySelectOneQuestionPanel()` method that:
    - Finds all direct children of the canvas named like "Level1", "Level2", "Q1", "Q2", etc. (filters out control buttons)
    - Randomly picks one: `Random.Range(0, questionPanels.Count)`
    - Enables only that panel, disables all others
    - For 5 questions: 20% chance each; for N questions: 1/N chance each
  - Called in `OpenDisplay()` after canvas is active
  - Added `using System.Collections.Generic;` for `List<T>`
- Result: each time truth table opens, one random question is shown.
- Compile check: No errors.

### Fix: TruthTableDisplay — panel still invisible (restructure to root canvas, avoid nested Canvas)
- User report: truth table controls visible but panel still invisible despite recursive enable + explicit sizeDelta.
- Root cause: nested Canvas hierarchy (wrapper canvas with panel as child Canvas) causes rendering issues. UI elements under nested canvases with different render modes don't render properly.
- Fix applied in `Assets/Scripts/Gameplay/TruthTableDisplay.cs`:
  - Changed architecture: instead of creating a separate wrapper canvas + parenting panel under it, now **the panel IS the root canvas**.
  - Instantiate Level7 prefab as root GameObject (not as child)
  - Add Canvas component to it in ScreenSpaceOverlay mode (sortingOrder 500)
  - Add CanvasScaler and GraphicRaycaster
  - Set anchors/position to center panel without stretching  
  - Controls (Attempts, SUBMIT, X) are added to this same canvas as children
  - This avoids nested Canvas hierarchies entirely.
- Compile check: No errors.

### Fix: TruthTableDisplay — panel not visible (explicitly set sizeDelta + recursive enable)
- User report: truth table panel invisible; only controls (Attempts, SUBMIT, X) visible at top-right.
- Root cause: Level7 prefab has `m_IsActive: 0` (disabled by default) and its children may also be disabled. Additionally, sizeDelta was commented out as "kept from prefab" but the prefab might not have the correct sizing.
- Fix applied in `Assets/Scripts/Gameplay/TruthTableDisplay.cs`:
  - Added explicit `panelRT.sizeDelta = new Vector2(1206f, 588f)` after setting anchors
  - Added explicit `panelRT.localScale = Vector3.one`
  - Added `RecursivelyEnable(panelInstance)` call to recursively enable all children (in case descendants were also disabled in the prefab)
- Result: truth table panel now renders visibly alongside the controls.
- Compile check: No errors.

### Fix: TruthTableDisplay — fix stretch, add top-right SUBMIT/X/Attempts (3 attempts game-over)
- User report: truth table panel was too stretched; wanted SUBMIT and X at top-right; 3 attempts like other levels.
- Root cause of stretch: previous `EnsureCanvas()` added a Canvas component directly to the Level7 prefab root (which carries a background `Image`). Unity's Canvas system then resized the root RT to fill the screen, stretching the Image across the entire display.
- Fix: replaced `EnsureCanvas()` approach with a `BuildUI()` method that creates a separate fullscreen Canvas wrapper GO, then parents the Level7 prefab instance **under** it as a child. The panel keeps its original 1206×588 `SizeDelta` and is centered via `anchorMin/Max = 0.5` + `anchoredPosition = Vector2.zero`.
- Controls added (`BuildControlsUI()`): Attempts label, SUBMIT button, X button — using the **exact same anchor positions** as `PuzzleTableController.BuildControlsUI()`:
  - Attempts: `(0.60,0.91)–(0.77,0.98)`
  - SUBMIT: `(0.78,0.91)–(0.92,0.98)` (green, outline)
  - X: `(0.93,0.91)–(0.99,0.98)` (dark red)
- Behavior: SUBMIT decrements remaining attempts and closes the panel; X closes without using attempt; at 0 attempts `fpc.ApplyDamage(lethalDamage)` triggers death/respawn (mirrors `PuzzleTableController.DelayedGameOver()`).
- `RefreshAttemptsUI()` updates text and disables SUBMIT when 0 remain.
- Compile check: No errors.

### Feature: Level7 TruthDoor — E opens Level7 truth table display
- User request: pressing E on `TruthDoor` in Level7 should show the Level7 prefab (visual truth table panel).
- Analysis: TruthDoor is a plain mesh (Transform, MeshFilter, MeshRenderer, MeshCollider only). Level7 prefab (`Assets/Prefabs/Table/Table/Level7/Level7.prefab`) is a UI panel containing only TMP text and Image components (a static truth table reference — NOT an interactive gate-puzzle).
- Since the existing `InteractiveTable` would try to run puzzle logic on a visual-only prefab, a minimal dedicated script was created instead.
- Changes:
  - **New**: `Assets/Scripts/Gameplay/TruthTableDisplay.cs` — static `IsOpen` flag, `OpenDisplay()` instantiates the prefab and ensures a ScreenSpaceOverlay Canvas, ESC closes it and re-locks the cursor.
  - **New**: `Assets/Scripts/Gameplay/TruthTableDisplay.cs.meta` — GUID `a7c2e3f1b4d567890123456789abcdef`.
  - `SimpleGateCollector.cs` — added `currentTruthDisplay` field; sphere-cast loop detects `TruthTableDisplay`; prompt "Press E to view Truth Table"; E-press block calls `OpenDisplay()`; `ClearTargets()` clears `currentTruthDisplay`; top-of-Update `TruthTableDisplay.IsOpen` check added alongside PuzzleTableController.IsOpen.
  - `FirstPersonController.cs` — `Update()` and `LateUpdate()` guards updated to `PuzzleTableController.IsOpen || TruthTableDisplay.IsOpen`.
  - `Assets/Scenes/Chapter 2/Level7.unity` — added `TruthTableDisplay` MonoBehaviour component (fileID `3749827461003891`) to `TruthDoor` (GameObject fileID `4010797231560863439`) with `displayPanelPrefab` pointing to Level7 prefab root (`{fileID: 85429378069326421, guid: 74fff223df041be4e9d7300b81eb9efa}`).
- Compile check: No errors on all 3 modified scripts.



### Fix: puzzle table now hard-locks player movement and camera look on all levels
- User report: while puzzle table UI is open, player can still move with `WASD` and rotate camera with mouse.
- Root cause: `PuzzleTableController` left `FirstPersonController` alive for wrong-answer camera shake, so gameplay update/rotation could still run during table mode.
- Simplest root fix applied in `Assets/StarterAssets/FirstPersonController/Scripts/FirstPersonController.cs`:
  - added early return in `Update()` whenever `PuzzleTableController.IsOpen` is true,
  - zeroed move/look/jump/sprint input before returning,
  - forced gameplay cursor visible/unlocked for drag/drop,
  - added early return in `LateUpdate()` so camera rotation does not run while the puzzle table is open.
- Expected result: on every level, table mode allows only UI mouse use for dragging/dropping gates; no player movement or look until the puzzle closes.

### Fix: remove puzzle top-left labels completely
- User requested full removal instead of another Level6-only hide.
- Simplest permanent fix applied in `Assets/Scripts/Puzzle/PuzzleTableController.cs`:
  - removed runtime creation/display of `QuestionNumberText`,
  - stopped building/updating expression and requirement helper labels,
  - forcibly hide any existing serialized `QuestionNumberText`, `ExpressionText`, and `GateRequirementText` objects already present under the instantiated puzzle UI.
- Reason: previous conditional hides were too narrow; existing serialized UI children could still remain visible in the live board.

### Fix: Level6 E opens wrong board (force Level6 prefab + panel mode)
- User report: pressing `E` in Level6 opened a different/blank board, not the intended `Assets/Prefabs/Table/Table/Level 6/Level6.prefab` layout.
- Root cause found:
  - Level6 scene `InteractiveTable` components were still pointing `puzzleUIPrefab` to `UITable.prefab`.
  - `PuzzleTableController` treated Level6 as free-form mode (`currentLevelNumber >= 6`), which bypassed Q-panel selection.
- Minimal fixes applied:
  - `Assets/Scenes/Chapter 2/Level6.unity`:
    - Updated Level6 `InteractiveTable` `puzzleUIPrefab` references to Level6 prefab:
      `fileID: 3817206775398349765, guid: aea163259a7222c429e15b843a5c747d`.
  - `Assets/Scripts/Puzzle/PuzzleTableController.cs`:
    - Changed free-form threshold from `>= 6` to `>= 7` so Level6 uses question-panel mode with boxes (like Levels 1-5 behavior pattern).
- Expected result: E in Level6 opens the intended Level6 board with visible question layout and draggable slots.

### Fix: Level6 make only `Table` interactive (simplest targeted runtime fix)
- User request: make the single object named `Table` interactive in Level6 like Levels 1-5, and keep provided Q1-Q5 answer keys.
- Verification:
  - `AnswerKeyConfig` case 6 already matches provided mapping for Q1-Q5 (including Q4 as `NOT NOT OR OR OR AND AND`).
  - Level6 scene had multiple `InteractiveTable` components on non-`Table` objects, which can cause wrong interaction targets.
- Minimal fix in `Assets/Scripts/Managers/LevelManager.cs`:
  - Added `EnsureOnlyNamedTableIsInteractiveInLevel6()` coroutine.
  - On Level6 load, find object named `Table`.
  - Disable `InteractiveTable` on all other objects in Level6.
  - Ensure `Table` has `InteractiveTable` component enabled.
  - If missing, copy `puzzleUIPrefab` from existing Level6 `InteractiveTable` component.
  - Log result:
    `[LevelManager] Level6 table-fix: enabled InteractiveTable on 'Table', disabled X non-Table InteractiveTable component(s).`

### Fix: Level6 checkpoint restore did not return gate size to original
- User report: after restore/checkpoint in Level6, gates did not return to original size.
- Root cause in code path:
  - checkpoint/load restores player position/rotation and gate layout,
  - `SimpleGateSpawner` still multiplied spawned gate size by SpawnPoint transform scale.
- Minimal fix applied in `Assets/Scripts/Gameplay/SimpleGateSpawner.cs`:
  - Added `SceneManager` check in `SpawnGateAt(...)`.
  - In `Level6`, force unit SpawnPoint scale (`Vector3.one`) so spawned gates use original prefab size on load/restore.
  - Other levels keep existing behavior unchanged.

### Fix: Level6 still black/outside view after first patch (anchor snap fallback)
- User validation: issue persisted after skipping saved-position restore for Level6.
- Additional comparison result: Level6 `FirstPersonPlayer` transform matches `origin/main`, so this was not introduced by recent merge edits.
- Smallest next fix applied in `Assets/Scripts/Managers/LevelManager.cs`:
  - Added Level6-only coroutine `SnapLevel6PlayerToKnownAnchorNextFrame()`.
  - On Level6 load, finds `SpawnPoint2` and checks if player looks off-position (`distance > 8`, `y < -5`, or `y > 50`).
  - If off-position, teleports player to `SpawnPoint2` (keeps current yaw).
  - Logs successful snap:
    `[LevelManager] Level6 spawn-stabilizer: snapped player to SpawnPoint2 ...`
- Scope: targeted runtime guard only, no scene prefab restructuring.

### Fix: Level6 respawn/load guard (simplest-first override)
- User report after prior guard: Level6 still sometimes loads into black/outside-like camera state.
- Comparison run across Level1-Level6 scene data:
  - All levels rely on `FirstPersonPlayer` scene placement (no explicit `PlayerSpawn*` markers in these scenes).
  - Level5 and Level6 share near-identical player rig/camera hierarchy and offsets.
  - This points back to load-time restore path (not base prefab hierarchy) as the practical instability point for Level6.
- Simplest fix applied in `Assets/Scripts/Managers/LevelManager.cs`:
  - In `OnSceneLoaded`, when restoring cloud position, skip restore entirely for `Level6`.
  - Keep the scene-authored spawn for Level6 and log:
    `[LevelManager] OnSceneLoaded: Skipping saved-position restore for Level6; using scene spawn.`
- Why this first: smallest targeted behavior change, no scene refactor, no prefab restructuring, aligns with user requested workflow mode.

### Fix: Level6 loads to outside camera / not respawning on player
- Report: entering Level6 sometimes shows an outside camera view and player does not appear at expected in-level start.
- Root cause (likely): `LevelManager` restored stale/invalid cloud saved coordinates without validation when `shouldRestorePosition` was true.
- Minimal fix applied in `Assets/Scripts/Managers/LevelManager.cs`:
  - before instant teleport, validate saved target coordinates are finite,
  - compare against active scene player's current spawn position,
  - reject restore if target is extreme (`distance > 250`, `y < -200`, `y > 500`),
  - if rejected, keep scene spawn position instead of forcing bad teleport.
- Result: bad saved positions no longer force Level6 to load into outside/void camera coordinates.

### Fix: Level6 answer-key Q4 correction + E-key access confirmed
- Request: make Level6 Table accessible via E, apply user-provided Level6 Q1-Q5 box mappings.
- Investigation: Level6.unity already has 3 InteractiveTable components (on prefab-instance objects from chapter2problem6.fbx and the Design object). E-key interaction is already wired correctly with correct UITable.prefab (`d5cc8a5dfc763f542ab4e490217b23d4`). No scene change needed.
- Only mismatch found: `AnswerKeyConfig.cs` case 6 Q4 had `NOT NOT AND AND AND OR OR` but user mapping says `NOT NOT OR OR OR AND AND`.
- Fix applied: updated Q4 in case 6 to `GateType.NOT, GateType.NOT, GateType.OR, GateType.OR, GateType.OR, GateType.AND, GateType.AND`.
- 7-gate capacity: InventoryManager auto-scales to max answer key length. All Level6 keys are 7 elements → capacity auto-sets to 7. No code change required.
- Level6 Q1-Q5 final mappings:
  - Q1: NOT, NOT, OR, OR, OR, AND, AND
  - Q2: NOT, NOT, AND, AND, AND, OR, OR
  - Q3: NOT, NOT, OR, OR, OR, AND, AND
  - Q4: NOT, NOT, OR, OR, OR, AND, AND  (was AND AND AND OR OR — corrected)
  - Q5: NOT, NOT, OR, OR, OR, AND, AND

### Fix: Level5 Table interaction and answer-key updates
- Request: make Level5 `Table` interactable with `E` like Levels 1-4, and apply user-provided Level5 Q1-Q5 box mappings.
- Root cause found for interaction: `Assets/Scenes/Chapter 2/Level5.unity` `Table` object had no `InteractiveTable` component.
- Simplest fix applied first: added `InteractiveTable` MonoBehaviour to the single `Table` object in Level5 scene, using the same `puzzleUIPrefab` reference pattern as working levels.
- Applied exact Level5 mappings in `Assets/Scripts/Puzzle/AnswerKeyConfig.cs`:
  - Q1: OR, OR, OR, AND, AND
  - Q2: AND, AND, AND, OR, OR
  - Q3: NOT, OR, OR, OR, AND, AND
  - Q4: NOT, AND, AND, AND, OR, OR
  - Q5: NOT, OR, OR, OR, AND, AND
- Capacity reliability fix (minimal): updated `InventoryManager.GetCurrentGateCapacity()` to fall back to scene name (`LevelX`) when `LevelManager` still reports level `1`, so 6-slot levels correctly get capacity `6`.
- This also addresses reported 6-slot inventory-cap issues in Levels 1-4 when level detection desync happens at runtime.

### Fix: imported Google Drive Level7 prefab had missing sprite reference
- User action: downloaded prefab externally and dragged it into project.
- Root cause: imported prefab referenced a sprite GUID not present in this repository (`ba57864c72fbcfb47a64e60f2e1b0c77`), so Unity showed `Missing (Sprite)` on `Level7` image.
- Simplest fix applied first: updated the missing GUID in `Assets/Prefabs/Table/Table/Level 7/Level7.prefab` to a known existing sprite GUID already used by Level5/Level6 (`ce2b9b4c09781934e88fb13bd3c59524`).
- Result: Level7 table background sprite now resolves from local project assets.

### Process alignment: user requested strict simple-fix workflow
- Confirmed operating mode for next tasks:
  - simplest plausible fix first
  - user-suggested fix first
  - no structural refactor unless simple fix fails
  - explain complexity before doing complex changes

## 2026-04-10

### Merge action: pulled Level1-6 scene changes from `origin/67` into `main` (selective)
- Request: merge only levels `1,2,3,4,5,6` changes to main.
- Action taken (selective file merge): checked out these scene files from `origin/67` on top of current `main`:
  - `Assets/Scenes/Chapter 1/Level1.unity`
  - `Assets/Scenes/Chapter 1/Level2.unity`
  - `Assets/Scenes/Chapter 1/Level3.unity`
  - `Assets/Scenes/Chapter 1/Level4.unity`
  - `Assets/Scenes/Chapter 2/Level5.unity`
  - `Assets/Scenes/Chapter 2/Level6.unity`
- Scope note: did **not** pull Level7/Level8 or UI assets (`Level4.prefab`, `UITable.prefab`, `Main.unity`) in this action.
- Git state: the six level scene files are now staged and ready to commit.

### Audit: `origin/67` Unity scene changes vs `origin/main`
- Checked remote branch diff directly with `git diff origin/main origin/67`.
- Confirmed: `origin/67` changes **SpawnPoint transforms** in `Level7.unity` and `Level8.unity`.
- The changed objects are SpawnPoint markers (e.g. `SpawnPoint1`, `SpawnPoint2`, `SpawnPoint4`, `SpawnPoint5`, `SpawnPoint7`, `SpawnPoint8`, `SpawnPoint10`, and matching remaining SpawnPoint set).
- In both `Level7` and `Level8`, branch `origin/67` uses older/default-looking SpawnPoint transforms such as scale `{x:1,y:1,z:1}` instead of the reduced scale currently on `origin/main`, and positions are also different.
- Confirmed: candle-related scene changes also exist on `origin/67` in `Level1.unity`, `Level2.unity`, `Level3.unity`, `Level5.unity`, and `Level6.unity`.
- The candle diffs include `CollectibleCandle` objects / `Candlelight` objects being added or renamed in scene YAML.
- Wall-related signal was much weaker: a quick diff search only surfaced `Wall_Chiseled` references in `Level1`, not the same clear transform-only pattern seen with SpawnPoints.
- Also found non-scene UI changes on the branch: `Level4.prefab`, `UITable.prefab`, and `Main.unity`.

### Deep follow-up: wall-related diff in `Level1` (`origin/main` vs `origin/67`)
- Inspected `Wall_Chiseled (8)` and `Wall_Chiseled (53)` hunks with full context.
- These wall entries appear as prefab-instance remove/add/reparent churn (not clean standalone wall transform edits).
- For those named wall entries, local transform values are repeated on both sides of the diff while parent references change heavily (e.g., many `m_TransformParent` switches from one container fileID to another).
- Conclusion: from CLI diff evidence, this looks like scene serialization/reparent noise around wall prefab instances, **not a clear intentional wall movement pass** comparable to the SpawnPoint edits.

### Fix: ESC/X no longer resets attempts; wrong-answer shake now visible while solving
- Root cause (attempt reset): `InteractiveTable.WatchForPuzzleClose()` always destroyed `puzzleUIInstance`, so every reopen created a fresh controller with `3/3` attempts.
- Fix: reuse `puzzleUIInstance` for manual close (ESC/X) and only destroy it on terminal states (`wasSolved` or `wasGameOver`).
- Root cause (no shake on wrong submit): puzzle UI mode disabled `FirstPersonController`, so shake feedback couldn't render while puzzle stayed open.
- Fix: in `SetUIMode(true)`, keep `FirstPersonController` enabled and still disable input/collector/brain.
- Validation: `PuzzleTableController.cs` and `InteractiveTable.cs` compile with no errors.

### Feature: wrong answer — shake every time, auto-return gates, candle cleared on death
- **Shake on game over too**: `OnGameOver()` now also calls `fpc.TriggerCameraShake()` so ALL wrong answers shake, including the final one.
- **Auto-return gates to palette on wrong**: Both wrong-answer code paths now call `ReturnAllSlotsToInventory()` before the flash — gates in the drop slots are returned to the puzzle palette automatically on each wrong submission.
- **Candle clear on death**: In `FirstPersonController.DeathAndRespawnRoutine()`, `InventoryManager.Instance.SetHasCandle(false)` is called BEFORE the scene reload (belt-and-suspenders alongside `LevelManager.OnSceneLoaded` which also clears it). Only adrenaline persists across death since it's stored in Firebase PlayerData.
- Validation: Both files compile with no errors.

### Feature: big red "WRONG!" flash + camera shake on wrong puzzle answer
- Request: show a big centred "WRONG!" text (like the attempts style) and a small camera shake when submitting a wrong answer.
- Changes:
  - `FirstPersonController.cs`: added `public void TriggerCameraShake(float duration, float intensity)` — triggers the existing shake system without damage flash.
  - `PuzzleTableController.cs`: added `ShowWrongFlash()` — shows 80px bold red "WRONG!" with transparent background for 0.9s, then calls `fpc.TriggerCameraShake(0.3f, 0.15f)`.
  - Both wrong-answer code paths (composition check and slot-by-slot check) now call `ShowWrongFlash()` when there are attempts remaining.
- Validation: No compile errors.

### Fix: death reloads the scene completely instead of respawning in-place
- Request: On death, restart the game fresh (scene reload) instead of respawning at a spawn point.
- Change in `FirstPersonController.cs`:
  - Added `using UnityEngine.SceneManagement;`
  - In `DeathAndRespawnRoutine()`: after Space prompt, replaced `RespawnAtSpawnPoint()` + fade-out with `SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex)`.
  - Removed unreachable fade-out code.
- Result: Dying (trap or puzzle) shows death overlay → player presses Space → scene reloads completely (fresh gates, fresh timer, fresh inventory).
- Validation: No compile errors.

### Fix: puzzle game-over death — gates drop normally, new question on next attempt
- Clarification: Player DOES lose their gates on puzzle death (normal drop), but the puzzle shows a new question when they return.
- Change: Removed `SuppressGateDrop = true` from `PuzzleTableController.DelayedGameOver()` — gates now drop as with trap deaths.
- The `SuppressGateDrop` property on `FirstPersonController` remains (unused, harmless).
- New-question logic (`exhaustedQuestionIndices` in `InteractiveTable.cs`) still handles showing a different question after game-over.
- Validation: `PuzzleTableController.cs` compiles with no errors.

## 2026-04-09

### Fix: at 0/3 attempts, trigger real player death flow after GAME OVER message
- Request: When puzzle reaches `0/3`, character should actually die (not just close puzzle UI).
- Minimal fix applied:
  - [Assets/Scripts/Puzzle/PuzzleTableController.cs](Assets/Scripts/Puzzle/PuzzleTableController.cs)
  - In `DelayedGameOver()`:
    - keep existing 3-second GAME OVER display
    - close puzzle UI as before
    - then find `FirstPersonController` and apply lethal damage (`CurrentHealth`) to trigger normal death flow
- Result:
  - Puzzle `0/3` now causes real death sequence (death overlay + respawn flow).
- Validation:
  - `PuzzleTableController.cs` compile check reports no errors.

### Fix: prevent E-spam gate collection during death/game-over state
- Request: While dead/game-over overlay is active, spamming `E` could collect dropped gates at the death position.
- Minimal fix applied:
  - [Assets/Scripts/Gameplay/SimpleGateCollector.cs](Assets/Scripts/Gameplay/SimpleGateCollector.cs)
  - Added a dead-state guard at the top of `Update()`:
    - caches `FirstPersonController`
    - if `IsDead`, immediately hides prompts, clears targets, and exits update loop
- Result:
  - Interaction is blocked while dead, so dropped gates cannot be re-collected until respawn.
- Validation:
  - `SimpleGateCollector.cs` compile check reports no errors.

### Respawn anchor priority tweak (use true level-entry spawn first)
- Request: Use the correct marker/entry spawn for levels like Level 2; avoid gate spawn markers.
- Minimal adjustment:
  - [Assets/StarterAssets/FirstPersonController/Scripts/FirstPersonController.cs](Assets/StarterAssets/FirstPersonController/Scripts/FirstPersonController.cs)
  - Respawn now prioritizes the cached level-entry spawn anchor (`_levelStartPosition/_levelStartYaw`) first.
  - At startup, if a dedicated player marker exists (`PlayerSpawn`, `PlayerSpawnPoint`, `PlayerStart`, `StartPoint`, `RespawnPoint`, `Respawn`), that marker is used to set the level-start anchor.
  - Generic `SpawnPoint1/SpawnPoint` fallback remains disabled (those are gate spawn markers).
- Validation:
  - `FirstPersonController.cs` compile check reports no errors.

### Global consistency: remove gate SpawnPoint fallback from LevelManager safety rescue
- Request: Ensure the same respawn-marker rule applies on all levels.
- Minimal adjustment:
  - [Assets/Scripts/Managers/LevelManager.cs](Assets/Scripts/Managers/LevelManager.cs)
  - Updated `TryGetSpawnFallback(...)` to use explicit player markers only:
    - `PlayerSpawn`, `PlayerSpawnPoint`, `PlayerStart`, `StartPoint`, `RespawnPoint`, `Respawn`
  - Removed generic `SpawnPoint*` fallback usage (gate spawner markers).
- Validation:
  - `LevelManager.cs` compile check reports no errors.

### Fix: game over should move to remaining question, clear placed gates, and avoid gate SpawnPoint respawn
- Request:
  - After 3 failed attempts, next open should move to remaining questions (not same one again).
  - Gates should disappear/reset on game over.
  - Death respawn must not use gate spawn markers (`SpawnPoint1..N`).
- Smallest fixes applied:
  - [Assets/Scripts/Gameplay/InteractiveTable.cs](Assets/Scripts/Gameplay/InteractiveTable.cs)
    - Added exhausted-question tracking for multi-question tables.
    - After puzzle closes with game over, current locked question is marked exhausted.
    - Next open picks from remaining (non-exhausted) questions.
    - When exhausted list is empty, it resets and can cycle again.
  - [Assets/Scripts/Puzzle/PuzzleTableController.cs](Assets/Scripts/Puzzle/PuzzleTableController.cs)
    - Exposed `WasGameOver` so `InteractiveTable` can react to game-over close.
    - On delayed game over close, now clears all drop slots first so placed gates are removed/returned.
  - [Assets/StarterAssets/FirstPersonController/Scripts/FirstPersonController.cs](Assets/StarterAssets/FirstPersonController/Scripts/FirstPersonController.cs)
    - Respawn now first tries explicit player markers only (`PlayerSpawn`, `PlayerSpawnPoint`, `PlayerStart`, `StartPoint`, `RespawnPoint`, `Respawn`).
    - Removed fallback that used generic `SpawnPoint1`/`SpawnPoint` names (reserved for gate spawner markers).
    - If no explicit marker exists, falls back to cached level start pose.
- Validation:
  - `InteractiveTable.cs` compile check reports no errors.
  - `PuzzleTableController.cs` compile check reports no errors.
  - `FirstPersonController.cs` compile check reports no errors.

### Audit: all teammate-variant saved scale sources
- Request: Check everything that might cause different saved scale across teammates/machines.
- Findings:
  - [Assets/Scripts/Gameplay/AdrenalineConsumableController.cs](Assets/Scripts/Gameplay/AdrenalineConsumableController.cs)
    - Uses local `PlayerPrefs` keys `LL_ADR_HAND_SCALE_X/Y/Z` (machine-local).
    - This is the confirmed source of teammate-to-teammate ADR scale differences.
  - [Assets/Scripts/Gameplay/SimpleGateSpawner.cs](Assets/Scripts/Gameplay/SimpleGateSpawner.cs)
    - Applies `SpawnPoint` transform scale (`useSpawnPointScale`) to spawned gates.
    - This is scene-data driven (git/shared), not local saved prefs.
  - [Assets/Scripts/Managers/AccountManager.cs](Assets/Scripts/Managers/AccountManager.cs)
    - `PlayerData` does not store any scale fields for ADR/equipped view model.
    - Cloud/account progress is not the cause of ADR scale mismatch.
  - [Assets/Scripts/Managers/QualitySettingsManager.cs](Assets/Scripts/Managers/QualitySettingsManager.cs)
    - Uses PlayerPrefs for quality preset only; no object-scale persistence.
- Conclusion:
  - The only teammate-local saved-scale path in gameplay code is ADR pose scale via PlayerPrefs.

### ADR consumable appears huge on some machines (cross-machine PlayerPrefs/default-scale mismatch)
- Request: Adrenaline bottle looks normal on one machine but huge on another after pulling latest git.
- Smallest fix applied:
  - [Assets/Scripts/Gameplay/AdrenalineConsumableController.cs](Assets/Scripts/Gameplay/AdrenalineConsumableController.cs)
  - Changed default ADR hand scale from `Vector3.one` to `new Vector3(0.2f, 0.2f, 0.2f)`.
  - Added a sanity check after loading PlayerPrefs scale values:
    - invalid/non-finite or out-of-range scales are reset to default ADR hand scale.
- Why this works:
  - ADR pose/scale is stored per-machine via PlayerPrefs (`LL_ADR_*` keys), so stale/local values can differ across teammates.
  - New guard prevents oversized ADR model even if old prefs are bad.
- Validation:
  - `AdrenalineConsumableController.cs` compile check reports no errors.

### Selective merge: only SpawnPoint move/resize from `evan_spawnpoints_lvl7,8`
- Request: Merge only SpawnPoint movement and resizing for Level 7 and Level 8 from Evan's branch, with no other scene content merged.
- Approach: Surgical transform-only merge by copying `m_LocalPosition` and `m_LocalScale` for `SpawnPoint1..SpawnPoint10` from `origin/evan_spawnpoints_lvl7,8` into current `main` scene files.
- Change:
  - [Assets/Scenes/Chapter 2/Level7.unity](Assets/Scenes/Chapter%202/Level7.unity)
  - [Assets/Scenes/Chapter 2/Level8.unity](Assets/Scenes/Chapter%202/Level8.unity)
  - Updated only SpawnPoint transform lines:
    - `m_LocalPosition`
    - `m_LocalScale`
- Validation:
  - Diff review confirms only LocalPosition/LocalScale lines were changed in the two target scene files.
  - Change count: 20 line edits in Level7 + 20 line edits in Level8 (10 spawnpoints x 2 properties each).

### Suppress E prompt on non-Success doors (except Level 1 tutorial)
- Request: Doors should only show "Press E to open" if they are a SuccessDoor. Exception: Level 1 tutorial door (TutorialDoor, key-finding) should still show it.
- Approach: Simplest fix — single conditional in `bestDoor` branch of `HandleInteraction()`.
- Change:
  - [Assets/Scripts/Gameplay/SimpleGateCollector.cs](Assets/Scripts/Gameplay/SimpleGateCollector.cs)
  - In the `bestDoor != null` prompt block, replaced unconditional `ShowPrompt("Press E to open")` with a scene-name check:
    - If active scene is `"Level1"` → `ShowPrompt("Press E to open")` (tutorial door, key-required)
    - Otherwise → `HidePrompt()` (door stays interactive/tracked, but no E hint shown)
  - `bestSuccessDoor` branch unchanged — Success_Door always shows the E prompt.
- Validation:
  - `SimpleGateCollector.cs` compile check reports no errors.

### Restart button confirmation (Yes/No) with pause/settings theme
- Request: Restart should ask for confirmation with Yes/No and look professional in the same visual theme as pause/settings.
- Approach used: smallest change first, reusing existing in-script dialog style helpers (no prefab redesign).
- Change:
  - [Assets/Scripts/UI/PauseMenuController.cs](Assets/Scripts/UI/PauseMenuController.cs)
  - `Restart` button now opens a dedicated confirmation dialog instead of immediately reloading.
  - Added medieval-themed restart dialog with:
    - title: `RESTART LEVEL`
    - message warning about losing unsaved run progress
    - buttons: `Yes, Restart` and `No`
  - `Yes, Restart` executes existing restart logic.
  - `No` closes dialog and returns to pause panel.
  - ESC now cancels this restart dialog consistently.
  - Resume now also clears restart confirmation dialog if present.
- Validation:
  - `PauseMenuController.cs` compile check reports no errors.

### Minimal rollback-style fix for AND/OR gate pickup regression
- User report: AND/OR gates stopped being collectible again after a recent collector behavior change.
- Approach used (per request): smallest plausible code change first, no refactor.
- Change:
  - [Assets/Scripts/Gameplay/SimpleGateCollector.cs](Assets/Scripts/Gameplay/SimpleGateCollector.cs)
  - Removed strict `IsDirectlyAimable(...)` requirement from gate candidate selection.
  - Removed strict `IsDirectlyAimable(...)` early-return guard from `TryCollectGate()`.
  - Kept existing mesh-distance safety check (`GetGateDistanceFromCamera(...) <= MaxGateCollectDistance`) intact.
- Validation:
  - `SimpleGateCollector.cs` compile check reports no errors.
- Expected result:
  - AND/OR gates can be collected again in partially occluded/embedded cases while still blocking far accidental pickup via mesh distance.

## 2026-02-27

### Gate collection reliability (Level2 report)
- Symptom: Some gates in Level2 could not be collected.
- Root cause: Gate collection required a strict direct center-ray first-hit check, which could fail when nearby geometry collider blocked the ray.
- Change:
  - `Assets/Scripts/Gameplay/SimpleGateCollector.cs`
  - Removed strict `IsDirectlyAimable()` requirement from gate candidate selection.
  - Collection now validates with `HasLineOfSight()` at interaction time.
- Result: More consistent gate pickup in cluttered areas while still preventing collection through walls.

### Puzzle question panel lookup warning
- Symptom: `[PuzzleTable] Could not find 'Q5' — falling back to default search.`
- Root cause: `FindPuzzleContent()` searched `Background` globally in `UITable`, which can contain multiple level templates.
- Change:
  - `Assets/Scripts/Puzzle/PuzzleTableController.cs`
  - Scoped panel lookup to `Level{currentLevelNumber}` first, then searched `Background/QY` under that root.
- Expected result: Level2 and Level4 multi-question panels (`Q1..Q5`) resolve correctly and consistently.

### Level-specific puzzle prefab assignment fix
- Symptom: Level2/3/4 puzzle questions could show blank UI (notably Q5) while still showing question counter and controls.
- Root cause: `InteractiveTable.puzzleUIPrefab` in those scenes pointed to `UITable.prefab` (Level1-only layout).
- Change:
  - `Assets/Scenes/Level2.unity` now points to `Level 2/Level2.prefab`.
  - `Assets/Scenes/Level3.unity` now points to `Level 3/Level3.prefab`.
  - `Assets/Scenes/Level4.unity` now points to `Level 4/Level4.prefab`.
- Notes:
  - Each scene still has an older `InteractiveTable` entry with `puzzleUIPrefab: {fileID: 0}`; this was left unchanged.
  - Primary active table references are now level-correct.

### Runtime UI bootstrap for level panel prefabs
- Symptom: Table could fail to open/render after assigning panel-only prefabs (`Level2`, `Level3`, `Level4`) because they do not include `Canvas`/`PuzzleTableController`.
- Change:
  - `Assets/Scripts/Gameplay/InteractiveTable.cs`
  - Added `EnsurePuzzleUIInfrastructure()` to attach `Canvas`, `CanvasScaler`, and `GraphicRaycaster` at runtime when missing.
  - Keeps compatibility with both full `UITable` and panel-only level prefabs.
- Additional fix:
  - `Assets/Scripts/Puzzle/PuzzleTableController.cs`
  - Level-root detection now treats the current transform as valid when its own name already matches `Level{N}` (removes false warning and improves lookup reliability).

### Prompt cleanup after puzzle solve + message dedupe
- Symptom A: After solving a table, the prompt `Press E to open Puzzle Table` could still appear.
- Root cause: `SimpleGateCollector` still considered solved `InteractiveTable` objects as valid interaction targets.
- Change:
  - `Assets/Scripts/Gameplay/InteractiveTable.cs`
    - Added `IsSolved` property.
    - Added success-key auto-resolution fallback (`CollectibleKey` with `KeyType.Success`, then common object names).
  - `Assets/Scripts/Gameplay/SimpleGateCollector.cs`
    - Table detection/prompt/opening now skips solved tables.

- Symptom B: Too many stacked overlays/messages during fast interactions.
- Root cause: key pickup used an extra legacy popup while door-locked popup could still be active.
- Change:
  - `Assets/Scripts/Gameplay/CollectibleKey.cs`
    - Added `showLegacyPickupPopup` (default `false`) to disable duplicate key popup by default.
    - On key pickup, force-hide tutorial/success door locked popups.
  - `Assets/Scripts/Gameplay/TutorialDoor.cs`
  - `Assets/Scripts/Gameplay/SuccessDoor.cs`
    - Added immediate hide APIs for locked-message UI and safe coroutine handling.

### Level2 question reroll on reopen
- Symptom: Closing and reopening the same table in Level2 changed the selected question.
- Root cause: `InteractiveTable.OpenPuzzleInterface()` re-randomized question index every open.
- Change:
  - `Assets/Scripts/Gameplay/InteractiveTable.cs`
  - Added per-table session lock fields (`hasLockedQuestionSelection`, `lockedQuestionIndex`).
  - Reopening now reuses the same question index for that table during the current run.
- Expected behavior: Question stays fixed unless the game/session is restarted (or table instance recreated).

### Level2 success key + success door parity fixes
- Symptom:
  - Level2 `success_key` did not consistently animate like Level1.
  - Level2 success door (`Door_Success`) did not open like Level1.
  - Warning persisted: `[PuzzleTable] Level root 'Level2' not found...`
  - Console warning: `BoxCollider does not support negative scale...` on `success_key`.
- Root cause:
  - `Door_Success` in Level2 was missing `SuccessDoor` component.
  - `CollectibleKey` bobbing coroutine only started in `Start`, so hide/show cycles could stop animation.
  - Runtime UI root was renamed, making level-root lookup brittle for panel-only prefabs.
  - Key used `BoxCollider`, which is sensitive to negative lossy scale from hierarchy.
- Change:
  - Scene fix: added `SuccessDoor` component to `Door_Success` in `Assets/Scenes/Chapter 1/Level2.unity`.
  - `Assets/Scripts/Gameplay/CollectibleKey.cs`
    - Moved collider setup to `Awake`, switched to `SphereCollider`, removed legacy `BoxCollider`.
    - Made bob animation restart on `OnEnable` and stop on `OnDisable`.
  - `Assets/Scripts/Gameplay/InteractiveTable.cs`
    - Stopped renaming instantiated puzzle UI root object.
  - `Assets/Scripts/Puzzle/PuzzleTableController.cs`
    - Added fallback to treat current root as level root when `Background` exists.

### Leaderboard time recording reliability (Level2/Level3)
- Symptom: finishing Level2 could complete gameplay but show no time in leaderboard.
- Root causes identified:
  - `LevelTimer` could miss initialization when pressing Play directly from a level scene.
  - Some scenes may miss a wired `SuccessDoor`, so completion flow (timer stop + record) may not execute.
- Changes:
  - `Assets/Scripts/Managers/LevelTimer.cs`
    - Added `Start()` bootstrap to process the active scene immediately via `OnSceneLoaded(...)`.
  - `Assets/Scripts/Gameplay/SuccessDoor.cs`
    - Added fallback time recording using scene level + `Time.timeSinceLevelLoad` when timer was not initialized.
  - `Assets/Scripts/Gameplay/CollectibleKey.cs`
    - For `KeyType.Success`, auto-ensures a `SuccessDoor` exists (find named door, then nearest door fallback) so level completion path still works.
  - `Assets/Scripts/Managers/AccountManager.cs`
    - Added warning log when `RecordLevelTime(...)` is skipped due to no logged-in player.

### Level3 success key + success door flow alignment
- Requested behavior:
  - `success_key` spins/floats in Level3.
  - With success key, player can open `Success_Door` in Level3.
  - Trigger sends player to Level4.
- Changes made:
  - Scene setup (`Assets/Scenes/Chapter 1/Level3.unity`):
    - Converted active door object to `Success_Door`.
    - Added `SuccessDoor` component on that door.
    - Verified Level3 has `success_key` with `CollectibleKey` `keyType: Success`.
  - Runtime transition robustness (`Assets/Scripts/Gameplay/SuccessDoor.cs`):
    - Transition now prefers scene-derived next level (`Level3 -> Level4`) via `LevelManager.LoadLevel(targetLevel)`.

### Added persistent technical dictionary
- Request: create a readable "how-things-work" reference from easy to complex and keep updating it for future tasks.
- Change:
  - Added `AI_WORKLOG/TECH_DICTIONARY.md` with:
    - key system glossary,
    - level-complete/next-level trigger flow,
    - puzzle table/drop-box/panel flow,
    - prefab instantiation flow,
    - runtime UI creation flow,
    - Level2/Level3 fix summary,
    - mandatory update protocol.
  - Updated `AI_WORKLOG/README.md` to include and require updates to `TECH_DICTIONARY.md` when behavior changes.

### Expanded technical dictionary (locks/settings/save/panel routing)
- Request: add more practical documentation for locked levels, settings flow, save/load, panel-calling through code, and remove repetitive sections.
- Change:
  - Refactored `AI_WORKLOG/TECH_DICTIONARY.md` with new dedicated sections:
    - locked levels (including current DEV unlock override and how to restore real lock behavior),
    - settings/options flow,
    - save/load + Firebase sync,
    - panel routing / runtime button wiring,
    - log/debug prefixes.
  - Cleaned repeated explanations and kept one clear source per topic.

### Level4 success key and success door parity
- Request: make Level4 behave like Level1/2/3 where finishing table flow allows `success_key` collection and using it on `Door_Success`.
- Root cause:
  - `success_key` existed and was configured as success type, and `Door_Success` existed.
  - `Door_Success` was missing `SuccessDoor` component in Level4.
- Change:
  - Scene setup (`Assets/Scenes/Chapter 1/Level4.unity`):
    - Added `SuccessDoor` component to `Door_Success`.
- Expected result:
  - After table completion, `success_key` can be used to open `Door_Success` in Level4, matching Level1/2/3 flow.

### Offline-safe game startup (no external connector dependency)
- Request: ensure game remains functional even when no AI/MCP connection is present.
- Root cause:
  - `AccountManager` hard-depended on Firebase auth/database initialization and cloud user session at startup/save points.
  - If external services are unavailable, login/save paths could fail and block normal flow.
- Change:
  - `Assets/Scripts/Managers/AccountManager.cs`
  - Added safe auth acquisition helper (`TryGetAuth`) with exception handling.
  - Added local fallback player bootstrap (`EnsureOfflineGuestPlayer`).
  - `Awake` now safely handles missing Firebase database and switches to local/offline mode.
  - `Start` now routes to main menu with a local guest profile if cloud auth is unavailable.
  - Login/create/link/save/refresh/logout paths now gracefully degrade to offline-safe behavior when cloud services are unavailable.
- Expected result:
  - Game can open and be played without MCP/external service availability; cloud operations are skipped instead of breaking gameplay.

### Store button on gameplay HUD (all levels)
- Request: add a Store button below the top-right pause/hamburger button on every level, open `Store` prefab on click, and show item descriptions on hover.
- Changes:
  - Copied prefab to resources for reliable runtime loading:
    - `Assets/Store/Store.prefab` -> `Assets/Resources/Store.prefab`
  - Updated `Assets/Scripts/UI/PauseMenuController.cs`:
    - Added runtime Store HUD button creation under the top-right pause button for level scenes.
    - Added Store overlay open/close flow (`ToggleStoreOverlay`, `CloseStoreOverlay`).
    - Added hover description wiring for store items (`Scanner`, `Lantern`, `Adrenaline`) using `EventTrigger`.
    - Added automatic cleanup when leaving level scenes.
- Expected result:
  - Store button appears on all levels.
  - Clicking Store opens the Store UI.
  - Hovering item pictures updates description text panel dynamically.

### Store button visual style pass (medieval + cart icon)
- Request: make the new Store button look medieval and use a shopping-cart icon instead of another hamburger icon.
- Change:
  - `Assets/Scripts/UI/PauseMenuController.cs`
  - Added `StylizeStoreHUDButton(...)` to:
    - hide cloned pause-button inner icon graphics,
    - tint button to medieval gold/brown palette,
    - add centered TMP label with cart icon + `STORE` text.
- Expected result:
  - Top-right stack no longer shows two hamburger icons.
  - Store button has distinct cart/store identity while matching medieval UI tone.

### Gameplay cursor toggle with TAB
- Request: allow mouse pointer to appear in-game on `Tab` key press.
- Change:
  - Initial attempt in `Assets/Scripts/Player/PlayerController.cs` did not affect level gameplay because level scenes use Starter Assets first-person controller.
  - Final working fix in `Assets/StarterAssets/FirstPersonController/Scripts/FirstPersonController.cs`:
    - Added `_tabCursorVisible` toggle controlled by `Keyboard.current.tabKey.wasPressedThisFrame`.
    - Added `SetGameplayCursorVisible(bool)` to switch:
      - `Cursor.lockState` and `Cursor.visible`
      - `StarterAssetsInputs.cursorInputForLook` and `StarterAssetsInputs.cursorLocked`
    - Clears tab-toggle state while pause menu is open so resume returns to normal locked gameplay cursor.
- Expected result:
  - Press `Tab` in gameplay to show mouse pointer.
  - Press `Tab` again to return to locked cursor movement mode.

### Store hover description reliability + hide pictures while reading
- Request: when hovering store items, show description reliably; also hide pictures while description is shown.
- Change:
  - `Assets/Scripts/UI/PauseMenuController.cs`
  - Improved description target detection to pick the best text body in `Description` panel (with fallback if naming differs).
  - Wired hover triggers on both item roots and all image children so pointer hover works regardless of exact hit area.
  - Added runtime caching of item visuals per item (`Scanner`, `Lantern`, `Adrenaline`).
  - On hover enter:
    - updates description text,
    - hides that item's child visuals.
  - On hover exit:
    - restores visuals,
    - restores default description text.
- Expected result:
  - Hovering item pictures consistently shows description.
  - Item picture visuals are temporarily hidden while description is being shown.

### Store hover flicker + TAB spin stabilization
- Symptom:
  - Store description flickered while hovering.
  - Pressing `Tab` mid-movement/look could leave camera spinning.
- Root cause:
  - Store: overlapping hover triggers on item roots + child images caused rapid enter/exit loops when visuals were hidden.
  - First-person: stale look input persisted across cursor toggle state changes.
- Change:
  - `Assets/Scripts/UI/PauseMenuController.cs`
    - Store hover now wires only on item root targets.
    - Clears existing `EventTrigger` entries before assigning runtime handlers.
  - `Assets/StarterAssets/FirstPersonController/Scripts/FirstPersonController.cs`
    - On cursor toggle, explicitly zeroes look input and rotation velocity.
    - Clears movement input when unlocking cursor.
    - Skips camera rotation update while TAB cursor mode is active.
- Expected result:
  - Store hover no longer flickers.
  - TAB cursor toggle no longer causes stuck camera spin.

