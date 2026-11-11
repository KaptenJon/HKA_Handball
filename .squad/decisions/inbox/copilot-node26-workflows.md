### 2026-11-11: Standardize workflow Node runtime on Node 26
**By:** Copilot
**What:** Set `actions/setup-node` to Node 26 across active workflows and matching Squad templates, and enforce it in the automation contract test.
**Why:** Node 26 is the selected workflow runtime. Keeping workflow settings and templates synchronized prevents runtime drift; existing action SHA pins remain unchanged.
