---
name: Lucia Observatory
description: A compact operational dashboard built from warm mineral neutrals, amber focus, and restrained depth.
colors:
  void: "var(--color-void)"
  obsidian: "var(--color-obsidian)"
  charcoal: "var(--color-charcoal)"
  basalt: "var(--color-basalt)"
  slate-warm: "var(--color-slate-warm)"
  stone: "var(--color-stone)"
  ash: "var(--color-ash)"
  dust: "var(--color-dust)"
  fog: "var(--color-fog)"
  mist: "var(--color-mist)"
  cloud: "var(--color-cloud)"
  light: "var(--color-light)"
  bright: "var(--color-bright)"
  amber: "var(--color-amber)"
  amber-dim: "var(--color-amber-dim)"
  amber-glow: "var(--color-amber-glow)"
  amber-pale: "var(--color-amber-pale)"
  rose: "var(--color-rose)"
  rose-dim: "var(--color-rose-dim)"
  sage: "var(--color-sage)"
  sage-dim: "var(--color-sage-dim)"
  ember: "var(--color-ember)"
  info: "var(--color-info)"
  on-accent: "var(--color-on-accent)"
typography:
  headline:
    fontFamily: "Outfit, system-ui, sans-serif"
    fontSize: "1.5rem"
    fontWeight: 600
    lineHeight: "2rem"
    letterSpacing: "normal"
  title:
    fontFamily: "Outfit, system-ui, sans-serif"
    fontSize: "1.25rem"
    fontWeight: 600
    lineHeight: "1.75rem"
    letterSpacing: "normal"
  body:
    fontFamily: "DM Sans, system-ui, sans-serif"
    fontSize: "0.875rem"
    fontWeight: 400
    lineHeight: "1.25rem"
    letterSpacing: "normal"
  control:
    fontFamily: "DM Sans, system-ui, sans-serif"
    fontSize: "0.875rem"
    fontWeight: 600
    lineHeight: "1.25rem"
    letterSpacing: "normal"
  label:
    fontFamily: "DM Sans, system-ui, sans-serif"
    fontSize: "0.75rem"
    fontWeight: 500
    lineHeight: "1rem"
    letterSpacing: "0.05em"
rounded:
  md: "6px"
  lg: "8px"
  xl: "12px"
  "2xl": "16px"
  full: "9999px"
spacing:
  "1": "4px"
  "1.5": "6px"
  "2": "8px"
  "2.5": "10px"
  "3": "12px"
  "4": "16px"
  "5": "20px"
  "6": "24px"
  "8": "32px"
components:
  button-primary:
    backgroundColor: "{colors.amber}"
    textColor: "{colors.on-accent}"
    typography: "{typography.control}"
    rounded: "{rounded.xl}"
    padding: "10px 20px"
  button-secondary:
    backgroundColor: "{colors.basalt}"
    textColor: "{colors.fog}"
    typography: "{typography.body}"
    rounded: "{rounded.xl}"
    padding: "10px 20px"
  input:
    backgroundColor: "{colors.basalt}"
    textColor: "{colors.light}"
    typography: "{typography.body}"
    rounded: "{rounded.xl}"
    padding: "12px 16px"
  navigation-item:
    textColor: "{colors.fog}"
    typography: "{typography.body}"
    rounded: "{rounded.lg}"
    padding: "10px 12px"
  theme-selector:
    backgroundColor: "{colors.basalt}"
    rounded: "{rounded.lg}"
    padding: "4px"
---

# Design System: Lucia Observatory

## Overview

**Creative North Star: "The Daylight Observatory"**

Lucia Observatory is an Operate-mode system for long technical sessions. It keeps the authenticated dashboard dense and predictable while warm mineral neutrals, amber focus, and small changes in depth preserve the product's identity in both bright and dim rooms.

Theme choice extends the existing shell instead of becoming a separate destination. System, Light, and Dark use one semantic role vocabulary, so setup, login, navigation, charts, dialogs, and operational pages keep the same hierarchy when the resolved mode changes.

Companion devices at `/companion-devices` extends this Operate system with owner approval and session management. The route keeps code matching, device identity and catalog-backed exact read access in one inline task, followed by issued sessions. It uses the existing Observatory identity and tokens. The direction contract in the first body comment of `lucia-dashboard/index.html` records `not-applicable-narrow-extension`; this increment introduces no new visual world.

