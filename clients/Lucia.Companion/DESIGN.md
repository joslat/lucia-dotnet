---
name: Lucia Companion
description: Native Constellation Home, offline StoryOrbit Day, contextual Bloom actions and local setup in an Observatory palette.
colors:
  light-canvas: "#f1f2ef"
  light-panel: "#ffffff"
  light-control: "#f5f6f3"
  light-line: "#626861"
  light-text: "#292e29"
  light-muted: "#626861"
  light-accent: "#8c5b0f"
  light-info: "#2563eb"
  light-problem: "#a13f3a"
  light-graph-line: "#c1c6bd"
  light-sage: "#397151"
  dark-canvas: "#0d0c0a"
  dark-panel: "#171512"
  dark-control: "#1a1816"
  dark-line: "#918b84"
  dark-text: "#ddd8d1"
  dark-muted: "#b0aaa3"
  dark-accent: "#e2a84b"
  dark-info: "#60a5fa"
  dark-problem: "#d4756b"
  dark-graph-line: "#3a3733"
  dark-sage: "#7dab8c"
typography:
  display:
    fontFamily: "Outfit"
    fontSize: "34px"
    fontWeight: 500
  headline:
    fontFamily: "Outfit"
    fontSize: "24px"
    fontWeight: 400
  body:
    fontFamily: "DM Sans"
    fontSize: "16px"
    fontWeight: 400
  value:
    fontFamily: "DM Sans"
    fontSize: "16px"
    fontWeight: 600
  label:
    fontFamily: "DM Sans"
    fontSize: "13px"
    fontWeight: 400
  compact-value:
    fontFamily: "DM Sans"
    fontSize: "14px"
    fontWeight: 600
  constellation-state:
    fontFamily: "DM Sans"
    fontSize: "14px"
    fontWeight: 400
  story-time:
    fontFamily: "Outfit"
    fontSize: "32px"
    fontWeight: 400
  time-detail:
    fontFamily: "DM Sans"
    fontSize: "12px"
    fontWeight: 400
rounded:
  focus-frame: "12px"
  detail-panel: "16px"
  orb: "36px"
  action-circle: "24px"
  caption: "8px"
  filter-pill: "24px"
  chapter-mark: "15px"
spacing:
  related: "8px"
  section: "20px"
  composition: "16px"
  group: "12px"
components:
  theme-button-light:
    backgroundColor: "{colors.light-control}"
    textColor: "{colors.light-text}"
    typography: "{typography.body}"
  theme-button-dark:
    backgroundColor: "{colors.dark-control}"
    textColor: "{colors.dark-text}"
    typography: "{typography.body}"
  place-orb-light:
    backgroundColor: "{colors.light-control}"
    textColor: "{colors.light-muted}"
    rounded: "{rounded.orb}"
    size: "72px"
  place-orb-dark:
    backgroundColor: "{colors.dark-control}"
    textColor: "{colors.dark-muted}"
    rounded: "{rounded.orb}"
    size: "72px"
  observation-row:
    typography: "{typography.body}"
    padding: "14px 0px"
  detail-panel-light:
    backgroundColor: "{colors.light-panel}"
    rounded: "{rounded.detail-panel}"
    padding: "{spacing.section}"
  detail-panel-dark:
    backgroundColor: "{colors.dark-panel}"
    rounded: "{rounded.detail-panel}"
    padding: "{spacing.section}"
  bloom-action-circle-light:
    backgroundColor: "{colors.light-panel}"
    textColor: "{colors.light-text}"
    rounded: "{rounded.action-circle}"
    size: "48px"
  bloom-action-circle-dark:
    backgroundColor: "{colors.dark-panel}"
    textColor: "{colors.dark-text}"
    rounded: "{rounded.action-circle}"
    size: "48px"
  setup-input:
    typography: "{typography.body}"
  story-place-filter-light:
    backgroundColor: "{colors.light-control}"
    textColor: "{colors.light-text}"
    typography: "{typography.body}"
    rounded: "{rounded.filter-pill}"
  story-place-filter-dark:
    backgroundColor: "{colors.dark-control}"
    textColor: "{colors.dark-text}"
    typography: "{typography.body}"
    rounded: "{rounded.filter-pill}"
  story-chapter-light:
    backgroundColor: "{colors.light-control}"
    textColor: "{colors.light-text}"
    typography: "{typography.body}"
    padding: "{spacing.group}"
  story-chapter-dark:
    backgroundColor: "{colors.dark-control}"
    textColor: "{colors.dark-text}"
    typography: "{typography.body}"
    padding: "{spacing.group}"
