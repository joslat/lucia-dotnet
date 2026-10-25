# Start here: companion handoff

Updated 25 October 2026. Work stopped at the user's request for a session handoff. The full application is not complete. Do not restart autonomous work until the user resumes it.

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
