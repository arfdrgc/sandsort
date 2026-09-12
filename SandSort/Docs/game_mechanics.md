# SandSort — Game Mechanics

This document defines the gameplay mechanics, gameplay rules, terminology, and design principles of SandSort, built around the selected concept **"Sand Idea"** (source: `Docs/selected_sand_idea.pdf`).

It serves as the Single Source of Truth for gameplay implementation under:

Assets/Game/CurrentGame/

Every gameplay system must follow the rules defined in this document.

IMPORTANT

Claude Code must treat this document as the authoritative gameplay specification.

If implementation conflicts with this document, this document always takes precedence.

Claude Code must not invent gameplay rules that are not defined in this document.

If an implementation decision requires a gameplay rule that is not defined here, Claude Code must identify the ambiguity and ask for clarification before implementing that gameplay behavior. See the **Open Design Questions** section — most unresolved decisions are already tracked there.


# Status

Version: 2.0 — replaces the previous "Pixel Loop Blast" gameplay spec, which described an unrelated block/ColorLoop puzzle that is not part of this project.

The concept described here ("Sand Idea") was selected on 2026-09-11 from `Docs/selected_sand_idea.pdf` as the game to build under `Assets/Game/CurrentGame/`.

No implementation of this core loop (Container, SandCanvas, Board) exists yet. Current work under `Assets/Game/CurrentGame/` (e.g. `SandCylinderDemo/`, `SandFeelDemo/`) is standalone sand-physics/feel prototyping, not this gameplay loop — see `Docs/pre_development_checklist.md`.

`Docs/level_design.md` and `Docs/pre_development_checklist.md` still describe the old "Pixel Loop Blast" concept and need to be rewritten to match this document before level-authoring or scaffolding work resumes.

This document is intentionally incomplete in places: the source PDF defines the concept at a design-brief level, not a full specification. Sections below distinguish what is defined from what is not — see **Open Design Questions**.

This document should be updated whenever gameplay rules change.


# Game Vision

SandSort is a casual 3D mobile puzzle game about sequencing, not just placement.

The board holds a set of hollow Containers of varying sizes and shapes, each assigned a color. Above the board is a SandCanvas: a picture rendered entirely out of colored sand pixels.

The player holds and drags a Container until at least part of it sits in the Board's topmost row, directly beneath the SandCanvas (see the Top Row Trigger rule under Container below). While docked there, sand pours down from the SandCanvas into the Container, raising the Container's FillLevel and eroding those pixels out of the picture from the bottom up. A Container that reaches 100% FillLevel is sealed and removed, freeing the space it occupied for other Containers.

The core tension is that every Container is simultaneously a piece the player is trying to fill and an obstacle blocking the movement of every other Container. The puzzle is not "where does this piece go" but "in what order can these pieces possibly reach their sand," since an early wrong move can physically block a later Container from ever reaching its matching color.

The entire game is built around this closed gameplay loop:

Container (unfilled, on Board)
   ↓
Player Holds & Drags Container
   ↓
Container positioned under matching-color SandCanvas pixels
   ↓
Sand pours: FillLevel rises, SandCanvas erodes
   ↓
FillLevel reaches 100%
   ↓
Container sealed and removed → space freed
   ↓
Repeat until all Containers are sealed (Win) or Timer/deadlock ends the level (Lose)


# Core Gameplay Layout

The gameplay screen is organized into two main areas plus a HUD:

┌───────────────────────────────┐
│      HUD (Coins / Timer)      │
├───────────────────────────────┤
│                               │
│          SAND CANVAS          │
│    (pixel-art picture made    │
│         of colored sand)      │
│                               │
├───────────────────────────────┤
│                               │
│         CONTAINER BOARD       │
│   (grid of Containers, each   │
│      assigned a TargetColor)  │
│                               │
└───────────────────────────────┘

The SandCanvas sits directly above the Board. As Containers fill, the SandCanvas is visibly eroded — this is the primary visual feedback of progress.

The player interacts only with the Board, by holding and dragging Containers.


# Core Gameplay Loop

A complete gameplay cycle follows this sequence:

1. The player holds a Container on the Board.
2. The player drags the Container toward the Board's topmost row (directly under the SandCanvas), specifically toward columns matching the Container's TargetColor.
3. The Container's movement is constrained by other Containers occupying the Board (see Open Design Questions for exact collision rules).
4. Once at least one of the Container's occupied cells is in the Board's topmost row, sand pours into it from that column — but only if that column's lowest not-yet-eroded SandPixel matches the Container's TargetColor (see the Bottom-Up Extraction rule under SandCanvas below).
5. The Container's FillLevel increases as sand pours in.
6. The SandCanvas pixels consumed by the pour are eroded (removed) from the picture, bottom pixel first per column.
7. When FillLevel reaches 100%, the Container is sealed.
8. The sealed Container is removed from the Board.
9. The space previously occupied by the sealed Container becomes free for other Containers to move through/into.
10. The game checks the Win Condition.
11. The game checks the Lose Condition.
12. The player may act on the next Container.