---

# Design System: Lucia Companion

## Overview

**Creative North Star: "Constellation / Observatory"**

Places use household outline icons, written names and explicit state labels. Their circles distinguish activity, unavailability and selection. The Observatory palette gives them quiet, warm surroundings in dark mode and pale, neutral surroundings in light mode. Outfit names screens and sections. DM Sans carries readings, control labels and explanations.

This record covers the built Avalonia Home screen, HomeConstellation, PlaceDetail, contextual Bloom actions, local Home setup, Customize section ordering, offline StoryOrbit Day, shell and control workbench. Home opens with an offline demo badge, synthetic reading time and a written attention summary. Its graph preserves explicit place order, and phone readings open in a separate detail view with Back. Story links a one-day dial, authored chapters and readable place timelines through one selection state. Authentication, live household and sensor bindings, and import remain unbuilt. This record describes the current UI; BUILD-PLAN.md owns the remaining work toward the full application.

Native sources own this system: `Lucia.Companion.App/App.axaml`, `CompanionTypography.cs`, `Styles/Controls.axaml`, `CompanionShell.axaml`, the files in `Screens/` and `Controls/`, and `ControlWorkbench.axaml`. Frontmatter lengths use CSS-compatible px notation for interchange. In native source these are Avalonia device-independent units, not physical screen pixels. Font sizes record the normal-size baseline; Android maps each text role through its native SP converter. The sidecar HTML/CSS snippets are illustrative normal-size representations for a design panel. They do not drive the native UI or qualify TalkBack, real-device accessibility, iOS, safe areas or performance.

**Key Characteristics:**

- Place icons pair names and written states with distinct state and selection rings.
- Semantic brushes switch together between light and dark.
- Observations distinguish current readings from last-known values and unknown times.
- Home and setup use separate native width thresholds for compact and expanded layouts.
- Configured connections and stable offsets give the home graph structure without a floor-plan claim.
- Contextual Bloom actions surround the selected place icon at normal text sizes and become a readable action list at large text sizes.
- Story keeps reported intervals, last-known values and missing reports visibly distinct.
- Home and Bloom remain static; Story optionally transitions only its selected time hand.
- Platform text settings resize individual type roles, with layout adaptations for larger text.
- Customize changes the order of the three Home sections without hiding any section or rearranging places.

## Colors

Warm amber marks attention and selection. Blue marks work in progress, muted red marks a reported problem, and sage marks a completed task step. Neutral controls leave written observations prominent.

### Primary

- Amber attention uses the light-accent and dark-accent tokens. Changed and Waiting share this role. In Home, a dotted circular amber ring marks the selected place. The workbench retains its rectangular selected frame as well as the circular ring.
- Story reuses amber for recorded lighting, selected chapters and selected place filters.

### Secondary

- Blue activity uses light-info and dark-info for Working.
- Sage uses light-sage and dark-sage for the completed Received step in the compact Lucia now fixture.
- Story reuses blue for Lucia request reports and sage for motion reports. These report kinds do not inherit Home's status meanings.

### Tertiary

- Muted red uses light-problem and dark-problem for Problem.

### Neutral

- Canvas tokens cover screen and workbench backgrounds.
- Panel tokens fill the selected-place detail container and the orb's pointer-over fill.
- Control tokens fill resting orbs and the theme buttons.
- Text tokens carry headings, names and readings.
- Story contact reports use the text role. Muted carries Last-known and unavailable spans; the graph-line role carries unreported track guides and ring scaffolding.
- Muted tokens carry explanations, timestamps and the default Quiet and Unavailable icon/state text.
- Line tokens draw orb outlines and observation dividers. In light mode LineBrush and MutedBrush intentionally have the same value; in dark mode they differ.
- Graph-line tokens draw panel borders, the detail icon border, configured connection lines and the faint graph dot field.

