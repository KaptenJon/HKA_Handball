# Arbetsrouting

Så avgör vi vem som tar vad.

> Visningsnamn visas i svenska `name-roll`-format för människor. Routing, agentmappar och issue-etiketter använder fortsatt stabila interna ID:n som `lisa`, `oskar` och `maja`.

## Routningstabell

| Arbetsområde | Går till | Exempel |
|-----------|----------|----------|
| Arkitektur, triage och större kodbeslut | lisa-lösningslotsen (`lisa`) | Scope, trade-offs, systemskisser, ägarskap för större ändringar |
| Backend, integrationer och spel-/domänlogik | oskar-bakändebossen (`oskar`) | API:er, datalager, kärnlogik, buggar i core-flöden |
| Frontend, UI och tillgänglighet | maja-gränssnittsglansen (`maja`) | Views, layout, inputflöden, UI-polish |
| Teststrategi och releaseverifiering | kalle-kvalitetskollen (`kalle`) | Testplaner, regressioner, flaky tests, verifieringssteg |
| CI/CD, byggkedja, deploy och drift | emil-driftdirektören (`emil`) | Pipelines, releaseflöden, rollback, observability |
| Dokumentation och release notes | anna-doklotsen (`anna`) | README, changelog, onboarding, användarguider |
| Dataunderlag och analys | sara-sifferstyrman (`sara`) | Metrics, dashboards, datakvalitet, uppföljning |
| Säkerhetsgranskning och riskbedömning | johan-säkerhetsvakten (`johan`) | Auth, secrets, threat modelling, incidentråd |
| Kodgranskning | lisa-lösningslotsen (`lisa`) | Review av större diffar, designpåverkan, tvärsnittsrisker |
| Testning | kalle-kvalitetskollen (`kalle`) | Skriva tester, hitta edge cases, verifiera fixes |
| Scope och prioriteringar | lisa-lösningslotsen (`lisa`) | Vad som byggs härnäst, trade-offs, beslut |
| Sessionsloggning | Scribe | Automatic — never needs routing |
| RAI-granskning | Rai | Content safety, bias checks, credential detection, ethical review |

## Teamform

1. **Tre kodspår, inte fler.** Primär implementation routas alltid till `lisa`, `oskar` eller `maja`.
2. **Stödteamet förstärker utan att ta över kodspåret.** QA, drift, docs, data och säkerhet ger underlag, granskning och verifiering; om kod måste skrivas på deras område samordnas det via en kodspecialist.
3. **Visningsnamn är för människor; interna ID:n är för stabilitet.** Behåll `squad:lisa`, `squad:oskar`, osv. även om rostertexten är mer göteborgsk.

## Issue-routning

| Label | Åtgärd | Vem |
|-------|--------|-----|
| `squad` | Triage: analysera issue, välj huvudägare och sätt `squad:{intern-id}` | lisa-lösningslotsen (`lisa`) |
| `squad:{intern-id}` | Ta ägarskap för issuet inom det egna ansvarsområdet | Medlemmen bakom det stabila interna ID:t |

### Så funkar issue-tilldelning

1. När en GitHub issue får etiketten `squad` triagerar **lisa-lösningslotsen** innehållet, sätter huvudägare och kommenterar kort varför.
2. Själva etiketten använder alltid det stabila interna ID:t, till exempel `squad:oskar` eller `squad:kalle`, även om visningsnamnet i team.md är mer personligt.
3. Om en issue behöver stöd från QA, drift, dokumentation, data eller säkerhet skrivs det i triage-kommentaren, men huvudetiketten pekar fortfarande på en ansvarig ägare.
4. Medlemmar kan föreslå omrouting genom att be **lisa-lösningslotsen** byta huvudägare när scope har glidit.

## Regler

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **När två personer kan ta jobbet**, välj kodspecialist för implementation och stödteamet för kontrollspåret.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn the tester to write test cases from requirements simultaneously.
7. **Issue-labeled work** — `squad` triageras av lisa-lösningslotsen; `squad:{intern-id}` routar till ägaren bakom det stabila interna ID:t.