# Controls

Current player input:

- Hold Container
- Drag Container
- Release Container

There is no Piece rotation, drop-from-above, or grid-snap-and-commit step described for this concept — positioning is a live hold-and-drag, not a place-and-confirm action. Whether release simply stops movement mid-board, or has its own validity rules, is not defined — see Open Design Questions.


# Gameplay Systems


## Container

A Container is a hollow object occupying an arbitrary set of Board cells (a Shape — 1×1, 2×1, L, T, or any other combination, not rectangles only), with a single TargetColor.

Properties:

- Shape (a set of occupied cells relative to an anchor cell — see Board's Shape Support below)
- AreaUnits (the Shape's cell count — used by the Capacity Model)
- TargetColor
- Capacity (see Capacity Model below — derived, not hand-authored)
- FillLevel (0–100%)
- State (Idle / Sealed)

Rules:

- **Top Row Trigger**: a Container only fills while at least one of its occupied cells is in the Board's topmost row (directly under the SandCanvas). Containers anywhere else on the Board cannot pour at all, regardless of color — they must be dragged to the top row first.
- A Container spanning multiple top-row columns draws from ALL of those columns at once — a larger Shape has proportionally more simultaneous "intake" columns, which pairs with the Capacity Model (a larger Shape also needs proportionally more sand).
- A Container's FillLevel only increases; it does not drain or reset.
- A Container at 100% FillLevel becomes Sealed and is removed from the Board.
- A Sealed Container's Board space becomes available to other Containers.
- Containers of different Shapes occupy correspondingly different amounts of Board space and act as obstacles to one another while Idle.


## Capacity Model (resolves Open Design Question #4/#5, prototype default, revised 2026-09-11)

A Container's Capacity is not an independently hand-authored number. It is derived from the SandCanvas itself, weighted by AreaUnits (the Shape's cell count) rather than split equally: take the total count of SandPixels of the Container's TargetColor across the whole SandCanvas, and divide it among every Container sharing that TargetColor in the level in proportion to each one's AreaUnits — Capacity(container) = TotalPixelsOfColor × AreaUnits(container) ÷ TotalAreaUnitsOfThatColor. Exact fractional shares are floored, and the pixels lost to flooring are distributed one each to the Containers with the largest fractional remainder first (the "largest remainder" apportionment method — the correct generalization of simple equal-split-plus-remainder to weighted shares; it reduces to the simple case when every Container of a color has the same AreaUnits). This guarantees total Container capacity for a color always equals total available sand of that color, by construction, and that e.g. a 4-cell Container gets exactly twice the Capacity of a 2-cell Container of the same color.

FillLevel = (SandPixels this specific Container has consumed) ÷ (this Container's derived Capacity).

**Depletion Tolerance Guarantee:** if a TargetColor's SandPixels are completely exhausted from the SandCanvas while a Container of that color has not yet reached its own Capacity (a rounding/reachability edge case inherent to the integer split above — see also `Docs/pre_development_checklist.md`), that Container is immediately pulled up to 100% FillLevel and Sealed regardless of its actual collected amount. This is a deliberate safeguard, not a bug: it means SandPixel depletion never causes a Lose (see Lose Condition) — only a Container that can never physically reach any of its color's remaining SandPixels does.

**Level Validation:** every level should be checked (see `SandLevelSO.validateLevel()`) so that every SandPixel color has at least one matching Container and vice versa — a color with no Container wastes that sand (the picture can never fully erode); a Container with no matching SandPixel color anywhere auto-completes immediately per the guarantee above.


## Board

The Board is the grid area holding all Containers for the level.

Responsibilities:

- Hold the set of Containers for the level (shapes, sizes, colors, starting positions).
- Enforce that Containers cannot overlap while moving.
- Free space when a Container is sealed and removed.
- Provide the surface the SandCanvas pours onto.

Supported per level (consistent with a grid-of-containers design, exact authoring format TBD — see `Docs/level_design.md`):

- Variable board sizes
- Variable Container shapes/sizes/colors per level


## SandCanvas

The SandCanvas is the picture built out of colored sand, positioned directly above the Board, one column per Board column. Visually it renders as a single procedurally-painted, grainy/noisy texture (no individual sand-grain objects) — see Rendering below; this reuses `SandCylinderDemo`'s actual rendering and particle classes directly rather than a lookalike.

Responsibilities:

- Display the level's target picture, composed of colored SandPixels arranged into per-column stacks.
- Pour sand down into any Container currently docked in the Board's top row (see Container's Top Row Trigger rule), from whichever of the Container's top-row columns are currently extractable.
- Erode (remove) SandPixels as their sand is poured out.
- Expose, at any time, which colors and roughly how much sand remain — needed to detect the Lose deadlock condition (see Lose Condition).

A SandPixel is the smallest unit of the SandCanvas and carries a single color.

**Bottom-Up Extraction:** each SandCanvas column is a bottom-to-top stack of SandPixels, not a set of independently-poppable pixels. Extraction always removes the LOWEST remaining (not-yet-eroded) SandPixel in a column first. If that lowest SandPixel's color doesn't match the Container trying to draw from that column, the column is blocked for that Container — even if SandPixels of its color exist higher up in the same column — until whatever color IS at the bottom gets cleared by a different Container. This is a deliberate sequencing mechanic (a column can force one color's Container to wait for another's), not a bug.

**Rendering (revised 2026-09-11 — no Cube objects):** the picture is one procedurally-painted `Texture2D` on a single Quad (`SandCanvasRenderer.cs`, porting the exact rendering architecture of `SandCylinderDemo/Scripts/SandCylinderRenderer.cs` — a color grid painted into a `Color32[]` buffer with per-texel hash noise for an organic grain look, applied via `SetPixels32`), not individual grain GameObjects. An eroded SandPixel paints fully transparent rather than being replaced by a backdrop block. The pour/erosion burst reuses `SandCylinderDemo`'s actual `SandExtractionParticleEffect` + `SandCylinderTunables` classes directly (not a reimplementation) — see `SandCanvas.setupPourEffect()`.


# Win Condition

The level is completed when every Container on the Board has reached 100% FillLevel and has been sealed and removed.

LEVEL COMPLETE


# Lose Condition

The player loses when either of the following is true:

1. The level Timer reaches zero before all Containers are sealed.
2. Some Container's TargetColor is structurally impossible for it to ever reach — no position exists anywhere on the Board (regardless of what currently occupies it) where one of that Container's cells would sit in the top row above a column that still contains any SandPixel of its color.

**Note (revised 2026-09-11):** a TargetColor's sand being fully depleted EVERYWHERE is deliberately *not* a Lose trigger on its own — see the Capacity Model's Depletion Tolerance Guarantee above. A Container whose color runs out entirely auto-completes instead of failing the level.

**Known limitation (found and worked around 2026-09-11, still open):** condition 2's check deliberately treats two kinds of TEMPORARY blocking as solvable rather than as a deadlock — (a) a target position currently occupied by another still-unsealed Container (it can move later), and (b) a column whose matching SandPixel is currently sitting under a different, not-yet-cleared color due to Bottom-Up Extraction (that blocking color can itself be cleared later by whichever Container matches it). Both are the core sequencing the game is built around, not failure states, and treating them as deadlocks was tried and produced false losses in a solvable test level. However, this means the check can only catch a narrow, truly-permanent impossibility (a color entirely absent from every column a Shape could ever reach) — it does **not** attempt real multi-Container solvability analysis, so a level that is only solvable in one specific move order, or genuinely unsolvable through a more subtle interaction than "this color literally doesn't exist anywhere reachable," may not be caught by this check at all (silently stuck until the Timer runs out) or in rare constructed cases could still misfire. Treat this as an Open Design Question needing a smarter algorithm eventually, not a finished feature — see Open Design Question #3.

On Lose, the player is offered a Continue: extra time, or a booster, to keep playing rather than immediately failing the level. The exact boosters and their effects are not yet defined — see Open Design Questions.


# Reference Games

- Color Block Jam
- Sand Blocks: Drop Puzzle
- Sand Loop

These are cited in the source design brief as the closest existing games to this concept and should be used as behavioral reference when a mechanic here is underspecified — but this document, once a detail is resolved and written down, takes precedence over the reference games.


# Art Style

Soft 3D studio render with a warm cream and pastel palette, matte plastic toy finish, and friendly rounded UI.


# Naming Convention

| Gameplay System | Class / System Name |
|---|---|
| Play Area | Board |
| Fillable Piece / Obstacle | Container |
| Sand-built picture | SandCanvas |
| Unit of the SandCanvas | SandPixel |
| Countdown | LevelTimer |
| Fail-recovery offer | Continue |

These names are provisional (no code implements this loop yet). If implementation settles on different names, update this table rather than letting code and document diverge.


# Design Principles

- The Board is the player's only interaction area.
- Every Container is simultaneously a goal (fill it) and an obstacle (it blocks others).
- Sequencing/movement order is the puzzle — not shape-fitting or matching per se.
- The SandCanvas erosion is the primary visual reward signal and must clearly communicate progress.
- The player should always understand why a level was lost (timer vs. deadlock).
- New gameplay features should extend this loop (Container fill → seal → free space) rather than replace it.


# Future Gameplay Features — Blocker Mechanics

The following blocker/obstacle mechanics are planned to deepen level design and difficulty (defined 2026-09-11). They are **not** part of the core loop and must not be implemented until the core mechanic (Container hold-drag → fill → seal) is finalized and feels right. When work on them begins, integrate them **in this order, one at a time**, and expand each into its own fully-specified section here (including how it modifies Board/Container/SandCanvas rules above) before implementation:

1. **Locked Containers** — locked in place until a specific condition or key is matched.
2. **Linked Containers** — two or more containers connected together; moving one pulls the other along.
3. **Frozen / Ice Blockers** — covered in ice layers that must be cleared before the container can move or accept sand.
4. **Hidden / Mystery Containers** — the required sand color is hidden until adjacent spaces or top layers are cleared.
5. **Fixed Grid Obstacles** — unmovable stationary blocks on the board that constrain navigation and sequencing.

~~6. Variable / Irregular Shapes~~ — **no longer a future blocker; pulled into the core loop on 2026-09-11.** Multi-cell Container Shapes (2×1, L, T, ...) are now part of the base Capacity Model (see Container's AreaUnits and Capacity Model above) rather than a later addition.

None of the remaining items are specified in enough detail to implement yet (e.g. what "condition or key" means for Locked Containers, how many ice layers, how linked movement resolves conflicting drag directions). Each must go through the same clarify-before-implementing process as the Open Design Questions below when its turn comes.


# Open Design Questions

These are gaps in the source design brief (`Docs/selected_sand_idea.pdf`) that must be resolved — with the user, not invented — before or during implementation of the corresponding system. Do not silently pick an answer.

1. ~~**Board granularity**~~ — RESOLVED (2026-09-11, prototype default): a discrete grid (`Board.cs`).
2. ~~**Movement/collision rules**~~ — RESOLVED (2026-09-11, prototype default): a Container's Shape (arbitrary cell set) can move to any in-bounds anchor whose cells are all currently unoccupied; an invalid release position reverts to the last valid one.
3. **Deadlock detection**: still open — see the Lose Condition's "Known limitation" note above. The current check only catches a color being entirely absent from every reachable column; it does not do real multi-Container solvability analysis, so it can both under- and (in rare constructed cases) over-report.
4. ~~**Sand depletion**~~ — RESOLVED (2026-09-11, prototype default): see the Capacity Model section under Container above. Each color has a fixed, finite pixel count on the SandCanvas; Container Capacity is derived from it, not independently authored.
5. **Fill rate**: Is FillLevel driven by real-time sand volume/particle count, elapsed time under matching sand, or discrete pixel-consumption steps? Still a prototype default (fixed-interval discrete-tick consumption, one SandPixel per tick — see `Container.cs`'s `_pourIntervalSeconds`), not a confirmed final answer.
6. **Level authoring**: Are Containers (shape, size, color, start position) and the SandCanvas (image, color regions) fully level-authored/static, or is there any runtime generation/randomization?
7. **Continue/booster specifics**: What boosters exist (e.g., shuffle, unstick a Container, add sand)? What does "extra time" grant exactly?
8. **Economy/progression**: Coins, difficulty tiers, and progression pacing are not defined for this concept yet (the old document's Economy/Progression/Combo sections described "Pixel Loop Blast" and do not carry over).
9. **Title**: "Sand Idea" is a working name in the source brief, not a confirmed game title.


# MoowCore Integration

Gameplay systems should integrate with existing MoowCore systems whenever possible.

Examples:

- GameState Manager
- Audio Manager
- UI Manager
- Pool Manager
- Event System

Analytics events will be defined separately.


# Implementation Rules for Claude Code

Claude Code must follow these rules when implementing gameplay:

1. This document is the authoritative gameplay specification.
2. Do not invent gameplay rules that are not defined here, especially for anything listed under Open Design Questions.
3. Do not silently change gameplay behavior to resolve an ambiguity.
4. If a gameplay rule is ambiguous, stop and ask for clarification.
5. Preserve the terminology defined in the Naming Convention.
6. Do not rename gameplay concepts without updating this document.
7. Keep gameplay state and gameplay rules separate from presentation effects where practical.
8. Gameplay completion must not depend solely on animation completion.
9. Visual animations must represent gameplay state changes rather than define them.
10. Any new gameplay mechanic must be added to this document before being treated as an authoritative gameplay rule.
11. Resolve an Open Design Question by discussing it with the user and updating this document — not by picking a default in code.