Each theme maps the same native resource keys to its own values. The sidecar records these bindings. These are discrete semantic pairs, not a native tonal scale. The sidecar's eight-step OKLCH ramps are generated preview aids, not colors used by the app.

**The Written State Rule.** Every place color has a visible state label. Color alone cannot distinguish Quiet from Unavailable or Changed from Waiting.

## Typography

Outfit is the display family. DM Sans is the body and label family. Both ship locally as static Regular and SemiBold faces with SIL OFL 1.1 licenses. Runtime resources exclude the original variable fonts. The type roles in frontmatter record the source requests; Avalonia resolves them against the packaged faces.

The title requests Medium, weight 500, although the package contains weights 400 and 600. Do not describe it as a separately bundled Medium face. Place names and observation values request SemiBold. Home constellation names and attention names use compact-value; constellation state labels use constellation-state. The explicit theme-button family and size are DM Sans at the body size; keep that override when using the Fluent button theme.

Text uses dynamic resources TextSize12, TextSize13, TextSize14, TextSize16, TextSize24, TextSize32 and TextSize34. Their default values match their numeric suffixes and the frontmatter baseline. CompanionTypography maps each role independently. On Android, MainActivity converts each base SP size with TypedValue.ApplyDimension and divides by display density to obtain native layout units. It reapplies the mapping on configuration changes. Android owns the font-scale curve, including nonlinear scaling where supported; this is not a global zoom of the interface. Other hosts retain the default resource values until a platform mapping is applied.

### Hierarchy

- Display names the workbench, Home setup and Story.
- Headline names sections and the selected place.
- Body carries explanations, observation labels and theme-button text.
- Value carries workbench place names and current observation values.
- Compact-value carries Home place names and attention names.
- Constellation-state carries Home place states.
- Label carries workbench orb states, observation timing, last-known values, task-step labels and sample notices.
- Story-time carries the dial's selected local time.
- Time-detail carries dial offsets, selected-time captions, timeline ruler labels and chapter rail times.

Line height and tracking use native defaults. No custom scale ratio, uppercase label convention or character-width limit is established.

**The Two Family Rule.** Use Outfit for titles and section headings, and DM Sans for control labels, readings and supporting text.

**The Native Text Rule.** Bind text to its matching TextSize resource. Keep geometry independent of the platform's per-role text mapping, and adapt constrained compositions when text needs more room.

## Layout

### Home and setup

Home and setup use 16-unit outer margins and composition gaps. Home changes to its expanded layout when its own Bounds.Width reaches 900 units. At the default section order, the graph and Lucia now sit in the main column; attention and selected-place details share a 340-unit side column. Below that threshold, all three sections follow the saved order in one column. Selecting a place opens a separate vertically scrolling detail view with "Back to your home". Back and Escape return to the graph and restore focus to the selected place. Opening phone detail focuses Back.

The default section order is Your places, Needs a look, Lucia now. Expanded Home puts the first preferred section at the top of the wide main column and always keeps Your places in that column. If Your places is first, the last preferred section sits below it and the middle section occupies the side column. If another section is first, Your places sits below that section and the remaining section occupies the side column. Selected-place detail follows the side section. Customize remains a footer Expander in the Home scroll region, outside this section grid.

When Home is narrower than 600 units and its home-name font exceeds 32 units, the offline badge moves below the name. The name then spans both header columns.

Setup changes at its own Bounds.Width of 800 units. Expanded setup uses two equal columns, a place list on the left and the selected place's form on the right. Compact setup replaces the list with a place chooser and stacks the form below it. The header and forms share one scrolling region; Save and Cancel or the discard question sit outside it. These are control-width thresholds, not physical device categories or browser viewport breakpoints.

The Home constellation keeps the supplied place order. Its panel starts with a 108-unit slot, multiplies that slot by the largest orb FontSize divided by its normal size of 16, and divides available width by the resulting slot width to derive column count. Larger text increases slot width and can reduce the column count. The cap remains three columns below 800 units of panel width and four above it. Child width can grow to 152 times that text ratio. The panel measures each row from the tallest child, with a minimum row height of 148 units. Up to six places use fixed varied offsets. Larger homes continue in stable rows with alternating vertical offsets. No physics, animation, automatic sorting by state or inferred room position affects placement.

