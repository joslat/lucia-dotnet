import { expect, test, type Page } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';

const pairingId = 'a'.repeat(32);
const sessionId = 'b'.repeat(32);
const code = 'ABCDE-FGHJK';
const ownerCatalog = () => ({ homeId: 'home-a', capturedAt: new Date().toISOString(), places: [
  { placeId: 'porch', name: 'Porch', sources: [{ sourceId: 'sensor.porch_temperature', name: 'Porch temperature', field: 'Temperature' }, { sourceId: 'binary_sensor.porch_door', name: 'Porch contact', field: 'Contact' }] },
  { placeId: 'garden', name: 'Garden', sources: [{ sourceId: 'sensor.garden_moisture', name: 'Garden moisture', field: 'Moisture' }] },
] });

async function ownerFixture(page: Page, sessions: unknown[] = []) {
  const writes: Array<{ path: string; method: string; body: unknown }> = [];
  await page.route('**/api/**', route => route.fulfill({ status: 404 }));
  await page.route('**/api/auth/status', route => route.fulfill({ json: { authenticated: true, setupComplete: true, hasKeys: true } }));
  await page.route('**/api/system/version', route => route.fulfill({ json: { version: 'synthetic-browser-test' } }));
  await page.route('**/api/companion/owner/**', async route => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    expect(new URL(request.url()).search).toBe('');
    expect(request.headers().authorization).toBeUndefined();
    expect(request.headers()['x-api-key']).toBeUndefined();
    if (path.endsWith('/antiforgery')) {
      await route.fulfill({ json: { requestToken: 'synthetic-csrf', headerName: 'RequestVerificationToken' } });
      return;
    }
    if (request.method() === 'GET' && path.endsWith('/sessions')) {
      await route.fulfill({ json: sessions });
      return;
    }
    if (request.method() === 'GET' && path.endsWith('/discovery')) {
      await route.fulfill({ json: ownerCatalog() });
      return;
    }
    expect(request.headers().requestverificationtoken).toBe('synthetic-csrf');
    writes.push({ path, method: request.method(), body: request.postData() ? request.postDataJSON() : null });
    if (path.endsWith('/review')) {
      await route.fulfill({ json: { id: pairingId, homeId: 'home-a', deviceName: 'Synthetic porch tablet', expiresAt: new Date(Date.now() + 600_000).toISOString(), status: 'Pending' } });
    } else {
      await route.fulfill({ status: 204 });
    }
  });
  await page.goto('/companion-devices');
  await expect(page.getByRole('heading', { name: 'Companion devices', exact: true })).toBeVisible();
  return writes;
}

