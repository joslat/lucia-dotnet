# Start here: companion handoff

Updated 11 November 2026. Read the current UI/UX refinement section and "Direction update, 10 November 2026" in [BUILD-PLAN.md](BUILD-PLAN.md) first; they supersede the old W4 continuation instructions below. The owner keeps a private plan and a live status tracker outside this repository; if they disagree with these files, they win.

**Where it stands.** The W5 app-only track is done: V00 navigation (phone bottom bar, tablet rail), V06 Home header and Needs you, V07 place captions and zones, V08 place page, V02 Bloom actions with hold-slide-release, V09 Story Day/Week and dates, V10 Story now/future and Lucia lane, V11 Today so far and V03 motion with Reduce motion. Three review rounds followed and their fixes are in. History-backed views run on clearly labelled sample data in the demo only; connected mode never falls back to samples. At head the full native suite passes (3 opt-in wire tests skipped) and the Android project builds with 0 warnings and 0 errors.

**What is next.**

The active [UI/UX quality goal](UI-UX-QUALITY.md) targets a 9.5/10 mean on six fixed dimensions. Cycle 6 completes UX12: visible named removal choices, guarded focus handoffs and a clear overview anchor after selected-place removal. The installed confirmation mean is 9.12; keep the goal active. UX13 will restore the approved hold instruction beside Your places. The owner requested simulation and deferred physical checks.

Current native verification passes 801/804, with 3 existing opt-in wire skips and 20 new cases. Android and installed results are in the private cycle-6 ledger. The suite requires a nonzero retained reading offset; disclosure checks never scroll the newly revealed choices from test code. Earlier checkpoints remain historical evidence.

The next assessment should exercise pending-operation feedback, navigation/value-change motion and compact-window journeys, then fix observed problems under the same scorecard. Cycle 3's design and technical confirmation findings are resolved. Do not repeat completed findings or manufacture the remaining score.

The broad presentation replan is now [UI-UX-PLAN.md](UI-UX-PLAN.md), with a complete current screen/state inventory, component hierarchy and motion contract. Its app-only baseline is implemented: reading rows retain identity and update exact words immediately with finite emphasis, actual pending work drives shared indeterminate/static progress, and approved view/stage arrivals settle once without refresh replay. Full native regression passes 677/680 with three existing wire skips and 24 new cases. Android builds with zero warnings/errors; phone/tablet light/dark normal/enlarged-text captures and the local HTML record are checked. Hardware and server-dependent gates remain separate.

Interaction follow-up F04 is implemented on 10 November. The hold charges visibly, Bloom unfolds and folds from the centre with interruptible easing, and pointer inactivity dismisses it without timing out keyboard, held, hovered-action or large-text choices. Shared button feedback, alignment, hand cursors, contextual help and synchronized Story cursors are in place. Full native regression passes 653/656 with three existing opt-in wire skips. Android builds with 0 warnings and 0 errors. Read [ARCHITECTURE.md](ARCHITECTURE.md) for the code map and [DESIGN.md](DESIGN.md) for the motion contract. Target-hardware timing and accessibility remain unqualified.

The app-only Reduce motion follow-up is implemented on 10 November. Desktop and Android load the saved device preference before constructing the app; More reports storage failures. Twelve new cases cover restart, unreadable data, failed-save retry, temporary background pause and phone/tablet light/dark rendering. The full native suite passes 636 of 639 cases, with the same three opt-in wire skips. Android builds with 0 warnings and 0 errors. The Windows helper and Android build require the normal user environment rather than the restricted agent sandbox. Real-device qualification remains pending.

- Without v2: continue app-only refinements and emulator coverage now. The owner deferred physical Q01 checks until a device is available; hardware touch, predictive Back, TalkBack and frame timing remain open. iOS needs a Mac with Xcode and an Apple developer account and has no project yet.
- Waits for the upstream v2 branch (`feat/personal-assistant-harness-api`, identity and the `/api/me` app API): porting pairing, sessions and reads onto v2; W4 Story on real history (T02–T05); Bloom's Ask Lucia and richer Lucia stages; the V05 Engine room.
- Do not change server code, pairing, sessions, grants or migrations until v2 settles, and do not build on v2 yet.