**The Stable Home Rule.** Preserve explicit room order through selection, pinning and state display. Place reordering belongs to setup's Move earlier and Move later controls. Customize changes section order independently of room order.

### Story Day

Story is one vertical ScrollViewer with a 16-unit horizontal and 20-unit vertical margin, section spacing, and no horizontal page scroll. At its own Bounds.Width of 900 units it places a 400-unit dial in a 420-unit left column and chapters in the flexible right column, with a 24-unit column gap. Below that threshold the dial sits above chapters and measures the available width minus 32 units, clamped between 240 and 340 units.

When the mapped CenterTime font exceeds 36 units, selected time and offset move below the dial. Its center caption reads "Time below". Chapter icon/time rails measure their content from a 52-unit minimum instead of fixing the rail width.

Place filters always form a manually scrolling horizontal strip, not an automatically rotating carousel. A native chooser containing All places and every configured place appears additionally when there are more than six places. The dial draws at most six visible place rings and prints their outer-to-inner order and count. The straight timelines remain available for every place in the current filter; the ring budget never limits access to reports.

Place by place follows the dial and chapters after a thin divider. Each place has four independent tracks in Lighting, Contact, Motion and Lucia order, a visible native time ruler and wrapping report buttons. A 24-unit gap separates timelines. Ruler positions cover the day boundary, quarter-elapsed instants and the next-day end. When the widest label needs more than one-sixth of the available ruler width, the first- and third-quarter labels hide, retaining the start, midpoint and next-day end. Local clock labels include offsets so repeated or skipped clock times do not change the elapsed-time coordinate system.

**The Independent Tracks Rule.** A report on one track supplies no evidence for another. Unmarked ring portions mean no supplied report, and dotted timeline guides remain unknown until a report covers them.

### Control workbench and shared atoms

The workbench remains a vertically scrolling stack with horizontal scrolling disabled. It has an outer margin of 24 units, a maximum content width of 1040 units, and the section spacing token between major blocks. Related headings, descriptions and panel content use the related spacing token.

Workbench theme buttons and place controls sit in horizontal WrapPanels. They wrap when available width runs out without width breakpoints. This workbench arrangement is separate from Home's measured constellation panel and responsive screen composition.

A PlaceOrb has minimum bounds of 100 by 126 units, with 10 units of padding and a 6-unit gap between its circular icon, name and state. Names wrap within a maximum width of 132 units. The orb may grow when its text needs more space.

An ObservationRow has two columns, a flexible label and an auto-sized value, separated by 12 units. Its value wraps within a maximum width of 150 units. A 5-unit gap separates row content and supporting text. Vertical row padding is 14 units. Frontmatter translates native Thickness `0,14` to CSS padding order for interchange.

## Elevation & Depth

Screens and panels use tonal layering and outlines. Panels carry a 1-unit GraphLineBrush border. The connection layer sits behind the native place buttons, with 1-unit lines inset 40 units from each endpoint and a 24-unit dot grid at opacity 0.28. These configured connections are also named in PlaceDetail.

Bloom is the deliberate lifted exception. Its opaque PanelBrush ellipse separates the radial actions from underlying text. A half-opacity black shade dims Home behind it. The action circles use native BoxShadow `0 4 14 0 #40000000`; the selected center uses `0 5 22 0 #60000000`. The sidecar translates these Avalonia ARGB shadows to CSS RGBA for previews. There is no blur backdrop, animated elevation or glow. Theme buttons and form controls retain their Fluent template for interaction details not explicitly overridden here.

**The Still Observatory Rule.** Home, place states and Bloom remain static. Story may transition its dial hand for 280 milliseconds with native CubicEaseOut when selection changes. The "Animate the time hand" checkbox below the timelines disables that transition. Reports, chapters and time selection remain usable without motion; no automatic playback or rotating carousel exists.

## Shapes

