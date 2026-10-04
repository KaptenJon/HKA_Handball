# Squad Team

handball
Ett göteborgskt handbollslag i spelform: hårt arbete, snabb återställning och lite skoj i omklädningsrummet.

Visningsnamnen nedan är svenska `name-roll`-namn. Kolumnen `Name` innehåller stabila interna ID:n för Squad, agentmappar och issue-etiketter; `Display Name` innehåller namnen som används i användarvänd kommunikation.

## Members

| Name | Role | Charter | Status | Display Name |
|------|------|---------|--------|--------------|
| lisa | Kodspecialist / teknisk lead | .squad/agents/lisa/charter.md | Active | lisa-lagkaptenen |
| oskar | Kodspecialist / backend | .squad/agents/oskar/charter.md | Active | oskar-backen |
| maja | Kodspecialist / frontend & UI | .squad/agents/maja/charter.md | Active | maja-vingen |
| kalle | QA-ledning / test | .squad/agents/kalle/charter.md | Active | kalle-målvakten |
| emil | DevOps / drift | .squad/agents/emil/charter.md | Active | emil-uppställningsledaren |
| anna | Dokumentation / tech writer | .squad/agents/anna/charter.md | Active | anna-matchanalytikern |
| sara | Data / analys | .squad/agents/sara/charter.md | Active | sara-statistiken |
| johan | Säkerhet / security | .squad/agents/johan/charter.md | Active | johan-säkerhetsvakten |
| axel | Arkitektur / lösningsarkitekt | .squad/agents/axel/charter.md | Active | axel-arkitektankaret |
| scribe | Session Logger | .squad/agents/scribe/charter.md | Active | Scribe |
| ralph | Work Monitor | .squad/agents/ralph/charter.md | Active | Ralph |
| rai | RAI Reviewer | .squad/agents/Rai/charter.md | Active | Rai |
| fact-checker | Verifierare | .squad/agents/fact-checker/charter.md | Active | Fact Checker |

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

## Model assignments

The CLI coordinator uses `.squad/config.json` for per-member selection; the
charters mirror those preferences. GitHub cloud agents use the `model` field in
their `.github/agents/*.agent.md` frontmatter. Issue kickoff explicitly requests
the Squad Cloud coordinator's model. Existing sessions are not changed.

| Model | CLI members | Custom-agent profiles |
|-------|-------------|-----------------------|
| `gpt-6.1-sol` | lisa, fact-checker | Squad, Squad Cloud, Issue Planner, Handball Knowledge Validator |
| `gpt-6-sol` | oskar, kalle, emil | Implementation Coder, Review Comment Fixer, Handball Software developer |
| `claude-sonnet-5.5` | maja | UI Polish Coder |
| `claude-opus-5.5` | johan, axel | PR Reviewer |
| `gpt-6-luna` | sara; default for unmapped CLI members | None |
| `claude-haiku-4.5` | anna, scribe, ralph, rai | None |

The selected IDs were verified against the signed-in Copilot model catalog.
Cloud choices also match GitHub's supported cloud-agent model list. The mapping
prioritizes implementation quality and independent review while keeping logging,
documentation and queue monitoring on a lightweight model. Model availability
can change; recheck the live catalog before future reassignment.

## Project Context

- **Project:** handball
- **Style:** Göteborgskt, hårt spelande handbollslag med tydliga roller och stark lagkänsla
- **Created:** 2026-09-06