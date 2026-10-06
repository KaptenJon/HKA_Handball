# HKA Handball 🏐

A free, offline handball game built with .NET MAUI.

## Features

- 🎮 **Touch & keyboard controls** – joystick, pass, shoot, defend
- 🏐 **Handball rules** – goal area, free throws, goalkeeper saves
- 📱 **Fully offline** – no internet, no ads, no data collection
- 🆓 **100% free** – open source, no in-app purchases
- 🏟️ **Indoor arena presentation** – maple court, painted goal areas, striped
  goals, shaded player kits and ball, and compact high-contrast match controls
- **TV camera** – an elevated long-side view with upright, depth-sorted players.
  The camera button cycles between **TV fast** (the whole court), **TV följ**
  (a closer view that smoothly follows the ball) and **plan** (a flat tactical
  view), without changing play. The following view widens to keep controlled
  defenders and the ball visible. Panning stops while the match is paused.

The court keeps the same 2:1 proportions and simulation coordinates on every
screen. Landscape Android phones are the primary layout: the camera fits the
court across the available screen below the scoreboard, with only a small edge
margin. Touch controls overlay the court instead of reserving space beside or
below it, in both single-player and local two-player matches. The aiming goal
keeps its size, and all three camera modes use the larger playing area.
Upright players, ball, shadows, selection rings and raised TV goal frames use
the court camera's uniform world scale, so zooming or resizing does not change
their proportions relative to the court. Jersey-number legibility is bounded
within the jersey; HUD and touch controls remain independent of camera zoom.
Running animation follows actual
movement speed: field players run with lowered, bent-elbow arm swings and
opposing leg strides; defensive guards return when slowing down. Goalkeepers
retain their raised blocking stance. Court primitives, clipping and player
positions share the same DIP-to-pixel camera projection on Android.
Android camera matrices are applied directly to the native canvas, preserving
X/Y translation and the view transform for court clipping and raised goals.
Following clamps the projected arena bounds and recentres after a possession
jump if smooth panning would temporarily hide the ball or a controlled player.
In the TV views, raised goal frames and net roofs add depth
above the floor. Fixed simulation steps catch up after brief dropped frames.

The menu's team-colour palettes wrap on smaller screens. Match controls resize
for compact landscape windows and local two-player play. White markers identify
the ball carrier; gold markers identify the controlled defender. The mini goal
labelled **SIKTA / SKJUT** spans both pass-button columns and keeps the goal's
3:2 proportions. Taps map to the visible posts (left/centre/right), including
when the goal is centred within a compact control. Match notices focus on goals,
free throws, penalties, suspensions and meaningful turnovers or restarts.
Routine possession, dribbling, pass and shot narration stays out of the HUD;
rule warnings and necessary action prompts remain visible.

### Ball handling

The ball carrier automatically dribbles continuously while moving, for both teams
and in single-player or two-player local mode. After stopping briefly and picking
up the ball, the carrier cannot dribble again until a new catch or possession.
They may take up to three steps and hold the ball for at most three seconds before
passing or shooting. Pass and shoot controls remain available during dribbling.

### Movement and positioning

Automatic field-player running uses a shared speed limit and smooth acceleration
and braking, rather than moving faster merely because the destination is farther
away. Fast breaks and pass-receiving runs retain their boosts, and manual controls
remain immediate. Releasing manual input hands the player's current movement
speed back to the automatic movement.

Attacking roles and lanes stay fixed when the ball carrier changes or a teammate
is suspended. Wings stay wide, backs retain their lanes, and the blue pivot keeps
a central position instead of continuously oscillating. Players take straight
routes to their positions whenever legal, with a short detour only when needed
to avoid a goal area. Passing no longer sends the passer toward a different lane.

### Team defense

Both blue (home) and red (away) defend in a stable **3–3 formation**: three field
players stay close to, but outside, the six-metre goal area, with three further forward.
Both rows shift together toward the ball side. Only one front-row defender
applies limited pressure, while the others retain their lanes rather than
swarming the same attacker. Players return to their defensive roles after
possession changes and restarts; suspensions leave the remaining roles intact.
The selected blue defender remains manually controllable in single-player mode,
and both teams' selected defenders remain controllable in local two-player mode.
Goalkeeper behaviour is unchanged.

## Download