The signature shape is a circle with a centered household outline icon. Its diameter and radius come from the place-orb component and orb radius tokens. The icon is 28 units square, drawn with a 1.7-unit stroke from authored 24-unit path geometry.

A dotted circular amber selection ring extends 6 units beyond the place circle and uses a 1-unit stroke with dash array `2,4`. In Home, selection leaves the surrounding rectangular frame transparent. A separate rounded rectangular frame encloses the full button for keyboard focus. It uses the focus-frame radius, a 2-unit border and the text role. The workbench also retains an amber selected rectangular frame.

Home Quiet places have neither a circle fill nor a state ring. Unavailable places have a transparent fill and a 1-unit dotted state ring with dash array `2,4`. Changed, Waiting, Working and Problem use colored 2-unit circles. Written names and states remain beneath every icon.

Panels use the detail-panel radius. Their default padding is the section token; Home overrides individual card padding to 12 or 16 units. PlaceDetail has a 56-unit circular icon anchor. BloomActionButton has a 48-unit action circle, a 21-unit icon and a small rounded caption. Its whole button measures 88 by 94 units. Observation dividers are straight 1-unit bottom borders. Theme-button and form-field shapes remain Fluent defaults rather than new project radius tokens.

Story adds concentric time rings, rounded place-filter pills and circular outline chapter marks. Recorded lighting uses a line; contact uses a diamond; motion uses a circle; Lucia uses a plus. The written legend names every marker. Last-known intervals use native dash array `3,2`; unavailable spans and no-data guides use `1,3`. Recorded non-lighting intervals add a marker at their start. These shapes describe reported evidence, never occupancy.

## Components

### Theme buttons and shell

System, Light and Dark are native buttons that change the application's RequestedThemeVariant. Their minimum height is 48 units. They explicitly use body typography and the control/text brush pair. Each has an accessible name that says which theme it selects. Their hover, focus, pressed and disabled presentation otherwise belongs to the installed Fluent template; no custom primary, destructive or ghost button family exists.

CompanionShell exposes Options, Setup and Story. Its Options context menu contains an Appearance submenu for System, Light and Dark and separate Customize Home and Control lab entries. Story opens Story Day and becomes Home while that screen is visible. Customize Home returns to Home, closes detail and actions, expands the Customize footer, and brings it into view with focus. Setup disables Setup, Story, Customize Home and Control lab until Save or Cancel returns Home; appearance remains available. A bottom status line can expose Retry save. There is no tab bar.

Native Back and Escape first close Home's open Bloom, then compact place detail, then an expanded Customize footer. Collapsing Customize focuses its Expander. Back from Story or Control lab returns Home. At the Home root with no open context the request stays unhandled so the OS can leave the app. Setup handles Back through its draft-discard question. The shell subscribes to its current TopLevel's BackRequested event while attached and unsubscribes when detached.

Android temporarily sets `android:enableOnBackInvokedCallback="false"` in its manifest. In the API 36 emulator, default predictive Back bypassed the app callbacks even though the headless route checks passed. The checked APK with this compatibility opt-out returned from native Garden detail to Home instead of the launcher. Predictive Back and its gesture animation still need separate qualification before this temporary opt-out can be removed.

The Android activity reuses one cached view only within the running process. An API 36 emulator density change and reset triggered recorded activity destruction and recreation in the same process, retaining an edited setup draft. This is not unsaved-draft storage. Process death restores saved setup metadata, pins and section-order preferences only.

### PlaceOrb

A native Button combines the circular icon, place name and written state. Activating it selects the place and opens its readings according to the compact or expanded Home layout. Selection and pinning are separate Boolean properties. Neither changes the place's state or order. A small amber pin path appears at the icon's upper right when pinned.

Quiet and Unavailable retain muted icon/state text. Home gives them the distinct ring treatments in Shapes. Changed and Waiting use amber. Working uses blue. Problem uses muted red. The place name keeps the text role in every state. Pointer-over uses PanelBrush for active circles; Home Quiet and Unavailable retain their transparent fills. Pressed lowers the icon group opacity to 0.7; disabled lowers the whole control to 0.5. Keyboard focus uses the text-colored rectangular border independently of the circular selection ring.

