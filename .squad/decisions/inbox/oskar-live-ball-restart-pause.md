### 2026-08-09: Split live-ball turnovers from restart pauses
**By:** Oskar
**What:** Removed the generic restart pause from `GiveBallToPlayer`/`GiveBallToOpponent` and now apply `ApplyRestartPause()` only at dead-ball restart handoffs such as passive play, goal-area violations, and missed shots.
**Why:** Interceptions, steals, pass breaks, and goalkeeper saves should stay live for more authentic handball flow, while whistle/restart situations still need the short restart pause.