Shared home layout extends the same route with an inline owner draft, complete review and checked save. Owners choose canonical places, display names, icons, exact source bindings and explicit physical neighbors. The first body comment of `lucia-dashboard/index.html` now records this narrow extension, with the earlier companion approval contract retained below it. Shared metadata never grants device access or changes Home Assistant.

**Key Characteristics:**
- Compact operational controls and route-first composition
- Warm neutral surfaces with amber reserved for focus, selection, and progress
- One semantic token vocabulary across light and dark modes
- Borders, translucency, and low glows instead of heavy elevation
- Inline device review and explicit access choices before owner approval
- Inline shared-layout drafts with complete review, checked save and explicit recovery choices

## Colors

The palette uses named material and state roles. Dark mode supplies the default values; `html[data-theme='light']` replaces their values without changing the names consumed by components.

### Primary

- **Amber:** The focus, active navigation, primary action, progress, and selected-control role. It resolves to `#e2a84b` in dark mode and `#8c5b0f` in light mode.
- **Dim Amber:** The quieter edge and connector role for active graph states. It resolves to `#c4923f` in dark mode and `#724808` in light mode.
- **Amber Glow:** The primary hover role. It resolves to `#c49030` in dark mode and `#714806` in light mode.

### Secondary

- **Sage:** Success and ready states. It resolves to `#7dab8c` in dark mode and `#397151` in light mode.
- **Rose and Ember:** Error text, borders, and low-opacity error fills. Rose resolves to `#d4756b` in dark mode and `#a13f3a` in light mode; ember resolves to `#c45a4a` and `#b42318`.
- **Information Blue:** Tool activity and informational graph states. It resolves to `#60a5fa` in dark mode and `#2563eb` in light mode.

### Neutral

- **Void:** The page canvas. It resolves to `#0d0c0a` in dark mode and `#f1f2ef` in light mode.
- **Obsidian:** The sidebar and shell background. It resolves to `#12110f` in dark mode and `#fbfcfa` in light mode.
- **Charcoal and Basalt:** Panel and control surfaces. They resolve to `#171512` and `#1a1816` in dark mode, then `#ffffff` and `#f5f6f3` in light mode.
- **Stone and Ash:** Borders, dividers, inactive graph edges, and scrollbars. They resolve to `#2e2b28` and `#3d3935` in dark mode, then `#d1d5ce` and `#b4bab1` in light mode.
- **Dust, Fog, Cloud, and Light:** Muted, secondary, body, and high-emphasis text. Their dark values run from `#918b84` to `#ece8e2`; their light values run from `#626861` to `#1c211c`.
- **On Accent:** Primary-button text. It resolves to `#17130d` in dark mode and `#fffdf9` in light mode.

### Named Rules

**The Semantic Remap Rule.** Page and component code uses semantic tokens such as `void`, `basalt`, `fog`, `light`, `amber`, `sage`, and `rose`. Only the root theme definition changes their values; page components do not carry light-specific or dark-specific classes.

**The Amber Restraint Rule.** Amber marks focus, selection, primary action, progress, or active system state. Neutral surfaces carry ordinary structure.

## Typography

**Display Font:** Outfit, with `system-ui` and `sans-serif` fallbacks
**Body Font:** DM Sans, with `system-ui` and `sans-serif` fallbacks
**Label/Mono Font:** DM Sans for labels; the browser monospace stack for keys and code

**Character:** Outfit gives headings and the Lucia wordmark a compact geometric shape. DM Sans keeps dense navigation, forms, status text, and data readable without making the dashboard feel like a generic admin template.

### Hierarchy

- **Headline** (600, `1.5rem`, `2rem`): Login, setup, and major page headings.
- **Title** (600, `1.25rem`, `1.75rem`): Setup steps and primary section titles.
- **Body** (400, `0.875rem`, `1.25rem`): Navigation labels, instructions, form text, and operational content.
- **Control** (600, `0.875rem`, `1.25rem`): Primary actions and high-emphasis controls.
- **Label** (500, `0.75rem`, `1rem`, `0.05em`): Field labels and compact shell captions; uppercase appears only on short structural labels.
- **Micro label** (500, `0.625rem`): Theme options, step metadata, and terse status details.

### Named Rules

**The Compact Type Rule.** Operational text stays between `0.625rem` and `1.5rem`. Larger display type is absent from the shipped dashboard.

## Layout

