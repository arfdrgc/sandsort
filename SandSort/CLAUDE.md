# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# ============================================================================
# MANDATORY: CODEBASE MEMORY POLICY
# ============================================================================

## Primary Rule

This repository uses *codebase-memory-mcp* as the PRIMARY source of repository knowledge.

For ANY request involving source code, architecture, implementations, symbols,
inheritance, dependencies, references, callers, callees, usages, relationships,
or project structure, ALWAYS use *codebase-memory-mcp FIRST*.

Treat repository-wide Grep, Glob and large-scale file reading as expensive
operations that should be avoided whenever possible.

## MCP Startup Report (MANDATORY)

At the beginning of every new Claude Code CLI session, before handling the first user request, generate a short startup report.

The report should include, whenever available:

- codebase-memory-mcp status (Running / Not Running)
- transport type (stdio / http / websocket)
- HTTP endpoint(s), if exposed
- listening port(s), if exposed
- MCP server version, if available
- Auto Index status (Enabled / Disabled)
- Index status (Loaded / Building / Updating), if available

Never guess missing information.
Only report values obtained from the running MCP server.

---

## REQUIRED Investigation Workflow

Every code investigation MUST follow this workflow:

1. Query codebase-memory-mcp.
2. Locate the exact symbol(s) using the returned graph/index.
3. Read ONLY the minimal implementation files required for verification.
4. Answer the user's question.

Do NOT begin investigations with Grep, Glob or broad repository exploration.

---

## Tool Priority

Always use tools in the following priority order:

1. codebase-memory-mcp
2. Read specific files suggested by MCP
3. Language Server / Symbol navigation
4. Grep / Glob (*LAST RESORT ONLY*)

Changing this order requires a clear reason.

---

## Repository Reading Policy

Never scan the repository simply to understand how something works.

Instead:

- Ask codebase-memory-mcp.
- Locate the relevant symbols.
- Read only those implementations.

Do not read dozens of files simply to build context.

---

## Testing Rules
- NEVER start playtests from `GameScene.unity`.
- ALWAYS load and run playtests starting from `Assets/Scenes/BaseScene.unity` to ensure all `DontDestroyOnLoad` managers (e.g., `ObjectPoolManager`) are correctly initialized.

---

## Token Efficiency Policy

Always optimize for the lowest possible token usage.

Avoid:

- recursive Grep
- wildcard Grep
- scanning entire folders
- reading unrelated files
- broad repository exploration
- reading multiple implementations "just in case"

Prefer:

- graph queries
- symbol lookups
- dependency lookups
- inheritance lookups
- reference lookups
- caller/callee lookups
- targeted file reads

---

## Before Using Grep

Before executing ANY Grep or Glob search, ask yourself:

> Can codebase-memory-mcp answer this question?

If the answer is YES:

DO NOT use Grep.

Only use Grep when at least one of these conditions is true:

- the information is not indexed
- searching comments
- searching string literals
- searching generated files
- searching non-code assets
- codebase-memory-mcp failed after multiple attempts

---

## Grep Accountability

If Grep or Glob is used:

- It should be the smallest search possible.
- Never search the entire repository unless absolutely necessary.
- Never repeat the same Grep multiple times.
- Prefer directory-scoped Grep over repository-wide Grep.

Before using Grep, first determine that codebase-memory-mcp cannot answer the request.

---

## Architecture Questions

Questions like these should ALWAYS start with codebase-memory-mcp:

- Where is X implemented?
- Who calls X?
- Who references X?
- What inherits from X?
- What depends on X?
- Which systems interact with X?
- What classes implement X?
- Where is this event dispatched?
- Where is this interface used?
- What is the flow of this feature?

These are structural questions and should almost never require repository-wide Grep.

---

## Goal

Minimize token consumption.

Prefer graph queries over text searches.

Prefer targeted file reads over repository exploration.

Prefer codebase-memory-mcp even when Grep could also solve the problem.

Grep is a fallback tool, not the primary discovery mechanism.

