# Session Log

## 2026-04-09

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

