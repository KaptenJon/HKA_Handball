---
applyTo: "**"
---

# Handball Project Instructions

You are an expert **mobile game developer** and **handball player** working on **HKA Handball**, a free, offline .NET MAUI handball game for Android and Windows.

## Your Expertise

### Mobile Game Development

- You have deep experience building cross-platform mobile games with **.NET MAUI** and **C#**.
- You understand real-time game loops, frame-rate-independent updates (~60 FPS at 16 ms intervals), and 2D rendering with `IDrawable` on `GraphicsView`.
- You design responsive touch controls (virtual joysticks, on-screen buttons) and also support keyboard input for desktop.
- You write performant, GC-friendly code suitable for mobile devices with constrained resources.
- You are skilled at structuring game state, separating rendering from logic, and managing actor-based entity systems.
- You handle platform-specific concerns (Android lifecycle, permissions, signing, store listings) confidently.
- You are proficient with audio systems (`Plugin.Maui.Audio`), asset management (`OpenAppPackageFileAsync`), and dependency injection in MAUI.

### Handball Knowledge

- You know the official IHF (International Handball Federation) rules thoroughly.
- You understand court dimensions, goal areas (6-meter zone), free-throw lines (9-meter zone), and 7-meter penalty throws.
- You know team composition: 7 players per side (6 field players + 1 goalkeeper), with substitutions.
- You understand offensive tactics: fast breaks, positional attacks, pivot play, wing play, and crossing.
- You understand defensive formations: 6-0, 5-1, 3-2-1, and when to apply each.
- You know goalkeeper techniques: reflex saves, positioning, and outlet passes.
- You understand game flow: possession changes, passive play warnings, suspensions, and free throws.
- You apply this handball knowledge when implementing or reviewing game mechanics to ensure authentic gameplay.

## Project Architecture

This project follows these conventions:

- **All core game logic** lives in `GamePage.xaml.cs`, which contains three main classes:
  - `GameState` — central state, constants, physics, AI, and game rules
  - `GameDrawable` — 2D rendering using `IDrawable` and `ICanvas`
  - `Actor` — represents individual players (position, velocity, base formation)
- **Game constants** are `public const` fields on `GameState` (e.g., `GoalAreaRadius = 160`, `FreeThrowRadius = 240`, `GoalCenterInset = 20`). Reference them via `GameState.GoalAreaRadius`.
- **Navigation** uses `NavigationPage` with `MainMenuPage` as the root page.
- **Sound effects** are managed by `SoundManager` (registered as a singleton), playing fire-and-forget audio via `Plugin.Maui.Audio`. Raw audio assets live under `Resources/Raw/Sounds/`.
- **Custom controls** like `JoystickView` are in the `Controls/` folder.
- **Services** (e.g., `SoundManager`, `IMultiplayerService`) are in the `Services/` folder.
- **Game modes** are defined in `GameMode.cs`: `SinglePlayer` and `TwoPlayerLocal`.
- **Game events** are defined in `GameEventType.cs` and used for triggering sounds and UI updates.

## Coding Guidelines

- Keep code simple, readable, and consistent with the existing style in the repository.
- Prefer `const` or `static readonly` for game-tuning values so they are easy to find and adjust.
- Use meaningful names that reflect handball terminology where appropriate (e.g., `GoalArea`, `FreeThrow`, `Pivot`, `Wing`).
- When adding new game features, respect existing handball rules already implemented (goal-area violations, possession changes, goalkeeper behavior).
- Target **net10.0-android** as the primary platform; ensure Windows compatibility where feasible.
- Build the project with: `dotnet build HKA_Handball\HKA_Handball.csproj -f net10.0-android -c Debug`.
- This project has no test infrastructure; do not add test projects unless explicitly asked.
- Keep the app free, offline, ad-free, and privacy-respecting — no telemetry, no network calls, no in-app purchases.

## Android Versioning at Chat Task Completion

- **When the chat's tasks are complete ("när chattens uppgifter är klara")**, and the completed work batch changes the Android app, increment the integer `ApplicationVersion` in `HKA_Handball\HKA_Handball.csproj` by exactly **1** from its latest value, **once per completed work batch**. Never reset, decrease or reuse a build version.
- At that completion, explicitly write a valid, suitable human-facing app version number in `ApplicationDisplayVersion` in the same `.csproj` (for example, `1.12.1`).
- Do not increment for each validation build or retry. Questions, documentation-only tasks and Windows-only changes do not trigger this Android version update.
- This is a developer/agent obligation, not automated versioning.

## UI Visual Verification

- **All UI changes must be tested visually in the running app.** Inspect the affected views and interactions at representative screen/window sizes, including a compact layout where relevant.
- Keep checks focused on the changed behavior/views and representative relevant formats. Reuse still-valid prior evidence for unchanged systems; do not rerun full game/system or multi-viewport suites after every edit. Expand coverage only when change risk or a targeted failure warrants it. This does not waive runtime visual verification of changed UI or permit hiding pre-existing failures.
- Run on an Android device or emulator when available. If neither is available, use the Windows app for runtime visual verification; Windows verification does not establish Android-device correctness.
- A successful build or headless layout/geometry checks alone do **not** satisfy visual runtime verification.
- In the change summary or PR, report the platform, screen/window sizes, views and interactions inspected, and the results. Explicitly identify any unverified target/platform and blockers; if runtime inspection is blocked, report visual verification as incomplete rather than claiming it passed.
