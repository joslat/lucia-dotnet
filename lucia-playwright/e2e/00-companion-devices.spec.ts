import { expect, test, type Page } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';

const pairingId = 'a'.repeat(32);
const sessionId = 'b'.repeat(32);
const code = 'ABCDE-FGHJK';

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
  await page.getByRole('button', { name: 'Add place access' }).click();
  await page.getByLabel('Place ID').fill('porch');
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

test('duplicate place IDs and fieldless rows cannot be approved', async ({ page }) => {
  const writes = await ownerFixture(page);
  await reviewCode(page);
  await page.getByLabel('Member ID').fill('member-a');
  await page.getByLabel('I matched this code and device with the intended person').check();
  await page.getByRole('button', { name: 'Add place access' }).click();
  await page.getByLabel('Place ID').fill('porch');
  await expect(page.getByRole('button', { name: 'Approve read access', exact: true })).toBeDisabled();
  await page.getByLabel('Temperature', { exact: true }).check();
  await page.getByRole('button', { name: 'Add place access' }).click();
  await page.getByLabel('Place ID').nth(1).fill('porch');
  await page.getByLabel('Contact', { exact: true }).nth(1).check();
  await expect(page.getByRole('button', { name: 'Approve read access', exact: true })).toBeDisabled();
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
    test.skip(!process.env.LUCIA_COMPANION_OWNER_CAPTURE_DIR, 'Opt-in bounded visual capture.');
    await page.setViewportSize({ width, height: 900 });
    await page.emulateMedia({ colorScheme: dark ? 'dark' : 'light' });
    await ownerFixture(page, [{ id: sessionId, deviceName: 'Synthetic garden phone with a long owner-assigned name', expiresAt: new Date(Date.now() + 3_600_000).toISOString(), grant: { homeId: 'home-a', memberId: 'member-a', places: [{ placeId: 'garden', fields: ['Moisture', 'Temperature'] }], canReadActivity: false, canReadHistory: true, canReadEngine: false } }]);
    await page.evaluate(value => { document.documentElement.style.fontSize = `${value * 100}%`; }, scale);
    const destination = process.env.LUCIA_COMPANION_OWNER_CAPTURE_DIR!;
    await mkdir(destination, { recursive: true });
    const prefix = `owner-${width}-${dark ? 'dark' : 'light'}-${scale}`;
    await page.screenshot({ path: path.join(destination, `${prefix}-form.png`) });
    await reviewCode(page);
    await page.getByLabel('Member ID').fill('synthetic-member');
    await page.getByRole('button', { name: 'Add place access' }).click();
    await page.getByLabel('Place ID').fill('porch');
    await page.getByLabel('Temperature', { exact: true }).check();
    await page.getByRole('heading', { name: 'Synthetic porch tablet', exact: true }).scrollIntoViewIfNeeded();
    await page.screenshot({ path: path.join(destination, `${prefix}-review.png`) });
    await page.getByText('Additional read access, off by default', { exact: true }).scrollIntoViewIfNeeded();
    await page.screenshot({ path: path.join(destination, `${prefix}-grant.png`) });
    await page.getByRole('button', { name: 'Revoke Synthetic garden phone with a long owner-assigned name', exact: true }).click();
    await page.getByRole('button', { name: 'Keep device access', exact: true }).scrollIntoViewIfNeeded();
    await page.screenshot({ path: path.join(destination, `${prefix}-revoke.png`) });
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBe(width);
  });
}
