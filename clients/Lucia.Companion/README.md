# Lucia Companion (experimental)

Native Avalonia foundation for the Constellation mobile/tablet client. This is an **offline Home, local setup and Story Day prototype**, not a connected household app or completed MVP. No network clients, credentials, model calls or physical actions. The control workbench remains available through Options, then Control lab.

## Smallest shared architecture

```text
Desktop launcher ─┐
                 ├─ App: themes, native controls, compositions and views
Android launcher ┘     └─ Core: platform-independent observation/place semantics
Tests ──────────────────┴─ unit + native headless interaction/render tests
```

Phone and tablet share state and controls. Layout follows available width, including split view and landscape, not device-model checks. Launchers own platform lifecycle/integration only. No duplicate phone/tablet applications or WebView. Core is reusable by another .NET client without Avalonia; App is reusable by Avalonia clients, not automatically by React. Add iOS/browser launchers only when needed. iOS requires a Mac, signing and runtime qualification; none is claimed here.

## Run and verify

From the repository root, with .NET 10:

```powershell
dotnet run --project clients/Lucia.Companion/Lucia.Companion.Desktop
dotnet test clients/Lucia.Companion/Lucia.Companion.Tests
dotnet build clients/Lucia.Companion/Lucia.Companion.slnx
```

Click/tap an orb or use Tab and Enter/Space to select sample observations. Phone opens a detail view with Back; tablet keeps details beside the graph. Place actions opens named Bloom shortcuts for readings, pinning and dismissal. Context menus offer the same actions. Options, then Appearance offers System, Light and Dark. All readings and task stages are synthetic.

Build your home stages place names/icons, physical connections, ordering, observation bindings and removal until Save. Choose a source and a supported report role; a source-role pair belongs to one place in the home. Existing bindings remain visible if a source disappears from discovery, with explicit removal. New places stay unavailable until qualified readings arrive. The default app offers labelled sample sources; real authorized discovery/import is not connected yet.

Optional native screenshots:

```powershell
$env:LUCIA_COMPANION_CAPTURE_DIR = Join-Path $env:TEMP 'lucia-companion-captures'
dotnet test clients/Lucia.Companion/Lucia.Companion.Tests
```

Tests use headless Avalonia with the real Skia renderer, not HTML. The 128 cases cover shared semantics, binding validation/persistence/recovery, competing local writers, explicit conflict recovery, native interactions, persisted section ordering, Back routing, draft protection and text-size mappings through 200% in phone/tablet light/dark layouts. These are not physical-device, safe-area, screen-reader or performance qualification.

### Android

The thin launcher targets .NET 10/API 36, minimum Android 12/API 31. It is outside the desktop solution so server/desktop contributors need no mobile workloads. With Android workload, API 36 platform/build-tools and JDK installed:

```powershell
dotnet build clients/Lucia.Companion/Lucia.Companion.Android
```

If the system SDK requires administrator rights, use a separate ignored SDK folder rather than changing system permissions. The installation command requires reviewing/accepting the SDK licenses:

```powershell
dotnet build clients/Lucia.Companion/Lucia.Companion.Android -t:InstallAndroidDependencies -p:AcceptAndroidSdkLicenses=True -p:AndroidSdkDirectory="$PWD/.tools/android-sdk"
dotnet build clients/Lucia.Companion/Lucia.Companion.Android -p:AndroidSdkDirectory="$PWD/.tools/android-sdk"
```

Debug packages embed their assemblies and can be installed directly without IDE fast-deployment side files. Android backup is disabled for the local setup. Debug signing is not a distribution release.

API 36 x86_64 emulator checks confirm direct APK installation, startup, place detail, Bloom actions, saved pins, Story scrolling and chapter/cursor selection. They also cover system Back, rotation, same-process activity recreation with an unsaved setup draft, a focused field above the keyboard, native font preference 2.0 and corrupt-setup preservation. These are bounded offline checks, not physical-device or full mobile qualification.

A later installed-build check also opens Customize through Options, moves a section and verifies its saved order. Native source/role choosers select a labelled sample temperature source, add its binding, save and restore it after a cold process restart. Only metadata is stored. This does not qualify live discovery or real household access.

