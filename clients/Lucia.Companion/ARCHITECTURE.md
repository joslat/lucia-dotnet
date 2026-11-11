# Companion client architecture

Updated 11 November 2026. This describes the implemented native clients. [UI-UX-PLAN.md](UI-UX-PLAN.md) owns the screen/state inventory and refinement sequence, [DESIGN.md](DESIGN.md) owns appearance and interaction rules, and [BUILD-PLAN.md](BUILD-PLAN.md) owns wider delivery status. The native client is separate from the React management dashboard in `lucia-dashboard`.

## Projects and entry points

All native client code is under `clients/Lucia.Companion/`.

| Project | Responsibility | Start reading |
|---|---|---|
| `Lucia.Companion.Core` | Immutable home/observation/history models, quality semantics, local profile contracts and pure hold rules. No Avalonia or network dependency | `HomeTopology.cs`, `Place.cs`, `Observation.cs`, `StoryDay.cs`, `HoldGesture.cs` |
| `Lucia.Companion.Connection` | Protected session coordination, HTTPS transport, grant checks and typed responses | `ICompanionConnection.cs`, `CompanionConnection.cs`, `ProtectedSessionVault.cs` |
| `Lucia.Companion.App` | Shared native controls, screens, view models, themes, persistence coordination and presentation motion | `App.axaml`, `App.axaml.cs`, `CompanionRoot.cs`, `CompanionShell.axaml.cs` |
| `Lucia.Companion.Desktop` | Desktop launcher and Windows DPAPI adapter | `Program.cs`, `Security/WindowsDeviceSecretProtector.cs` |
| `Lucia.Companion.Android` | Android application/activity, lifecycle, platform text sizes and Keystore adapter | `AndroidApplication.cs`, `MainActivity.cs`, `Security/AndroidDeviceSecretProtector.cs` |
| `Lucia.Companion.Tests` | Pure model, persistence, protected transport and native headless interaction/render tests | `TestAppBuilder.cs`, `NativeCapture.cs`, the tests for the relevant control |

```mermaid
flowchart TD
    Desktop[Desktop launcher] --> App[Shared Avalonia App]
    Android[Android launcher] --> App
    App --> Connection[Connection library]
    App --> Core[Core models and rules]
    Connection --> Core
    Tests[Native and unit tests] -.-> App
    Tests -.-> Connection
    Tests -.-> Desktop
```

Desktop and Android use the same screens and user controls. They do not render the HTML artboards, and there is no separate mobile UI implementation. There is no iOS launcher yet.

## Composition and state

`CompanionRoot` starts on Connection and opens the demo only through explicit selection. A verified connection opens `ConnectedHomeWorkspace`, whose controllers read permitted data into `HomeViewModel` and the shared shell. A failed connection never substitutes sample household data.

The root wraps its content in `Controls/InputPaneViewport.cs`. It observes the top level's input pane, reserves only the docked keyboard area that overlaps the existing safe-area viewport, and brings the unchanged focused field into view after layout. Detach removes the observer, clears the inset and invalidates deferred focus work. Status bars and navigation bars remain under Avalonia's existing safe-area handling.

`CompanionShell` owns the four destinations, local save status and full-screen setup/conflict workflows. `ShellNavigation` considers a rail at 600 logical units and uses it only when its naturally measured height fits. Compact windows fall back to the same bottom-bar actions. Live text changes coalesce a layout check without replacing buttons or moving focus. The rail has an 88-unit minimum and grows through native label measurement; bottom navigation retains its wrapping rules. `HomeScreen` adapts its overview/detail composition at 900 units. Story has its own width and text-size adaptations. Available space and text size decide layouts rather than a phone/tablet device-name check.

`HomeViewModel` owns selection, open detail/actions, pins and section order. Immutable incoming data updates existing place controls when layout identity is unchanged, preserving focus and an active hold. `HomeProfileSession` coordinates checked profile persistence. Controls request changes; they do not obtain household credentials or issue physical commands.

## Design mapped to controls

Paths below are relative to `Lucia.Companion.App/`.

