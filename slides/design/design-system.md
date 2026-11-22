# Design system

## Direction

Academic, calm, and data-forward. Use a clean warm-white canvas, deep navy headings, teal for system flow, and amber only for caveats or unresolved items. No stock imagery is needed: native diagrams and charts make the information clearer and stay editable.

## Canvas and grid

- 16:9 widescreen: 13.333 × 7.5 in.
- Safe margin: 0.52 in left/right, 0.40 in top, 0.34 in bottom.
- Content area starts below a 0.86 in title band; footer baseline at y=7.15 in.
- Use a 12-column mental grid; 0.16–0.24 in gutters.
- Keep one primary takeaway and at most three supporting groups per slide.

## Colors

| Token | Hex | Use |
|---|---|---|
| `ink` | `10243A` | Main text, titles, dark panels. |
| `navy` | `173B5E` | Architecture / primary series. |
| `teal` | `0B817A` | Workflow, selected service and microservice series. |
| `sky` | `DCEEF2` | Light diagram fill. |
| `paper` | `F7F9FB` | Background. |
| `white` | `FFFFFF` | Cards and contrast text. |
| `slate` | `526273` | Supporting copy. |
| `line` | `D7E0E8` | Dividers and chart grid. |
| `amber` | `B86E16` | Caveat / attention callout. |
| `amberLight` | `FFF2D9` | Caveat background. |
| `red` | `B84949` | Explicit failure outcome only. |
| `green` | `2E7D5B` | Verified success outcome only. |

## Typography

- Font family: Segoe UI, with Aptos fallback. Confirm actual Windows font rendering in PowerPoint before final delivery.
- Slide title 27–31 pt, section label 10–11 pt, body 17–20 pt, chart/table 13–16 pt, footnote 9–10 pt.
- Use sentence case; bold only for the key figure or short label.
- Vietnamese diacritics must render cleanly; avoid condensed fonts and all-caps body copy.

## Shape and chart treatment

- Rounded rectangles with 0.08–0.12 in corner feel; consistent 1 pt line or no line.
- Connectors 1.5 pt; synchronous calls solid navy, asynchronous events teal dashed, feedback/compensation amber.
- Use native PowerPoint chart objects for benchmark bars. Avoid 3D effects.
- Tables use high-contrast header row, alternating white/paper rows, minimal rules.
- Citation footer: short source labels on left; slide number on right; keep contrast readable.

## Components

- `addSlideBase`: paper background, section label, title, footer/citation.
- `addCard`: rounded panel, optional accent strip, heading, concise body.
- `addArrow`: reusable connector with label and style (sync/event/compensation).
- `addPill`: small status or source marker.
- `addSpeakerNotes`: original script cue and narration; never modify the source script.
