# Future Plans

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