| Design concept | Implementation | Ownership |
|---|---|---|
| Place constellation and connections | `Controls/HomeConstellation.axaml.cs`, `ConstellationPanel.cs`, `ConnectionLayer.cs` | Stable place order, configured edges, input gesture coordination |
| Place orb, state, selection and charge | `Controls/PlaceOrb.cs`, `HoldCharge.cs`, `Styles/Controls.axaml` | Native button semantics, written state, semantic brushes and a decorative hold arc |
| Contextual Bloom | `Controls/BloomActions.axaml`, `BloomActions.axaml.cs`, `BloomActionButton.cs` | Named actions, radial/list presentation, unfold/fold and protected idle dismissal |
| Needs you | `Controls/AttentionList.cs`, `AttentionPlaceRow.cs` | Retained place actions, current explanations, press/focus continuity and deliberate removal fallback |
| Place detail/page | `Controls/PlaceDetail.axaml`, `PlacePage.axaml` | Shared observations and explanation, compact page or expanded side pane |
| Story dial, chapters and tracks | `Screens/StoryScreen.axaml`, `Controls/StoryOrbit.cs`, `ChapterList.cs`, `PlaceTimeline.cs`, `TimelineTrackLayer.cs` | Shared selected instant and filter, native text/buttons beside drawing |
| Shared button feedback | `Controls/InteractionFeedback.cs` | Mouse hover, press compression, finite colour transitions and lifecycle cleanup |
| Contextual help | `Controls/ContextHelp.cs` | Delayed wrapping tooltips with matching accessible help |
| Reading continuity and update feedback | `Controls/ObservationRows.cs`, `ObservationRow.axaml.cs` | Retain unique rows within exact place/binding scope, update real text immediately and apply one finite emphasis |
| Actual operation progress | `Controls/OperationProgress.cs` | Native indeterminate activity only while the owning controller/session is busy; static dots for reduced motion |
| View/stage arrival | `Controls/SurfaceEntrance.cs` | One finite position settle on approved presentation boundaries; no replay on ordinary data refresh |
| Presentation visibility | `Controls/PresentationVisibility.cs` | Observe the owner and its ancestors so hidden, detached or backgrounded presentation stops |
| Colour, type and control templates | `App.axaml`, `Styles/Controls.axaml`, `Assets/Fonts/` | Observatory light/dark semantic roles, local Outfit/DM Sans and consistent native targets |

Story, charts and Today so far are labelled sample data in the demo. Connected historical Story and Engine room remain incomplete. Appearance does not establish data capability or authorization.

## Input and motion

Home exposes a direct Review reports shortcut and an empty-home setup route through the existing shell editor flow. Setup entry focuses the home-name field; returning restores the visible invoker or Home navigation. Connection removal remains behind an explicit disclosure and the existing confirmation. Collapsed options are excluded from visibility and focus immediately.

Story chapters cache against the actual view model as well as chapter/filter identity. Date navigation therefore cannot retain handlers for the previous day. Timelines are cached within one immutable day model and reconciled by place ID; a report remains attached when selecting it narrows the filter. Bloom maps focus between equivalent radial/list actions after text adaptation, with version and intervening-focus guards. Effective hiding cancels pending holds and closes overlays immediately.

The pure `HoldGesture` distinguishes a quick tap, pending hold, scroll cancellation and a completed hold. `HomeConstellation` supplies elapsed time and pointer events, draws charge only while pending, and suppresses the tap after a hold. `HomeScreen` owns the popup's anchor, selected-place actions and focus return. The same actions remain reachable through named buttons and the native context menu.

Android can request a context menu independently of the framework's Holding gesture. The orb consumes context requests while its Bloom hold is pending or active, preventing two menus from stacking. After release, the regular context-menu route remains available.

Home records the overview scroll offset and the actual page invoker before opening a compact place page. The page starts at zero offset; return restores the overview and a visible valid invoker or deliberate fallback. A navigation version and intervening-focus guard prevent old callbacks from affecting another destination.

`BloomActions` owns its presentation timers. Actions travel from the selected centre in 240 ms with a maximum 45 ms stagger; closing travels inward for 140 ms. A pointer-opened radial menu folds after one idle second. An active hold, hovered action, keyboard session or large-text list keeps choices available. Opening again cancels an old closing completion. Once folding starts, its actions stop accepting input.

`InteractionFeedback` attaches to native buttons through an attached style property. State colours interpolate for 200 ms; ordinary feedback uses 120–180 ms. Place and Bloom action hover uses a 2% scale change, and pressing compresses to 97.5%. Content alignment and padding remain part of the templates. Information rows retain left-aligned text. Keyboard focus stays immediate and distinct from hover.