[![Google Play](https://img.shields.io/badge/Google%20Play-Download-green?logo=google-play)](https://play.google.com/store/apps/details?id=com.kaptenjon.hkahandball)

## Build

### Android versioning when chat tasks are complete

**When the chat's tasks are complete ("när chattens uppgifter är klara")**, and
the completed work batch changes the Android app, increment the integer
`ApplicationVersion` in `HKA_Handball\HKA_Handball.csproj` by exactly **1** from
its latest value, **once per completed work batch**. Never reset, decrease or
reuse a build version.

At that completion, explicitly write a valid, suitable human-facing app version
number in `ApplicationDisplayVersion` in the same `.csproj` (for example, `1.12.1`).
Do not increment for each validation build or retry. Questions,
documentation-only tasks and Windows-only changes do not trigger this update.
These are developer/agent responsibilities, not automated version updates.

```bash
# Debug (Android)
dotnet build -f net10.0-android

# Release AAB for Google Play
dotnet publish -f net10.0-android -c Release \
  -p:AndroidSigningStorePass=YOUR_PASSWORD \
  -p:AndroidSigningKeyPass=YOUR_PASSWORD
```

### First-time setup

1. Generate a signing keystore: `bash generate-keystore.sh` (or `pwsh generate-keystore.ps1`)
2. Keep the `.keystore` file safe – you need it for every Play Store update

See **[RELEASING.md](RELEASING.md)** for the full step-by-step guide to sign,
build, and publish to Google Play (including how to add the signing key to
GitHub Actions).

## UI visual verification

**All UI changes must be tested visually in the running app.** Open the affected
views and exercise the changed interactions at representative screen/window
sizes, including a compact layout where relevant.

Keep checks focused on changed behavior/views and representative relevant
formats. Reuse still-valid prior evidence for unchanged systems; do not rerun
full game/system or multi-viewport suites after every edit. Expand coverage only
when change risk or a targeted failure warrants it. Changed UI must still be
visually verified in the running app; disclose blockers and pre-existing failures.

Use an Android device or emulator when available. If neither is available, run
the Windows app for visual runtime inspection. Windows verification is useful,
but does not establish Android-device correctness. Successful builds and
headless layout/geometry checks alone do **not** satisfy this requirement.

Report the platform, screen/window sizes, views and interactions inspected, and
the results in the change summary or PR. Identify unverified targets/platforms
and blockers explicitly. If runtime inspection cannot be performed, report
visual verification as incomplete, not passed. This requirement does not change
the rule against adding test projects unless explicitly requested.

## Privacy

This app collects **no data**. See [Privacy Policy](PRIVACY_POLICY.md).

## Squad issue automation

New issues are triaged and assigned to Copilot with the compact **Squad Cloud**
coordinator. Squad keeps the specialist owner (`squad:lisa`, `squad:oskar`,
`squad:maja`, etc.), delegates planning, implementation and review to the existing
custom agents, and creates a draft PR. The interactive `squad.agent.md` is too
large for GitHub's 30,000-character cloud profile limit.

### Repository setup

1. Enable Copilot cloud agent for this repository.
2. Create an **Actions secret** named `COPILOT_ASSIGN_TOKEN` using a user token
   belonging to a maintainer with Copilot access. For a fine-grained PAT, select
   this repository and grant Metadata read plus Actions, Contents, Issues and
   Pull requests read/write. Installation tokens and the default `GITHUB_TOKEN`
   cannot substitute for the user token when assigning work to Copilot.
   Never paste the token into an issue, chat, source file or agent prompt.
3. Enable repository **Allow auto-merge** and protect `master` with at least one
   approving review, dismissal of stale approvals, and the required
   `build-android` and `automation-contracts` checks. Keep the existing CodeQL
   and code-quality rules.

A human reviews the draft, approves workflow runs if GitHub requests approval,
and marks the PR ready. Squad Reviewed Auto-Merge then enables native squash
auto-merge for same-repository Copilot PRs closing a Squad issue. GitHub merges
only when the branch rules are satisfied; a new commit invalidates stale
approvals. An agent review is not a substitute for the required human approval.

All new issues enter triage. In this public repository, issues opened by people
without write access wait for a maintainer to add a `squad` or `squad:*` label
before paid agent execution. Weekly issues dispatch the pipeline directly;
labels added by `GITHUB_TOKEN` are not used to trigger a second workflow.

To pause work, add `go:no`, `go:blocked` or `go:needs-human`. To retry a failed
kickoff or process an existing issue such as #111, use **Actions -> Issue Agent
Pipeline -> Run workflow** and supply `issue_number`. Existing Copilot assignees
prevent duplicate starts; retries update a single execution-status comment.
Assignment verification, heartbeat retries and PR author checks share the same
case-insensitive Copilot identity check, including the REST login `Copilot`.
Retrying an already-assigned issue corrects any old false kickoff-failure comment
without starting another session.
Ralph's heartbeat also re-dispatches open, unassigned Squad issues. Issues from
external contributors still need a maintainer-triggered retry if kickoff fails.

Workflow script validation (no .NET test project is needed):

Checkout, GitHub Script, Setup Node and Pages actions in active workflows and
installed Squad templates are pinned to verified Node 24 releases. This action
runtime is separate from the Node version selected for project commands. The
contract checks reject deprecated action pins so the Node 20 warnings cannot be
reintroduced by a template update.

```powershell
node --test .github\scripts\squad-automation.test.cjs
```

## Security

To report a vulnerability, please see [Security Policy](SECURITY.md).