# Live Trade Rules Monitor & Control - Phase 3 UI Upgrade Plan

Goal: upgrade the current grid-based monitor UI without changing trading behavior or execution flow.

## 1. Checkbox Column - DONE

- Replace the text-based `Enabled` cell with a `DataGridViewCheckBoxColumn`.
- Preserve critical-disable confirmation before committing `false`.
- Recalculate disabled/would-have summary immediately after changes.

## 2. Numeric Editor - DONE

- Add a numeric editor pattern for number rules:
  - `NumericUpDown` for bounded values.
  - Text fallback for very large or decimal-heavy values.
  - Respect `MinValue`, `MaxValue`, and `Unit`.
- Validate before applying preview values.

## 3. TrackBar / Slider - DONE

- Add slider only for simple bounded numeric rules.
- Keep slider and numeric editor synchronized.
- Do not add sliders for unbounded, text, list, enum, or monitor-only values.

## 4. Dropdown Editor - DONE

- Use dropdowns for enum rules:
  - Trading mode.
  - Scalping direction mode.
  - Rollout stage where applicable.
- Populate from existing enum values, not hardcoded strings where possible.

## 5. Checklist / List Editor - DONE

- Add a small modal or inline editor for list/session rules:
  - Allowed pairs.
  - Recommended sessions.
  - Avoid sessions.
  - No-trade windows.
- Preserve existing list formats during save.

## 6. Colored Bars - DONE

- Replace the current text feedback column with a compact color bar.
- Use consistent colors:
  - Green = PASS / Low.
  - Yellow/Orange = WARNING / disabled-but-would-block.
  - Red = BLOCK / High.
  - Gray = DISABLED / NOT_CHECKED.

## 7. Collapsible Groups - DONE

- Replace the global group filter with collapsible group panels inside each tab.
- Each group should expose:
  - Expand/collapse.
  - Enable Group.
  - Disable Group with confirmation.
  - Reset Group if safe.
- Do not add a global Disable All.

## 8. Expandable Per-Row Advanced Details - DONE

- Move function name, variable name, source file/path, internal key, raw value, runtime mode, and audit source into expandable row details.
- Keep the default row focused on user-facing rule information.

## 9. Validation - DONE

- Build after each UI slice.
- Verify log double-click still opens Log Details.
- Verify monitor opens from all contextual entry points.
- Verify disabled rules remain visible and export correctly.