`MotionSettings` combines the saved device preference, a separate system reduction request and a temporary background pause. Windows and Android restore the preference before constructing controls. `AndroidMotionObserver` reads the current `Settings.Global.AnimatorDurationScale` at startup/resume and observes it while foregrounded. Zero or invalid scale suppresses effects without persisting or replacing the personal setting. More reports the override. Under reduced motion, controls settle immediately and a pending hold shows a static indication. All new transitions and timers stop on detach or backgrounding. The only custom frame timer is the active pending hold. No polling timer drives decorative motion.

The shell owns one `CompanionCrossFade` with native 180 ms opacity transitions and CubicEaseInOut. Each new transition starts from the painted opacity and cancels the old operation. `Stop` removes the transitions, restores opacity and settles visibility synchronously. Backgrounding or a live reduction request therefore cannot leave an old page partially visible or hide the newest page when its old completion arrives.

The Story dial and straight-track cursors use the same 280 ms selection transition and the same Animate the time hand setting. Selection labels and source timestamps update immediately. Colour interpolation and cursor travel never alter observations, fabricate a lifecycle stage or infer occupancy.

Reading rows are retained by unique label/role only inside an unchanged place and exact ordered binding scope. The current observation model lacks a per-reading source ID, so ambiguous duplicate labels and changed provenance start fresh. Removed readings leave immediately. `ObservationRow.Update` writes the complete new value, quality and timing first, then allows a 360 ms soft background emphasis for a meaningful value/quality change. It never interpolates numeric data, hides the new words, replays on a timestamp-only update or queues old changes.

`OperationProgress` reads no services and owns no progress clock. Views feed their existing busy flags for Connection, Home, activity, shared review, local-profile loading and conflict review. The native progress bar is indeterminate, with no percentage text. Three static dots replace it under reduced motion. `PresentationVisibility` observes ancestor visibility and stops activity immediately when the containing view is hidden.

`SurfaceEntrance` is enabled only on transform-free presentation containers for Connection stages, verified Home readiness and the place page. It keeps text fully visible while translating four logical units to rest over 220 ms. Data refresh, keyboard focus and preference changes do not replay it. Existing shell navigation owns the other screen transitions. Charts render their actual evidence and unknown coverage immediately, with no invented intermediate readings or simulated loading for absent history.

## Local save feedback

`HomeProfileSession.IsSaving` counts the entire serial queue, including snapshots waiting for older writes. `RequiresGlobalNotice` combines actual loading/saving with recovery flags. The shell never infers state by matching status strings. Each completed intermediate write advances the checked revision; only the latest request confirms success. Review and resolution cannot overtake pending writes.

The shell keeps progress and failure/conflict recovery global, but moves idle context into More. Setup explains its draft and explicit apply action. Versioned completed-outcome text appears on the current Home overview or place page. Navigation consumes it without replay, and conflict handlers recheck their originating view before returning. Retry focus moves to status while pending and to the current destination when the notice closes. A memory-only session publishes no durable-save confirmation.

`StoryWeek` measures date/count/ruler text at the mapped font sizes. It reserves text columns or moves the lane below its header, reducing tick density when necessary. Font changes are coalesced after layout and retain the native action controls. This adaptation does not infer extra reports or change their times.

## Persistence and service boundary

Local home metadata uses `LocalHomeStore` with revision checks and explicit conflict recovery. Reduce motion uses a separate device-local preference file, with same-directory replacement and visible failure feedback. Protected session material stays in the vault and platform protection adapters. Report snapshots, display animation state and background pause are not persisted as household history.

The existing AgentHost remains the service boundary. The planned v2 member identity and `/api/me` port will replace the pilot identity/session integration; it must preserve the shared UI and the distinction between permission, metadata and report quality. Server, pairing, grant and migration changes remain on hold until that direction settles.

## Run and qualify

From the repository root:

```powershell
dotnet run --project clients/Lucia.Companion/Lucia.Companion.Desktop
dotnet test clients/Lucia.Companion/Lucia.Companion.Tests
dotnet build clients/Lucia.Companion/Lucia.Companion.Android -p:AndroidSdkDirectory="$PWD/.tools/android-sdk"
```