The authenticated shell reserves a fixed `256px` sidebar from the `768px` breakpoint upward. Main content uses `16px` horizontal padding on small screens, `24px` from `640px`, and `32px` from `1024px`. The current route remains the primary content in the first viewport.

Below `768px`, the sidebar becomes an off-canvas drawer with a dimmed overlay and a sticky `56px` top bar. The drawer keeps the full `256px` width. Its transform uses a `300ms` standard easing transition, while opacity changes use `200ms ease`.

Public setup and login screens center a single column over the observatory canvas. Login is capped at `384px`; setup is capped at `672px`. Mobile screens reserve `96px` above the content so the fixed theme selector does not overlap the task, then remove that offset from `640px` upward.

The theme selector sits at the upper-right shell edge on loading, setup, and login views with a fixed `170px` width. After authentication it moves into the sidebar footer under the Appearance label.

Companion devices centers a single column capped at `64rem`, with `32px` between major groups. Its heading and security guidance precede the labelled code form. Review expands inside the approval panel; connected sessions follow below. The panel uses `20px` padding, increasing to `24px` at `640px`. Forms and action rows wrap, long identities break within their available width, and the approval code can wrap between its two five-character groups. Actions and checkbox labels in this route have a `3rem` minimum height. These are local task adaptations, not replacements for the shared shell or compact control scale.

Shared home layout opens inline between device approval and issued sessions. The editor stacks place selection and details below `1280px`; at that breakpoint it uses two columns with a `1:1.6` ratio and zero minimum track widths. Fieldsets, rows and text containers can shrink. Source names and legends wrap, while exact source IDs can break anywhere to stay inside the panel during enlarged phone text stress. Review and recovery remain vertical lists with stone dividers and wrapping action rows. No new palette, elevation or entrance motion accompanies this editor.

### Named Rules

**The Route-First Rule.** Theme controls stay at the shell edge. They do not displace the current workflow or become a page-level feature.

## Elevation & Depth

The system is layered rather than lifted. Surface changes and low-contrast borders separate the canvas, shell, panels, and controls. Public cards add an 80% basalt glass fill, a softened stone border, and `12px` backdrop blur. Amber glows are limited to identity marks and public cards: `0 0 20px -5px` at 15% amber and `0 0 10px -3px` at 12% amber. Active mesh nodes may add a compact `12px` state-colored glow.

### Shadow Vocabulary

- **Identity glow** (`0 0 20px -5px color-mix(in srgb, var(--color-amber) 15%, transparent)`): Lucia marks on public screens.
- **Panel glow** (`0 0 10px -3px color-mix(in srgb, var(--color-amber) 12%, transparent)`): Login and setup glass panels.
- **Active-state glow** (`0 0 12px` with the current state border at 25% alpha): Processing mesh nodes only.

### Named Rules

**The Restrained Depth Rule.** Borders and tonal layering do the structural work. Glow identifies a live or branded focal point; it does not decorate ordinary containers.

## Shapes

Controls use gently curved corners in a short scale: `6px` for compact icon and segmented buttons, `8px` for navigation items and small containers, `12px` for fields, actions, graph nodes, and operational cards, and `16px` for public login and setup panels. Full circles are reserved for progress steps, spinners, and status dots.

Borders are one pixel and low contrast. Active navigation adds a `3px` amber bar on the left with only its right corners rounded, preserving the vertical edge of the shell.

## Components

### Buttons

- **Shape:** Primary and secondary actions use a `12px` radius and compact `10px 20px` padding.
- **Primary:** Amber background, on-accent text, `0.875rem` semibold label. Hover changes to amber-glow; disabled controls keep their shape and drop to 40% opacity with a blocked cursor.
- **Secondary:** Basalt background, stone border, fog text. Hover shifts the border toward amber and the text toward light.
- **Success:** A low-opacity sage fill with sage text, used for connection tests and positive actions rather than general confirmation copy.

### Cards / Containers

- **Corner Style:** Operational cards use `12px`; public auth and setup panels use `16px`.
- **Background:** Operational cards use basalt at 50% or charcoal. Public panels use the shared glass treatment.
- **Shadow Strategy:** Most cards have no shadow. Public panels use the small amber panel glow.
- **Border:** One-pixel stone borders separate nested operational regions.
- **Internal Padding:** `20px` for operational cards and `24px` to `32px` for public panels.

### Inputs / Fields

