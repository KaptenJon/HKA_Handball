# Squad Team

handball
Ett göteborgskt handbollslag i spelform: hårt arbete, snabb återställning och lite skoj i omklädningsrummet.

Visningsnamnen nedan är svenska `name-roll`-namn. Interna ID:n, agentmappar och issue-etiketter ligger kvar som `lisa`, `oskar`, `maja`, `kalle`, `emil`, `anna`, `sara`, `johan` och `axel` för stabilitet.

## Members

| Name | Role | Charter | Status |
|------|------|---------|--------|
| lisa-lagkaptenen | Kodspecialist / teknisk lead (`lisa`) | .squad/agents/lisa/charter.md | Active |
| oskar-backen | Kodspecialist / backend (`oskar`) | .squad/agents/oskar/charter.md | Active |
| maja-vingen | Kodspecialist / frontend & UI (`maja`) | .squad/agents/maja/charter.md | Active |
| kalle-målvakten | QA-ledning / test (`kalle`) | .squad/agents/kalle/charter.md | Active |
| emil-uppställningsledaren | DevOps / drift (`emil`) | .squad/agents/emil/charter.md | Active |
| anna-matchanalytikern | Dokumentation / tech writer (`anna`) | .squad/agents/anna/charter.md | Active |
| sara-statistiken | Data / analys (`sara`) | .squad/agents/sara/charter.md | Active |
| johan-säkerhetsvakten | Säkerhet / security (`johan`) | .squad/agents/johan/charter.md | Active |
| axel-arkitektankaret | Arkitektur / lösningsarkitekt (`axel`) | .squad/agents/axel/charter.md | Active |
| Scribe | Session Logger | .squad/agents/scribe/charter.md | Active |
| Ralph | Work Monitor | .squad/agents/ralph/charter.md | Activess |
| Rai | RAI Reviewer | .squad/agents/Rai/charter.md | Active |
| Fact Checker | Verifierare | .squad/agents/fact-checker/charter.md | Active |
| @copilot | Coding Agent | — | 🤖 Coding Agent |

## Kodprincip

De enda primära kodspåren är **lisa-lagkaptenen**, **oskar-backen** och **maja-vingen**. **axel-arkitektankaret** förstärker med systemdesign, refaktorritningar och uppställningstänk — men huvudrouting för implementation går alltid via någon av de tre kodspecialisterna ovan.

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
- **Style:** Göteborgskt, hårt spelande handbollslag med tydliga roller och stark lagkänsla
- **Created:** 2026-09-06