Choose Explore the demo for the sample Home and Story. Native tests can capture phone/tablet light/dark frames through `LUCIA_COMPANION_CAPTURE_DIR`. Headless interaction and rendered-frame checks establish bounded desktop/native behavior. Android hardware frame time, touch feel, TalkBack, safe areas, predictive Back, installed connection recovery and iOS still require their own qualification.


The shell delays only a newly queued save-only notice by 150 ms. A continuous queue shares one monotonic deadline; added writes do not restart it. Existing recovery and loading stay immediate, including Retry. Completion cancels the reveal without imposing a minimum wait. Hidden, detached and paused views stop the timer, and return checks actual elapsed work before showing progress.

Persistent local-save problems show a concise summary and a named Details toggle. The complete session explanation remains in accessible help and a bounded native scroll area. Recovery actions use natural widths in a WrapPanel, retaining complete labels at doubled text. Back closes Details before changing routes. Review setup enters the existing guarded unreadable-file editor and never writes on entry.


`HomeViewModel.ReplaceHome` retains the selected place by ID and keeps its detail/page/actions open only while that place remains. Removing it clears all three presentation flags. Story's visible interaction hint follows the actual Day/Week mode and supplements native slider, named row and chapter input.


Editor removal owns a versioned Loaded focus handoff. It rechecks presentation, selected ID, disclosure/import/discard state and intervening focus. Selection rebuilds, import, save/cancel, ancestor hide and detach invalidate pending work. Opening focuses Keep place while revealing the question and both actions; Keep/Back return to Remove; Confirm changes only the draft and reveals the remaining-place selector or Add place. No text field receives focus merely because a place was removed.

Before applying setup, the shell captures Home's selected place ID. Only its removal requests a new overview anchor. The guarded return discards the invalid invoker and offsets, then focuses Your places or the empty setup action through each active scroll ancestor. A surviving selection keeps the existing page and reading position.


PlaceOrb's transparent template backdrop makes spacing and padding part of the input target. HomeConstellation captures the TopLevel for press, move and final release samples. A root change cancels the gesture. HoldGesture exposes slide intent only beyond the existing 10-unit movement tolerance, and clears it on return to origin, release or a new press. Home snapshots release intent before reset and requires it for armed actions, invocation and outside dismissal. A stationary release leaves the named menu available. No extra timer or animation loop is introduced.

UX14 will separate setup context from its scrolling form. Expanded layouts will bound the existing master pane; compact layouts will keep a concise current-place chooser above the form. Both reuse the same selection and draft validation pipeline. Save setup remains the explicit persistence boundary.


UX14 now bounds EditorGrid beside the fixed commands. EditorScroll owns the form; the expanded places pane has a star-height list. CompactPlaceContext keeps the existing chooser outside the form scroller. Both presentations share one selection path and the same management controls, reparented between two hosts. Home name remains available with zero places. MeasureOverride measures current header/actions, spacing and padding before choosing expanded layout; no LayoutUpdated polling remains.

Selection retains native item identity and commits valid fields only to the draft. Invalid selection restores both selectors and closes an open chooser before revealing the error and input. QueueEditorFocus versions all delayed handoffs. Its native-selection allowance covers only focus within the initiating selector; unrelated user focus, hidden/detached views, changed selection, import and discard invalidate the handoff. Responsive changes capture focused management actions before hiding/detaching and restore the same control. Import hides the whole editing region, and discard disables it.

An open compact chooser keeps its layout when the IME closes. DropDownClosed invalidates measurement so a fitting master pane can return after the choice. A queued focus request has precedence over automatic reflow handoffs only while its version is current; completion clears that marker. Invalid selection can therefore reveal its field without a later selector-focus request overriding it. Outside dismissal and hidden/detached contexts retain their own focus.

Place validation is directly beside PlaceNameInput and uses mapped TextSize13. TryApplyCurrent routes blank and model validation failures to that message. HomeErrorText remains outside the optional form for general and empty-home failures. Successful edits clear both messages. The concise blank-name error is Enter a place name.


Explicit OpenStoryFor requests a guarded day-context reveal after the view is laid out. A navigation epoch changes on Navigate and host-content replacement. Current Story input, hiding and detachment also invalidate pending work. The callback rechecks view/root identity, model/filter/instant/chapter, Day mode and intervening visible focus, then focuses Day and resets the reader to its top. Ordinary Story-tab navigation does not request this reveal.

