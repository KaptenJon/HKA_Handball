# Squad Team

> handball

> Visningsnamnen nedan är svenska `name-roll`-namn. Interna ID:n, agentmappar och issue-etiketter ligger kvar som `lisa`, `oskar`, `maja`, `kalle`, `emil`, `anna`, `sara` och `johan` för stabilitet.

## Koordinator

| Name | Role | Notes |
|------|------|-------|
| Squad | Coordinator | Routes work, enforces handoffs and reviewer gates. |

## Kodspecialister

| Visningsnamn | Roll | Charter | Status |
|------|------|---------|--------|
| lisa-lösningslotsen | Kodspecialist / teknisk lead (`lisa`) | .squad/agents/lisa/charter.md | Active |
| oskar-bakändebossen | Kodspecialist / backend (`oskar`) | .squad/agents/oskar/charter.md | Active |
| maja-gränssnittsglansen | Kodspecialist / frontend & UI (`maja`) | .squad/agents/maja/charter.md | Active |

## Professionellt stödteam

| Visningsnamn | Roll | Charter | Status |
|------|------|---------|--------|
| kalle-kvalitetskollen | QA-ledning / test (`kalle`) | .squad/agents/kalle/charter.md | Active |
| emil-driftdirektören | DevOps / drift (`emil`) | .squad/agents/emil/charter.md | Active |
| anna-doklotsen | Dokumentation / tech writer (`anna`) | .squad/agents/anna/charter.md | Active |
| sara-sifferstyrman | Data / analys (`sara`) | .squad/agents/sara/charter.md | Active |
| johan-säkerhetsvakten | Säkerhet / security (`johan`) | .squad/agents/johan/charter.md | Active |

## Built-ins

| Name | Role | Charter | Status |
|------|------|---------|--------|
| Scribe | Session Logger | .squad/agents/scribe/charter.md | Active |
| Ralph | Work Monitor | .squad/agents/ralph/charter.md | Active |
| Rai | RAI Reviewer | .squad/agents/Rai/charter.md | Active |
| Fact Checker | Verifierare | .squad/agents/fact-checker/charter.md | Active |
| @copilot | Coding Agent | — | 🤖 Coding Agent |

## Kodprincip

De enda primära kodspåren är **lisa-lösningslotsen**, **oskar-bakändebossen** och **maja-gränssnittsglansen**. Stödteamet driver kvalitet, drift, dokumentation, analys och säkerhet — men huvudrouting för implementation går alltid via någon av de tre kodspecialisterna ovan.

## Coding Agent

<!-- copilot-auto-assign: false -->

| Name | Role | Charter | Status |
|------|------|---------|--------|
| @copilot | Coding Agent | — | 🤖 Coding Agent |

### Capabilities

**🟢 Good fit — auto-route when enabled:**
- Bug fixes with clear reproduction steps
- Test coverage (adding missing tests, fixing flaky tests)
- Lint/format fixes and code style cleanup
- Dependency updates and version bumps
- Small isolated features with clear specs
- Boilerplate/scaffolding generation
- Documentation fixes and README updates

**🟡 Needs review — route to @copilot but flag for squad member PR review:**
- Medium features with clear specs and acceptance criteria
- Refactoring with existing test coverage
- API endpoint additions following established patterns
- Migration scripts with well-defined schemas

**🔴 Not suitable — route to squad member instead:**
- Architecture decisions and system design
- Multi-system integration requiring coordination
- Ambiguous requirements needing clarification
- Security-critical changes (auth, encryption, access control)
- Performance-critical paths requiring benchmarking
- Changes requiring cross-team discussion

## Project Context

- **Project:** handball
- **Created:** 2026-08-09