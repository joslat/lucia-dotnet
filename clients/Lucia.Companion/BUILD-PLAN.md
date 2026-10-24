# Approved build sequence

## Active full-application goal, 24 October 2026

The user renewed autonomous implementation authority after the offline checkpoint `aa2334e`. The goal is the working phone/tablet application, with the approved Constellation layouts, capabilities and motion. Completing an increment does not finish this goal. The earlier offline stopping condition is superseded for ongoing development. Keep prior evidence and do not rebuild completed controls without a concrete gap.

| Wave | Tasks | Exit evidence | Current status |
|---|---|---|---|
| W1 Configuration | C01 persisted Customize section order; C02 typed sensor-role bindings and review; C03 authorized discovery/import with explicit merge; C04 revision/conflict and corrupt-profile recovery | Unit/native tests for persistence, migration, cancel, invalid/removed bindings, import idempotence and conflict preservation | C01/C02/C04 implemented, reviewed and checked on installed Android; 128 tests pass. C03 depends on W2's secure discovery boundary |
| W2 Secure connection | S01 inspect/reuse host identity and define native session threat model; S02 host-enforced member/home/field/admin policy; S03 pairing or supported native identity, expiry/renewal/revocation; S04 platform secure storage and connection/logout UI | Reviewed protocol plus adversarial authorization, expiry/replay/revocation tests and local HTTPS client/server integration | In progress. Pairing, HTTP/session coordinator, protected file vault and Windows DPAPI adapter implemented. 155 scoped server/auth/migration and 155 native tests pass on Windows with no skips. Android/iOS storage, owner/native connection UI, member-removal semantics and real projection enforcement remain open |
| W3 Live Home | H01 typed authorized observations with timestamps/quality; H02 scoped discovery/configuration endpoints; H03 household-safe correlated task projections; H04 revisioned snapshots/updates and reconnect/background recovery; H05 connect shared UI with explicit demo/live states | Real application transport against an isolated local host, source-adapter tests, no fixture substitution in connected mode, permission and reconnect tests | Pending |
| W4 Story | T01 authorized historical source and boundary samples; T02 effective-dated bindings, coverage and retention; T03 deterministic evidence-linked chapters; T04 Day/date navigation and Week where supported; T05 shared selection and failures | Host/client integration, timezone/DST and missing-track tests, phone/tablet captures; real source availability recorded | Pending |
| W5 Design completion | V01 layout/control inventory against pinned artboards; V02 contextual Bloom and gesture cancellation; V03 interruptible Story motion, reduced-motion/background rules; V04 remaining detail/task workflows; V05 separately authorized Engine room backed by honest runtime usage/cost data | Native interaction/motion tests, bounded visual comparison and independent finish review, no inert or fabricated operational controls | Pending |
| W6 Qualification | Q01 end-to-end installed Android phone/tablet checks; Q02 accessibility/keyboard/large text/safe areas; Q03 process death, offline/reconnect and revocation; Q04 performance and animation measurements; Q05 iOS/signing/distribution preparation where tooling/authority exists; Q06 final capability matrix and runbook | Reproducible evidence for each supported target; external/hardware gates remain explicitly incomplete until actually run | Pending |

Work test-first, compose small controls into larger controls/screens and keep domain, transport, persistence and platform responsibilities separate. Reuse the existing AgentHost instead of inventing a mobile service. Synthetic isolated integration tests are allowed and must be labelled; connecting a real household or executing physical actions is not part of unattended verification. Do not expose ports, create paid resources, publish or push. Keep private evidence outside this repository. Missing hardware or access blocks only its dependent checks, not unrelated implementation.

Next, implement and qualify Android Keystore protection on the dedicated emulator, then wire native connection/recovery and browser owner approval/device UI. Keep iOS storage/signing gates explicit. The separate Connection library now protects received credentials before grant verification, records renewal intent before sending, serializes cooperating instances, protects replacements before use and refuses to replay an interrupted renewal. Windows DPAPI and real HTTPS coordinator tests pass. Refresh-proof revocation works even after access expiry or interrupted rotation; offline disconnect reports local removal without claiming remote revocation. None of this is wired into the visible app yet. This is not a completed native login or real projection boundary. C03 follows authorized discovery, not another fixture-only screen. C04 protects local metadata writes, not cross-device synchronization or household access.

The broader request includes designed read-only capabilities previously deferred from the first visual MVP, including Week and Engine room. Physical controls, push notifications, inferred presence and an unhelpful hourly Patterns grid remain excluded unless separately requested. A screen is not complete because it compiles or looks correct with samples. Acceptance requires real adapters, host authorization, error/recovery paths and supported-target proof. No claim of fully tested iOS or screen-reader behavior may be based on Windows/headless/emulator captures.