## Git Commit operation
- Always use --author="Baris <arfdrgc@gmail.com>" flag on every git commit command.
- Never add a Co-Authored-By: line to any commit message. The commit must have no trace of Claude as author or co-author.

## What this repo is

A Unity 6 (`6000.3.20f1`) hyper-casual mobile game project, "Pixel Loop Blast". The repo root as far as git is concerned is actually the **parent directory** (`.../pixelloopblast/`, one level above this `PixelLoopBlast/` Unity project folder) — the `.gitignore` lives there and is scoped with a `/PixelLoopBlast/` prefix.

The project is structured as a reusable studio template for spinning up hyper-casual prototypes quickly, split into three parts under `Assets/Game/`:

- **`Assets/Game/MoowCore/`** — the shared, game-agnostic framework (event bus, singleton base classes, managers, ads/SDK layer, scene management). This is the "engine" layer meant to be reused across every game built from this template.
- **`Assets/Game/ReferanceGame/`** — a complete, playable reference/demo game (a fishing-themed color-sorting game: match fish cases into docks/objective zones) built on top of MoowCore. Treat this as a worked example of how to use MoowCore's patterns, not as production code to ship.
- **`Assets/Game/CurrentGame/`** — currently **empty**. This is the intended destination for the actual "Pixel Loop Blast" game-specific code/assets; that work has not started yet. There is no "Pixel Loop Blast"-specific gameplay code anywhere in the repo yet.

Everything else under `Assets/` (AppsFlyer, Firebase, MaxSdk, Obi, StylizedWater2, Dreamteck, NaughtyAttributes, ToonyColorsPro, TextMesh Pro, com.unity.uiextensions, etc.) is third-party plugin/asset-store content — don't modify it, and don't count it when reasoning about "the game's code."

Scenes: only `Assets/Scenes/BaseScene.unity` and `Assets/Scenes/GameScene.unity` exist.

## Current work status

`TODO.md` (repo root of this Unity project) is the rolling status file — what is
actually done, what is deliberately deferred, and what comes next. **Read it
before starting or resuming work under `Assets/Game/CurrentGame/`**, and update
it when a stage lands. It complements `Docs/` (which holds specs, i.e. what the
game *should* do) by recording the real state of the implementation.

Short version as of 2026-09-15: the PNG-based sand pattern workflow is live
(Faz 0 + Faz 1 complete), the palette carries 17 sand colours, and a 9-colour
example pattern is at v2 (`SandSort_FaultScarp9_9x10.png`) with all 9 colours
reachable from the extraction band. The sand simulation, extraction, capacity
and container systems have not been modified and must not be — post-extraction
deformation is a core feature, so no shape-freezing system may be added.

## Game Design Reference

`Docs/game_mechanics.md` holds the design spec for "Pixel Loop Blast" itself — core loop, controls, win/lose conditions, progression, economy, and which MoowCore events/managers each mechanic hooks into. Consult it before implementing anything under `Assets/Game/CurrentGame/`, and keep it updated as gameplay decisions are made — it is the source of truth for *what* the game should do, complementing this file's description of *how* the codebase is structured.

`Docs/level_design.md` holds the level-authoring spec — how the systems defined in `game_mechanics.md` are configured per level (PuzzleGrid layout, ObjectiveArea composition, ColorLoop capacity, Piece weights, difficulty pacing/rhythm). Consult it when building levels or the level data format/editor for `Assets/Game/CurrentGame/`, and keep it updated as level design rules and balancing decisions are made.

`Docs/pre_development_checklist.md` holds the scaffolding checklist — the concrete scripts, ScriptableObjects, prefabs, scenes, events, and config work needed before/while implementing `Assets/Game/CurrentGame/`. Consult it when starting or resuming CurrentGame implementation work, and check items off / update it as scaffolding is completed or design decisions change.

## Working in this codebase

