# SandSort — Level Design

This document defines how levels are designed, balanced, and configured for the **"Sand Idea"** concept.

While **game_mechanics.md** defines the gameplay rules, this document defines how those rules are applied to individual levels.

It serves as the primary reference for:

- Level Design
- Difficulty Balancing
- Board/Container Authoring
- SandCanvas Authoring
- Future Level Editor

---

> **Important**
>
> Claude Code must treat this document as the authoritative level design specification.
>
> Gameplay rules are defined in **game_mechanics.md**.
>
> This document only defines how those gameplay systems are configured per level.
>
> Several sections below are placeholders rather than settled rules, because they depend on `game_mechanics.md`'s **Open Design Questions** (Board granularity, movement/collision rules, deadlock detection, sand depletion model). Do not treat those placeholders as final — resolve the underlying design question with the user first.

---

# Status

**Version:** 2.0 (Draft) — replaces the previous version, which was written for an unrelated concept ("Pixel Loop Blast": PuzzleGrid/Piece/ColorLoop/ObjectiveArea). None of that terminology applies here.

Level balancing has not been finalized. No levels for this concept have been authored or playtested yet — `Data/Levels/SandSort_Test_Level.asset` exists but its schema/contents have not been confirmed against this document.

This document should evolve throughout development as new levels are created and playtested.

---

# Design Philosophy

The purpose of each level is **not simply to become harder**.

The core tension of Sand Idea is sequencing: every Container is both a piece to fill and an obstacle blocking others. A well-designed level makes the player discover the *order* in which Containers must move — not just where they eventually end up.

Levels should create a rhythm of challenge by alternating between Easy, Hard, and Very Hard experiences (see Difficulty Rhythm).

Players should frequently experience:

- Success
- Recovery
- Challenge
- Satisfaction

rather than continuous frustration.

Difficulty should feel dynamic instead of linear.

---

# Level Parameters

Each level should define the following configurable parameters. Exact field types/format depend on the still-open Board granularity and level-authoring questions in `game_mechanics.md`.

| Parameter | Description |
|------------|-------------|
| Difficulty | Easy / Hard / Very Hard |
| Board Size / Shape | Dimensions or layout of the Board |
| Container Set | Shape, size, TargetColor, and starting position for each Container |
| SandCanvas Image | The picture and its per-region colors |
| LevelTimer Duration | Time allowed before Lose-by-timeout |
| Allowed Colors | Colors used in this level |

---

# Level Anatomy

Every level consists of the following configurable systems.

## Board

Defines:

- Size/shape of the play area
- Fixed Grid Obstacles (planned blocker mechanic — not available until it's implemented, see `game_mechanics.md`'s Future Gameplay Features)
- Starting positions of all Containers

## Container Set

Defines, per Container:

- Shape (including Irregular Shapes once that blocker mechanic exists)
- Size
- TargetColor
- Starting position
- Any blocker state (Locked / Linked / Frozen / Hidden), once those mechanics exist — not for the initial core-loop levels

## SandCanvas

Defines:

- The target picture
- Which regions of the picture are which color
- How much sand exists per color (depends on the still-open sand depletion model — see `game_mechanics.md`)

## LevelTimer

Defines:

- Total time allowed
- (Not yet defined: whether time can be gained mid-level, e.g. from combos or rewards)

## Difficulty

Every level belongs to one difficulty category.

- Easy
- Hard
- Very Hard

Difficulty influences balancing only.

Core gameplay rules never change.

---

# Difficulty Philosophy

Difficulty should be created by combining multiple gameplay parameters.

Avoid relying on only one variable (such as Board size or Container count).

Difficulty may be adjusted through:

