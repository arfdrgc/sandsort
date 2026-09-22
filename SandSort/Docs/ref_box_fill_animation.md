# Reference: Box Sand-Fill Animation (ref_game_2_2)

Observation notes for the box filling animation in a reference game. This file
records **what is visible in the clip only** — it is not a spec, and nothing
here has been implemented or decided for SandSort.

- Source clip: `~/Documents/UI Tasarımlar/SandSort/ref_game_2_2_sand fill_animation.mp4`
- Analysed: 2026-09-21
- Clip: 25.97 s, 1080 × 2340 portrait, 522 frames, variable frame rate
  (~20 fps average, frame gaps 40–77 ms). All timestamps below are clip time
  and are accurate to about ±0.05 s because of that frame spacing.
- All pixel values are in source resolution (1080 px wide screen).
- Audio track exists but was not analysed.

## 1. Scene layout in the clip

- Top: a wide glass **sand band** holding one colour of pixel sand (blue),
  lying as low piles on the band floor.
- Below it: a 6-column grid of dark tiles. Grid cell ≈ 129 px.
- One blue **box**, seen top-down with a slight tilt so a front lip is visible
  along its bottom edge. On-screen size 271 × 295 px (≈ 2 × 2 cells, ≈ 25 % of
  screen width). It carries a percentage label on a small tab in its top-left
  corner.
- While the box is held by the player it has a solid white outline (~6 px).
  The clip starts with the box already held, so the pick-up moment is not shown.

## 2. Timeline overview

| Clip time (s) | Phase | Label |
|---|---|---|
| 0.00 – 2.26 | Box dragged from the lower grid up to the top-right corner | 0 % |
| 2.26 – 3.49 | Box creeps upward toward the band (x fixed against the right wall) | 0 % |
| 3.54 – 3.77 | **First contact burst**: sand pile above the box is absorbed | 0 → 24 % |
| 3.77 – 5.70 | Box stationary, no sand above it | 24 % (plateau, 1.98 s) |
| 5.70 – 23.81 | Box slides left under the band; sand absorbed as it goes | 24 → 99 % |
| 24.03 | Label hits 100 %, outline drops, completion sequence starts | 100 % |
| 24.03 – 24.50 | Box squash → pop up → shrink away | – |
| 24.50 – 25.22 | Glow flash, three stars fly out and hold | – |
| 25.22 → end | Screen dims, confetti + win logo (level complete) | – |

Total time from first sand contact to 100 %: **20.5 s**, but this is paced by
the player's drag speed, not by an animation duration (see §4).

## 3. Start state and first contact

**Start state (0 %)**: interior is a flat, untextured medium blue
(≈ RGB 34, 67, 189), lighter rim, a visible inner back-wall strip (~30 px tall)
under the top rim. Label reads `0%`.

**Trigger**: filling starts while the box top is still ~33 px below its final
resting line under the band (box top y = 907 at the first non-zero frame; it
later clamps at y ≈ 874). The box does not need to be fully seated.

**First contact burst, frame by frame**:

| Clip time | Label | What is visible |
|---|---|---|
| 3.491 | 0 % | Last empty frame. Pile above the box intact. |
| 3.543 | 7 % | A small cluster of dark pixels appears in the **centre** of the interior (~15 % of its area). |
| 3.620 | 12 % | Dark cluster has grown outward as a dithered, ragged blob (~60 % of interior). Sand pixels of the pile above look scattered/lifted along the box's top edge. |
| 3.661 | 19 % | Interior fully covered by dark navy pixel-noise. Particle stream at its densest. |
| 3.723 | 23 % | Particles falling into the interior. |
| 3.765 | 24 % | Pile above the box is completely gone. Label stops. |
| → ~4.15 | 24 % | Remaining particles fall, thin out and disappear. |

- 0 → 24 % takes **≈ 0.27 s** (5 frames), i.e. roughly 90–100 %/s.
- The empty → sand-covered interior transition is a **radial dithered reveal
  from the centre**, ~0.12–0.17 s long. It happens only once, at first contact.
- The mean interior colour drops from (34, 67, 189) to (2, 57, 111) over those
  three frames — the box visibly gets *darker* when the first sand lands.

## 4. Fill progression

### 4.1 What drives the percentage

- The label tracks **sand removed from the band**, essentially 1:1 and with no
  visible lag: at 24 % the band had lost 24.0 % of its blue pixels; at 50 %,
  ≈ 51 %.
- The label updates at the moment sand leaves the band, **not** when the
  falling particles arrive. During the first burst it runs slightly ahead of
  the visible pile shrinkage.
- The label **jumps** to its new value; there is no count-up tween. Values are
  skipped whenever a burst is large (never shown in this clip: 1–6, 8–11,
  13–18, 20–22, 40, 45, 86).
- No pop, scale or colour change on the label when the value changes.

### 4.2 Staircase shape

Progress is a staircase — short bursts separated by flat plateaus — because
sand is only absorbed when the box moves under new sand.