PlaceFilterBar retains its native strip and chooser. Changed model/selection or strip/viewport metrics request a versioned horizontal reveal. The callback rechecks current selection, visibility and root, clamps Offset.X, and leaves vertical offset and focus alone. Oversized chips show their beginning. Time-only refreshes do not schedule another reveal. Day report/request totals use VisibleIntervals; Week uses its already filtered intervals. Scope wording distinguishes the place or whole home, and present attention explicitly says Home now.

<!-- cycle10-checkpoint:start -->

## Responsive attention and qualification

AttentionPlaceRow retains complete reasons with natural wrapping. HomeScreen moves only sections whose parent/order changes, then restores displaced focus synchronously when its original control remains visible and no replacement focus exists. TodaySoFar uses a wrapping header. ShellNavigation includes current-page wording in the portable automation name.

Android/Qualification/AccessibilityQualification.cs is compiled only by the existing explicit Debug qualification flag. It probes native identities, focus and events, restores the temporary UiAutomation service request and labels synthetic invalid-draft/busy fixtures. It is excluded from ordinary builds. Native/headless and instrumentation evidence do not establish TalkBack speech or hardware frame pacing.

<!-- cycle10-checkpoint:end -->

<!-- cycle11-checkpoint:start -->

## Panel arrival ownership and field feedback

Main Home, place, More and Story panels assemble at screen/component presentation: opacity 0.12 to 1 with a 6-unit rise,220 ms CubicEaseOut and28 ms stagger capped at 84 ms. Cleanup is bounded at 335 ms. Actual saved/visual reading order controls the sequence. Mode entry can animate its newly presented component. Ordinary values, report refresh and text reflow do not restart it. Input stays enabled; hidden/detached/reparented, paused and reduced-motion targets settle synchronously. Connection/recovery feedback remains immediate with the existing boundary settle, preserving prompt loading and error information.

PanelArrival owns one versioned presentation per scope. It uses finite framework transitions, bounded one-shot callbacks and temporary visibility watchers. Nested scopes own their own targets. Home responsive reparenting cancels only displaced panel motion; clearing all scopes never moves focus. The prior Place detail wrapper settle is removed to avoid double arrival.

OperationProgress exposes one named text/status peer and no numeric descendants while its visual bar/dots remain. Editor errors describe their actual field, persist through unrelated successful edits and clear only after that field validates or its place context changes. Correction restores normal purpose help. General setup errors have a separate region. Native accessibility focus confirms each explanation on the focused field; no spoken-delivery claim is made.

Android Debug qualification contains a separately callable motion trace. An external runner supplies genuine simulator Home/resume, policy changes and touch input; it records actual app state and restores settings. It is excluded from the ordinary APK. UX19 will address the known Android accessibility SetText replacement limitation and qualify any remaining interaction gap. Physical touch, spoken TalkBack, traversal and hardware frame pacing remain owner-deferred, separate qualification work.

<!-- cycle11-checkpoint:end -->

<!-- cycle12-checkpoint:start -->

## Android text actions and activity ownership

Android ACTION_SET_TEXT now replaces the complete value, including empty and Unicode input. Instance-specific native identities keep repeated import rows distinct. Current-route, attachment, visibility, enabled, read-only and password checks protect editing. ACTION_FOCUS reaches the normal TextBox focus/input path and retains read-only selection access. Unrelated events, accessibility focus and other actions use the original provider/delegate. The activity restores its original delegate on teardown. Windows behavior and dependencies are unchanged.

The adapter lives in Lucia.Companion.Android. [AndroidTextReplacement](Lucia.Companion.Android/AndroidTextReplacement.cs) owns the public view/delegate hook. [NativeTextFields](Lucia.Companion.Android/NativeTextFields.cs) owns eligible field identities. [TextReplacementProvider](Lucia.Companion.Android/TextReplacementProvider.cs) and [TextReplacementDelegate](Lucia.Companion.Android/TextReplacementDelegate.cs) preserve the existing native bridge. Debug-only qualification is excluded from the ordinary manifest.

Physical touch, spoken TalkBack/traversal and hardware frame pacing remain owner-deferred qualification. Server/v2 work stays on hold. No desktop control was used.

<!-- cycle12-checkpoint:end -->