- Number and shape variety of Containers
- How tightly Containers obstruct each other's paths to their color
- SandCanvas color layout (how spread out vs. clustered each color's sand is)
- LevelTimer duration
- Once implemented: which blocker mechanics are present and how many (see Future Blocker Difficulty below)

---

# Difficulty Rhythm

Difficulty should follow waves rather than a straight progression.

Example rhythm:

Easy

Easy

Easy

Hard

Easy

Easy

Hard

Easy

Easy

Very Hard

This is an example only.

Level designers may adjust the rhythm whenever needed.

---

# Board & Container Guidelines

Board/Container layouts should:

- Always have at least one valid solution (a sequence of moves that seals every Container).
- Avoid unavoidable deadlocks — verify no starting arrangement locks a Container away from its own color permanently (see `game_mechanics.md`'s deadlock definition; the exact check depends on the still-open deadlock-detection algorithm).
- Make sequencing the primary decision: a good level should have more than one "obviously first" move, forcing the player to reason about order.
- Avoid excessive randomness in Container placement — hand-tuned obstruction, not random clutter, is what creates the puzzle.

Board size or Container count alone should never determine difficulty.

---

# SandCanvas Guidelines

The SandCanvas should encourage planning, not just execution.

Guidelines:

- Avoid placing all of one color's sand in a single tiny region if a Container of that color starts far away or heavily obstructed — unless that difficulty is intentional.
- Mix regions that are quick to reach with regions that require earlier Containers to be cleared first.
- Ensure every Container's TargetColor has enough matching sand in the SandCanvas to reach 100% FillLevel (exact amount depends on the still-open sand depletion/fill-rate model).
- Introduce visual variety in the picture itself — it is the primary reward, so it should look worth uncovering.

---

# Sand Pattern Authoring (PNG)

A level's starting picture is a pixel-art PNG painted in Aseprite / Piskel / any lossless pixel
editor. No code is written per picture, and no Unity work beyond dropping in the file and assigning
it.

Note what this picture is and is not: it is the level's **opening frame**. The moment extraction
starts the sand falls, mixes and collapses — that erosion is the mechanic, not a defect. Author for
a strong first read, not for a shape that survives play.

## 1. Where the file goes

`Assets/Game/CurrentGame/Data/SandPatterns/<LevelName>.png`

Import settings are forced automatically for anything in that folder
(`SandPatternTextureImportSettings`). Do not change them by hand — read/write, Point filter, no
mipmaps, no compression, and the Android/iOS RGBA32 overrides all matter, and the last one silently
breaks colour matching on device if it is lost.

## 2. Resolution

The board fixes the width; the PNG's own aspect ratio then fixes the height, so the sand area is a
uniform scale of the picture.

| | Rule |
|---|---|
| **Recommended (1:1, no resampling)** | width = `boardSize.x` × 34 px |
| Also clean (exact 2× upscale) | width = `boardSize.x` × 17 px |
| Height | a multiple of the same per-block size; **max 11 blocks = 374 px** |
| Anything else | works, but non-integer upscaling makes edges ragged |

Common widths: `boardSize.x` 5 → 170 px · 7 → 238 px · 8 → 272 px · 10 → 340 px.

## 3. Palette

Colours are matched **exactly** — there is no nearest-colour fallback, and a pixel that is off by
1/255 is rejected with its hex and coordinate in the Console.

So never eyedropper a colour out of a screenshot: the sand is rendered with per-pixel colour noise
and the camera adds bloom, vignette and colour grading on top, so nothing on screen equals the real
palette value. Get the palette as a file instead:

> `Assets/Game/CurrentGame/Data/Sand/SandPalette.asset` → Inspector gear menu →
> **Export Palette (.gpl for Aseprite/Piskel)**

Load that `.gpl` in the paint tool and paint only from it.

## 4. Alpha and anti-aliasing

- **Alpha < 128 means EMPTY** — no sand at all. Use it for headroom above the sand's surface, or for
  holes inside the picture. Alpha is checked before colour, so a transparent pixel is never read as
  sand whatever its RGB happens to be.
- Leftover space from height rounding is left empty at the **top**, never at the bottom — the bottom
  row is where extraction reads.
- **Turn anti-aliasing off.** Soft edges produce off-palette pixels and the import will reject the
  file. Same for soft brushes, gradients and layer opacity below 100%.

## 5. Assigning it to a level

On the level asset (`SandLevelSO`), set **Sand → Sand Texture** to the PNG. That is the whole
wiring; capacities, the sand area's size and the camera framing all follow from it automatically.

Per-colour cell counts come straight from the PNG's pixel counts, and each colour's total Container
capacity is derived from them — so a picture is balanced by construction. What still needs a human
eye is the *distribution* (see Color Distribution below): a colour present as only a few hundred
pixels makes a fast Container, a colour covering half the picture makes a slow one.

## 6. The old hand-painted pattern still works

`_sandPattern` (a `SandCylinderPatternData` painted in its own Inspector) remains as a fallback and
older levels keep using it untouched. When both are assigned the **texture wins** and validation
says so — clear one of them. New levels should use the PNG.

---

# Color Distribution

Sand available in the SandCanvas per color should reasonably match what the level's Containers of that color need to fill.

Avoid situations where:

- A color's sand is insufficient for its Container(s) to ever reach 100%.
- A color's sand vastly exceeds what's needed, making that Container trivial.

Color scarcity/abundance should only be used intentionally, as a difficulty lever.

---

# Container Authoring

Container placement should never be completely random.

The level author (manual or, later, a generator) should consider:

- Which Containers block which other Containers' paths, and in what order that resolves.
- Overall solvability — there must exist at least one full sequence of moves that wins the level.
- Shape/size variety appropriate to the level's difficulty category.

Players should feel challenged rather than cheated — an unsolvable level is a bug, not difficulty.

---

# Level Balancing Checklist

Every level should be reviewed using the following checklist.

□ Level is solvable (a winning move sequence exists).

□ No unavoidable deadlocks.

□ Every Container's color has enough matching SandCanvas sand to fill it.

□ LevelTimer duration feels fair for the intended difficulty.

□ Container placement/obstruction feels fair, not cheap.

□ Difficulty matches its intended category.

□ Level can be completed without relying on luck.

---

# Future Level Data

The final level format is not yet defined (depends on the still-open level-authoring question in `game_mechanics.md`).

Possible implementation:

- ScriptableObject
- JSON
- Custom Level Editor

---

# Future Blocker Difficulty

Once the blocker mechanics roadmap (`game_mechanics.md`'s Future Gameplay Features — Blocker Mechanics) begins being implemented, each mechanic becomes an additional difficulty lever, introduced one at a time in this order:

1. Locked Containers
2. Linked Containers
3. Frozen / Ice Blockers
4. Hidden / Mystery Containers
5. Fixed Grid Obstacles
6. Variable / Irregular Shapes

This section should be expanded with concrete authoring guidance (how many locked Containers per difficulty tier, etc.) only once each mechanic is actually specified and implemented — not before.

---

# Playtesting Notes

This section should contain observations collected during playtesting.

Examples:

- Level completion rate
- Average time-to-complete vs. LevelTimer duration
- Retry count
- Player frustration points (e.g. accidental deadlocks)
- Unexpected sequencing strategies
- Balancing adjustments

---

# Future Improvements

Possible future additions:

- Custom Level Editor
- Difficulty Analyzer
- Automatic Solvability / Deadlock Checker
- Heatmap Visualization
- Balancing Metrics
