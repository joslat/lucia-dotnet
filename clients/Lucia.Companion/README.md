# Lucia Companion (experimental)

Native Avalonia client for the Constellation mobile/tablet design. It opens a real HTTPS device-connection preview, with protected storage on Windows and Android. The dashboard has an owner approval/device-management pilot. Home, local setup and Story Day remain an explicitly selected synthetic demo; live household views are not built yet, so this is not a completed MVP. No model calls or physical actions. The demo control workbench remains available through Options, then Control lab.

## Smallest shared architecture

```text
Desktop launcher ─┐
                 ├─ App: themes, native controls, compositions and views
Android launcher ┘     └─ Core: platform-independent observation/place semantics
Tests ──────────────────┴─ unit + native headless interaction/render tests
```

App uses the separate Connection library for HTTPS pairing and session operations. Connection references Core only. Platform launchers supply protected storage; the shared screen never receives raw credentials or the pending device secret.

Phone and tablet share state and controls. Layout follows available width, including split view and landscape, not device-model checks. Launchers own platform lifecycle/integration only. No duplicate phone/tablet applications or WebView. Core is reusable by another .NET client without Avalonia; App is reusable by Avalonia clients, not automatically by React. Add iOS/browser launchers only when needed. iOS requires a Mac, signing and runtime qualification; none is claimed here.

## Run and verify

From the repository root, with .NET 10:

```powershell
dotnet run --project clients/Lucia.Companion/Lucia.Companion.Desktop
dotnet test clients/Lucia.Companion/Lucia.Companion.Tests
dotnet build clients/Lucia.Companion/Lucia.Companion.slnx
```

Choose Explore the demo explicitly to open sample observations. Click/tap an orb or use Tab and Enter/Space to select them. Phone opens a detail view with Back; tablet keeps details beside the graph. Place actions opens named Bloom shortcuts for readings, pinning and dismissal. Context menus offer the same actions. Options, then Appearance offers System, Light and Dark. All demo readings and task stages are synthetic. Options, then Connection returns to connection management without discarding the demo session.

Build your home stages place names/icons, physical connections, ordering, observation bindings and removal until Save. Choose a source and a supported report role; a source-role pair belongs to one place in the home. Existing bindings remain visible if a source disappears from discovery, with explicit removal. New places stay unavailable until qualified readings arrive. The default app offers labelled sample sources; real authorized discovery/import is not connected yet.

Optional native screenshots:

```powershell
$env:LUCIA_COMPANION_CAPTURE_DIR = Join-Path $env:TEMP 'lucia-companion-captures'
dotnet test clients/Lucia.Companion/Lucia.Companion.Tests
```

The 178 client tests cover semantics, transport, protected storage, session recovery and native interactions. UI cases use headless Avalonia with the real Skia renderer, not HTML. They cover binding validation, competing local writers, conflict recovery, persisted section ordering, Back routing, draft protection and text-size mappings through 200% in phone/tablet light/dark layouts. Connection checks include explicit demo separation, approval identity, grant disclosure and focus continuity. Windows-only DPAPI tests explicitly skip on other operating systems. None of these tests qualifies physical-device accessibility, safe areas or performance.

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

The connection-preview APK also passes a bounded API 36 installed check. Cold startup opens connection first. A closed emulator-loopback HTTPS port produces a written error, saves no session and never opens the demo automatically. Explicit demo entry, Options then Connection, and system Back return work. The native keyboard can consume the first Back to hide itself. With the soft keyboard enabled, the focused host field remains above it. Native 200% text changes the form layout. The existing synthetic setup hash stays unchanged; the crash buffer is empty. These checks do not establish successful installed TLS pairing, real-source integration, TalkBack or hardware qualification.

### Native navigation and text

Back closes Bloom before phone detail, then returns Story or the control lab to Home. At Home root it lets Android leave the app. Setup Back first closes a removal question, then asks before discarding changed fields, including invalid unsaved text. Keep editing preserves those fields; Discard changes never saves them. Explicit Cancel remains an intentional discard. The keyboard can consume the first Back to hide itself.

