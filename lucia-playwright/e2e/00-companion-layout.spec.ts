import { expect, test, type Page } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';

const origin = 'http://synthetic-ha.invalid/';
const revision = 'a'.repeat(32);
const catalog = () => ({ homeId: 'home-a', capturedAt: new Date().toISOString(), places: [
  { placeId: 'porch', name: 'Porch', sources: [{ sourceId: 'sensor.porch_temperature', name: 'Porch temperature', field: 'Temperature' }] },
  { placeId: 'garden', name: 'Garden', sources: [{ sourceId: 'sensor.garden_moisture', name: 'Garden moisture', field: 'Moisture' }] },
] });
const layout = (name = 'Our home') => ({ homeId: 'home-a', sourceBaseUrl: origin, name,
  places: [{ id: 'front', name: 'Front porch', icon: 'Door', sourcePlaceId: 'porch' }], connections: [],
  bindings: [{ placeId: 'front', sourcePlaceId: 'porch', sourceId: 'sensor.porch_temperature', field: 'Temperature' }] });
const saved = (name = 'Our home', nextRevision = revision) => ({ homeId: 'home-a', sourceBaseUrl: origin, sourceMatches: true,
  revision: nextRevision, layout: layout(name), updatedAt: new Date().toISOString(), updatedBy: 'synthetic-owner' });

async function fixture(page: Page, initial: ReturnType<typeof saved> | null = null, inAppHistory = false) {
  const writes: Array<{ expectedRevision: string | null; layout: ReturnType<typeof layout> }> = [];
  let current = initial;
  await page.route('**/api/**', route => route.fulfill({ status: 404 }));
  await page.route('**/api/auth/status', route => route.fulfill({ json: { authenticated: true, setupComplete: true, hasKeys: true } }));
  await page.route('**/api/system/version', route => route.fulfill({ json: { version: 'synthetic-layout-test' } }));
  await page.route('**/api/companion/owner/**', async route => {
    const request = route.request(); const path = new URL(request.url()).pathname;
    expect(request.headers().authorization).toBeUndefined(); expect(request.headers()['x-api-key']).toBeUndefined();
    if (path.endsWith('/sessions')) return route.fulfill({ json: [] });
    if (path.endsWith('/discovery')) return route.fulfill({ json: catalog() });
    if (path.endsWith('/antiforgery')) return route.fulfill({ json: { requestToken: 'synthetic-proof', headerName: 'RequestVerificationToken' } });
    if (path.endsWith('/layout') && request.method() === 'GET') return route.fulfill(current ? { json: current }
      : { status: 404, json: { error: 'layout_not_configured', homeId: 'home-a', sourceBaseUrl: origin, sourceMatches: true } });
    expect(request.method()).toBe('PUT'); expect(request.headers().requestverificationtoken).toBe('synthetic-proof');
    const body = request.postDataJSON(); writes.push(body);
    current = { ...saved(body.layout.name, 'b'.repeat(32)), layout: body.layout };
    return route.fulfill({ json: current });
  });
  if (inAppHistory) { await page.goto('/'); await page.getByRole('link', { name: 'Companion devices', exact: true }).click(); }
  else await page.goto('/companion-devices');
  await page.getByRole('button', { name: 'Edit shared layout', exact: true }).click();
  const editor = page.getByRole('region', { name: 'Shared home layout', exact: true });
  await expect(editor.getByLabel('Home display name')).toBeVisible();
  return { writes, editor };
}