- Average rate over the sweep (24 → 99 %): **≈ 4.1 %/s**.
- Box horizontal speed over the sweep: 484 px in 18.1 s ≈ **27 px/s**
  (≈ 0.1 box-width per second). Movement is continuous and unsnapped
  (free sub-cell positions), clamped to the board's side walls and to a fixed
  line under the band (box top y ≈ 873–875).
- Typical bursts: 1–4 % within 40–250 ms. Fastest seen: 56 → 59 % in 0.14 s,
  33 → 36 % in 0.20 s, 46 → 50 % in 0.24 s, 51 → 55 % in 0.24 s.
- Typical plateaus: 0.2–1.0 s. Longest: 24 % (1.98 s, box not moving),
  30 % (0.97 s), 97 % (0.97 s).

### 4.3 Percent vs clip time (every change observed)

| % | t (s) | % | t (s) | % | t (s) | % | t (s) |
|---|---|---|---|---|---|---|---|
| 7 | 3.54 | 39 | 9.80 | 61 | 13.81 | 81 | 16.88 |
| 12 | 3.62 | 41 | 10.41 | 62 | 13.98 | 82 | 17.27 |
| 19 | 3.66 | 42 | 10.60 | 63 | 14.10 | 83 | 17.32 |
| 23 | 3.72 | 43 | 10.84 | 64 | 14.14 | 84 | 17.74 |
| 24 | 3.77 | 44 | 10.90 | 65 | 14.53 | 85 | 18.18 |
| 25 | 5.74 | 46 | 11.41 | 66 | 14.63 | 87 | 18.59 |
| 26 | 5.85 | 47 | 11.47 | 67 | 14.76 | 88 | 18.94 |
| 27 | 5.93 | 48 | 11.52 | 68 | 14.88 | 89 | 19.41 |
| 28 | 6.54 | 49 | 11.61 | 69 | 14.95 | 90 | 19.67 |
| 29 | 7.19 | 50 | 11.66 | 70 | 15.15 | 91 | 19.95 |
| 30 | 7.40 | 51 | 12.38 | 71 | 15.24 | 92 | 20.08 |
| 31 | 8.37 | 52 | 12.43 | 72 | 15.29 | 93 | 20.64 |
| 32 | 8.56 | 53 | 12.47 | 73 | 15.41 | 94 | 20.99 |
| 33 | 9.07 | 54 | 12.52 | 74 | 15.46 | 95 | 21.63 |
| 34 | 9.13 | 55 | 12.61 | 75 | 15.68 | 96 | 22.02 |
| 35 | 9.21 | 56 | 13.37 | 76 | 15.90 | 97 | 22.40 |
| 36 | 9.27 | 57 | 13.41 | 77 | 16.03 | 98 | 23.37 |
| 37 | 9.64 | 58 | 13.46 | 78 | 16.31 | 99 | 23.81 |
| 38 | 9.74 | 59 | 13.50 | 79 | 16.54 | 100 | 24.03 |
|  |  | 60 | 13.75 | 80 | 16.68 |  |  |

## 5. How the fill level is shown inside the box

The box is viewed from above, so there is **no rising horizontal fill line**.
Fill level is communicated by three things changing together:

1. **Brightness of the sand surface** — deep sand is dark navy, sand near the
   rim is bright blue. Mean colour of the interior:

   | Label | Mean RGB | Notes |
   |---|---|---|
   | 0 % | 34, 67, 189 | flat, no texture (empty box floor) |
   | 19–32 % | 2, 57, 111 | darkest; **does not change across this range** |
   | 39 % | 3, 63, 121 | |
   | 50 % | 5, 77, 146 | |
   | 60 % | 6, 88, 167 | |
   | 70 % | 8, 99, 187 | |
   | 80 % | 9, 111, 208 | |
   | 90 % | 10, 120, 222 | |
   | 95–100 % | 10, 124, 227 | flattens out; no visible change after ~95 % |

   Brightening is roughly linear between ~33 % and ~80 % (≈ 2 blue-channel
   units per 1 %), then eases off toward the top. It moves in the same steps
   as the label — no separate smoothing is visible.

2. **Pixel-noise texture** — the surface is a grid of ~6 px sand pixels in
   mixed shades. Contrast between pixels is low when dark (std ≈ 7), highest
   around 70–80 % (std ≈ 16–17), slightly lower again when nearly full
   (≈ 14). The pattern is **static while the percentage is constant** (no
   shimmer or idle animation) and re-rolls as sand is added.

3. **Surface rising toward the rim** — the visible sand area grows as the box
   fills: its top edge moves up by roughly 30 px between 24 % and 97 %,
   swallowing the inner back-wall strip, and the side margins narrow by a few
   pixels. Near 100 % the sand reaches the rim on all sides. There is a soft
   dark shading along the inner top edge (wall shadow) that shrinks with it.

The fill is spatially uniform — no directional gradient, no heap forming under
the point where particles enter.

## 6. Sand band behaviour while the box fills

