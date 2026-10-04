# HKA Handball 🏐

A free, offline handball game built with .NET MAUI.

## Features

- 🎮 **Touch & keyboard controls** – joystick, pass, shoot, defend
- 🏐 **Handball rules** – goal area, free throws, goalkeeper saves
- 📱 **Fully offline** – no internet, no ads, no data collection
- 🆓 **100% free** – open source, no in-app purchases

## Download

[![Google Play](https://img.shields.io/badge/Google%20Play-Download-green?logo=google-play)](https://play.google.com/store/apps/details?id=com.kaptenjon.hkahandball)

## Build

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