The accessible name contains the place name, state and reason. Selection updates its help text. A tooltip carries the reason. These are implemented hooks, not a claim of completed screen-reader qualification.

### HomeConstellation and ConnectionLayer

ConstellationPanel arranges native PlaceOrb buttons above a noninteractive ConnectionLayer. Lines show configured physical neighbors, not a floor plan or sensor ownership. The visible explanation states this limit. An empty home shows an invitation to add a room, porch or garden.

Each orb has a native context menu for View readings, Place actions and Toggle pin. Home help text announces selection and pinning. The selected icon is the placement anchor for Bloom opened from the graph.

### ObservationRow

A read-only row pairs a label with a semibold value, then shows timing or a last-known explanation below. Current requires a current quality, a value and an observed time. Stale, Unavailable or Unknown occupy the current-value position when that condition is unmet.

A retained old value appears as "Last known" with its observation time. It never occupies the current-value position. Without an old value, the timing line carries the observed time or "Observation time unknown". The row's accessible name combines its qualified content. A bottom divider separates readings.

### PlaceDetail and detail panels

PlaceDetail contains the selected icon, name, written state and reason, a Place actions button, named physical connections and ObservationRows. It explains when there are no configured connections or observation bindings. Tablet shows it alongside the graph; phone shows it in the separate detail view. The workbench retains its simpler selected-place panel.

### BloomActions and BloomActionButton

Bloom is a native overlay Popup with centered placement relative to the selected graph icon, or the PlaceDetail icon when opened from phone detail. At normal text size its radial composition measures 288 by 292 units, with Readings above and Pin place or Unpin and Close below the selected center icon. The opaque circular backing and shaded Home keep actions clear of underlying text. It is contextual and static.

When Bloom's mapped body FontSize exceeds 20 units, a 288-unit-wide panel replaces the radial layout. It has the place heading and full-width Readings, Pin place or Unpin, and Close native buttons in a vertical ScrollViewer. The scroll area has a 400-unit maximum height, with an additional cap based on available top-level height. Opening focuses the visible layout's Readings button. Both layouts invoke the same actions and dismissal paths.

The named circular action buttons are native Buttons. Pointer-over colors their circle border amber. Keyboard focus uses a 3-unit text-colored circle border. Pressed lowers the whole action's opacity to 0.7. Labels stay visible; no hover, hold or special gesture is required.

The Popup uses light dismissal and the overlay layer. Opening focuses Readings. Close, Escape and outside dismissal close it and restore focus to the phone Place actions button or selected graph orb. Readings closes Bloom and opens the selected place's detail. Pin toggles the named pin state while leaving Bloom open. These are implemented focus paths, not a claim of a qualified modal focus trap on every platform.

### AttentionList

Needs a look contains native full-width buttons for Problem, Waiting and Unavailable places, in that priority order. Each button has a semibold 14-unit place/state label and a 13-unit reason limited to two visible lines with ellipsis. The accessible name retains the full reason. Activating a row opens that place's readings. Empty attention has a written "Nothing needs a look in this sample" state.

### LuciaNow

The compact fixture has three equal columns. Received · done is a sage outlined 16-unit circle. Reading · current is a filled blue circle. Result · pending is a thin neutral outline. Written stage names accompany every marker. Its example request and explicit synthetic/no-running-request notice are part of the control. No request execution, animated progress or completed physical outcome is implied.

### HomeEditorScreen

Setup uses labelled native TextBoxes for home and place names, a place ListBox or ComboBox according to width, an icon ComboBox and named neighbor CheckBoxes. TextBoxes and chooser/neighbor targets have a 48-unit minimum height where set by source. Inputs use DM Sans body typography, with Fluent field, selection and focus presentation.

Add place, Move earlier, Move later and Remove place edit a draft. Removal reveals Confirm removal and Keep place inline. Validation and unreadable-profile replacement warnings use ProblemBrush and written text. Save setup or Replace saved setup and Cancel sit below the scrolling header and form. Local storage contains setup metadata, pins and Home section order only. It does not establish visual tokens or persist sensor readings.