- Sand is removed from the band **only in the columns directly above the box's
  horizontal span**. The pile ends in a near-vertical cut that stays aligned
  with the box's leading (left) edge as it moves.
- The rest of the pile **keeps its profile**: no large-scale slump or
  avalanche toward the removed side is visible; the remaining silhouette at
  8 s, 12 s and 16 s is the original silhouette truncated from the right. Only
  the cut edge itself shows a short slope of a few pixels.
- Removal is near-instant per column: the 270 px-wide right-hand pile vanished
  within ~5 frames (≈ 0.25 s).
- The last remnant of sand in the band disappears on the same frame the label
  reaches 100 %.

## 7. Secondary effects during filling

**Falling particles** (the only secondary effect while filling):

- Small bright-blue squares, ~8–12 px, several rotated ~45° (diamond look).
  Brighter than the dark sand bed, so they read clearly against it.
- Spawn along the box's **top edge at the x-position where sand was just
  removed** — top-right corner for the first burst, top-left corner for the
  whole leftward sweep (the leading edge).
- Fall downward into the interior with a slight sideways drift, travelling
  ~40–60 % of the interior height while shrinking/fading. Lifetime
  ≈ 0.3–0.45 s.
- Count scales with the burst: ~40–50 visible at the peak of the first
  0 → 24 % burst, 3–8 for the 1–3 % bursts during the sweep.
- Drawn above the rim, the label tab and the band's lower frame.
- The interior does not flash, ripple or bounce when particles land.

**Not present during filling**: no box scale pulse, no squash, no shake, no
glow, no label animation, no change to the white outline.

## 8. Completion sequence (100 %)

T = 24.03 s (the frame the label first shows `100%`).

| T + (s) | Clip time | What is visible |
|---|---|---|
| 0.00 | 24.03 | Label `100%`. White held-outline **disappears on this frame** (control is taken from the player). Box scale ≈ 0.95. |
| 0.05 | 24.08 | Scale ≈ 0.89 — anticipation squash, uniform (no stretch), centred. |
| 0.09 | 24.12 | Starts moving up and growing. |
| 0.16 | 24.19 | Scale ≈ 1.07, box has risen ~50 px and now overlaps the band (drawn above it). |
| 0.23 – 0.37 | 24.26 – 24.40 | Holds at scale ≈ 1.10 — a short hang of ~0.15–0.2 s. A faint darker patch (shadow) is visible on the grid where the box had been. |
| 0.41 | 24.44 | Scale ≈ 0.64 and collapsing fast toward its centre (one frame). |
| 0.47 | 24.50 | Box gone. Soft white-yellow glow orb (~45 px) at the box centre. |
| 0.51 | 24.54 | Glow orb larger (~57 px). |
| 0.55 | 24.58 | Glow replaced by **three 5-point stars** leaving the centre. |
| 0.55 – 0.85 | 24.58 – 24.89 | Stars fly out in three directions (up, left, down-right) with tapered pale trails. Fast start, strong deceleration (ease-out); they stop ~130–180 px from the centre. Trails shorten and vanish as the stars stop. |
| 0.85 – 1.19 | 24.89 – 25.22 | Stars sit in place, essentially static. |
| 1.19 | 25.22 | Full-screen dim overlay appears in a single step; stars fade with it. |
| 1.25 → | 25.28 → | Confetti burst from the bottom and the win logo letters pop in one by one (level-complete UI; outside the scope of the box animation). |

Easing read: squash (≈ 0.05–0.09 s, quick ease-in) → overshoot rise to 1.10
(≈ 0.1 s, ease-out) → hang → very fast shrink (≤ 0.1 s, ease-in). Whole box
exit ≈ **0.47 s**; box exit + stars ≈ **1.2 s**.

The sand texture and the `100%` label stay on the box through the squash and
pop and scale with it.

## 9. Looping / repeating behaviour

- **Nothing loops.** Both idle states (empty box, partially filled box with no
  sand above it) are completely static.
- The only repeating element is the particle burst, re-triggered once per
  absorption step, scaled to the amount absorbed.
- The centre-out dithered reveal happens once (first sand only).
- The completion sequence plays once and hands over to the win screen.

## 10. Start / end states summary

| State | Look |
|---|---|
| Empty (0 %) | Flat medium-blue interior, inner back wall visible, `0%` label, white outline while held |
| Filling | Dark → bright pixel-noise surface, surface creeping up to the rim, stepped label, particle bursts at the leading top edge |
| Just before full (97–99 %) | Bright blue noise right up to the rim; visually indistinguishable from 100 % except for the label |
| Full (100 %) | Outline off → squash → pop → shrink → glow → three stars; box is removed from the board |

## 11. Limits of this analysis

- One clip, one box, one colour, one level (the box completed the level, so a
  non-final box completion was not observed).
- Pick-up and release of the box are not in the clip.
- Drag movement is player input; its speed/easing says nothing about the
  game's own animation curves.
- Frame spacing (40–77 ms) limits timing resolution; sub-frame easing shapes in
  the completion sequence are inferred from 2–4 samples each.
