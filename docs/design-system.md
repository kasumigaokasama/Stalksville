# Stalksville Design System

Source of truth: `apps/web/src/styles.scss` (CSS custom properties + shared component classes).
This document explains the intent; if tokens change, update both.

## Direction

Dark-first analyst UI — closer to Linear / GitHub / modern SOC tooling than to a generic admin
dashboard (master plan §6). Dark is the default; a light theme is available via the shell toggle
(`[data-theme="light"]` token overrides, persisted to localStorage). Accents stay restrained;
saturated color is reserved for meaning, never decoration:

- risk / errors
- warnings
- **changes** (the product's core signal)
- evidence & confidence
- active state

## Core tokens

| Token | Value | Use |
| --- | --- | --- |
| `--stl-bg` | `#0b0e14` | App background |
| `--stl-bg-raised` | `#11151f` | Cards, sidebar |
| `--stl-bg-hover` | `#171c29` | Hover surfaces |
| `--stl-border` | `#212839` | Subtle separators |
| `--stl-border-strong` | `#2d3550` | Inputs, emphasized borders |
| `--stl-text` | `#e6e9f0` | Primary text |
| `--stl-text-muted` | `#8b93a7` | Secondary text |
| `--stl-text-faint` | `#5c6478` | Placeholders, tertiary |
| `--stl-accent` | `#7c9aff` | Primary actions, links, active nav |
| `--stl-success` | `#4ade80` | Connected / current / available |
| `--stl-warning` | `#fbbf24` | Ended / requires clan bot / caution |
| `--stl-danger` | `#f87171` | Errors / disabled capabilities |
| `--stl-radius` | `8px` | Cards, overlays |
| `--stl-radius-sm` | `5px` | Buttons, inputs |

Typography: `Inter` / system stack at 14px base; identifiers, hashes and numbers use
`--stl-mono` (JetBrains Mono → Consolas fallback) — intelligence data is data, not prose.

## The semantic pair: observed vs derived

The product's most important distinction gets its own color pair, used consistently in tags,
chips and evidence panels everywhere:

| Token | Value | Meaning |
| --- | --- | --- |
| `--stl-observed` | `#7dd3fc` (sky) | **Observed** — data returned directly by Wolvesville |
| `--stl-derived` | `#c084fc` (violet) | **Derived** — conclusions calculated by Stalksville (changes, relationships, membership history), always evidence-backed |

Rule of thumb: if a number or statement would survive deleting the intelligence engine, it is
observed; if Stalksville computed it, it is derived and must be visually marked (`.stl-tag--observed`
/ `.stl-tag--derived`).

## Shared classes

- `.stl-page`, `.stl-page-header` — page scaffold and title row
- `.stl-card` — raised panel (scrolls horizontally when tables overflow small screens)
- `.stl-tag` (+ `--observed --derived --success --warning --danger`) — status chips
- `.stl-button` (+ `--primary`) — actions
- `.stl-input` — text inputs
- `.stl-table` — data tables
- `.stl-kv` — definition-list key/value grids
- `.stl-empty` — empty states
- `.field-chip` — change-record field labels (violet, derived semantics)
- `.stl-palette-panel` — CDK dialog chrome for the command palette
- `.stl-dialog-panel` — CDK dialog chrome for regular dialogs (erase confirmation)
- `.visually-hidden` — screen-reader-only labels for icon-only controls and filter selects
- `stl-skeleton` (`shared/ui/skeleton`) — shimmer loading placeholders replacing bare "Loading…" text

## Accessibility patterns

- Dossier tabs: real `role="tablist"`/`role="tab"` with roving tabindex, arrow/Home/End
  navigation and `aria-selected`; the content pane is a labelled `role="tabpanel"`.
- Command palette: combobox + listbox semantics (`aria-activedescendant`), arrow-key result
  navigation, Enter opens, Esc closes.
- The unread-alert count is announced through an `aria-live="polite"` region in the shell.
- Destructive actions (player erasure) use a CDK dialog with a mandatory typed reason.

## Layout

Desktop: fixed 220px sidebar (Overview / Players / Highscores / Ranked / Clans / Investigations /
Graph / Timeline / Alerts / Analytics / Settings / Admin), 52px topbar whose controls are the
hamburger (mobile) and the Ctrl+K search trigger, content column max-width 1180px. Below 900px
the sidebar becomes an overlay drawer behind the hamburger (backdrop, closes on navigate).
Component styles live next to their components; only tokens and cross-feature primitives belong
in `styles.scss`.
