# UI and UX quality goal

Updated 11 November 2026. The owner requested autonomous improvement, dynamic verification and reassessment until the mean reaches 9.5/10. The goal is active. The app-only boundaries in [BUILD-PLAN.md](BUILD-PLAN.md) still apply.

## Fixed scorecard

These are reviewer judgments, supported by evidence, rather than benchmark measurements. Keep the same six dimensions and equal weighting between reviews. Do not add a high-scoring dimension to raise the mean, treat a passing test count as a design score, or round a result below 9.5 up to the target.

| Dimension | Baseline | Cycle 1 review | Evidence and remaining limit |
|---|---:|---:|---|
| UI design | 8.4 | 8.7 | Constellation composition, typography and themes; clearer action hierarchy and native accent alignment |
| UX | 8.0 | 8.8 | Direct attention review, an empty-home setup route and retry-first recovery |
| Usability | 8.1 | 9.0 | Native keyboard/pointer tests establish retained actions, correct Story dates and predictable focus |
| Design fidelity | 8.5 | 8.7 | Approved identity and saved section order remain; phone attention uses a direct shortcut rather than an enforced layout change |
| Motion and switching | 8.0 | 8.3 | Intermediate frames, overlapping navigation and cancellation are tested; device timing and touch feel remain unmeasured |
| Accessibility | 7.8 | 8.5 | Focus continuity, large-text adaptation and 48-unit targets; screen-reader traversal and announcements need device evidence |
| **Mean** | **8.13** | **8.67** | **Target not achieved** |

The cycle-1 scores were recorded at the first combined visual inspection. Final empty-state corrections and test confirmation do not automatically earn additional points. A subsequent review must justify any increase.

Baseline Assessment A and technical Assessment B ran independently. The technical reviewer found a real Story cache-identity bug; native input reproduced it before the fix. The web detector returned no findings because it supports neither AXAML nor C#. It scanned zero applicable native files and provides no native quality claim. A second source review found no further concrete P0/P1/P2 regression in the implemented changes.

## Correctness and responsiveness gates

Scores cannot hide a broken primary task. Before accepting the target, require zero known P0/P1 defects in the reviewed app scope, working recovery and cancellation, truthful values and provenance, reachable native input equivalents, no clipping of essential controls, and no remaining untested claim presented as fact.

Dynamic verification covers real headless native input and rendered frames. It includes presses during report refresh, keyboard selection after reordering, Story chapter activation after consecutive date changes, report filtering, live Bloom text-size changes, hidden holds and overlays, setup entry/return, connection-removal cancellation, and navigation interrupted every 25 ms. It also retains the value/progress/arrival frame tests from [UI-UX-PLAN.md](UI-UX-PLAN.md).

Cycle-1 final verification passed 712 native cases with 3 existing opt-in wire skips and no failures, including 35 new cases. Android builds with zero warnings/errors. A further eight-case check uses actual Tab navigation to reach setup and verifies its target is in view on phone/tablet, both themes and normal/doubled text. The combined native capture review confirmed the final empty/setup, recovery and accent states. The initial full run found a tablet viewport regression from the new shortcut; placing it beside the headline fixed the regression without weakening that check.

The 16.7 ms frame budget at 60 Hz remains a measurement target. Headless execution does not establish hardware latency, GPU frame pacing, startup time, memory behavior or Android touch capture. The Windows automation helper was unavailable in this session; no desktop window was driven through it. ADB listed no connected devices. Native screenshots are native renders, not physical-device evidence.

## Implemented in cycle 1

- Retain uniquely identified attention buttons and their internal text controls. Healthy report changes preserve focus and a press in progress. Removed focused entries use a deliberate remaining-row or heading fallback.
- Make the attention headline actionable through Review reports, without changing saved Home section order. The tablet shortcut sits beside the headline to preserve its first viewport.
- Offer Set up places directly on empty Home, use a setup headline and omit instructions for nonexistent graph controls. Entry and return preserve keyboard focus and the existing draft/save safeguards.
- Lead connection recovery with retry. Disclose removal under Connection options, retain exact consequences and existing confirmations, and exclude collapsed options from focus immediately.
- Bind Story chapter caching to the actual day model. Retain a selected report's timeline when filtering rather than detach its focused button.
- Transfer Bloom focus to the corresponding visible action after a live text-size change. Guard deferred focus against navigation, hiding and intervening user focus.
- Cancel pending holds and close overlays immediately when Home hides. Hidden button feedback settles and clears old hover state.
- Apply the approved light/dark accent to native controls, raise Day/Week targets to 48 logical units, and remove a duplicated empty-reading explanation.

## Next review

Review further evidence against this same scorecard. The immediate qualification need is an Android phone/tablet for touch, TalkBack, safe areas, system Back and frame-time checks, or a guided manual review with recorded results. The owner has been asked which is available. Keep the goal active; do not replace missing evidence with decorative animation or a manufactured 9.5 score.

Connected history, richer service stages, Engine room and the v2 identity port remain separately gated. This quality goal does not authorize changing those dependencies.