test('owner explicitly composes and reviews shared places, bindings and physical connections before first save', async ({ page }) => {
  const { writes, editor } = await fixture(page);
  await editor.getByLabel('Home display name').fill('Our garden home');
  await editor.getByLabel('Available place').selectOption('porch');
  await editor.getByRole('button', { name: 'Add selected place', exact: true }).click();
  await editor.getByLabel('Place display name').fill('Front porch');
  await editor.getByLabel('Place icon').selectOption('Door');
  await editor.getByRole('checkbox', { name: 'Bind Porch temperature · Temperature', exact: true }).check();
  await editor.getByLabel('Available place').selectOption('garden');
  await editor.getByRole('button', { name: 'Add selected place', exact: true }).click();
  await editor.getByRole('checkbox', { name: 'Connect to Front porch', exact: true }).check();
  expect(writes).toHaveLength(0);
  await editor.getByRole('button', { name: 'Review shared changes', exact: true }).click();
  await expect(editor.getByRole('heading', { name: 'Review your shared layout', exact: true })).toBeFocused();
  await expect(editor.getByText('Our garden home', { exact: true })).toBeVisible();
  await editor.getByRole('button', { name: 'Save shared layout', exact: true }).click();
  await expect(editor.getByRole('status')).toContainText('Shared layout saved');
  expect(writes).toHaveLength(1);
  expect(writes[0].expectedRevision).toBeNull();
  expect(writes[0].layout.homeId).toBe('home-a'); expect(writes[0].layout.sourceBaseUrl).toBe(origin);
  expect(writes[0].layout.places).toHaveLength(2); expect(writes[0].layout.connections).toHaveLength(1); expect(writes[0].layout.bindings).toHaveLength(1);
  expect(writes[0].layout.bindings[0].sourceId).toBe('sensor.porch_temperature');
  expect(JSON.stringify(writes)).not.toMatch(/canRead|accessToken|refreshToken|numberValue|sectionOrder|pins/);
});

test('editing after review invalidates save and cancel review preserves the draft', async ({ page }) => {
  const { writes, editor } = await fixture(page, saved());
  await editor.getByLabel('Home display name').fill('My draft');
  await editor.getByRole('button', { name: 'Review shared changes', exact: true }).click();
  await editor.getByRole('button', { name: 'Back to editing', exact: true }).click();
  await expect(editor.getByLabel('Home display name')).toHaveValue('My draft');
  await expect(editor.getByRole('button', { name: 'Save shared layout', exact: true })).toHaveCount(0);
  await editor.getByLabel('Home display name').fill('Different draft');
  expect(writes).toHaveLength(0);
});

test('a stale save preserves the draft and requires comparing the latest setup before a checked replacement', async ({ page }) => {
  const { editor } = await fixture(page, saved());
  let writes = 0;
  await page.route('**/api/companion/owner/layout', async route => {
    if (route.request().method() === 'GET') return route.fulfill({ json: saved('Other owner changes', 'c'.repeat(32)) });
    writes++;
    if (writes === 1) return route.fulfill({ status: 409, json: { error: 'layout_changed_review_again' } });
    expect(route.request().postDataJSON().expectedRevision).toBe('c'.repeat(32));
    expect(route.request().postDataJSON().layout.name).toBe('My kept draft');
    return route.fulfill({ json: saved('My kept draft', 'd'.repeat(32)) });
  });
  await editor.getByLabel('Home display name').fill('My kept draft');
  await editor.getByRole('button', { name: 'Review shared changes', exact: true }).click();
  await editor.getByRole('button', { name: 'Save shared layout', exact: true }).click();
  await expect(editor.getByRole('alert')).toContainText('draft is kept');
  await expect(editor.getByRole('button', { name: 'Save shared layout', exact: true })).toHaveCount(0);
  await editor.getByRole('button', { name: 'Read latest shared setup', exact: true }).click();
  await expect(editor.getByText('Other owner changes', { exact: true })).toBeVisible();
  await expect(editor.getByText('My kept draft', { exact: true })).toBeVisible();
  expect(writes).toBe(1);
  await editor.getByRole('button', { name: 'Review my draft against this version', exact: true }).click();
  await editor.getByRole('button', { name: 'Save shared layout', exact: true }).click();
  await expect(editor.getByRole('status')).toContainText('Shared layout saved'); expect(writes).toBe(2);
});

