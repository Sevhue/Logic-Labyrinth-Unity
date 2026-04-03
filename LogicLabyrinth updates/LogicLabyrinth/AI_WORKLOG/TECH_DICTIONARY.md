# Technical Dictionary and How-Things-Work

Purpose: quick, readable reference for how the project behaves.
Audience: future you (and future AI sessions).
Style: easy to complex, with direct file references.

---

## 1) Core terms (easy)

- **Interact Prompt**: the `Press E ...` text for nearby interactables.
  - Core: `Assets/Scripts/Gameplay/SimpleGateCollector.cs`

- **Interactive Table**: world table object that opens puzzle UI.
  - Core: `Assets/Scripts/Gameplay/InteractiveTable.cs`

- **Puzzle Table Controller**: puzzle UI logic (question panel selection, drop slots, answer checks).
  - Core: `Assets/Scripts/Puzzle/PuzzleTableController.cs`

- **Success Key (`success_key`)**: appears after puzzle solve, grants success-door access.
  - Core: `Assets/Scripts/Gameplay/CollectibleKey.cs`

- **Success Door (`Success_Door` / `Door_Success`)**: end-of-level door to transition next level.
  - Core: `Assets/Scripts/Gameplay/SuccessDoor.cs`

- **Level Timer**: tracks elapsed level time and records best times.
  - Core: `Assets/Scripts/Managers/LevelTimer.cs`

- **Leaderboard Data**: public-safe player timing/progress snapshot in Firebase `leaderboard`.
  - Write: `Assets/Scripts/Managers/AccountManager.cs`
  - Read/UI: `Assets/Scripts/UI/LeaderboardPanel.cs`

---

## 2) Next-level trigger flow (medium)

When player has success key and interacts with success door:

1. `SuccessDoor.TryInteract()` checks `SuccessDoor.PlayerHasSuccessKey`.
2. Door unlock/open animation runs, then fade transition begins.
3. Time gets recorded:
   - Normal: `LevelTimer.Instance.StopAndRecordTime()`
   - Fallback: scene-level + `Time.timeSinceLevelLoad`
4. Progress updates via `AccountManager.UnlockNextLevel()`.
5. Scene transition:
   - Prefer scene-derived next level (ex: `Level3 -> Level4`).
   - Uses `LevelManager.LoadLevel(targetLevel)` when possible.

Files:
- `Assets/Scripts/Gameplay/SuccessDoor.cs`
- `Assets/Scripts/Managers/LevelTimer.cs`
- `Assets/Scripts/Managers/AccountManager.cs`

---

## 3) Puzzle table and drop-box UI flow (medium)

When player presses E at table:

1. `SimpleGateCollector` picks nearest valid table target.
2. `InteractiveTable.OpenPuzzleInterface()` runs.
3. Question index is chosen once per table/session and reused until restart.
4. UI prefab is instantiated.
5. If prefab is panel-only, runtime adds missing UI stack:
   - `Canvas`, `CanvasScaler`, `GraphicRaycaster`
6. `PuzzleTableController` receives:
   - selected question index
   - answer key
   - expression requirements
7. Controller resolves and activates the target panel (`LevelX/Background/QY`).

Files:
- `Assets/Scripts/Gameplay/SimpleGateCollector.cs`
- `Assets/Scripts/Gameplay/InteractiveTable.cs`
- `Assets/Scripts/Puzzle/PuzzleTableController.cs`

---

## 4) Prefab instantiation pattern (medium)

Common runtime pattern:

- Scene object stores prefab reference (example: `puzzleUIPrefab`).
- Runtime does:
  - `Instantiate(prefab)`
  - activate object
  - inject required data/components
  - open/use it

Important note:
- Level2/3/4 puzzle prefabs can be panel-only.
- Runtime bootstrap in `InteractiveTable` keeps them functional.

Primary file:
- `Assets/Scripts/Gameplay/InteractiveTable.cs`

---

## 5) Locked levels (current vs future) (medium)

Current status:
- **DEV MODE is ON** for easier testing.
- `LevelManager.CanAccessLevel(int level)` currently returns `true` for all levels.
- `LevelSelectionController` also hardcodes unlock state (`unlockedLevels = 99`) for testing visuals.

Where real lock logic is meant to come from:
- `AccountManager.PlayerData.unlockedLevels`
- `AccountManager.PlayerData.lastCompletedLevel`
- Level progression increments in `AccountManager.UnlockNextLevel()`

Files:
- `Assets/Scripts/Managers/LevelManager.cs`
- `Assets/Scripts/UI/LevelSelectionController.cs`
- `Assets/Scripts/Managers/AccountManager.cs`

If we re-enable real locks later:
- remove DEV overrides in `CanAccessLevel()`
- use player data in `LevelSelectionController.SetupChapterLevels()`
- keep `UnlockNextLevel()` as progression source of truth

---

## 6) Settings / options flow (medium)

Settings are managed primarily by pause/menu controller:

- In-level:
  - `ESC` or gear opens pause menu.
  - Pause menu opens options overlay.
- In main menu:
  - Options button routes to same options system.
- Options include quality presets and related tooltip UI.

Files:
- `Assets/Scripts/UI/PauseMenuController.cs`
- `Assets/Scripts/UI/UIManager.cs`

