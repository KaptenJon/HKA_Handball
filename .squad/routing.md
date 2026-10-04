# Arbetsrouting

Så avgör vi vem som tar vad på planen.

> Visningsnamn finns i kolumnen `Display Name` i team.md och visas i svenska `name-roll`-format för människor. Kolumnen `Name`, routing, agentmappar och issue-etiketter använder stabila interna ID:n som `lisa`, `oskar`, `maja`, `kalle`, `emil`, `anna`, `sara`, `johan` och `axel`.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Arkitektur, systemdesign och större refaktorplaner | axel | MAUI-struktur, modulgränser, GamePage-flöden, trade-offs före stora ändringar |
| Triage, scope och större kodbeslut | lisa | Issue-triage, prioriteringar, ägarskap och sista ordet när kodspår krockar |
| Backend, integrationer och spel-/domänlogik | oskar | API:er, datalager, kärnlogik, buggar i core-flöden |
| Frontend, UI och tillgänglighet | maja | Views, layout, inputflöden, UI-polish |
| Teststrategi och releaseverifiering | kalle | Testplaner, regressioner, flaky tests, verifieringssteg |
| CI/CD, byggkedja, deploy och drift | emil | Pipelines, releaseflöden, rollback, observability |
| Dokumentation och release notes | anna | README, changelog, onboarding, användarguider |
| Dataunderlag och analys | sara | Metrics, dashboards, datakvalitet, uppföljning |
| Säkerhetsgranskning och riskbedömning | johan | Auth, secrets, threat modelling, incidentråd |
| Kodgranskning | lisa | Review av större diffar, designpåverkan, tvärsnittsrisker |
| Testning | kalle | Skriva tester, hitta edge cases, verifiera fixes |
| Scope och prioriteringar | lisa | Vad som byggs härnäst, trade-offs, beslut |
| RAI-granskning | rai | Content safety, bias checks, credential detection, ethical review |
| Claim verification, hallucination detection, counter-hypothesis analysis, source validation | fact-checker | Verifiera påståenden och källor |
| PR triage, technical decisions | lisa | PR-triage och tekniska beslut |
| Code review, regression risks | kalle | Review och regressionsrisker |
| Handball mechanics, UX impact | maja | Spelmekanikens UX-effekter |
| Priorities, validation, next steps | lisa | Prioriteringar och verifiering |
| Spelmekanik, MAUI, handboll | oskar | Speldomän och MAUI |
| C#, tillstånd, AI | lisa | Kod och tillstånd |
| XAML, touch, UX | maja | Gränssnitt och input |
| Regler, fysik, taktik | axel | Designunderlag för spelregler |

## Teamform

1. **Tre kodspår, inte fler.** Primär implementation routas alltid till `lisa`, `oskar` eller `maja`.
2. **Stödteamet förstärker utan att ta över kodspåret.** QA, drift, docs, data, säkerhet och arkitektur ger underlag, granskning och verifiering; om kod måste skrivas på deras område samordnas det via en kodspecialist.
3. **Arkitektur kan routas separat utan att skapa ett fjärde kodspår.** `axel` leder större systemskisser, MAUI-upplägg och refaktorplaner, medan implementation fortfarande landar hos `lisa`, `oskar` eller `maja`.
4. **Visningsnamn är för människor; interna ID:n är för stabilitet.** Behåll `squad:lisa`, `squad:oskar`, `squad:axel`, osv. även om rostertexten ljuder mer som ett göteborgskt handbollslag.

## Issue-routning

| Label | Åtgärd | Vem |
|-------|--------|-----|
| `squad` | Triage: analysera issue, välj huvudägare och sätt `squad:{intern-id}` | lisa-lagkaptenen (`lisa`) |
| `squad:{intern-id}` | Ta ägarskap för issuet inom det egna ansvarsområdet | Medlemmen bakom det stabila interna ID:t |

### Så funkar issue-tilldelning

1. När en GitHub issue får etiketten `squad` triagerar **lisa-lagkaptenen** innehållet, sätter huvudägare och kommenterar kort varför.
2. Själva etiketten använder alltid det stabila interna ID:t, till exempel `squad:oskar`, `squad:kalle` eller `squad:axel`, även om visningsnamnet i team.md är mer personligt.
3. Om en issue behöver stöd från QA, drift, dokumentation, data, säkerhet eller arkitektur skrivs det i triage-kommentaren, men huvudetiketten pekar fortfarande på en ansvarig ägare.
4. Medlemmar kan föreslå omrouting genom att be **lisa-lagkaptenen** byta huvudägare när scope har glidit.

## Regler

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **När två personer kan ta jobbet**, välj kodspecialist för implementation, `axel` för arkitekturritningen och övrigt stödteam för kontrollspåret.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn the tester to write test cases from requirements simultaneously.
7. **Issue-labeled work** — `squad` triageras av lisa-lagkaptenen; `squad:{intern-id}` routar till ägaren bakom det stabila interna ID:t.