- **Style:** Basalt fill, stone border, light text, muted dust placeholder, `12px` radius, and `12px 16px` padding.
- **Focus:** Remove the default outline, change the border to amber, and add a one-pixel amber ring at 30% alpha.
- **Error / Disabled:** Error feedback uses rose text with ember border and fill at low opacity. Disabled actions use opacity rather than a separate color system.

### Navigation

The desktop navigation is a vertically scrolling list of `0.875rem` medium labels with `18px` Lucide icons. Items use an `8px` radius and `10px 12px` padding. Hover adds a low stone fill; the active route changes to amber, adds an 8% amber fill, and shows the left indicator. On mobile, labeled icon buttons open and close the same navigation in an off-canvas drawer.

### Theme Selector

System, Light, and Dark form a three-column segmented control. Each option has a Lucide icon, a visible `0.625rem` label, a descriptive tooltip, an accessible name, and `aria-pressed` state. The group has `role="group"` and the accessible name Theme. Selected options use the obsidian surface, amber text, and a small shadow; unselected options use dust text and a stone hover fill. Keyboard focus uses a two-pixel amber ring at 60% alpha.

System is the fallback for missing or invalid stored values. It follows `prefers-color-scheme` and updates when the operating system changes. Explicit Light or Dark choices persist under `lucia-theme`. A head script resolves the saved preference before React renders, and the provider keeps `data-theme` and the native `color-scheme` property synchronized.

### Agent Mesh

The mesh is a fixed-height `420px` charcoal field with a stone border and `12px` corners. Nodes share the same shape and semantic state colors: amber for processing, blue for tool calls, sage for response generation, rose for errors, and neutral basalt for idle. Active edges thicken from one to two pixels and animate; active non-error nodes pulse without becoming draggable or selectable.

### Companion device review and grants

The owner route reuses Outfit headings, DM Sans forms, labelled Field controls and decorative Lucide outlines. Charcoal panels and stone dividers organize the task. Amber marks the code, keyboard focus and approval action; rose text identifies decline and revoke actions. No new palette, depth treatment or entrance motion is introduced.

Review shows the intended device's untrusted name, the normalized code, home ID, current host and expiry. The owner must match the code with the intended person. Changing the code clears the review and confirmation; expiry also clears them and asks for a new code. A successful review focuses the device heading. Status and error messages have written feedback and a programmatic focus target.

The grant editor keeps the owner-assigned Member ID as a manual field until a membership roster exists. Places and observation fields come only from the authorized home catalog, with exact, case-sensitive place IDs shown beside names and source counts. Selecting a place leaves its fields off. Each selected place needs at least one available Temperature, Humidity, Moisture, Light, Contact, Motion or Leak field before approval. A place without supported fields cannot be selected. No places means no place observations. Activity summaries, historical observations and Engine runtime summaries remain independent choices, each off by default. Engine permission covers approved summaries, never administration or raw conversations. Approval also requires the intended-person confirmation.

The picker uses stone dividers, native checkboxes and wrapping observation labels inside the existing panel. Search matches a place name or exact ID. Chosen places stay at the top even when they do not match the query; the picker starts with 25 unselected matches and Show more places adds 25. The selection cap is 200 places, with a separate 16,000-byte UTF-8 approval request limit. An oversized selection stays editable with inline guidance to remove places or observations; approval stays disabled and submission is blocked. The page checks this size before clearing the review. Loading, an empty supported catalog and unavailable or invalid metadata have distinct written states. Reload available places clears selected places and intended-person confirmation before fetching again. There is no manual place-ID fallback.

Catalog GET uses the owner API and must match the reviewed home. The client caps the streamed body at 512 KiB, accepts at most 2,000 places and 2,000 source-field entries, and validates identifiers, labels, known observation fields and duplicates. Names and room connections do not grant access. The catalog capture time describes a snapshot; the server checks current owner authority again on approval.

Each mutation obtains fresh CSRF proof from the real owner API. Same-origin requests use the owner session, bypass response caching and reject redirects. An ambiguous failure never triggers an automatic replay. Approval clears the editor before sending and reports that the device must finish its check; approval alone is not an issued session. The owner key stays in the dashboard.

Decline and revoke expand an inline question naming the device. Focus starts on Keep request or Keep device access. Keep and Escape close the question and return focus to the invoking control. Revocation requires a host response. Its copy explains that the host rejects the session on its next request, including renewal, and cannot remotely erase offline screens or local setup metadata.

