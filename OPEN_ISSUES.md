# Open issue triage

This is the current open-issue overview for `KaptenJon/HKA_Handball`. It was
collected with the GitHub CLI on **2026-09-06**:

```text
gh issue list --repo KaptenJon/HKA_Handball --state open --limit 100
```

The issue state below describes GitHub's issue state. The pipeline notes
describe the latest automation comments and do not mean that implementation
has started.

## Snapshot

There are **seven open issues**. All are unassigned. There is one unrelated
open pull request (#23); no implementation pull request is referenced by the
issues below.

| Priority | Issue | Status | Pipeline / triage status |
| --- | --- | --- | --- |
| P1 | [#73 Fix away possessions not resetting passive-play timer on passes](https://github.com/KaptenJon/HKA_Handball/issues/73) | Open, `bug` | Concrete, narrowly scoped gameplay bug. The automated pipeline stopped at handball-context validation because its validator reported no clear handball context, despite the issue explicitly describing possessions and passive play. |
| P1 | [#71 Implement legal ball-handling violations for the ball carrier](https://github.com/KaptenJon/HKA_Handball/issues/71) | Open, `enhancement` | Foundational gameplay/rules work. Validation passed, but the implementation-agent API failed; the issue has no assignee or implementation PR. The `squad:oskar` label also did not resolve to a squad member. |
| P1 | [#72 Add whistle-based free-throw restarts and feedback for possession violations](https://github.com/KaptenJon/HKA_Handball/issues/72) | Open, `enhancement` | Depends on the violation model in #71. Validation passed, but the implementation-agent API failed; the issue has no assignee or implementation PR. The `squad:lisa` label also did not resolve to a squad member. |
| P2 | [#79 Weekly Improvement 2026-W35](https://github.com/KaptenJon/HKA_Handball/issues/79) | Open, `weekly-improvement` | Generic game-flow theme (possession transitions, controls, AI pacing). Validation and preflight passed, but the implementation-agent API failed. No concrete candidate has been selected. |
| P2 | [#75 Weekly Improvement 2026-W33](https://github.com/KaptenJon/HKA_Handball/issues/75) | Open, `weekly-improvement` | Generic bug-fixing theme. Validation and preflight passed, but the implementation-agent API failed. No concrete bug has been selected. |
| P2 | [#81 Weekly Improvement 2026-W36](https://github.com/KaptenJon/HKA_Handball/issues/81) | Open, `weekly-improvement` | Generic bug-fixing theme. Validation and preflight passed, but the implementation-agent API failed. No concrete bug has been selected. |
| P3 | [#78 Weekly Improvement 2026-W34](https://github.com/KaptenJon/HKA_Handball/issues/78) | Open, `weekly-improvement` | Generic visual-polish theme. Validation and preflight passed, but the implementation-agent API failed. No concrete UI change has been selected. |

## Recommended order

1. **Re-run or manually triage #73.** It has a specific acceptance criterion,
   a small likely change surface, and a direct player-experience impact. Fixing
   it first also provides a low-risk way to verify the passive-play timer for
   both teams.
2. **Design and implement #71 before #72.** Ball-carrier legality is the
   prerequisite state model. Define the step, hold-time, and dribble/re-control
   transitions and their reset points before adding whistle feedback and
   free-throw placement.
3. **Convert one weekly issue into a concrete task, rather than treating
   #75/#78/#79/#81 as four independent features.** #75 and #81 overlap as
   bug-fixing umbrellas; #79 overlaps with the restart/possession work in
   #71/#72; and #78 is lower priority until a specific UI target is named.
   Close or supersede duplicate weekly issues once a concrete child task is
   selected.

## Patterns and follow-up

- **Automation is the immediate bottleneck:** every issue that passed
  validation reports a failed implementation-agent API call. The fallback
  comments ask for manual agent invocation, so the issues remain open without
  an owner or PR.
- **Squad labels are stale:** #71, #72, and #73 reference squad labels that
  the pipeline could not resolve. Label maintenance or a documented fallback
  owner is needed before relying on automatic assignment.
- **Validation needs handball-aware wording:** #73 is clearly about
  handball possession and passive play, but the validator rejected it. The
  validator should recognize the game's established terminology, or the issue
  should be reworded before reopening the pipeline.
- **Weekly issues need deduplication:** recurring themed issues are useful
  intake buckets, but they currently have no selected deliverable. A single
  concrete issue, owner, and acceptance test should be recorded before
  implementation.

This document is a status snapshot, not a claim that any issue has been
implemented. Re-run the `gh issue list` and issue timeline checks before using
it as a later planning baseline.