The installed conflict-recovery check deliberately changes the synthetic saved file while a session has another draft. Saving refuses the stale overwrite. Review shows both versions; system Back preserves the session; explicit reload uses the saved setup; explicit replacement saves the session version and survives a cold restart with bindings and section order retained. The original synthetic setup was restored afterward and the emulator shut down. These checks do not qualify arbitrary external-editor races, hardware accessibility or production household data.

### Native navigation and text

Back closes Bloom before phone detail, then returns Story or the control lab to Home. At Home root it lets Android leave the app. Setup Back first closes a removal question, then asks before discarding changed fields, including invalid unsaved text. Keep editing preserves those fields; Discard changes never saves them. Explicit Cancel remains an intentional discard. The keyboard can consume the first Back to hide itself.

The single Android activity reuses its view within the application process. Rotation and activity recreation retain the current screen, selection and draft. Process death starts from saved setup metadata and pins only. Unsaved drafts, readings and Story history are not written to disk.

Android maps each of the seven base font sizes through native SP conversion. Layout expands with text rather than zooming all geometry. At larger text, Home uses fewer columns, Bloom offers a scrollable action list, and Story prints the selected time below the dial. Normal text retains the radial composition.

The prototype temporarily opts out of predictive Back in its manifest. On the tested API 36 launcher, default predictive Back bypassed app navigation; the compatibility flag restored system Back routing. Android documents this [temporary migration option](https://developer.android.com/about/versions/16/behavior-changes-16#predictive-back). Predictive gesture animation remains a release gate, alongside real-device/TalkBack traversal, broader keyboard/inset coverage, iOS, memory pressure, performance and distribution signing.

## Status and engineering boundaries

Implemented: observation semantics, validated topology, shared selection, HomeConstellation, attention, read-only detail, named BloomActions, local setup, metadata/pin persistence and a thin fixture-driven StoryOrbit Day. [BUILD-PLAN.md](BUILD-PLAN.md) records remaining gates. The host has opt-in native sessions and owner-approved pairing endpoints. The owner approval UI, native client authentication/storage, real history sources and live integration remain unimplemented.

- Core references neither Avalonia nor server/HA/model packages. App references Core; launchers reference App. The separate host-side security module does not add server dependencies to the native client.
- HomeViewModel owns shared selection and pins without UI dependencies. HomeEditorViewModel owns draft edits. HomeProfileSession coordinates persistence/recovery; LocalHomeStore owns file I/O. Controls raise named intents, not service calls.
- Reuse controls before inventing base classes. One class per C# file. No empty ports, DI container or generic repositories. Local persistence now exists because Save and preference restoration need it.
- Freshness is qualified at the data boundary, not guessed by UI timers. Unknown never means zero. Stale/unavailable values stay last-known. Closed is not locked; completed requests are not physical results.
- Containment, physical adjacency, observation bindings and visual coordinates are distinct. The foundation does not invent these relationships.
- No cloud host is required for this offline slice. Future live services belong behind reviewed native sessions and household authorization, never inside controls.

See [font provenance and licenses](Lucia.Companion.App/Assets/Fonts/README.md) and DESIGN.md for native style rules.

## Local setup storage

The prototype saves `Lucia/Companion/offline-home.json` under the platform's local application-data folder. It contains setup metadata, source ID/role bindings and preferences, never readings, source catalogs, credentials or task history. Optional schema-1 bindings default to empty for older files. New places retain unavailable state until qualified readings arrive, even when bindings are configured. Metadata does not grant access to a household or device.

Writes use a same-directory temporary file, a cooperating-process file lock and a SHA-256 revision of the exact saved bytes. Null revision permits creation only. A session queues rapid edits and carries forward each successful revision. A stale writer cannot replace a newer file, including during corrupt-file repair. The small `.lock` sidecar remains on disk so waiting writers cannot accidentally lock different files. No readings or secrets go into that file. External editors must not modify the setup while the app is running; the lock coordinates companion writers, not arbitrary editors.

Invalid schema, invalid topology or unreadable JSON leaves the original file untouched and disables automatic saving. When the original bytes can be read safely, Build your home offers explicit Replace saved setup. An oversized or inaccessible file cannot be replaced blindly; repair storage and reopen the app. An ordinary write failure leaves the session usable and offers Retry save.

A changed saved revision stops automatic saving and offers Review save conflict. The scrollable review shows the session and saved home name, counts, pins and section order. Expand Places and bindings to compare place order, names, icons, physical neighbors and source-role metadata. Replace saved setup with my changes performs a checked replacement. Discard my changes and use saved setup explicitly discards the session version. Back without choosing preserves both. A second change while reviewing refuses the stale choice and displays the latest saved version. Unreadable, removed and different-home files cannot be loaded as this session's setup. There is no automatic merge or recovery of unsaved session changes after process death.

Customize at the bottom of Home, or Options then Customize Home, changes the order of Your places, Needs a look and Lucia now. Named earlier/later buttons work without dragging. Restore default order resets the preference. All sections remain present and room positions stay unchanged. Phone follows the chosen order; tablet prioritizes the first section while keeping the room graph in the wide column. Back collapses Customize before leaving Home. The additive optional schema-1 preference defaults for older saved files.

The binding picker reports rejected duplicate assignments beside its own controls, focuses the error and names the existing place plus the removal step. Missing discovery never deletes existing metadata. Removing a place removes its draft connections and bindings; Cancel leaves the saved setup untouched. A bound place without readings says so rather than claiming no bindings exist.

Current verification includes core/configuration tests and native headless phone/tablet checks, not a production data boundary or full platform qualification. Production Story history, authorized discovery/import and live adapters remain future work. BUILD-PLAN.md tracks the active full-application goal; this checkpoint does not complete it.

## Native-session host foundation

This is not yet a connected native sign-in screen. Companion access is disabled by default. `Companion:Enabled` requires a stable `Companion:HomeId` and trusted HTTPS. The app still runs offline. A separate native transport library now passes HTTPS integration tests, but platform storage/session coordination and screen wiring remain open. Do not give the app a dashboard key or internal service token to bypass that work.

The host registers `LuciaCompanion` as an explicit authentication scheme. It is not part of the existing default API-key/internal-token scheme, `Administrator`, or `ExternalOrInternal` policies. The native credential carries no broad scope or administrator role. Dashboard headers/cookies and internal credentials are not accepted as a companion identity. `GET /api/companion/homes/{homeId}/session` returns only that device's session/grant metadata and uses `Cache-Control: no-store`, including denied responses. Insecure companion requests fail without redirecting a credential to another address. Disabled routes return 404.

An owner-assigned `CompanionGrant` identifies one home and member, explicit place IDs and observation fields. Activity, history and Engine access default to false. There are no wildcard grants. IDs compare exactly; names and physical connections grant no access. `CompanionHome` checks the identity and route home only. Future projections must filter each place, field, event, history interval and aggregate against the current grant. A home-level policy alone does not authorize returning all household data. Engine permission will authorize sanitized administrator aggregates, not general appliance administration or raw personal transcripts.

The shared relational session service uses the existing SQLite/PostgreSQL configuration database. Configuration schema 4 adds session and spent-refresh tables without converting existing keys; schema 5 adds pending pairing and per-home quota tables. PostgreSQL queries explicitly name the public schema. Both access and refresh credentials contain 256 random bits and have separate prefixes; the server stores their SHA-256 hashes, never the credentials. Credential requests/responses redact their `ToString()` output and must never be logged or put in a URL.

Defaults are five-minute access, seven-day idle refresh and a thirty-day absolute device session. Configuration validates positive, ascending lifetimes, with maximums of fifteen minutes, thirty days and ninety days respectively. Renewal atomically replaces both credentials and invalidates the previous access credential. It never extends the absolute session expiry. A spent refresh credential revokes the entire device session, including the newly rotated access credential. Clients must serialize renewal; ambiguous delivery failure requires re-pairing rather than blindly replaying the old refresh credential. Logout/revocation invalidates the durable session, not just local preferences.

These options are read at host startup. Restart after changing them; use durable device revocation for immediate access removal rather than relying on a configuration reload.

Authentication reads current durable state on each request, with no API-key cache involved. A revoked device is refused immediately by other instances using that database. Expired credentials always fail, regardless of cleanup. A host worker removes expired replay metadata and sessions in bounded batches every fifteen minutes, including when companion routes are disabled. A large backlog may take multiple batches. It is not an audit-log retention service.

The design reuses ASP.NET Core's [explicit scheme policies](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/authorize-with-a-specific-scheme?view=aspnetcore-10.0) and applies [refresh-rotation guidance](https://www.rfc-editor.org/rfc/rfc9700.html#section-4.14). This Lucia-specific device-session foundation is not an OAuth/OIDC authorization server or a claim of RFC conformance. Owner-approved pairing remains the selected local-pilot direction. A supported identity-provider integration would instead require a separate [native browser/PKCE design](https://www.rfc-editor.org/rfc/rfc8252.html).

Current proof uses isolated SQLite databases, temporary PostgreSQL 17 containers and loopback Kestrel HTTPS with a per-test certificate trusted only by that test client. It covers permissions, scheme isolation, cross-home denial, mixed broad/native credentials, expiry, concurrent renewal/replay, pairing, revocation, retention and schema migration. No real AgentHost startup, household, HA endpoint, model or cloud service participates. Existing authentication tests remain part of the regression checks. This does not qualify native storage, production TLS/reverse proxies, real projections or a third-party security audit. Owner UI, membership management, platform storage and the connected app remain required work.

### Owner-approved pairing API

Pairing uses two separate codes. The native device retains a 256-bit `deviceCode`; the owner reviews a ten-character `userCode`. The short code cannot redeem credentials. Display the same code on the device and owner review, and approve only after the owner checks the intended device and exact grant. Device names are untrusted labels, not proof of identity. This Lucia-specific protocol is not an OAuth device authorization implementation.

| Endpoint under `/api/companion` | Caller and result |
|---|---|
| `POST /pairings` | Anonymous HTTPS JSON `{deviceName}`. Returns codes, expiry and five-second poll interval |
| `POST /pairings/redeem` | Device JSON `{deviceCode}`. Pending 202, slow polling 429, denied 403, expired/consumed 410, invalid 401; success returns credentials once |
| `POST /session/renew` | Device JSON `{refreshToken}`. Rotates both credentials or returns 401 |
| `GET /session` | Native Bearer. Learns its approved home/member/grant without a client-supplied home hint |
| `POST /session/logout` | Native Bearer. Durably revokes that session without a home hint |
| `GET /homes/{homeId}/session` | Native Bearer. Returns own current session/grant only |
| `POST /homes/{homeId}/session/logout` | Native Bearer. Durably revokes that session |
| `GET /owner/antiforgery` | Current owner key/cookie. Returns identity-bound request token and Secure/HttpOnly/SameSite=Strict cookie |
| `POST /owner/pairings/review` | Current owner JSON `{userCode}`. Returns pending intent metadata, never the device secret |
| `POST /owner/pairings/{id}/approve` | Current owner JSON `{codeConfirmed:true,grant:{homeId,memberId,places,canReadActivity,canReadHistory,canReadEngine}}`. Exact grant, no wildcards; fields are string enum names |
| `POST /owner/pairings/{id}/cancel` | Current owner. Denies pending or approved but unconsumed intent |
| `GET /owner/sessions` | Current owner. Non-revoked, absolute-unexpired device metadata/grants, never credentials or hashes |
| `DELETE /owner/sessions/{id}` | Current owner. Durably revokes a device |

Owner routes use the existing Administrator API-key scheme, not native or internal tokens. Every request checks current key state without relying on cached validation. Cookie mutations require ASP.NET antiforgery proof; an explicit API-key header is non-ambient. Owner routes reject any Authorization header, so a native Bearer plus an ambient owner cookie cannot approve devices. The browser approval/device management UI is still to be built.

`Companion:PairingLifetime` defaults to ten minutes, allowed one to fifteen minutes. `Companion:MaximumPendingPairings` defaults to 32, allowed 1 to 128. A database transaction locks the home quota across instances. Poll timing is durable too. Approval binds one immutable grant; redemption and credential insertion commit together. A storage failure rolls back both, allowing retry. Successful delivery lost after commit cannot recover plaintext credentials; re-pair and revoke the orphaned device if needed.

All companion requests have a 16 KiB body limit. Per-process, per-remote-IP limits allow five starts, 120 credential/session requests and 60 owner requests per minute, with no queue and Retry-After on rejection. These are local-pilot abuse limits, not distributed DDoS protection. Qualify trusted proxy forwarding before deployment; never trust arbitrary forwarded headers. Expired pairing rows are removed in bounded batches during creation and maintenance. Approval metadata is not a permanent audit log.

The owner assigns an opaque member ID with the grant. There is no separate member account/roster yet. Removing an owner key after a valid approval does not retroactively revoke that device grant. Cancel an unconsumed intent or explicitly revoke its issued session. Full member-removal semantics, owner UI, native vault/TLS/client integration and real projection filtering remain open.

### Native transport library

`Lucia.Companion.Connection` references Core only, not Avalonia, AgentHost, HA or model packages. It owns HTTP and validated immutable wire types; Core remains independent of network I/O. The visible app does not call it yet. There is no new NuGet package.

`CompanionApiClient` accepts one HTTPS origin with no embedded credentials, query, fragment or path prefix. Reverse-proxy path-prefix hosting is not supported yet. Production uses the platform certificate trust policy, disables redirects, cookies and default credentials, and never sends an owner key. Test-only constructors are internal to the two test assemblies; the integration host pins only its own temporary certificate. The public constructor refuses that untrusted certificate.

Pairing/refresh secrets travel in JSON bodies only; access credentials use a per-request Bearer header. Methods do not retry credential-changing requests. Pairing reports pending, throttle, denial, expiry and consumed intent separately. Session metadata reveals only the current device grant, not a household catalog. Wire parsing rejects missing required constructor inputs, malformed credentials, invalid fields and duplicate places. Exceptions and credential `ToString()` output omit response bodies and raw transport diagnostics.

Responses are limited to 64 KiB. Headers and content each have a thirty-second deadline; caller cancellation remains cancellation. Content needs a separate deadline because [ResponseHeadersRead ends HttpClient's timeout at the headers](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpcompletionoption?view=net-10.0). An ambiguous renewal/redemption must not replay an old credential. The forthcoming session coordinator must serialize renewals, save replacements securely before use, and clear protected state after revocation/logout.

Proof includes scripted hostile responses and the actual transport against isolated loopback Kestrel HTTPS with real SQLite pairing/session services. It covers successful approval/grant discovery/renewal/logout, old-access rejection, pending/throttle/denial, certificate rejection, bounded/stalled bodies, cancellation and redacted errors. It does not qualify a platform vault, installed Android networking, production deployment or connected UI.

## StoryOrbit Day

Story opens one synthetic day, 23 October 2026, in the sample home's Europe/Zurich timezone. Choose a chapter, tap the dial, use the native time slider or select a named report. One StoryViewModel owns the selected instant, place filter and chapter. Phone stacks the dial and chapters; tablet places them side by side. The timelines show the same reports and cursor.

The ring budget is six visible places, with stable outer-to-inner order. A manually scrollable place strip and a native chooser for larger homes let any place take focus. All place timelines remain available. The animation checkbox can turn off hand motion. There are no inert Week, Patterns or future-date buttons.

StoryDay maps elapsed instants rather than assuming every day has 24 hours. Clock-change tests cover 23/25-hour Zurich days and distinguish the repeated hour using UTC offsets. Normalized intervals are half-open, clipped to the day and cannot overlap within one place/observation track. Chapters link to recorded evidence clipped to their own intervals.

No report means unknown, not off, closed or unoccupied. Last-known and unavailable are distinct from recorded evidence. Motion uses explicit one-minute sample reporting buckets, never occupancy intervals. A completed sample Lucia request does not prove a physical action.

This is a control spike, not the production history pipeline. The sample chapters are authored fixture data, not inferred narratives. Grouping uses the demo's current place setup. Versioned historical bindings, authorized sources, coverage acquisition, pagination/retention, general chapter rules and live queries remain unimplemented. Story does not persist reports or history.