test('an unconfirmed save is never replayed and a failed recovery read cannot authorize another write', async ({ page }) => {
  const { editor } = await fixture(page, saved()); let writes = 0;
  await page.route('**/api/companion/owner/layout', route => {
    if (route.request().method() === 'PUT') { writes++; return route.abort('failed'); }
    return route.fulfill({ status: 503, json: { error: 'layout_unavailable' } });
  });
  await editor.getByLabel('Home display name').fill('Draft survives');
  await editor.getByRole('button', { name: 'Review shared changes', exact: true }).click();
  await editor.getByRole('button', { name: 'Save shared layout', exact: true }).click();
  await expect(editor.getByRole('alert')).toContainText('draft is kept');
  await editor.getByRole('button', { name: 'Read latest shared setup', exact: true }).click();
  await expect(editor.getByRole('alert')).toContainText('draft is kept');
  await expect(editor.getByText('Draft survives', { exact: true })).toBeVisible(); expect(writes).toBe(1);
});

test('missing or moved sources stay visible and require explicit removal, never silent rebinding', async ({ page }) => {
  const existing = saved(); existing.layout.bindings[0].sourceId = 'sensor.removed';
  const { writes, editor } = await fixture(page, existing);
  await expect(editor.getByText('sensor.removed', { exact: false })).toBeVisible();
  await expect(editor.getByRole('button', { name: 'Review shared changes', exact: true })).toBeDisabled();
  await editor.getByRole('button', { name: 'Remove unavailable binding sensor.removed · Temperature', exact: true }).click();
  await editor.getByRole('button', { name: 'Review shared changes', exact: true }).click();
  await editor.getByRole('button', { name: 'Save shared layout', exact: true }).click();
  await expect(editor.getByRole('status')).toContainText('Shared layout saved'); expect(writes[0].layout.bindings).toEqual([]);
});

test('removing a place is confirmed, cleans only its draft relationships and does not save automatically', async ({ page }) => {
  const { writes, editor } = await fixture(page, saved());
  await editor.getByRole('button', { name: 'Remove selected place', exact: true }).click();
  await expect(editor.getByRole('button', { name: 'Keep place', exact: true })).toBeFocused();
  await editor.getByRole('button', { name: 'Keep place', exact: true }).click();
  await expect(editor.getByRole('button', { name: 'Remove selected place', exact: true })).toBeFocused();
  await expect(editor.getByLabel('Place display name')).toHaveValue('Front porch');
  await editor.getByRole('button', { name: 'Remove selected place', exact: true }).click();
  await editor.getByRole('button', { name: 'Confirm removal from draft', exact: true }).click();
  await expect(editor.getByText('No places in this draft.', { exact: false })).toBeVisible(); expect(writes).toHaveLength(0);
  await expect(editor.getByLabel('Available place')).toBeFocused();
});

test('closing a dirty editor requires an explicit discard and Keep restores focus without saving', async ({ page }) => {
  const { writes, editor } = await fixture(page, saved());
  await editor.getByLabel('Home display name').fill('Kept draft');
  await editor.getByRole('button', { name: 'Close layout editor', exact: true }).click();
  await expect(editor.getByRole('button', { name: 'Keep editing', exact: true })).toBeFocused();
  await editor.getByRole('button', { name: 'Keep editing', exact: true }).click();
  await expect(editor.getByRole('button', { name: 'Close layout editor', exact: true })).toBeFocused();
  await expect(editor.getByLabel('Home display name')).toHaveValue('Kept draft');
  await editor.getByRole('button', { name: 'Close layout editor', exact: true }).click();
  await page.keyboard.press('Escape');
  await expect(editor.getByRole('button', { name: 'Close layout editor', exact: true })).toBeFocused();
  await editor.getByRole('button', { name: 'Close layout editor', exact: true }).click();
  await editor.getByRole('button', { name: 'Discard draft and close', exact: true }).click();
  await expect(editor.getByRole('button', { name: 'Edit shared layout', exact: true })).toBeFocused(); expect(writes).toHaveLength(0);
});

