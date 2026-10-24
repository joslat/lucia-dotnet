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

Build your home stages place names/icons, physical connections, ordering and removal until Save. New places are unavailable until observation bindings exist. There is no source import or real binding editor yet.

Optional native screenshots:

```powershell
$env:LUCIA_COMPANION_CAPTURE_DIR = Join-Path $env:TEMP 'lucia-companion-captures'
dotnet test clients/Lucia.Companion/Lucia.Companion.Tests
```

Tests use headless Avalonia with the real Skia renderer, not HTML. They do not qualify phone/tablet runtime, safe areas, screen readers, large text, lifecycle or performance.

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

Debug packages embed their assemblies and can be installed directly without IDE fast-deployment side files. Android backup is disabled for the local setup. Debug signing is not a distribution release. Device lifecycle, back navigation, system insets and suspend/resume remain unqualified.

An API 36 x86_64 emulator smoke check confirms direct APK installation, startup, place detail, named Bloom actions and local pin persistence. It also confirms Story launch, touch scrolling, chapter selection with a matching timeline cursor, and in-app return Home. It caught and fixed a zero-width startup layout failure. This is not physical-device or full mobile qualification.

## Status and engineering boundaries

Implemented: observation semantics, validated topology, shared selection, HomeConstellation, attention, read-only detail, named BloomActions, local setup, metadata/pin persistence and a thin fixture-driven StoryOrbit Day. [BUILD-PLAN.md](BUILD-PLAN.md) records remaining gates. Real history sources, authentication and live integration are not implemented yet.

- Core references neither Avalonia nor server/HA/model packages. App references Core; launchers reference App. Server code is unchanged.
- HomeViewModel owns shared selection and pins without UI dependencies. HomeEditorViewModel owns draft edits. HomeProfileSession coordinates persistence/recovery; LocalHomeStore owns file I/O. Controls raise named intents, not service calls.
- Reuse controls before inventing base classes. One class per C# file. No empty ports, DI container or generic repositories. Local persistence now exists because Save and preference restoration need it.
- Freshness is qualified at the data boundary, not guessed by UI timers. Unknown never means zero. Stale/unavailable values stay last-known. Closed is not locked; completed requests are not physical results.
- Containment, physical adjacency, observation bindings and visual coordinates are distinct. The foundation does not invent these relationships.
- No cloud host is required for this offline slice. Future live services belong behind reviewed native sessions and household authorization, never inside controls.

See [font provenance and licenses](Lucia.Companion.App/Assets/Fonts/README.md) and DESIGN.md for native style rules.

## Local setup storage

The prototype saves `Lucia/Companion/offline-home.json` under the platform's local application-data folder. It contains setup metadata and pin preferences, never readings or task history. New places retain unavailable state until bindings exist. Metadata does not grant access to a household or device.

Writes use a same-directory temporary file and serialized replacement. Invalid schema, invalid topology or unreadable JSON leaves the original file untouched and disables automatic saving. Build your home then offers an explicit Replace saved setup action. An I/O failure leaves the session usable and offers Retry save.

Current verification includes core/configuration tests and native headless phone/tablet checks, not a production data boundary or full platform qualification. Production Story history, source import, observation-binding editing and section-order preferences remain future work.

## StoryOrbit Day

Story opens one synthetic day, 23 October 2026, in the sample home's Europe/Zurich timezone. Choose a chapter, tap the dial, use the native time slider or select a named report. One StoryViewModel owns the selected instant, place filter and chapter. Phone stacks the dial and chapters; tablet places them side by side. The timelines show the same reports and cursor.

The ring budget is six visible places, with stable outer-to-inner order. A manually scrollable place strip and a native chooser for larger homes let any place take focus. All place timelines remain available. The animation checkbox can turn off hand motion. There are no inert Week, Patterns or future-date buttons.

StoryDay maps elapsed instants rather than assuming every day has 24 hours. Clock-change tests cover 23/25-hour Zurich days and distinguish the repeated hour using UTC offsets. Normalized intervals are half-open, clipped to the day and cannot overlap within one place/observation track. Chapters link to recorded evidence clipped to their own intervals.

No report means unknown, not off, closed or unoccupied. Last-known and unavailable are distinct from recorded evidence. Motion uses explicit one-minute sample reporting buckets, never occupancy intervals. A completed sample Lucia request does not prove a physical action.

This is a control spike, not the production history pipeline. The sample chapters are authored fixture data, not inferred narratives. Grouping uses the demo's current place setup. Versioned historical bindings, authorized sources, coverage acquisition, pagination/retention, general chapter rules and live queries remain unimplemented. Story does not persist reports or history.
