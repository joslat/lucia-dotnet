# UI and UX quality goal

Updated 11 November 2026. The owner requested autonomous improvement, dynamic verification and reassessment until the mean reaches 9.5/10. The goal is active. The app-only boundaries in [BUILD-PLAN.md](BUILD-PLAN.md) still apply.

## Fixed scorecard

These are reviewer judgments, supported by evidence, rather than benchmark measurements. Keep the same six dimensions and equal weighting between reviews. Do not add a high-scoring dimension to raise the mean, treat a passing test count as a design score, or round a result below 9.5 up to the target.

| Dimension | Baseline | Cycle 1 | Cycle 2 | Cycle 3 | Evidence and remaining limit |
|---|---:|---:|---:|---:|---|
| UI design | 8.4 | 8.7 | 8.7 | 9.0 | Clearer Home reason, contextual storage and readable enlarged Week/rail |
| UX | 8.0 | 8.8 | 9.0 | 9.2 | Local confirmations, global recovery and explicit setup drafts; navigation does not replay old success |
| Usability | 8.1 | 9.0 | 9.2 | 9.4 | Queued saves, retry focus, conflict choices and live text reflow retain working actions |
| Design fidelity | 8.5 | 8.7 | 8.7 | 8.9 | Constellation, saved order, type and semantic brushes remain; rail grows through native measurement |
| Motion and switching | 8.0 | 8.3 | 8.7 | 8.7 | Prior interruption and motion-policy evidence remains; cycle 3 supplies no new frame-pacing qualification |
| Accessibility | 7.8 | 8.5 | 8.9 | 9.1 | Written selected day, correct mode title, whole report words, separated ruler ticks and enlarged rail labels; TalkBack remains deferred |
| **Mean** | **8.13** | **8.67** | **8.87** | **9.05** | **Target not achieved** |

The cycle-1 scores were recorded at the first combined visual inspection. Cycle 2 records the next independent review. Cycle 3 initially scored 8.95; its confirmation review scored 9.05 after enlarged Week and rail defects were corrected. Test counts do not automatically earn points. A subsequent review must justify any increase.

Baseline Assessment A and technical Assessment B ran independently. The technical reviewer found a real Story cache-identity bug; native input reproduced it before the fix. The web detector returned no findings because it supports neither AXAML nor C#. It scanned zero applicable native files and provides no native quality claim. A second source review found no further concrete P0/P1/P2 regression in the implemented changes.

## Correctness and responsiveness gates

Scores cannot hide a broken primary task. Before accepting the target, require zero known P0/P1 defects in the reviewed app scope, working recovery and cancellation, truthful values and provenance, reachable native input equivalents, no clipping of essential controls, and no remaining untested claim presented as fact.

Dynamic verification covers real headless native input and rendered frames. It includes presses during report refresh, keyboard selection after reordering, Story chapter activation after consecutive date changes, report filtering, live Bloom text-size changes, hidden holds and overlays, setup entry/return, connection-removal cancellation, and navigation interrupted every 25 ms. It also retains the value/progress/arrival frame tests from [UI-UX-PLAN.md](UI-UX-PLAN.md).

Cycle-1 final verification passed 712 native cases with 3 existing opt-in wire skips and no failures, including 35 new cases. Android builds with zero warnings/errors. A further eight-case check uses actual Tab navigation to reach setup and verifies its target is in view on phone/tablet, both themes and normal/doubled text. The combined native capture review confirmed the final empty/setup, recovery and accent states. The initial full run found a tablet viewport regression from the new shortcut; placing it beside the headline fixed the regression without weakening that check.

The 16.7 ms frame budget at 60 Hz remains a measurement target. Headless execution does not establish hardware latency, GPU frame pacing or Android touch capture. The Windows automation helper was unavailable; no desktop window was driven through it. Native screenshots are native renders, not physical-device evidence.

## Cycle 2 and Android simulation

The owner deferred physical checks because no device is available and requested simulation. Testing now includes the real APK in an isolated API 36 x86_64 emulator, using synthetic demo data. This is additional runtime evidence; physical qualification remains scheduled for later.