test('review cannot save behind an open discard question', async ({ page }) => {
  const { writes, editor } = await fixture(page, saved());
  await editor.getByLabel('Home display name').fill('Reviewed draft');
  await editor.getByRole('button', { name: 'Review shared changes', exact: true }).click();
  await editor.getByRole('button', { name: 'Close layout editor', exact: true }).click();
  await expect(editor.getByRole('button', { name: 'Save shared layout', exact: true })).toBeDisabled();
  await editor.getByRole('button', { name: 'Keep editing', exact: true }).click();
  await expect(editor.getByRole('button', { name: 'Save shared layout', exact: true })).toBeEnabled(); expect(writes).toHaveLength(0);
});

test('source failures preserve a loaded draft and reload does not reset its names or bindings', async ({ page }) => {
  const { writes, editor } = await fixture(page, saved());
  await editor.getByLabel('Home display name').fill('Source recovery draft');
  let reads = 0;
  await page.route('**/api/companion/owner/discovery', route => ++reads === 1 ? route.fulfill({ status: 503 }) : route.fulfill({ json: catalog() }));
  await editor.getByRole('button', { name: 'Reload available sources', exact: true }).click();
  await expect(editor.getByRole('alert')).toContainText('could not load');
  await expect(editor.getByRole('button', { name: 'Review shared changes', exact: true })).toBeDisabled();
  await editor.getByRole('button', { name: 'Reload available sources', exact: true }).click();
  await expect(editor.getByLabel('Home display name')).toHaveValue('Source recovery draft');
  await expect(editor.getByRole('checkbox', { name: 'Bind Porch temperature · Temperature', exact: true })).toBeChecked(); expect(writes).toHaveLength(0);
});

test('a changed configured source blocks review rather than migrating old metadata silently', async ({ page }) => {
  const current = saved(); current.sourceMatches = false;
  const { writes, editor } = await fixture(page, current);
  await editor.getByLabel('Home display name').fill('Changed');
  await expect(editor.getByText('The configured source changed.', { exact: false })).toBeVisible();
  await expect(editor.getByRole('button', { name: 'Review shared changes', exact: true })).toBeDisabled(); expect(writes).toHaveLength(0);
});

test('invalid names remain editable and do not enable a write', async ({ page }) => {
  const { writes, editor } = await fixture(page, saved());
  await editor.getByLabel('Place display name').fill('  ');
  await expect(editor.getByRole('button', { name: 'Review shared changes', exact: true })).toBeDisabled();
  await editor.getByLabel('Place display name').fill('Renamed porch');
  await expect(editor.getByRole('button', { name: 'Review shared changes', exact: true })).toBeEnabled(); expect(writes).toHaveLength(0);
});

test('finish regression: SPA Back and Forward retain the draft but require a fresh comparison', async ({ page }) => {
  const { writes, editor } = await fixture(page, saved(), true);
  await editor.getByLabel('Home display name').fill('History kept draft');
  await page.evaluate(() => history.back());
  await expect(page).toHaveURL(/:\d+\/$/);
  await page.evaluate(() => history.forward());
  await expect(page).toHaveURL(/\/companion-devices$/);
  await expect(editor.getByText('History kept draft', { exact: true })).toBeVisible();
  await expect(editor.getByRole('button', { name: 'Save shared layout', exact: true })).toHaveCount(0);
  await expect(editor.getByRole('button', { name: 'Read latest shared setup', exact: true })).toBeVisible();
  expect(writes).toHaveLength(0);
});

test('finish regression: Escape keeps a place and returns focus to removal', async ({ page }) => {
  const { writes, editor } = await fixture(page, saved());
  await editor.getByRole('button', { name: 'Remove selected place', exact: true }).click();
  await page.keyboard.press('Escape');
  await expect(editor.getByRole('button', { name: 'Keep place', exact: true })).toHaveCount(0);
  await expect(editor.getByRole('button', { name: 'Remove selected place', exact: true })).toBeFocused(); expect(writes).toHaveLength(0);
});