- There is no `.asmdef` for game code — everything under `Assets/Game` compiles into the default `Assembly-CSharp` assembly (no assembly boundaries to respect there).
- There is **no test framework wired up** (no `*Tests.cs`, no test `.asmdef`) and **no CI/CD** (no GitHub Actions, fastlane, etc.). There is no build/lint/test command to run from the CLI — verification means opening the project in the Unity Editor and playing the scene, or reasoning through the code directly.
- `Packages/manifest.json` notable dependencies: `com.cysharp.unitask` (UniTask, used for async SDK calls), `com.unity.render-pipelines.universal` (URP), `com.unity.cinemachine`, `com.unity.splines`, `com.unity.timeline`, plus a `com.coplaydev.unity-mcp` (MCPForUnity) integration and the AppLovin MAX scoped registry for ad mediation.
- `.vscode/settings.json` points `dotnet.defaultSolution` at `PixelLoopBlast.slnx`; C# editing/IntelliSense in this repo is oriented around that solution file, not a generic dotnet project.

## MoowCore architecture

MoowCore has no DI container / service locator. Instead it combines a **global string-keyed pub/sub event bus** with **two singleton base classes** and **ScriptableObject-driven config**. Learn this pattern before touching gameplay code — almost everything communicates through it.

- **Event bus**: `Runtime/Core/Dispatcher/Dispatcher.cs` is a `SingletonDontDestroy<Dispatcher>` holding listeners keyed by event name. Don't call it directly — use the extension methods in `Runtime/Extensions/MoowExtensions.cs` (`this.addListener<T>(Events.X, handler)`, `removeListener`, `dispatchEvent`, `propagateEvent`). `propagateEvent` bubbles an event up a GameObject's transform hierarchy (DOM-style). Event name constants live in the `Events` class in `Scripts/Controller/AppConstants.cs`.
- **Singleton base classes** (`Runtime/Core/*.cs`): `BaseSingleton<T>` is scene-scoped (found via `FindObjectsByType`, dies with the scene) — use for per-scene managers like `GameManager`, `LevelManager`. `SingletonDontDestroy<T>` persists across scene loads (`DontDestroyOnLoad`) — use for app-lifetime services like `Dispatcher`, `ApplicationInitializer`, `AudioManager`. Almost every manager under `Scripts/Managers` inherits one of these two.
- **Managers** (`Scripts/Managers`) are focused, event-reactive domain singletons (e.g. `GameManager`, `AudioManager`, `GameDataManager`, `LevelManager`, `LevelGenerator`, `InventoryManager`, `PowerUpManager`, `CameraManager`, `MoowAnalyticsManager`), typically configured via ScriptableObjects and wired together purely through dispatched events rather than direct references.
- **Scene management**: `Scripts/SceneManagement/SceneManager.cs` (a `BaseSingleton`, name shadows `UnityEngine.SceneManagement.SceneManager` — fully-qualify when both are in scope) does additive load/unload with `LoadingScreenType`/`TransitionType`, progress tracking, and `AddCustomWaitFunction` gating.
- **Ads/SDK layer** (`Scripts/Moow_Ads_Core`, `SDK_Core`): ads use a provider + fallback pattern — `IAdsProvider` interface, `AdsService` (plain C# singleton, not a MonoBehaviour) tries a primary provider then falls back to a secondary and normalizes the result. `AdMobAdsProvider` is currently a stub (always reports ready/success) — real AppLovin/AdMob wiring is not finished. `SDK_Core/SdkConfig.cs` is a single ScriptableObject holding all third-party keys (AppMetrica, AppsFlyer, Facebook, AppLovin, per-platform ad unit IDs) but is just a data holder — most integrations (AppsFlyer, AppMetrica) are declared but not yet called anywhere in MoowCore; Firebase Analytics (`SDK_Core/FirebaseAnalyticsService.cs`) is the one integration actually wired up, using UniTask.
- **UI**: no broad custom widget framework — popups implement the minimal `IPopup` interface (`Scripts/Interfaces`). `ITriggerable` is the standard collider-trigger contract used across gameplay.

When extending or reading gameplay logic, expect it to hook in via `addListener`/`dispatchEvent` on well-known `Events.*` names rather than direct method calls between systems — search for the relevant event name across the codebase to find all participants before assuming you've found every caller.