Approved 24 October 2026. Build native Constellation controls, not an HTML wrapper. Keep SRP/KISS/DRY practical: add a layer only when the current slice needs it. Dependency direction and small responsibilities provide CLEAN/SOLID boundaries without a framework of empty interfaces.

| Order | Deliverable | Gate/status |
|---|---|---|
| Foundation | Pure Core, themes, PlaceOrb, ObservationRow, outline icons | Initial implementation, sample-data workbench |
| Compositions | HomeConstellation, physical connections, attention and read-only details | Implemented; empty/single/40-place layout tests |
| Interactions | Shared selection, named BloomActions, context menu, pins and Back | Implemented; device qualification remains open |
| Screens | Home and local setup; then StoryOrbit Day | Home/setup and thin Story Day implemented with fixtures; live history remains open |
| Local metadata | Add/name/icon/connect/reorder/remove places, bind/unbind source roles, save/cancel, pins and section order | Implemented, including checked revisions and explicit conflict recovery; authorized discovery/import remains open |
| Live boundary | Native session, secure storage, authorization, projections | Must precede live household data |
| Qualification | Devices, accessibility, lifecycle, performance, distribution | Offline API 36 navigation/recreation/text checks complete; physical devices and release gates remain open |

## HomeConstellation, initial implementation complete

Reuse the atoms. Introduce topology here, not before: containment, optional physical connections, sensor bindings and display coordinates are separate. Reject self/duplicate/cross-home edges; cycles and disconnected places are valid. Lines are decorative; places remain semantic native buttons. Urgency must not continuously rearrange stable positions.

One selection state drives constellation and detail. Narrow layouts open detail separately; expanded layouts can place it alongside. Named tap/context/keyboard actions precede long-press acceleration. No hover-only actions. Test empty/single/large homes, long labels, large text and keyboard order.

## Home and local setup, initial implementation complete

Home puts Your places first, then attention and a clearly synthetic Lucia now example. Build your home stages edits until Save. New places have no bindings/readings and remain unavailable. Pin preferences persist without reordering places automatically.

LocalHomeStore writes versioned metadata, source ID/role bindings, pins and Home section order, never observations or task history. Cooperating writers use a file lock, exact-byte revisions and same-directory temporary replacement. Rapid session edits serialize without self-conflicts. A stale revision stops saving and opens an explicit session/saved review; neither version is discarded automatically. Both replacement and loading recheck the reviewed revision. Corrupt files stay untouched until explicit checked replacement, and files that cannot be read safely cannot be overwritten blindly. Failed ordinary writes leave session state and offer Retry. The binding editor keeps missing sources explicit and prevents duplicate source-role ownership. Authorized source import remains open. The following original acceptance scope is retained so that gap remains visible.

Compose Your places first, read-only Lucia now and Customize. Add only the configuration storage/state management needed here. Add/import place, choose icon, review bindings, save/cancel; no host mutations until an authenticated boundary exists. Preferences may reorder sections deliberately, not unexpectedly.

## StoryOrbit Day, initial fixture spike implemented

StoryDay owns timezone-aware elapsed-day mapping, typed normalized intervals and evidence-linked chapters. StoryViewModel owns one selection shared by the circular dial, named native chapter/report buttons, place filters and timelines. Six-ring budget, all-place readable timelines, missing/last-known/unavailable states and an animation-off option are implemented. Tests cover phone/tablet, both themes, empty/single/40-place homes and 23/25-hour clock changes. The sample chapters are authored, not generated by production rules.

Use the signature name StoryOrbit. Orbit, chapters and flattened place timelines share one temporal dataset and selection, initially deterministic fixtures. Real data needs authorized history, timezone/DST tests, explicit coverage gaps and binding policy. Place filters scroll when long. Week/hourly Patterns and full Engine analytics stay deferred.

## Live services and release

The bounded offline Android hardening slice is complete. Shared tests cover Back hierarchy, dirty setup protection, handler reattachment and per-role text sizing through 200%. The API 36 emulator checks Back, rotation, density-triggered activity recreation with a retained draft, focused-field visibility with the keyboard, native text preference 2.0, corrupt-setup preservation and saved metadata after process restart. The launcher uses a temporary predictive-Back compatibility opt-out. No physical-device, TalkBack, predictive gesture animation, iOS, performance or distribution qualification is claimed. Unsaved drafts survive activity recreation only within the process, not process death. Do not expand this completed offline slice into live services without a new request.

Review native sign-in, device sessions, secure storage, renewal/revocation and household authorization. Browser cookies are not automatically mobile auth. Reuse the Lucia host where suitable; no mobile microservice by default. No admin secrets or inferred device-control authority in clients.

Release requires actual phone/tablet light/dark, text scaling, safe areas, screen readers, keyboard/back, offline/reconnect, suspend/resume, revoked sessions, performance and signing tests. iOS needs a Mac/device matrix. Physical actions, push and operational analytics need separate approved designs.