**Working setup.** .NET SDK 10.0.403 (master's `global.json`) and the Android workload. Run builds and tests one at a time on low-memory machines. Test-first, one class per file, and the impeccable skill before any UI change (see `AGENTS.md`).

The 25 October handoff below remains the record of the server-side history prerequisite.

## Read first

1. Repository `AGENTS.md`, including its test-first, one-class-per-file and required skill rules.
2. This file and [BUILD-PLAN.md](BUILD-PLAN.md), the task authority.
3. [README.md](README.md) for commands, connection protocols and acceptance limits.
4. [PRODUCT.md](PRODUCT.md), [DESIGN.md](DESIGN.md) and `.impeccable/design.json` before UI work. Preserve the approved native Constellation direction.

Branch: `feat/constellation-companion-foundation`. Remote: `origin`, the owner's `joslat/lucia-dotnet` fork. The user authorized committing and pushing this handoff. That is not authorization to merge, publish a release, open a PR or contact anyone.

## What works

Phone and tablet use one shared Avalonia application with adaptive layouts, not separate UI implementations or a WebView. Desktop and Android launchers provide platform integration. Core holds platform-independent semantics; Connection owns protected HTTPS/session operations; App composes native controls and screens.

Delivered implementation:

- HomeConstellation, room details, explicit named Bloom actions, light/dark themes, household outline icons, Outfit and DM Sans.
- W1 configuration: customization, typed source bindings, authorized import and checked conflict/corrupt-profile recovery.
- W2 Windows/Android connection pilot: owner-approved pairing, exact grants, renewal/revocation, DPAPI/Keystore storage and recovery. Member roster/removal and iOS are not implemented.
- W3 connected Home, source timestamps/quality, revisioned recovery and attributed Lucia activity. Connected failures never substitute the demo.
- Shared household configuration, SH01-SH04, delivered at `aaaa015`: checked SQLite/PostgreSQL storage, owner editor, filtered member reads, native comparison/protected reread, Add to draft, then separate Save. Personal section order and matching-ID pins remain personal. This is manual adoption, not push synchronization.
- StoryOrbit Day prototype with linked sample chapters, filters and timelines. This is not production history.

## Exact stopping point: W4 history prerequisite

The handoff adds a tested source/history boundary. It does not complete production Story or enable connected Story screens.

- `lucia.HomeAssistant/Services/HomeAssistantClient.cs`: correct presence-based history flags and numeric `significant_changes_only`; reject null responses; cap bodies at 8 MiB, including unknown-length streams; preserve a body-read timeout after `ResponseHeadersRead`.
- `lucia.Agents/Companion/CompanionHistoryReader.cs`: exact current-grant source discovery, validated source calendar, bounded recorder reads, typed values and explicit boundary samples. At most 256 sources, batches of 16 and 50,000 samples. Windows are half-open, at most eight elapsed days and within `Companion:HistoryLookbackDays`, default 30. Lookback is a query policy, not a recorder-retention promise.
- `CompanionHistorySnapshot`, `CompanionHistoryTrack`, `CompanionHistorySample` and `CompanionHistoryQuality` contain typed data only. They do not expose raw attributes, context, coordinates or source configuration. Empty tracks do not prove inactivity or coverage.
- `lucia.AgentHost/Companion/CompanionHistoryApi.cs`: tested protected endpoint composition, exact offset-bearing query timestamps, late identity/capability/field rechecks and a 4 MiB response budget.
- **The history services/route are intentionally not registered in production `Program.cs`.** The isolated security test host registers them for integration proof. Do not expose this route merely because its tests pass.
- `CurrentPlaceId` is current access provenance, not proof of a historical room assignment. Historical samples use recorder `last_updated`, never the live `last_reported` heartbeat.

## First task for the next agent

Finish T02's effective-dated binding and coverage/retention policy before activating the history route. Start with tests for a source moving between rooms, changed field semantics, grant reductions, unknown earlier bindings and gaps in recorder retention. Current registry metadata must not retroactively authorize or label another room's historical data. Boundary samples are not transition events or proof of continuous recording.

Then finish the same W4 delivery in order:

1. Integrate the qualified history contract into protected native transport under the existing expected-identity/grant lease. Recheck capability/source provenance and preserve cancellation/recovery.
2. T03: generate deterministic chapters linked to actual evidence. Do not infer occupancy or physical success.
3. T04: connect real Day/date navigation and Week. Reuse `StoryDay`, `StoryViewModel`, StoryOrbit and the timelines; preserve timezone/DST correctness and explicit missing tracks.
4. T05: one shared selection across controls, permission changes, cancellation, gaps and failures, followed by scoped native visuals and finish review.
5. W5: complete approved Bloom/motion/workflows and separately authorized Engine room.
6. W6: qualify installed Android, actual devices/accessibility/performance, iOS and release readiness. Do not call the full application finished before these gates pass.

Do not reopen completed Home/shared-setup controls for speculative polishing. Keep one active delivery, M-STORY-01, rather than marking every future capability as in progress. W4 is paused at this handoff; W5/W6 are queued.

## Verification and limits

The preceding shared-setup checkpoint passed all 499 native tests, including three actual-wire UI cases, and 576 relevant server tests. Android debug had zero warnings/errors. Those results are retained evidence, not a new run for every subsequent change.

The final handoff run passed 639 expanded server/auth/history/provider and selected HA-client tests, zero failures/skips, against synthetic local fixtures. This is a selected regression suite, not every repository test. Existing MSB4011 and ASPIRE010 warnings are unrelated and retained. No native UI or Android code changed in this history prerequisite, and no new native visual, emulator, device or iOS qualification is claimed.

Basic commands from the repository root:

```powershell
dotnet run --project clients/Lucia.Companion/Lucia.Companion.Desktop
dotnet test clients/Lucia.Companion/Lucia.Companion.Tests
dotnet test lucia.Tests/lucia.Tests.csproj --filter "FullyQualifiedName~CompanionHistory|FullyQualifiedName~HomeAssistantHistoryClientTests"
dotnet build clients/Lucia.Companion/Lucia.Companion.Android/Lucia.Companion.Android.csproj -p:AndroidSdkDirectory="$PWD/.tools/android-sdk"
```

The three native wire UI tests need the isolated fixture documented in README; ordinary native runs skip them. Never run household integration tests merely to increase a test count. Read the test scope and fixture first.

## Private material and authority

Private design exports, analysis, screenshots and TRX files remain outside this repository. On the original workstation, read the private constellation-adoption `START-HERE.md` and `full-application-progress.json` for exact evidence paths and original artboard references. Do not add that directory to Git or copy its contents into a public commit.

No real household access, physical controls, secrets, paid/cloud resources, model downloads, messages to people or release publication are authorized. Use synthetic data and isolated loopback hosts. Only the dedicated test emulator may be used for a bounded check; shut it down afterward. Real device, TalkBack, safe-area, installed successful networking/lifecycle, performance and Mac/Xcode/iOS/signing/distribution gates remain genuinely open. The legacy offline heartbeat stays paused.

UX12 is delivered and confirmed in native input and the installed emulator. UX13 restores the approved Home hold guidance without changing the interaction contract. Mean 9.12; physical accessibility and frame timing remain deferred and the 9.5 target stays active.