Connected devices lists issued, unexpired sessions with a last-checked time, not online presence. Each row exposes its exact member, home, place fields, separate Activity, History and Engine grants, session ID and expiry. An issued grant changes through revocation and pairing again. Loading, empty and unavailable lists have distinct written states.

**The Exact Grant Rule.** Show the reviewed device and code before authorization, keep each read permission explicit and off until selected, and report only the session or revocation state the host confirmed.

The catalog picker finish review returned `ship` after one material fix for oversized approval recovery; remaining findings were clear. The independent reviewer initially viewed 12 of 24 round1 captures across 390px and 1024px widths, light and dark themes, and normal and doubled root text size. The final round2 evidence has 28 captures, including four size-recovery captures. The final regression passed all 30 checks, split into 22 functional and eight layout checks, including recovery from 100 long place IDs. That size case failed before the fix. Layout checks run even when screenshot output is disabled. An earlier matrix run had one form-reset failure; its isolated rerun and the full final regression passed, and the cause remains unproven.

One production-bundle browser integration passed again after the fix. It used HTTPS, SQLite and a Home Assistant wire fixture to select Porch Temperature, obtain antiforgery proof, approve and redeem the grant, read a restricted native report of 21.5, reject administration and revoke access and refresh. It used a synthetic owner-cookie validator and a browser-only fixture certificate bypass. This evidence does not establish production certificate trust, real login, household acceptance, Android TLS, hardware or accessibility qualification. Doubled root font size is synthetic text and rem stress, not browser zoom. The single scoped detector returned no findings before the size fix; it was not rerun. The final build and scoped four-file lint passed with the existing bundle-size warning. Private captures remain outside the public repository. Native Home, Story, Week and Engine work, motion, platform and release goals remain governed by the companion BUILD-PLAN; this picker is one checkpoint.

### Companion shared layout

`CompanionDevicesPage.tsx` places `CompanionLayoutEditor.tsx` before the issued-session list. The editor uses the existing charcoal panel, stone borders, Outfit headings, DM Sans fields, labelled native selects and checkboxes, and decorative Lucide outlines. Primary Review and Save actions use amber; removal and discard actions use rose text on secondary controls. Actions retain the route's `3rem` minimum height and visible amber focus.

`CompanionLayoutPlaceEditor.tsx` edits presentation names and one of the supported Home, Sofa, Garden, Kitchen, Door or Bed icons. Adding a catalog place creates no bindings or connections. The owner chooses each exact `sourcePlaceId`, `sourceId` and observation `field`; source labels never determine ownership. A source-and-field pair already bound elsewhere is disabled. Missing or moved bindings remain visible and block review until the owner removes them explicitly. Reload available sources leaves the draft intact. Physical connections require explicit neighboring-place choices. They describe adjacency, not a measured floor plan, source ownership or access.

Place order changes only through Move place earlier or Move place later. `CompanionLayoutSummary.tsx` lists the complete shared name, counts, places, icons and neighbors, with exact source-place IDs and source-and-field bindings in native disclosures. The summary appears in review and in both kept-draft and latest-setup recovery sections. Review focuses its heading; Back to editing returns focus to Review shared changes. Save replaces shared presentation metadata at the inspected version. Personal pins, client section order, local drafts and device permissions remain independent.

`companion-layout.ts` validates names, exact metadata keys, canonical source bindings and explicit connections before review. Limits are 200 places, 1,000 connections, 2,000 bindings and a 524,288-byte UTF-8 save request. `companion-layout-api.ts` reads owner metadata with a one-MiB response cap and sends a checked `PUT` through the existing owner-session transport with fresh antiforgery proof. First creation uses a null expected revision. Replacement uses the inspected owner revision, a 32-character lowercase hexadecimal GUID. It is distinct from the member projection revision, a 64-character uppercase hexadecimal hash. A verified save response must match the submitted layout, home and source binding.

Conflict, network loss or an unverifiable save response keeps the draft in recovery without replay. Read latest shared setup fetches both owner metadata and the current source catalog. The owner then chooses Review my draft against this version or Discard my draft and use latest. Reading alone neither replaces the draft nor authorizes another save. A changed home or source disables reuse of the draft against that baseline. Unavailable places or bindings return the kept draft to editing for explicit correction. Recovery choices focus the review heading or status target as appropriate; removing an unavailable binding focuses the persistent Sources fieldset.