The single Android activity reuses its view within the application process. Rotation and activity recreation retain the current screen, selection and draft. After process death the app starts at connection verification. Demo setup metadata and pins restore only on explicit demo entry. Unsaved drafts, readings and Story history are not written to disk.

Android maps each of the seven base font sizes through native SP conversion. Layout expands with text rather than zooming all geometry. At larger text, Home uses fewer columns, Bloom offers a scrollable action list, and Story prints the selected time below the dial. Normal text retains the radial composition.

The prototype temporarily opts out of predictive Back in its manifest. On the tested API 36 launcher, default predictive Back bypassed app navigation; the compatibility flag restored system Back routing. Android documents this [temporary migration option](https://developer.android.com/about/versions/16/behavior-changes-16#predictive-back). Predictive gesture animation remains a release gate, alongside real-device/TalkBack traversal, broader keyboard/inset coverage, iOS, memory pressure, performance and distribution signing.

## Status and engineering boundaries

Implemented: connection-first native preview, protected restoration/recovery, owner dashboard approval/device management, observation semantics, validated topology, shared selection, HomeConstellation, attention, read-only detail, named BloomActions, local setup, metadata/pin persistence and a thin fixture-driven StoryOrbit Day. [BUILD-PLAN.md](BUILD-PLAN.md) records remaining gates. The host has opt-in native sessions and owner-approved pairing endpoints. iOS storage, authorized source projections, real history and live views remain open.

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

The native connection screen calls the real session API. The dashboard's Companion devices page lets an owner approve, decline and revoke exact device grants. Authorized discovery and live household adapters remain unfinished. Companion host access is disabled by default. `Companion:Enabled` requires a stable `Companion:HomeId` and trusted HTTPS. Windows and Android use protected storage; unavailable protection shows an error and an explicit demo choice, never a plaintext fallback. Do not give the app a dashboard key or internal service token.

Enter the intended HTTPS origin and a device name. The waiting screen retains the canonical host, exact submitted name and human approval code while checks run or pause. In Companion devices on the trusted HTTPS dashboard, the owner reviews that code, matches the intended device/person and chooses exact read access. The pilot uses explicit member/place IDs until the authorized catalog exists. Verified state discloses the device grant, not sample household readings. Foreground entry revalidates an existing connection. Retry, local forgetting and disconnect are separate actions; removal requires confirmation. Offline disconnect says local removal only when host revocation cannot be confirmed. Credentials are never shown to the screen.

The host registers `LuciaCompanion` as an explicit authentication scheme. It is not part of the existing default API-key/internal-token scheme, `Administrator`, or `ExternalOrInternal` policies. The native credential carries no broad scope or administrator role. Dashboard headers/cookies and internal credentials are not accepted as a companion identity. `GET /api/companion/homes/{homeId}/session` returns only that device's session/grant metadata and uses `Cache-Control: no-store`, including denied responses. Insecure companion requests fail without redirecting a credential to another address. Disabled routes return 404.

An owner-assigned `CompanionGrant` identifies one home and member, explicit place IDs and observation fields. Activity, history and Engine access default to false. There are no wildcard grants. IDs compare exactly; names and physical connections grant no access. `CompanionHome` checks the identity and route home only. Future projections must filter each place, field, event, history interval and aggregate against the current grant. A home-level policy alone does not authorize returning all household data. Engine permission will authorize sanitized administrator aggregates, not general appliance administration or raw personal transcripts.

The shared relational session service uses the existing SQLite/PostgreSQL configuration database. Configuration schema 4 adds session and spent-refresh tables without converting existing keys; schema 5 adds pending pairing and per-home quota tables. PostgreSQL queries explicitly name the public schema. Both access and refresh credentials contain 256 random bits and have separate prefixes; the server stores their SHA-256 hashes, never the credentials. Credential requests/responses redact their `ToString()` output and must never be logged or put in a URL.

Defaults are five-minute access, seven-day idle refresh and a thirty-day absolute device session. Configuration validates positive, ascending lifetimes, with maximums of fifteen minutes, thirty days and ninety days respectively. Renewal atomically replaces both credentials and invalidates the previous access credential. It never extends the absolute session expiry. A spent refresh credential revokes the entire device session, including the newly rotated access credential. Clients must serialize renewal; ambiguous delivery failure requires re-pairing rather than blindly replaying the old refresh credential. Logout/revocation invalidates the durable session, not just local preferences.

These options are read at host startup. Restart after changing them; use durable device revocation for immediate access removal rather than relying on a configuration reload.

Authentication reads current durable state on each request, with no API-key cache involved. A revoked device is refused immediately by other instances using that database. Expired credentials always fail, regardless of cleanup. A host worker removes expired replay metadata and sessions in bounded batches every fifteen minutes, including when companion routes are disabled. A large backlog may take multiple batches. It is not an audit-log retention service.

The design reuses ASP.NET Core's [explicit scheme policies](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/authorize-with-a-specific-scheme?view=aspnetcore-10.0) and applies [refresh-rotation guidance](https://www.rfc-editor.org/rfc/rfc9700.html#section-4.14). This Lucia-specific device-session foundation is not an OAuth/OIDC authorization server or a claim of RFC conformance. Owner-approved pairing remains the selected local-pilot direction. A supported identity-provider integration would instead require a separate [native browser/PKCE design](https://www.rfc-editor.org/rfc/rfc8252.html).

Current host proof uses isolated SQLite databases, temporary PostgreSQL 17 containers and loopback Kestrel HTTPS with a per-test certificate trusted only by that test client. It covers permissions, scheme isolation, cross-home denial, mixed broad/native credentials, expiry, concurrent renewal/replay, pairing, revocation, retention and schema migration. No real AgentHost startup, household, HA endpoint, model or cloud service participates. Existing authentication tests remain part of the regression checks. The production owner bundle also passes an isolated real HTTPS/SQLite browser approval/revocation flow. Its owner-cookie validator is synthetic and only the test browser ignores the fixture certificate. Host/browser tests do not qualify platform storage or real login; separate Windows/Android checks are described below. Production TLS/proxies, real projections, membership management, iOS storage, installed successful native sign-in and release security review remain open.

### Owner-approved pairing API

Pairing uses two separate codes. The native device retains a 256-bit `deviceCode`; the owner reviews a ten-character `userCode`. The short code cannot redeem credentials. Display the same code on the device and owner review, and approve only after the owner checks the intended device and exact grant. Device names are untrusted labels, not proof of identity. This Lucia-specific protocol is not an OAuth device authorization implementation.

| Endpoint under `/api/companion` | Caller and result |
|---|---|
| `POST /pairings` | Anonymous HTTPS JSON `{deviceName}`. Returns codes, expiry and five-second poll interval |
| `POST /pairings/redeem` | Device JSON `{deviceCode}`. Pending 202, slow polling 429, denied 403, expired/consumed 410, invalid 401; success returns credentials once |
| `POST /session/renew` | Device JSON `{refreshToken}`. Rotates both credentials or returns 401 |
| `POST /session/revoke` | Device JSON `{refreshToken}`. Current or spent refresh proof revokes only its session, without issuing new credentials. Idempotent 204 does not reveal whether a credential existed |
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

Owner routes use the existing Administrator API-key scheme, not native or internal tokens. Every request checks current key state without relying on cached validation. Cookie mutations require ASP.NET antiforgery proof; an explicit API-key header is non-ambient. Owner routes reject any Authorization header, so a native Bearer plus an ambient owner cookie cannot approve devices. The Companion devices dashboard page uses existing same-origin cookies, obtains fresh antiforgery proof for each mutation and never asks for native credentials. A lost approval response clears its editor and requires a deliberate new review, not a blind replay.

`Companion:PairingLifetime` defaults to ten minutes, allowed one to fifteen minutes. `Companion:MaximumPendingPairings` defaults to 32, allowed 1 to 128. A database transaction locks the home quota across instances. Poll timing is durable too. Approval binds one immutable grant; redemption and credential insertion commit together. A storage failure rolls back both, allowing retry. Successful delivery lost after commit cannot recover plaintext credentials; re-pair and revoke the orphaned device if needed.

All companion requests have a 16 KiB body limit. Per-process, per-remote-IP limits allow five starts, 120 credential/session requests and 60 owner requests per minute, with no queue and Retry-After on rejection. These are local-pilot abuse limits, not distributed DDoS protection. Qualify trusted proxy forwarding before deployment; never trust arbitrary forwarded headers. Expired pairing rows are removed in bounded batches during creation and maintenance. Approval metadata is not a permanent audit log.

The owner assigns an opaque member ID with the grant. There is no separate member account/roster yet. Removing an owner key after a valid approval does not retroactively revoke that device grant. Cancel an unconsumed intent or explicitly revoke its issued session. Full member-removal semantics, installed native TLS integration and real projection filtering remain open. The pilot grant editor uses exact case-sensitive IDs, rejects duplicate/fieldless place rows and defaults all additional permissions to off. Its session list reports issued sessions and last-read time, never device online status.

### Native transport library

`Lucia.Companion.Connection` references Core only, not Avalonia, AgentHost, HA or model packages. It owns HTTP and validated immutable wire types; Core remains independent of network I/O. The visible app calls its UI-safe `ICompanionConnection` boundary. It exposes display codes and verified session metadata, never credential sets or the pending device secret. There is no new NuGet package.

`CompanionApiClient` accepts one HTTPS origin with no embedded credentials, query, fragment or path prefix. Reverse-proxy path-prefix hosting is not supported yet. Production uses the platform certificate trust policy, disables redirects, cookies and default credentials, and never sends an owner key. Test-only constructors are internal to the two test assemblies; the integration host pins only its own temporary certificate. The public constructor refuses that untrusted certificate.

Pairing/refresh secrets travel in JSON bodies only; access credentials use a per-request Bearer header. Methods do not retry credential-changing requests. Pairing reports pending, throttle, denial, expiry and consumed intent separately. Session metadata reveals only the current device grant, not a household catalog. Wire parsing rejects missing required constructor inputs, malformed credentials, invalid fields and duplicate places. Exceptions and credential `ToString()` output omit response bodies and raw transport diagnostics.

Responses are limited to 64 KiB. Headers and content each have a thirty-second deadline; caller cancellation remains cancellation. Content needs a separate deadline because [ResponseHeadersRead ends HttpClient's timeout at the headers](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpcompletionoption?view=net-10.0). An ambiguous renewal/redemption must not replay an old credential. The session coordinator serializes renewal and protects replacements before use.

Proof includes scripted hostile responses and the actual transport/coordinator against isolated loopback Kestrel HTTPS with real SQLite pairing/session services. It covers approval/grant discovery/renewal/logout, old-access rejection, pending/throttle/denial, certificate rejection, bounded/stalled bodies, cancellation and redacted errors. These HTTPS tests use ephemeral test encryption, not a production platform key. Separate platform checks cover actual Windows DPAPI and bounded Android Keystore storage. Installed Android networking, production deployment and connected UI remain unqualified.

### Protected sessions and recovery

`ProtectedSessionVault` writes only ciphertext to `session.bin` in its supplied private directory, separate from `offline-home.json`. The stored schema contains endpoint, credential set, verification phase and approved home/member identifiers. It contains no observations, source catalog, grants or task history. The Windows launcher supplies `Lucia/Companion/secure-session` under local application data with CurrentUser DPAPI. Android supplies `NoBackupFilesDir/lucia-session` with its Keystore protector. No app-unlock or biometric requirement is claimed.

`ISessionVaultLease` holds the `.session.lock` file exclusively across cooperating instances, including credential-changing HTTP requests. Other callers wait with cancellation, bounded at 75 seconds. The sidecar is not deleted or replaced. Writes protect bytes first, flush a same-directory temporary file, then replace ciphertext. Corrupt, oversized, unreadable or unsupported-schema files fail closed and cannot be overwritten by ordinary Save. Explicit Forget is local-only recovery. Protection failure and cancelled writes preserve the prior file. This is not protection against a same-user malicious process, arbitrary editors, filesystem rollback or device compromise.

The Windows launcher provides `WindowsDeviceSecretProtector` using [CurrentUser DPAPI](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata?view=net-10.0), with a Lucia-specific purpose. Microsoft package `System.Security.Cryptography.ProtectedData` 10.0.12 is its only new direct dependency. The app does not store its own Windows key. Android has a platform adapter described below; iOS Keychain is not implemented yet. Unsupported targets must not fall back to plaintext. Plaintext buffers are cleared after serialization/parsing; managed credential strings cannot be guaranteed erased from memory.

`DeviceSessionCoordinator` protects newly redeemed credentials in Verifying state before reading the grant. A temporary grant-read outage can recover without repeating redemption. It returns permissions only after a successful server read and stores exact approved home/member identity for later comparison. A foreign endpoint receives no stored credential. Revocation or identity changes clear the local connection; ordinary network failures do not fabricate an offline grant or silently switch to sample data.

Before renewal, the coordinator durably writes Renewing. It then sends the refresh once, checks the replacement session identity, and protects replacement credentials before any authenticated read. If cancellation, lost delivery or saving fails after that marker, the next restore never replays the old refresh. It removes local credentials, attempts bounded refresh-proof revocation and requires pairing again. Real SQLite/HTTPS tests prove that spent refresh proof revokes a newly rotated orphan; PostgreSQL verifies the same query with a shadowed search path.

Disconnect clears local ciphertext before a bounded revocation attempt. It returns false when only local removal is confirmed, including an offline host. The owner can revoke that device remotely. It does not claim logout succeeded if local storage could not be cleared. Forget deliberately claims no remote revocation. The connection screen names these distinctions and asks for confirmation before either removal.

Tests cover separate coordinators issuing one renewal, marker/write ordering, failed protection before sending, failed replacement storage, caller cancellation, interrupted renewal recovery, delayed verification, endpoint/identity mismatch, offline disconnect, revocation and corruption preservation. Windows tests reopen the actual DPAPI-backed file through a new protector instance and launch a hidden synthetic probe process to verify file-lease exclusion. Full installed Android/iOS connection qualification, process termination at every write boundary, filesystem power-loss guarantees, backup/restore and release security review remain open gates.

### Android Keystore qualification

`AndroidDeviceSecretProtector` uses an app-scoped non-exportable AES-256 key from Android Keystore and the platform AES/GCM/NoPadding cipher. Android generates each 96-bit IV; the envelope includes format version, IV and ciphertext with a 128-bit authentication tag. A Lucia-specific authenticated purpose binds the payload format. This follows Android's [Keystore GCM example](https://developer.android.com/reference/android/security/keystore/KeyGenParameterSpec). No extra package or key file is required.

Missing keys, invalid versions, truncated or modified ciphertext fail closed. Reading never creates a replacement key. Only encryption can create a missing key, after the vault's ordinary read checks or explicit local Forget. The adapter does not require a biometric prompt for background renewal and adds no app-unlock gate. Hardware backing is device-dependent and not claimed from an emulator. Production wiring must use the app's `NoBackupFilesDir`, separate from local setup metadata; Android backup is already disabled in the application.

Qualification source is excluded from ordinary builds. Opting it in is allowed only in Debug; Release explicitly rejects the property. The runner creates separate `Lucia.Qualification.*` keys and temporary no-backup directories. It never reads the production key, saved home, household data or network endpoints. Default checks remove their own keys/files. Staged process tests retain only their named synthetic run until resume or explicit cleanup.

With the dedicated `LuciaPhoneApi36` emulator on port 5580 only:

```powershell
dotnet build clients/Lucia.Companion/Lucia.Companion.Android -p:AndroidSdkDirectory="$PWD/.tools/android-sdk" -p:EnableCompanionQualification=true
$taskAdb = "$PWD/.tools/android-sdk/platform-tools/adb.exe"
& $taskAdb -s emulator-5580 emu avd name # Verify LuciaPhoneApi36, never use a physical device.
& $taskAdb -s emulator-5580 install -r --no-streaming clients/Lucia.Companion/Lucia.Companion.Android/bin/Debug/net10.0-android/org.lucia.companion.prototype-Signed.apk
& $taskAdb -s emulator-5580 shell am instrument -w -r org.lucia.companion.prototype/org.lucia.companion.prototype.SessionVaultQualification
$taskRunId = [Guid]::NewGuid().ToString('N')
& $taskAdb -s emulator-5580 shell am instrument -w -r -e stage seed -e runId $taskRunId org.lucia.companion.prototype/org.lucia.companion.prototype.SessionVaultQualification
& $taskAdb -s emulator-5580 shell am force-stop org.lucia.companion.prototype
& $taskAdb -s emulator-5580 shell pidof org.lucia.companion.prototype # Must be absent before resume.
& $taskAdb -s emulator-5580 shell am instrument -w -r -e stage resume -e runId $taskRunId org.lucia.companion.prototype/org.lucia.companion.prototype.SessionVaultQualification
```

Each instrumented run must report `status=passed` and `INSTRUMENTATION_CODE: -1`. The adb exit code alone is insufficient. Twelve default checks cover randomization, key reuse/non-exportability, tampering, format bounds, vault exclusion, ciphertext-only files, restored renewal state, corruption/key-loss preservation and explicit Forget. The two-stage probe confirms a key and encrypted renewal marker survive force-stop into a fresh process. This is not an interrupted real HTTP rotation, every write/power-loss boundary, hardware/biometric proof or installed connected sign-in. Existing synthetic setup remains unchanged.

If a staged run fails before cleanup, use `-e stage cleanup -e runId $taskRunId` with the same instrumentation component. Rebuild/install the ordinary app without `EnableCompanionQualification`, verify the component is absent, and stop the dedicated emulator after the bounded check. Keep evidence outside the public repository. iOS, real-device storage/lifecycle, backup/restore, installed networking and release security review remain open.

## StoryOrbit Day

Story opens one synthetic day, 23 October 2026, in the sample home's Europe/Zurich timezone. Choose a chapter, tap the dial, use the native time slider or select a named report. One StoryViewModel owns the selected instant, place filter and chapter. Phone stacks the dial and chapters; tablet places them side by side. The timelines show the same reports and cursor.

The ring budget is six visible places, with stable outer-to-inner order. A manually scrollable place strip and a native chooser for larger homes let any place take focus. All place timelines remain available. The animation checkbox can turn off hand motion. There are no inert Week, Patterns or future-date buttons.

StoryDay maps elapsed instants rather than assuming every day has 24 hours. Clock-change tests cover 23/25-hour Zurich days and distinguish the repeated hour using UTC offsets. Normalized intervals are half-open, clipped to the day and cannot overlap within one place/observation track. Chapters link to recorded evidence clipped to their own intervals.

No report means unknown, not off, closed or unoccupied. Last-known and unavailable are distinct from recorded evidence. Motion uses explicit one-minute sample reporting buckets, never occupancy intervals. A completed sample Lucia request does not prove a physical action.

This is a control spike, not the production history pipeline. The sample chapters are authored fixture data, not inferred narratives. Grouping uses the demo's current place setup. Versioned historical bindings, authorized sources, coverage acquisition, pagination/retention, general chapter rules and live queries remain unimplemented. Story does not persist reports or history.
