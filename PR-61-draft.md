# Fix high-value stability bug (#61)

**Branch:** `squad/61-fix-high-value-bug`  
**Suggested assignee:** Oskar  
**Issue:** Closes #61

## Summary

Det här PR:et fixar en högvärdig stabilitetsbugg som påverkar spelarupplevelsen. Fokus är att reproducera felet tydligt, åtgärda root cause och verifiera att Android-bygget fortfarande går grönt utan regressions.

## Why this first

Stabilitetsproblem går före polish och spelkänsla. Innan vi finlirrar med lull-lull behöver spelet stå stadigt, annars blir det pannkaka av hela kalaset.

## Scope

- Reproducera buggen med tydliga steg
- Identifiera root cause
- Implementera fix
- Verifiera med build och smoke-test
- Dokumentera före/efter i PR

## Acceptance criteria

- [ ] Buggen är reproducerad med dokumenterade steg
- [ ] Buggen går inte längre att reproducera efter fix
- [ ] Root cause är beskriven kort och tydligt
- [ ] Android build lyckas: `dotnet build HKA_Handball/HKA_Handball.csproj -f net10.0-android -c Debug`
- [ ] Relevanta manuella verifieringssteg är dokumenterade i PR

## Verification

### Reproduction

1. Dokumentera exakt scenario här
2. Lägg till device/OS-version här
3. Lägg till eventuella loggar eller felmeddelanden här

### After fix

1. Bekräfta att samma scenario inte längre orsakar felet
2. Kör smoke-test på berörd del av spelet
3. Bekräfta att Android-bygget fortfarande lyckas

## Root cause

_Fyll i efter implementation._  
Beskriv kort varför felet uppstod och varför fixen är rätt.

## Risk check

- Ingen telemetry
- Ingen nätverkskod
- Offline-first bevaras
- Handbollsregler och gameplay ska fortsatt kännas autentiska

## Reviewer notes

- Be om review från Lisa om fixen påverkar core game flow eller arkitektur
- Be om review från Kalle om buggen kräver extra verifieringssteg