Back or Escape first closes an open removal question. With pending draft changes it asks "Discard the unsaved setup changes?" with Keep editing and Discard changes. The pending-change check includes invalid, uncommitted field text, icon changes and neighbor changes. While this question is visible, editing is disabled and normal Save/Cancel commands hide. Focus starts on Keep editing. Back again closes the question and retains the draft. With no changes, Back returns Home directly. The explicit Cancel command still leaves setup immediately without saving.

### Customize and HomeSectionOrder

Customize is a native footer Expander with three labelled rows: Your places, Needs a look and Lucia now. HomeSectionOrder uses the existing body text, muted supporting text and theme buttons. Rows have a flexible wrapping label followed by two auto-sized movement columns, with 8-unit column and row gaps. A 12-unit stack gap separates the instruction, row list and restore button.

The earlier and later buttons have a 48-unit minimum width and height. Their authored 20-unit outline paths use the shared 1.7-unit stroke: `M4,14 L12,6 20,14` for earlier and `M4,8 L12,16 20,8` for later. Native accessible names and tooltips say "Move [section label] earlier" or "Move [section label] later". No dragging or glyph icon is required. The first earlier and last later button disable at their boundaries.

Moving swaps adjacent sections, retains focus on an available movement button for that section, and brings it into view. Restore default order resets Your places, Needs a look, Lucia now and focuses the Your places later button. Its caption is a wrapping native TextBlock, and the button has the explicit accessible name "Restore default order". Restore disables when the order already matches the default. All three sections must appear exactly once; this control never hides attention or invents a new section.

HomeViewModel owns the order and preference notification; controls only request changes. HomeProfileSession saves it with local setup and pins. SectionOrder is an optional additive field in the existing version-1 profile, and a missing field restores the default. Loading restores the preference without initiating an automatic save. The visible storage status remains the authority for whether saving succeeded.

This increment has native desktop/headless evidence. It does not establish an installed Android or real-device result for Customize; earlier emulator checks belong to their stated increments.

### PlaceIcons

Home, Garden, Door, Sofa, Kitchen and Bed are authored vector paths in `PlaceIcons.cs`. They share the same stroke treatment inside PlaceOrb. Use those paths rather than a character glyph or platform emoji. The sidecar includes the actual path geometry as inline SVG representations.

### StoryScreen and StoryOrbit

The built Day screen is explicitly sample history for 23 October 2026 in Europe/Zurich, not live household data. StoryViewModel holds one selected instant, place filter and optional chapter selection for the dial, chapter list, scrubber and timelines. Choosing a chapter moves the shared cursor to its start and highlights its outer arc. Choosing a report sets its place and start time. A pointer on the dial or track is an accelerator; named buttons and the native slider provide equivalent time selection.

StoryDay maps elapsed instants between home-local day boundaries, including daylight-saving changes. The heading states the home timezone and elapsed day length. At normal text size the dial center shows local time, UTC offset and Selected time or Chapter start; larger selected time moves below it as described in Layout. The native slider's arrows move one elapsed minute and page keys move one elapsed hour. Its accessible help includes the selected time.

The dial uses at most six place rings, recorded report arcs, hour tick marks and a shared time hand. Rings without supplied reports are dotted; otherwise their neutral base is a guide, not a claim of continuous data. Lighting arcs use a 3-unit stroke; other report arcs use 5 units. StoryDrawing supplies the marker, brush and dash grammar shared with the straight tracks. Native drawing stays separate from the textual evidence.

### StoryLegend and PlaceFilterBar

StoryLegend wraps named markers for Light report, Door / gate, Motion report, Lucia request, No data and Last-known. PlaceFilterBar uses native 48-unit-minimum theme buttons with the filter-pill radius. A selected filter has a 2-unit AccentBrush border; an unselected filter has a 1-unit GraphLineBrush border. Selected help text names its state. The optional all-place ComboBox supplements the strip beyond six places.

### ChapterList