test('finish regression: removing an unavailable binding focuses its stable source section', async ({ page }) => {
  const current = saved(); current.layout.bindings[0].sourceId = 'sensor.removed';
  const { editor } = await fixture(page, current);
  await editor.getByRole('button', { name: 'Remove unavailable binding sensor.removed · Temperature', exact: true }).click();
  await expect(editor.getByRole('group', { name: 'Sources to display', exact: true })).toBeFocused();
});

test('finish regression: navigation keeps an unconfirmed recovery draft without carrying a save authorization', async ({ page }) => {
  const { editor } = await fixture(page, saved(), true);
  await page.route('**/api/companion/owner/layout', route => route.request().method() === 'PUT' ? route.abort('failed') : route.fulfill({ json: saved() }));
  await editor.getByLabel('Home display name').fill('Unconfirmed kept draft');
  await editor.getByRole('button', { name: 'Review shared changes', exact: true }).click();
  await editor.getByRole('button', { name: 'Save shared layout', exact: true }).click();
  await expect(editor.getByRole('alert')).toContainText('draft is kept');
  await page.evaluate(() => history.back()); await expect(page).toHaveURL(/:\d+\/$/);
  await page.evaluate(() => history.forward()); await expect(page).toHaveURL(/\/companion-devices$/);
  await expect(editor.getByText('Unconfirmed kept draft', { exact: true })).toBeVisible();
  await expect(editor.getByRole('button', { name: 'Save shared layout', exact: true })).toHaveCount(0);
});

test('finish regression: route links preserve a draft, while sign-out clears its authenticated memory scope', async ({ page }) => {
  const { editor } = await fixture(page, saved());
  await editor.getByLabel('Home display name').fill('Navigation only draft');
  await page.getByRole('link', { name: 'Traces', exact: true }).click();
  await page.getByRole('link', { name: 'Companion devices', exact: true }).click();
  await expect(editor.getByText('Navigation only draft', { exact: true })).toBeVisible();
  expect(await page.evaluate(() => [localStorage, sessionStorage].some(storage => Object.keys(storage).some(key => (storage.getItem(key) ?? '').includes('Navigation only draft'))))).toBe(false);
  await page.route('**/api/auth/logout', route => route.fulfill({ status: 204 }));
  await page.route('**/api/auth/login', route => route.fulfill({ json: { authenticated: true } }));
  await page.getByRole('button', { name: 'Sign Out', exact: true }).click();
  await expect(page.getByLabel('API Key', { exact: true })).toBeVisible();
  await page.getByLabel('API Key', { exact: true }).fill('synthetic-login-only');
  await page.getByRole('button', { name: 'Sign In', exact: true }).click();
  await page.getByRole('link', { name: 'Companion devices', exact: true }).click();
  await expect(editor.getByRole('button', { name: 'Edit shared layout', exact: true })).toBeVisible();
  await expect(editor.getByText('Navigation only draft', { exact: true })).toHaveCount(0);
});

for (const choice of ['discard', 'invalid-draft']) test(`finish regression: recovery ${choice} returns focus to a meaningful status`, async ({ page }) => {
  const { editor } = await fixture(page, saved());
  await page.route('**/api/companion/owner/layout', route => route.request().method() === 'GET'
    ? route.fulfill({ json: saved('Latest version', 'c'.repeat(32)) }) : route.fulfill({ status: 409, json: { error: 'layout_changed_review_again' } }));
  await editor.getByLabel('Home display name').fill('Recovery focus draft');
  await editor.getByRole('button', { name: 'Review shared changes', exact: true }).click();
  await editor.getByRole('button', { name: 'Save shared layout', exact: true }).click();
  await expect(editor.getByRole('alert')).toContainText('draft is kept');
  if (choice === 'invalid-draft') {
    const currentCatalog = catalog(); currentCatalog.places[0].sources = [];
    await page.route('**/api/companion/owner/discovery', route => route.fulfill({ json: currentCatalog }));
  }
  await editor.getByRole('button', { name: 'Read latest shared setup', exact: true }).click();
  await editor.getByRole('button', { name: choice === 'discard' ? 'Discard my draft and use latest' : 'Review my draft against this version', exact: true }).click();
  await expect(editor.getByRole('status')).toBeFocused();
  await expect(editor.getByLabel('Home display name')).toHaveValue(choice === 'discard' ? 'Latest version' : 'Recovery focus draft');
});

