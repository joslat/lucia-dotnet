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

Implemented: observation semantics, validated topology, shared selection, HomeConstellation, attention, read-only detail, named BloomActions, local setup, metadata/pin persistence and a thin fixture-driven StoryOrbit Day. [BUILD-PLAN.md](BUILD-PLAN.md) records remaining gates. Real history sources, authentication and live integration are not implemented yet.

- Core references neither Avalonia nor server/HA/model packages. App references Core; launchers reference App. Server code is unchanged.
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

## StoryOrbit Day

Story opens one synthetic day, 23 October 2026, in the sample home's Europe/Zurich timezone. Choose a chapter, tap the dial, use the native time slider or select a named report. One StoryViewModel owns the selected instant, place filter and chapter. Phone stacks the dial and chapters; tablet places them side by side. The timelines show the same reports and cursor.

The ring budget is six visible places, with stable outer-to-inner order. A manually scrollable place strip and a native chooser for larger homes let any place take focus. All place timelines remain available. The animation checkbox can turn off hand motion. There are no inert Week, Patterns or future-date buttons.

StoryDay maps elapsed instants rather than assuming every day has 24 hours. Clock-change tests cover 23/25-hour Zurich days and distinguish the repeated hour using UTC offsets. Normalized intervals are half-open, clipped to the day and cannot overlap within one place/observation track. Chapters link to recorded evidence clipped to their own intervals.

No report means unknown, not off, closed or unoccupied. Last-known and unavailable are distinct from recorded evidence. Motion uses explicit one-minute sample reporting buckets, never occupancy intervals. A completed sample Lucia request does not prove a physical action.

This is a control spike, not the production history pipeline. The sample chapters are authored fixture data, not inferred narratives. Grouping uses the demo's current place setup. Versioned historical bindings, authorized sources, coverage acquisition, pagination/retention, general chapter rules and live queries remain unimplemented. Story does not persist reports or history.