Related behavior:
- Pause flow manages `Time.timeScale`, cursor lock, event system, and input enabling/disabling.
- Uses runtime wiring for buttons and runtime-instantiated overlays.
- During gameplay, `Tab` now toggles cursor lock/visibility in `StarterAssets/FirstPersonController` for quick pointer access in level scenes.

---

## 7) Save/load and cloud sync flow (medium -> advanced)

Data owner:
- `AccountManager.PlayerData` stores progression, gates, times, save position, level unlocks.

Save flow:
1. Pause menu save captures player position/rotation and current level.
2. Inventory counts sync into player data.
3. `AccountManager.SavePlayerProgress()` writes to Firebase (`users/{uid}`).
4. Public leaderboard snapshot is also updated.

Load/continue flow:
1. `LevelManager.ContinueGame()` refreshes latest player data from Firebase.
2. If `savedLevel > 0`, resume mid-level and restore position.
3. Else continue from `lastCompletedLevel + 1`.
4. Inventory syncs from cloud values.

Files:
- `Assets/Scripts/UI/PauseMenuController.cs`
- `Assets/Scripts/Managers/LevelManager.cs`
- `Assets/Scripts/Managers/AccountManager.cs`

Offline-safe behavior:
- If Firebase/auth is unavailable, `AccountManager` now creates a local guest profile and keeps menu/game flow running.
- Cloud sync/leaderboard writes are skipped safely (with warnings) instead of breaking gameplay.

---

## 8) Panel routing and UI calls through code (advanced)

Central panel router:
- `UIManager` is the main switchboard for menu/game panels.
- Common calls:
  - `ShowMainMenu()`
  - `ShowMainLoginPanel()`
  - `ShowStoryBoardPanel()`
  - `ShowLeaderboardsPanel()`
  - `ShowGameUI()`

Runtime wiring:
- Buttons are often wired in code (`onClick`), not only Inspector.
- This is used to recover from scene swaps, duplicates, and stale references.

Scene transition behavior:
- On scene load, `UIManager` re-finds references and shows proper panel set.
- `LevelManager` also ensures `UIManager` exists and bootstrap systems are present.

Files:
- `Assets/Scripts/UI/UIManager.cs`
- `Assets/Scripts/Managers/LevelManager.cs`

Gameplay Store overlay:
- `PauseMenuController` now also injects a runtime Store HUD button in level scenes.
- It clones/places the button under the top-right pause control, opens `Store` prefab, and handles hover description events.
- Runtime load source is `Resources/Store.prefab` for scene-independent behavior.
- Current hover mappings are: `Scanner`, `Lantern`, `Adrenaline`.
- Visual pass: cloned pause icon graphics are hidden and replaced with a medieval-tinted cart + `STORE` TMP label.
- Hover stability: triggers are wired on item roots (single target per item) to avoid enter/exit flicker loops; while hovered, item visuals are hidden and restored on pointer exit.

---

## 9) Logging and debugging conventions (advanced)

Useful prefixes:
- `[PuzzleTable]` question/panel lookup and activation
- `[InteractiveTable]` table open and key spawn logic
- `[SuccessDoor]` success-door flow and transition
- `[LevelTimer]` timer start/stop and level timing
- `[AccountManager]` save/load and leaderboard writes
- `[ContinueGame]` detailed load pipeline diagnostics
- `[PauseMenu]` save/exit/settings runtime actions

When diagnosing issues:
- Check Console for these prefixes first.
- Then verify scene object names and required components.

---

## 10) Snapshot of key fixes already in place

- More reliable gate collection (`HasLineOfSight`).
- Level-root-scoped puzzle panel lookup for multi-question levels.
- Session-locked table question selection (no reroll on reopen).
- Solved tables no longer show open prompt.
- Success key popup/message cleanup to reduce UI stacking.
- Success key collider moved to `SphereCollider` (negative-scale warning avoided).
- Success key bob/spin restarts on re-enable.
- Level2/Level3 success-door wiring fixed.
- Level4 success-door wiring fixed.
- Level3 success transition forced to Level4 path reliably.
- Timer/leaderboard recording hardened for direct level testing.
- Account/login/save flow now has offline fallback so game remains functional without external connector/cloud services.
- Added gameplay Store button/overlay with hover descriptions wired from code for all levels.

Primary files:
- `Assets/Scripts/Gameplay/SimpleGateCollector.cs`
- `Assets/Scripts/Gameplay/InteractiveTable.cs`
- `Assets/Scripts/Puzzle/PuzzleTableController.cs`
- `Assets/Scripts/Gameplay/CollectibleKey.cs`
- `Assets/Scripts/Gameplay/SuccessDoor.cs`
- `Assets/Scripts/Managers/LevelTimer.cs`
- `Assets/Scripts/Managers/AccountManager.cs`

---

## 11) Update protocol (must follow every new request)

For every new fix/task:

1. Add what changed and why in `SESSION_LOG.md`.
2. Update active/completed items in `TASKS.md`.
3. If behavior/workflow changed, update this file (`TECH_DICTIONARY.md`).
4. If it creates follow-up work, update `FUTURE_PLANS.md`.

This file is the practical "how it works" memory.