for (const theme of ['light', 'dark']) test(`finish regression: enlarged phone contains source text ${theme}`, async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 900 });
  await page.addInitScript(theme => { localStorage.setItem('lucia-theme', theme); document.addEventListener('DOMContentLoaded', () => { document.documentElement.style.fontSize = '32px'; }); }, theme);
  const { editor } = await fixture(page, saved());
  const panel = (await editor.boundingBox())!; const sources = editor.getByRole('group', { name: 'Sources to display', exact: true });
  const section = (await sources.boundingBox())!;
  expect(section.x + section.width).toBeLessThanOrEqual(panel.x + panel.width - 15);
  const overflow = await sources.evaluate(element => {
    const bounds = element.getBoundingClientRect(); const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT);
    let node: Node | null; const bad: string[] = [];
    while ((node = walker.nextNode())) { if (!node.textContent?.trim()) continue; const range = document.createRange(); range.selectNodeContents(node);
      for (const rect of range.getClientRects()) if (rect.width && (rect.right > bounds.right + 1 || rect.left < bounds.left - 1)) bad.push(node.textContent!); }
    return bad;
  });
  expect(overflow).toEqual([]);
});

for (const width of [390, 1280]) for (const theme of ['light', 'dark']) for (const scale of [1, 2]) {
  test(`shared editor layout ${width} ${theme} text${scale}`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    await page.addInitScript(({ theme, scale }) => {
      localStorage.setItem('lucia-theme', theme);
      document.addEventListener('DOMContentLoaded', () => { document.documentElement.style.fontSize = `${16 * scale}px`; });
    }, { theme, scale });
    const current = saved(); current.layout.name = 'Our home with a porch, garden and a very long display name';
    const { editor } = await fixture(page, current);
    await editor.getByLabel('Place display name').fill('Front porch beside the garden and side entrance');
    const capture = async (state: string) => {
      await expect(editor.getByRole('button', { name: 'Close layout editor', exact: true })).toBeEnabled();
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBe(true);
      for (const button of await editor.getByRole('button').all()) {
        if (!await button.isVisible()) continue;
        const box = await button.boundingBox(); expect(box).not.toBeNull();
        expect(box!.x).toBeGreaterThanOrEqual(-1); expect(box!.x + box!.width).toBeLessThanOrEqual(width + 1);
        expect(box!.height).toBeGreaterThanOrEqual(47);
      }
      if (process.env.COMPANION_LAYOUT_CAPTURE_DIR) {
        await mkdir(process.env.COMPANION_LAYOUT_CAPTURE_DIR, { recursive: true });
        await editor.screenshot({ path: path.join(process.env.COMPANION_LAYOUT_CAPTURE_DIR, `${width}-${theme}-text${scale}-${state}.png`) });
      }
    };
    await capture('edit');
    await editor.getByRole('button', { name: 'Review shared changes', exact: true }).click(); await capture('review');
    await page.route('**/api/companion/owner/layout', route => route.request().method() === 'GET'
      ? route.fulfill({ json: saved('Latest owner setup', 'c'.repeat(32)) }) : route.fulfill({ status: 409, json: { error: 'layout_changed_review_again' } }));
    await editor.getByRole('button', { name: 'Save shared layout', exact: true }).click();
    await editor.getByRole('button', { name: 'Read latest shared setup', exact: true }).click();
    await expect(editor.getByText('Latest owner setup', { exact: true })).toBeVisible(); await capture('comparison');
  });
}
