# Lucia Dashboard

The Lucia Dashboard is a React management UI for the [Lucia](../README.md) home assistant AI platform. It provides real-time monitoring, agent configuration, trace inspection, and full system administration through a mobile-responsive interface with System, Light, and Dark themes.

## Tech Stack

- **React 19** with TypeScript 5.9
- **Vite 7** for development and bundling
- **Tailwind CSS 4** for styling
- **TanStack Query 5** for server-state management
- **React Router 7** for client-side routing
- **XYFlow (React Flow)** for agent mesh graph visualization
- **Lucide React** for icons
- **Playwright** for E2E testing

## Prerequisites

- **Node.js 22+**
- A running Lucia AgentHost backend (default: `http://localhost:5151`)

## Getting Started

```bash
# Install dependencies
npm install

# Start development server
npm run dev
```

The dev server starts at **http://localhost:7233** by default and proxies API requests to the AgentHost backend.

### Running via Aspire

When launched through the .NET Aspire AppHost (`dotnet run --project lucia.AppHost`), the dashboard starts automatically. Aspire injects the AgentHost URL via environment variables so no manual proxy configuration is needed.

## Available Scripts

| Script | Description |
|--------|-------------|
| `npm run dev` | Start Vite dev server with HMR |
| `npm run build` | Type-check with `tsc` then build for production |
| `npm run lint` | Run ESLint across the project |
| `npm run preview` | Preview the production build locally |

## Project Structure

```
src/
├── api.ts              # Typed API client (50+ functions)
├── types.ts            # Shared TypeScript interfaces
├── App.tsx             # Root component, routing, sidebar navigation
├── auth/
│   └── AuthContext.tsx  # Authentication provider and route guards
├── pages/              # 20 page components (one per route)
│   ├── ActivityPage.tsx          # Real-time agent mesh & event stream
│   ├── AgentDefinitionsPage.tsx  # Create/edit agent definitions
│   ├── AgentsPage.tsx            # Agent registry & test chat
│   ├── AlarmsPage.tsx            # Alarm clock management
│   ├── ConfigurationPage.tsx     # System configuration editor
│   ├── EntityLocationPage.tsx    # Entity-to-area mapping
│   ├── ExportPage.tsx            # JSONL dataset export for fine-tuning
│   ├── ListsPage.tsx             # List management
│   ├── LoginPage.tsx             # API key login
│   ├── MatcherDebugPage.tsx      # Entity matcher diagnostics
│   ├── McpServersPage.tsx        # MCP tool server management
│   ├── ModelProvidersPage.tsx    # LLM provider configuration
│   ├── PluginsPage.tsx           # Plugin installation & management
│   ├── PresencePage.tsx          # Presence sensor configuration
│   ├── PromptCachePage.tsx       # Routing/chat cache management
│   ├── SetupPage.tsx             # Guided onboarding wizard
│   ├── SkillOptimizerPage.tsx    # Skill threshold tuning
│   ├── TasksPage.tsx             # Active & archived task viewer
│   ├── TraceDetailPage.tsx       # Individual trace inspection
│   └── TraceListPage.tsx         # Trace browsing with filters
├── components/         # Shared reusable components
│   ├── CustomSelect.tsx          # Styled dropdown select
│   ├── EntityMultiSelect.tsx     # Multi-entity picker
│   ├── MeshGraph.tsx             # React Flow agent mesh visualization
│   ├── PluginConfigTab.tsx       # Plugin configuration panel
│   ├── PluginRepoDialog.tsx      # Plugin repository browser
│   ├── RestartBanner.tsx         # Service restart notification
│   ├── SkillConfigEditor.tsx     # Skill parameter editor
│   └── SpanTimeline.tsx          # Trace span timeline view
└── hooks/
    └── useActivityStream.ts      # SSE hook for live activity events
```

## API Proxy

The Vite dev server proxies API requests to the AgentHost backend. The proxy target is resolved in this order:

1. `services__lucia-agenthost__https__0` — set by Aspire
2. `services__lucia-agenthost__http__0` — set by Aspire
3. `http://localhost:5151` — default fallback

Proxied route prefixes: `/api`, `/agents`, `/a2a`, `/agent` (POST only).

## Authentication

The dashboard uses API key authentication with HMAC-signed sessions:

1. **First run** — The setup wizard at `/setup` generates a dashboard API key.
2. **Login** — Users authenticate at `/login` with their API key.
3. **Route guards** — `AuthContext` redirects unauthenticated users to login and users who haven't completed setup to the wizard.

## Appliance OS update prerequisites

The Appliance page reports why an OS update is blocked, including unavailable
RootFS A/B checks or disabled redundancy. The manager rechecks prerequisites
when an operation is submitted. Missing readiness fields from an older manager
do not count as approval. Lucia-only updates and normal service controls remain
available. The installer also warns that writing an NVMe image does not enable
RootFS A/B in firmware; it does not remove the offline setup path.

The two update controls can target different release tags. App-only releases
do not hide an older compatible OS update or the latest supported installer.
The installer link opens the release that actually contains its image.