- Place-page entry starts at its own top. Back restores the previous overview offset and visible invoker. Navigation versions prevent delayed callbacks from moving focus into an old destination.
- `CompanionCrossFade` owns the native opacity transitions. Disabling motion settles an active fade immediately; cancellation and rapid reentry cannot hide the newest page.
- Android reads `animator_duration_scale` before controls are created, on resume and through a foreground observer. A zero or invalid scale reduces effects independently of the saved checkbox. More explains the system override without overwriting the personal preference.
- The emulator exposed a cached Android policy read that applied one change late. Reading the current global value directly fixed both the first disable and first reenable. Resume and cold startup with animations disabled also pass.
- With the real soft keyboard enabled and system text at 2.0, setup commands were obscured. `InputPaneViewport` reserves the overlapping keyboard area inside the existing safe-area viewport and scrolls the still-focused field into view after layout. Closing the keyboard restores the full viewport.
- An Android native context request could open the ordinary menu behind Bloom despite the gesture-level holding opt-out. Pending and active Bloom holds now consume that request; ordinary context actions remain available after release.

The native suite passes 734 of 737 cases, with 3 existing opt-in wire skips and no failures. This cycle adds 22 cases. Test-first failures cover page continuity, motion interruption, keyboard overlap and duplicate native menus. The Android build and installed confirmation are recorded with the local evidence ledger.

Runtime checks include phone and expanded layouts, light/dark appearance, Android system text at 2.0, real IME visibility, draft retention/discard, system Back, consecutive Story dates, Week navigation and system animation policy changes. Screenshots, accessibility XML and runtime settings are retained. The hierarchy exposes named actions; it does not prove spoken TalkBack output. Software-rendered emulator startup and sparse framework frame samples do not qualify the app's animation frame budget.

## Cycle 3 and context clarity

Home leads with the highest-priority place's actual report reason. Counts, saved section order, source labels and the no-attention safety qualification remain intact. Story's title follows Day or Week, and its selected day is written in the accessible action name.

Idle storage guidance lives under Your home in More. Setup identifies its draft and names Save setup explicitly. The global notice appears for actual loading or saving, failures, conflicts and unreadable files. Queued saves remain busy until the final checked snapshot settles; intermediate writes still advance the revision. Conflict review cannot overtake a draining queue. Ordinary saves keep navigation and editing available.

Only the latest completed outcome produces a local Home or place-page confirmation. Navigation consumes it without replaying it later. Both conflict choices confirm their actual outcome when returning from the same review. Retry repairs keyboard focus to the current status, then to the selected navigation action when recovery disappears. Session-only changes never claim a durable save.

The review found fixed-width Week text and rail labels breaking at enlarged text. Week now measures its text, keeps report words whole and places the lane on a second row when necessary. Ruler tick density follows available space. The rail retains an 88-unit minimum and grows through native measurement with one-line labels. Live reflow retains focused day actions.

Final native verification passes 754 of 757 cases, with the same 3 opt-in wire skips and 20 new cases. Twelve initial clarity failures and further retry/page/conflict/reflow failures were recorded before their corrections. The first full run hit an existing 100 ms hidden-hold test failure; its isolated run passed. That test now uses the production 450 ms threshold, checks charge clearing immediately on hide, and waits 600 ms beyond the threshold. The final full run passes. Android and installed-emulator results are recorded in the local ledger.

The design confirmation resolved its two material findings, and the technical confirmation found no concrete P0/P1/P2 issue in that fix batch. The review covers the inspected changes; it does not prove the entire goal complete.

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

Continue app-only runtime assessment against the same scorecard, including pending-operation feedback, navigation/value-change motion and compact-window journeys. The cycle-2 findings and cycle-3 confirmation fixes are implemented. Further changes should address observed problems, not add decoration to chase a number. Physical touch, TalkBack, broader safe areas, predictive Back and hardware frame timing remain owner-deferred. Keep the 9.5 goal active until evidence supports it.

Connected history, richer service stages, Engine room and the v2 identity port remain separately gated. This quality goal does not authorize changing those dependencies.
