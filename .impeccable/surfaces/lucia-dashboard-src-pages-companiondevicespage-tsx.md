---
version: 1
slug: "lucia-dashboard-src-pages-companiondevicespage-tsx"
primary_target: "lucia-dashboard/src/pages/CompanionDevicesPage.tsx"
related_targets: ["lucia-dashboard/src/components/CompanionGrantEditor.tsx","lucia-dashboard/src/companion-api.ts"]
---

scope: Companion devices at /companion-devices in the existing owner dashboard.
mode: Operate
audience: Self-hosting home owners approving intended native phones and tablets.
job: Match a code and device with its intended person, choose exact read access, decline an unconsumed request, inspect issued sessions and revoke deliberately.
constraints: Existing Observatory controls, typography and semantic themes. No owner key in native clients. No implicit place/field grant. Manual case-sensitive identifiers remain a labelled pilot limitation until authorized discovery. Device names are untrusted labels. An issued session is not proof a device is online. No physical control scope.
direction: Extend the existing task-panel form inline, followed by an issued-device list. Keep human code, host, device and expiry together; expand exact grant selection after review. Confirm decline/revoke inline with a safe Keep action, not a new modal language.
proof: Real isolated HTTPS/SQLite browser approval, CSRF rejection and durable revoke; synthetic browser failure and responsive checks. These do not qualify real login, installed Android TLS, hardware, screen readers or live household projections.
unresolved: Authorized place picker, membership roster/removal, real-source/native integration and remaining full-app/platform gates.