Progress shows the current phase, not a weighted estimate of the whole update.
Downloads aggregate all parts; OS writes use uncompressed partition sizes.
Unknown totals use an indeterminate progress bar. Validation and restart are
separate phases, and reaching 100% does not mark an update successful.
Reloading or reconnecting retrieves the same operation ID. Failures keep their
last phase and measured bytes. Older OS helpers report indeterminate apply
progress until updated through an approved OS or repair path.

## Running version

Non-appliance deployments display the running server's version below Sign Out
in the sidebar, including in the mobile drawer. Docker builds embed `VERSION`
as `LUCIA_VERSION`; the footer reads it through the authenticated
`GET /api/system/version` endpoint. Untagged local builds report `development`.
An unavailable endpoint shows `Version unavailable` without blocking navigation.

## Voice onboarding traces
Cmd Traces labels voice enrollment as **Voice onboarding**. Its detail view shows
the stage, whether another reply is needed, and the returned conversation ID
alongside the incoming request context. Personal answers are redacted, and no
command-match score or LLM invocation is shown for the local workflow.

## User memories

Open **User memories** in the sidebar, or **View memories** on an enrolled voice
profile. The page uses the stable profile ID to list personal memories, including
preferred name, birthday, and preferences shared during conversations. Existing
room and preference entries remain available. It supports search, inline editing, and
confirmed deletion of individual entries. Edits retain the entry's expiration.

Internal chat-history entries are excluded. The store records a save time and
optional expiration, but does not record whether the author was onboarding or an
AI tool. Results are limited to 200 matches; use search to find older entries.
Administrator sessions can manage other profiles through the existing memory API.
With Neo4j Agent Memory enabled, this page still shows the built-in store only.
Onboarding answers and facts learned in conversation are in the Neo4j graph.

## Building for Production

```bash
npm run build
```

Output is written to `dist/`. The production build is a static SPA that expects the AgentHost API to be available at the same origin (or behind a reverse proxy).

## Companion devices, experimental

Open Companion devices to review an intended phone/tablet's approval code. Use the trusted HTTPS dashboard with a current administrator session. The host must enable `Companion:Enabled` and configure a stable `Companion:HomeId`. Disabled, forbidden and failed reads show an error, never an empty successful list.

Match the code and device with the intended person. Device names are untrusted labels. Choose an owner-assigned member ID, then use the authorized catalog to select exact places and supported observations. Place names are paired with case-sensitive IDs and source counts. Fields are never selected automatically. Search keeps chosen places visible; Show more places reveals another25 unselected choices. The member ID remains manual until a membership roster exists. No places or additional activity/history/Engine permissions are selected by default. These permissions do not enable physical controls or appliance administration. Connected native Home reads are implemented; production activity/history/Engine and platform qualification remain unfinished.

Catalog discovery requires `Companion:SourceBaseUrl` to match the configured HA origin. It contains metadata only, never readings or raw HA attributes. The browser checks the reviewed home, a 512 KiB response cap, bounded counts, identifiers, labels, fields and duplicates. Loading, a legitimate empty catalog and failure are different states. Failure disables approval rather than showing old/manual/sample choices. Reload clears place selections and the intended-person confirmation before reading again. The owner must review the new selection deliberately.

Selection is capped at 200 places and the serialized approval has a separate 16,000-byte UTF-8 limit. The editor and API share the exact wrapped-body size check. An oversized selection remains editable with written guidance; approval stays disabled until it fits. Local validation runs before discarding the review. Once an actual approval request begins, ambiguous failures still clear the editor and require a new code review, never a blind replay.

Approval waits for device redemption before an issued session can appear. Refresh devices shows current server sessions with exact grants and expiry, not online status. Decline and revoke require separate inline confirmation. Keep restores focus; Escape closes the question. Editing a code discards its previous review. Expired requests cannot be approved. Ambiguous approval failure removes the editor and requires a new review; the browser does not replay it automatically. Every mutation obtains fresh antiforgery proof using the existing same-origin session. No owner/native secrets belong in the code field or URL.

The isolated tests in `lucia-playwright/e2e/00-companion-devices.spec.ts` intercept all APIs with labelled synthetic data. `00-companion-https.spec.ts` is opt-in and uses the production bundle with actual isolated HTTPS/SQLite companion services. Its synthetic owner-cookie validator and test-only certificate bypass do not qualify real login, deployment TLS or installed native networking. To run that integration without a household host:

```powershell
npm run build --prefix lucia-dashboard
dotnet run --project lucia-playwright/fixtures/CompanionBrowserHost -- --owner-home
# In a second terminal, use the loopback HTTPS URL printed by the fixture.
$env:SKIP_DOCKER = '1'
$env:COMPANION_HTTPS_URL = 'https://127.0.0.1:<printed-port>'
npm exec --prefix lucia-playwright -- playwright test --config=lucia-playwright/playwright.config.ts 00-companion-https.spec.ts
```

The fixture uses a temporary database/certificate, built dashboard assets and a synthetic HA REST/WebSocket source, all on loopback. Enter `stop` or press Ctrl+C afterward; owner-home mode also has a nine-minute safety limit. The integration chooses a discovered place/field, approves with fresh antiforgery proof, redeems a device session, reads its restricted source report and revokes access/refresh. It never loads real AgentHost secrets or contacts a household, real HA, model or cloud service. Private captures belong outside the public repository.