`CompanionLayoutDraftProvider.tsx` wraps authenticated routes in `App.tsx`. Its context in `components/companion-layout-draft-context.ts` holds one metadata-only draft and its owner baseline in memory across route links and browser Back/Forward. It stores no credentials, permission cache or catalog and writes no draft to browser storage. Returning to the editor always opens recovery with no catalog, comparison or Save action. Fresh owner and catalog reads must precede an explicit baseline choice. The provider retains the unload warning while the draft exists across routes. Reload, closing the dashboard or leaving the authenticated scope loses the draft; sign-out and sign-in start without it.

Place removal and closing a dirty editor each open an inline question with Keep focused. Keep and Escape cancel without mutation and return focus to the invoking control. Confirming place removal deletes only that draft place and its bindings and connections, then focuses the remaining-place select or available-place select. Explicit close discards the draft and returns focus to Edit shared layout. Written status and error feedback use programmatic focus targets.

**The Checked Layout Rule.** Keep shared presentation separate from permission, require explicit source bindings and neighbors, and save only after complete review against an inspected owner revision. A kept draft requires a fresh comparison after route return or an uncertain save.

The owner shared-layout final review returned `ship` for four repaired findings: draft loss across authenticated route navigation, enlarged-phone source overflow, Escape cancellation of removal and discard questions, and focus after recovery or unavailable-binding removal. The private round2 matrix contains 24 PNGs across `390px` and `1280px`, light and dark themes, normal and doubled root text, and edit, review and comparison states. The final reviewer inspected six of these captures plus source and targeted assertions. Element captures retain a sticky-mobile-header limitation. Doubled root text is synthetic rem stress, not browser zoom or accessibility qualification.

Supplied final execution evidence was 30 Node metadata and transport checks, 58 production-browser checks with zero skipped, build and scoped lint, and two isolated actual loopback HTTPS/SQLite/synthetic Home Assistant protocol browser checks. Those two checks cover shared first creation, conflict, replacement and real antiforgery, plus existing pairing and revoke. The earlier development-server run passed 56 checks and failed two; their causes remain unproven, and both passed in the final production run. The final reviewer did not rerun tests, launch a browser, create captures or run another detector. The known bundle warning and original single empty detector result remain bounded evidence. Earlier native 441 and server 576 test counts belong to the prior transport checkpoint and were not rerun for this editor.

The verdict covers this owner editor checkpoint. Shared delivery remains active, and native review and adoption remain unimplemented. These checks do not establish household acceptance, hardware, accessibility, native/platform/release completion, real production login or production certificate trust. No private evidence paths or new visual tokens are part of this record.

## Do's and Don'ts

### Do:

- **Do** use the semantic color roles from `index.css` in every page and component.
- **Do** keep the System, Light, and Dark options together and preserve their storage, pre-render resolution, and operating-system behavior.
- **Do** place the selector at the upper-right edge on public screens and in the sidebar footer after authentication.
- **Do** expose selection with `aria-pressed`, label icon-only controls, hide decorative icons from assistive technology, and retain visible keyboard focus.
- **Do** reserve amber for focus, selection, primary action, progress, and active system state.
- **Do** keep intended-person code matching, catalog-backed exact grants and separate default-deny Activity, History and Engine choices visible in companion approval.
- **Do** retain conservative Keep focus and Escape return paths for inline decline and revocation.
- **Do** distinguish reviewed approval, issued sessions, last-checked metadata and host-confirmed revocation.
- **Do** show exact shared-layout source bindings and explicit neighbors separately from device grants.
- **Do** retain an unsaved layout draft only in authenticated session memory and require fresh comparison after route return or uncertain save.
- **Do** keep source text within enlarged phone panels and return focus after inline questions, recovery choices and binding removal.

### Don't:

- **Don't** add mode-specific classes or raw light/dark palette values to page components; remap semantic tokens at the root.
- **Don't** promote appearance controls into the primary route content.
- **Don't** use heavy shadows or broad glows on ordinary operational cards.
- **Don't** replace semantic success, error, information, and progress states with amber alone.
- **Don't** infer access from a device name, place name, physical connection or unselected observation field.
- **Don't** automatically replay an owner mutation after an ambiguous failure or reuse a review after the code changes.
- **Don't** present issued sessions as online devices or owner approval as completed native live-data, platform or release work.
- **Don't** infer a source binding or physical connection from place names, icons or layout order.
- **Don't** replay a shared-layout save, silently discard a kept draft or treat its retained baseline as a fresh comparison.
- **Don't** present saved owner metadata as completed native adoption or hardware and accessibility qualification.