Authored sample chapters are native full-width buttons. Each has a content-sized icon/time rail with a 52-unit minimum, an 8-unit column gap and 12-unit padding. The rail contains a 30-unit outlined circle with a 16-unit household icon and a written start time that does not wrap. The content has a semibold title, full local start/end range with offsets, and evidence labels. A selected chapter uses a 2-unit amber border; unselected chapters have no visible button border. Tablet aligns these beside the dial; phone stacks them below it. An empty filter explains that no recorded chapters were supplied.

Chapters reference recorded evidence that intersects their interval. Evidence ranges clip to that chapter rather than borrowing a report's full duration. These chapters are authored fixtures, not generated behavioral summaries.

### PlaceTimeline, TimelineTrackLayer and TimelineRuler

Each place heading has its authored icon and name. Beneath it, a 64-unit drawing area holds four lanes at 14-unit intervals. Dotted neutral guides extend across each lane. Supplied intervals draw only on their own kind's lane, followed by a common vertical time cursor. The visible ruler labels time and offset at up to five elapsed-day positions, reduces quarter labels when cramped, and names the final boundary "next day".

Every supplied report also has a native button with its complete start/end range, both offsets, label and quality. Its accessible name names the place and report kind; accessible help identifies the synthetic source. Buttons wrap within available width. A place with no reports says that all four tracks are unknown. Last-known is dashed, unavailable is dotted, and missing reports never imply off, closed or unoccupied. Motion markers represent one-minute report buckets, not presence intervals.

Story has no service history or history persistence. A changed Home topology rebuilds the sample Story from the current demo setup. The screen explicitly says historical binding policy is not implemented. Native headless rendering establishes the reviewed layout. A prior API 36 emulator smoke check covered Story launch, touch scrolling, chapter/cursor selection and in-app return Home. Scoped platform-hardening emulator checks also covered native Back with the temporary manifest opt-out, rotation, Android font_scale 2, focused-field visibility with the keyboard, and same-process activity recreation with draft retention. The corrupt-profile warning preserved the existing file bytes. These checks do not qualify predictive Back gesture animation, TalkBack, real devices, iOS, safe areas or performance.

## Do's and Don'ts

### Do:

- Do use the semantic brush pairs for light and dark rather than hardcoding a single theme into a native control.
- Do show each place's name and written state alongside its icon.
- Do retain the explicit DM Sans body-size override on theme buttons.
- Do keep observation quality, last-known values and observation time visible.
- Do identify Home, Story and workbench data as synthetic before their observations or history.
- Do use the authored household paths for the implemented place icons.
- Do preserve explicit place order and keep pinning independent of graph position.
- Do keep Home's circular selection ring distinct from its rectangular keyboard-focus frame.
- Do attach Bloom to the selected icon, keep its named actions legible and restore focus when dismissed.
- Do keep all place timelines reachable when the dial's six-ring budget is exceeded.
- Do preserve separate evidence tracks, full local time ranges, offsets, quality and source help.
- Do provide the native slider and chapter/report buttons alongside decorative dial input.
- Do keep the Story hand transition optional with the visible animation checkbox.
- Do use dynamic TextSize resources for platform-adjusted type and let constrained layouts adapt.
- Do keep all Bloom actions reachable in its large-text list.
- Do preserve pending setup changes on Back until the user chooses Discard changes.
- Do keep every Home section present when reordering and expose named movement controls with disabled boundaries.

### Don't:

- Don't use color as the only indication of place state or selection.
- Don't promote an old reading to the current-value position.
- Don't infer locked from closed, occupancy from Quiet, or a physical result from a completed request.
- Don't present authentication, live bindings or import as built.
- Don't claim real-device accessibility, safe-area or performance qualification from desktop renders.
- Don't treat illustrative sidecar HTML/CSS or generated color ramps as native implementation tokens.
- Don't treat configured graph connections or static layout offsets as a measured floor plan.
- Don't treat unmarked ring segments, unknown track spans or absent chapters as proof that nothing happened.
- Don't convert motion report buckets into occupancy or continuous presence.
- Don't claim live history, persisted history or a historical binding policy from the authored Story fixture.
- Don't treat the in-process activity-view cache as persisted unsaved drafts.
- Don't claim TalkBack, device, iOS or performance qualification from text-resource mapping or headless checks.
- Don't confuse Home section order with the stable order of places in the graph.