async function reviewCode(page: Page) {
  await page.getByLabel('Approval code').fill(code);
  await page.getByRole('button', { name: 'Review device' }).click();
  await expect(page.getByText('Synthetic porch tablet', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Synthetic porch tablet', exact: true })).toBeFocused();
}

test('owner matches the code and chooses only exact read grants before approving', async ({ page }) => {
  const writes = await ownerFixture(page);
  await reviewCode(page);
  await expect(page.getByRole('button', { name: 'Approve read access', exact: true })).toBeDisabled();
  await page.getByLabel('Member ID').fill('member-a');
  await page.getByRole('checkbox', { name: 'Choose Porch (porch)', exact: true }).check();
  await page.getByLabel('Temperature', { exact: true }).check();
  await page.getByLabel('I matched this code and device with the intended person').check();
  await page.getByRole('button', { name: 'Approve read access', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Approved');
  const approval = writes.find(write => write.path.endsWith('/approve'))!;
  expect(approval.body).toEqual({ codeConfirmed: true, grant: { homeId: 'home-a', memberId: 'member-a', places: [{ placeId: 'porch', fields: ['Temperature'] }], canReadActivity: false, canReadHistory: false, canReadEngine: false } });
  expect(writes.filter(write => write.path.endsWith('/approve'))).toHaveLength(1);
  expect(JSON.stringify(writes)).not.toContain('deviceCode');
});

test('editing the code discards an old review and cannot approve its stale identity', async ({ page }) => {
  const writes = await ownerFixture(page);
  await reviewCode(page);
  await page.getByLabel('Approval code').fill('OTHER-CODE1');
  await expect(page.getByText('Synthetic porch tablet', { exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Approve read access', exact: true })).toHaveCount(0);
  expect(writes.filter(write => write.path.endsWith('/approve'))).toHaveLength(0);
});

test('denial uses explicit confirmation and no approve call', async ({ page }) => {
  const writes = await ownerFixture(page);
  await reviewCode(page);
  await page.getByRole('button', { name: 'Decline this request', exact: true }).click();
  await expect(page.getByText('Decline access for Synthetic porch tablet?', { exact: true })).toBeVisible();
  expect(writes.filter(write => write.path.endsWith('/cancel'))).toHaveLength(0);
  await page.getByRole('button', { name: 'Confirm decline', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Declined');
  expect(writes.filter(write => write.path.endsWith('/cancel'))).toHaveLength(1);
  expect(writes.filter(write => write.path.endsWith('/approve'))).toHaveLength(0);
});

test('revoke keeps exact device and grants visible and requires a deliberate confirmation', async ({ page }) => {
  const writes = await ownerFixture(page, [{ id: sessionId, deviceName: 'Synthetic garden phone', expiresAt: new Date(Date.now() + 3_600_000).toISOString(), grant: { homeId: 'home-a', memberId: 'member-a', places: [{ placeId: 'garden', fields: ['Moisture'] }], canReadActivity: false, canReadHistory: false, canReadEngine: false } }]);
  await expect(page.getByText('Synthetic garden phone', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Revoke Synthetic garden phone', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Keep device access', exact: true })).toBeFocused();
  await page.getByRole('button', { name: 'Keep device access', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Revoke Synthetic garden phone', exact: true })).toBeFocused();
  expect(writes.filter(write => write.method === 'DELETE')).toHaveLength(0);
  await page.getByRole('button', { name: 'Revoke Synthetic garden phone', exact: true }).click();
  await page.getByRole('button', { name: 'Confirm revocation', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Revoked');
  expect(writes.filter(write => write.method === 'DELETE').map(write => write.path)).toEqual([`/api/companion/owner/sessions/${sessionId}`]);
});

test('failed owner read never masquerades as an empty device list', async ({ page }) => {
  await ownerFixture(page);
  await page.route('**/api/companion/owner/sessions', route => route.fulfill({ status: 403 }));
  await page.getByRole('button', { name: 'Refresh devices', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('Owner access');
  await expect(page.getByText('No active companion devices.', { exact: true })).toHaveCount(0);
});

test('expired reviews cannot grant access', async ({ page }) => {
  await ownerFixture(page);
  await page.route('**/api/companion/owner/pairings/review', route => route.fulfill({ json: { id: pairingId, homeId: 'home-a', deviceName: 'Synthetic expired tablet', expiresAt: new Date(Date.now() - 1000).toISOString(), status: 'Pending' } }));
  await page.getByLabel('Approval code').fill(code);
  await page.getByRole('button', { name: 'Review device' }).click();
  await expect(page.getByRole('alert')).toContainText('expired');
  await expect(page.getByRole('button', { name: 'Approve read access', exact: true })).toHaveCount(0);
});

test('ambiguous approval failure cannot be automatically replayed or leak the response body', async ({ page }) => {
  await ownerFixture(page);
  let approvals = 0;
  await page.route('**/api/companion/owner/pairings/*/approve', async route => {
    approvals++;
    await route.fulfill({ status: 503, json: { privateDiagnostic: 'synthetic-response-body-not-for-display' } });
  });
  await reviewCode(page);
  await page.getByLabel('Member ID').fill('member-a');
  await page.getByLabel('I matched this code and device with the intended person').check();
  await page.getByRole('button', { name: 'Approve read access', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('could not confirm');
  await expect(page.getByRole('button', { name: 'Approve read access', exact: true })).toHaveCount(0);
  await expect(page.getByText('synthetic-response-body-not-for-display')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Review device', exact: true })).toBeEnabled();
  expect(approvals).toBe(1);
});

test('catalog selection cannot duplicate a place and fieldless selections cannot be approved', async ({ page }) => {
  const writes = await ownerFixture(page);
  await reviewCode(page);
  await page.getByLabel('Member ID').fill('member-a');
  await page.getByLabel('I matched this code and device with the intended person').check();
  await page.getByRole('checkbox', { name: 'Choose Porch (porch)', exact: true }).check();
  await expect(page.getByRole('button', { name: 'Approve read access', exact: true })).toBeDisabled();
  await page.getByLabel('Temperature', { exact: true }).check();
  await page.getByRole('checkbox', { name: 'Choose Porch (porch)', exact: true }).check();
  await expect(page.getByRole('checkbox', { name: 'Choose Porch (porch)', exact: true })).toHaveCount(1);
  await page.getByRole('button', { name: 'Approve read access', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Approved');
  const approval = writes.find(write => write.path.endsWith('/approve'))!;
  expect((approval.body as { grant: { places: unknown[] } }).grant.places).toEqual([{ placeId: 'porch', fields: ['Temperature'] }]);
});

test('catalog failure is not an empty home and retry discards old selections and confirmation', async ({ page }) => {
  await ownerFixture(page);
  await page.route('**/api/companion/owner/discovery', route => route.fulfill({ status: 503, json: { secret: 'private-source-detail' } }));
  await reviewCode(page);
  await page.getByLabel('Member ID').fill('member-a');
  await page.getByLabel('I matched this code and device with the intended person').check();
  await expect(page.getByRole('alert')).toContainText('available places');
  await expect(page.getByRole('button', { name: 'Approve read access', exact: true })).toBeDisabled();
  await expect(page.getByText('private-source-detail')).toHaveCount(0);
  await page.unroute('**/api/companion/owner/discovery');
  await page.getByRole('button', { name: 'Reload available places', exact: true }).click();
  await page.getByRole('checkbox', { name: 'Choose Porch (porch)', exact: true }).check();
  await page.getByLabel('Temperature', { exact: true }).check();
  await page.getByLabel('I matched this code and device with the intended person').check();
  await page.getByRole('button', { name: 'Reload available places', exact: true }).click();
  await expect(page.getByRole('checkbox', { name: 'Choose Porch (porch)', exact: true })).not.toBeChecked();
  await expect(page.getByLabel('I matched this code and device with the intended person')).not.toBeChecked();
});

for (const corruption of ['other-home', 'duplicate-place', 'duplicate-source', 'unknown-field', 'too-many-places', 'too-many-sources', 'invalid-label', 'oversize']) {
  test(`invalid catalog is rejected before access selection: ${corruption}`, async ({ page }) => {
    const writes = await ownerFixture(page);
    const catalog = ownerCatalog();
    if (corruption === 'other-home') catalog.homeId = 'another-home';
    if (corruption === 'duplicate-place') catalog.places.push(catalog.places[0]);
    if (corruption === 'duplicate-source') catalog.places[1].sources.push(catalog.places[0].sources[0]);
    if (corruption === 'unknown-field') catalog.places[0].sources[0].field = 'UnlockDoor';
    if (corruption === 'too-many-places') catalog.places = Array.from({ length: 2001 }, (_, index) => ({ placeId: `room-${index}`, name: 'Room', sources: [] }));
    if (corruption === 'too-many-sources') catalog.places[0].sources = Array.from({ length: 2001 }, (_, index) => ({ sourceId: `sensor.temperature_${index}`, name: 'Temperature', field: 'Temperature' }));
    if (corruption === 'invalid-label') catalog.places[0].name = 'Misleading\u202eplace';
    await page.route('**/api/companion/owner/discovery', route => corruption === 'oversize'
      ? route.fulfill({ body: 'x'.repeat(524289), contentType: 'application/json' }) : route.fulfill({ json: catalog }));
    await reviewCode(page);
    await expect(page.getByRole('alert')).toContainText('available places');
    await expect(page.getByRole('checkbox', { name: /^Choose / })).toHaveCount(0);
    expect(writes.filter(write => write.path.endsWith('/approve'))).toHaveLength(0);
  });
}

test('only discovered fields are offered and a legitimate empty catalog stays explicit', async ({ page }) => {
  await ownerFixture(page);
  await reviewCode(page);
  await page.getByRole('checkbox', { name: 'Choose Garden (garden)', exact: true }).check();
  await expect(page.getByLabel('Moisture', { exact: true })).toBeVisible();
  await expect(page.getByLabel('Contact', { exact: true })).toHaveCount(0);
  await page.route('**/api/companion/owner/discovery', route => route.fulfill({ json: { ...ownerCatalog(), places: [] } }));
  await page.getByRole('button', { name: 'Reload available places', exact: true }).click();
  await expect(page.getByText('No supported places are available in this home.', { exact: true })).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);
});

test('search retains chosen access and no field is silently selected', async ({ page }) => {
  await ownerFixture(page);
  await reviewCode(page);
  await page.getByRole('checkbox', { name: 'Choose Porch (porch)', exact: true }).check();
  await expect(page.getByLabel('Temperature', { exact: true })).not.toBeChecked();
  await expect(page.getByLabel('Contact', { exact: true })).not.toBeChecked();
  await page.getByLabel('Temperature', { exact: true }).check();
  await page.getByLabel('Find a place', { exact: true }).fill('garden');
  await expect(page.getByRole('checkbox', { name: 'Choose Porch (porch)', exact: true })).toBeChecked();
  await expect(page.getByLabel('Temperature', { exact: true })).toBeChecked();
  await expect(page.getByRole('checkbox', { name: 'Choose Garden (garden)', exact: true })).toBeVisible();
});

test('large catalogs disclose more choices without losing selected fields during search', async ({ page }) => {
  await ownerFixture(page);
  await page.route('**/api/companion/owner/discovery', route => route.fulfill({ json: { ...ownerCatalog(), places: Array.from({ length: 60 }, (_, index) => ({
    placeId: `room-${index}`, name: 'Side room', sources: [{ sourceId: `sensor.room_${index}`, name: 'Temperature', field: 'Temperature' }],
  })) } }));
  await reviewCode(page);
  await expect(page.getByRole('checkbox', { name: /^Choose / })).toHaveCount(25);
  await page.getByRole('button', { name: 'Show more places', exact: true }).click();
  await expect(page.getByRole('checkbox', { name: /^Choose / })).toHaveCount(50);
  await page.getByLabel('Find a place', { exact: true }).fill('room-55');
  await page.getByRole('checkbox', { name: 'Choose Side room (room-55)', exact: true }).check();
  await page.getByLabel('Temperature', { exact: true }).check();
  await page.getByLabel('Find a place', { exact: true }).fill('room-1');
  await expect(page.getByRole('checkbox', { name: 'Choose Side room (room-55)', exact: true })).toBeChecked();
  await expect(page.getByLabel('Temperature', { exact: true })).toBeChecked();
  await expect(page.getByRole('checkbox', { name: 'Choose Side room (room-1)', exact: true })).toBeVisible();
});

test('oversized approval retains editable choices and reducing the selection restores approval', async ({ page }) => {
  test.setTimeout(90000);
  const writes = await ownerFixture(page);
  const id = (index: number) => 'r'.repeat(125) + index.toString().padStart(3, '0');
  await page.route('**/api/companion/owner/discovery', route => route.fulfill({ json: { ...ownerCatalog(), places: Array.from({ length: 100 }, (_, index) => ({
    placeId: id(index), name: `Place ${index}`, sources: [{ sourceId: `sensor.room_${index}`, name: 'Temperature', field: 'Temperature' }],
  })) } }));
  await reviewCode(page);
  await page.getByLabel('Member ID').fill('member-a');
  await page.getByLabel('I matched this code and device with the intended person').check();
  for (let more = 0; more < 3; more++) await page.getByRole('button', { name: 'Show more places', exact: true }).click();
  for (let index = 0; index < 100; index++) {
    await page.getByRole('checkbox', { name: `Choose Place ${index} (${id(index)})`, exact: true }).check();
    await page.getByRole('group', { name: `Place ${index} observations`, exact: true }).getByLabel('Temperature', { exact: true }).check();
  }
  await expect(page.getByRole('button', { name: 'Approve read access', exact: true })).toBeDisabled();
  await expect(page.getByRole('alert')).toContainText('too large');
  await expect(page.getByLabel('Member ID')).toHaveValue('member-a');
  await expect(page.getByRole('checkbox', { name: `Choose Place 99 (${id(99)})`, exact: true })).toBeChecked();
  expect(writes.filter(write => write.path.endsWith('/approve'))).toHaveLength(0);
  if (process.env.LUCIA_COMPANION_OWNER_CAPTURE_DIR) {
    for (const width of [390, 1024]) for (const scale of [1, 2]) {
      await page.setViewportSize({ width, height: 900 });
      await page.evaluate(value => { document.documentElement.style.fontSize = `${value * 100}%`; }, scale);
      await page.getByRole('alert').scrollIntoViewIfNeeded();
      await page.screenshot({ path: path.join(process.env.LUCIA_COMPANION_OWNER_CAPTURE_DIR, `owner-size-${width}-${scale}.png`) });
      expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBe(width);
    }
    await page.evaluate(() => { document.documentElement.style.fontSize = '100%'; });
  }
  for (let index = 90; index < 100; index++) await page.getByRole('checkbox', { name: `Choose Place ${index} (${id(index)})`, exact: true }).uncheck();
  await expect(page.getByRole('alert')).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Approve read access', exact: true })).toBeEnabled();
  expect(writes.filter(write => write.path.endsWith('/approve'))).toHaveLength(0);
});

test('malformed device grants are an unavailable list, never an empty success', async ({ page }) => {
  await ownerFixture(page);
  await page.route('**/api/companion/owner/sessions', route => route.fulfill({ json: [{ id: sessionId, deviceName: 'Synthetic invalid phone', expiresAt: new Date(Date.now() + 60_000).toISOString(), grant: { homeId: 'home-a', memberId: 'member-a', places: [{ placeId: 'porch', fields: ['UnlockDoor'] }], canReadActivity: false, canReadHistory: false, canReadEngine: false } }] }));
  await page.getByRole('button', { name: 'Refresh devices', exact: true }).click();
  await expect(page.getByRole('alert')).toContainText('invalid observation access');
  await expect(page.getByText('No active companion devices.', { exact: true })).toHaveCount(0);
  await expect(page.getByText('Synthetic invalid phone', { exact: true })).toHaveCount(0);
});

for (const width of [390, 1024]) for (const dark of [false, true]) for (const scale of [1, 2]) {
  test(`owner layout ${width} ${dark ? 'dark' : 'light'} ${scale}x`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    await page.emulateMedia({ colorScheme: dark ? 'dark' : 'light' });
    await ownerFixture(page, [{ id: sessionId, deviceName: 'Synthetic garden phone with a long owner-assigned name', expiresAt: new Date(Date.now() + 3_600_000).toISOString(), grant: { homeId: 'home-a', memberId: 'member-a', places: [{ placeId: 'garden', fields: ['Moisture', 'Temperature'] }], canReadActivity: false, canReadHistory: true, canReadEngine: false } }]);
    await page.evaluate(value => { document.documentElement.style.fontSize = `${value * 100}%`; }, scale);
    const destination = process.env.LUCIA_COMPANION_OWNER_CAPTURE_DIR;
    const prefix = `owner-${width}-${dark ? 'dark' : 'light'}-${scale}`;
    const capture = async (state: string) => {
      if (!destination) return;
      await mkdir(destination, { recursive: true });
      await page.screenshot({ path: path.join(destination, `${prefix}-${state}.png`) });
    };
    await reviewCode(page);
    await page.getByLabel('Member ID').fill('synthetic-member');
    await page.getByRole('heading', { name: 'Choose places and observations', exact: true }).scrollIntoViewIfNeeded();
    await capture('picker');
    await page.getByRole('checkbox', { name: 'Choose Porch (porch)', exact: true }).check();
    await page.getByLabel('Temperature', { exact: true }).check();
    await page.getByRole('checkbox', { name: 'Choose Porch (porch)', exact: true }).scrollIntoViewIfNeeded();
    await capture('selection');
    await page.route('**/api/companion/owner/discovery', route => route.fulfill({ status: 503 }));
    await page.getByRole('button', { name: 'Reload available places', exact: true }).click();
    await expect(page.getByRole('alert')).toContainText('available places');
    await page.getByRole('alert').scrollIntoViewIfNeeded();
    await capture('unavailable');
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBe(width);
  });
}
