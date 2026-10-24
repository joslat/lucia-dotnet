import { expect, test } from '@playwright/test';

// Opt-in isolated fixture, never BASE_URL or a household host. Certificate bypass is test-context only.
test('owner browser grants and revokes a real isolated HTTPS/SQLite device session', async ({ page, context, playwright }) => {
  test.skip(!process.env.COMPANION_HTTPS_URL, 'Start the synthetic CompanionBrowserHost fixture and set COMPANION_HTTPS_URL.');
  const origin = new URL(process.env.COMPANION_HTTPS_URL!);
  expect(origin.protocol).toBe('https:');
  expect(['localhost', '127.0.0.1']).toContain(origin.hostname);
  await context.addCookies([{ name: '.lucia.session', value: 'synthetic-cookie', url: origin.origin, secure: true, httpOnly: true, sameSite: 'Strict' }]);
  const device = await playwright.request.newContext({ baseURL: origin.origin, ignoreHTTPSErrors: true });
  try {
    const started = await device.post('/api/companion/pairings', { data: { deviceName: 'Synthetic HTTPS tablet' } });
    expect(started.status()).toBe(200);
    const challenge = await started.json();
    const deniedCsrf = await context.request.post(`${origin.origin}/api/companion/owner/pairings/review`, { data: { userCode: challenge.userCode } });
    expect(deniedCsrf.status()).toBe(403);
    await page.goto(`${origin.origin}/companion-devices`);
    await page.getByLabel('Approval code').fill(challenge.userCode);
    await page.getByRole('button', { name: 'Review device' }).click();
    await expect(page.getByText('Synthetic HTTPS tablet', { exact: true })).toBeVisible();
    await expect(page.getByLabel('Code to match')).toHaveText(challenge.userCode);
    await page.getByLabel('Member ID').fill('synthetic-member');
    await page.getByRole('checkbox', { name: 'Choose Porch (porch)', exact: true }).check();
    await page.getByLabel('Temperature', { exact: true }).check();
    await page.getByLabel('Historical observations', { exact: true }).check();
    await page.getByLabel('I matched this code and device with the intended person').check();
    await page.getByRole('button', { name: 'Approve read access', exact: true }).click();
    await expect(page.getByRole('status')).toContainText('Approved');
    const redeemed = await device.post('/api/companion/pairings/redeem', { data: { deviceCode: challenge.deviceCode } });
    expect(redeemed.status()).toBe(200);
    const credentials = await redeemed.json();
    const headers = { Authorization: `Bearer ${credentials.accessToken}` };
    const own = await device.get('/api/companion/session', { headers });
    expect(own.status()).toBe(200);
    const session = await own.json();
    expect(session.homeId).toBe('home-a');
    expect(session.memberId).toBe('synthetic-member');
    expect(session.places).toEqual([{ placeId: 'porch', fields: ['Temperature'] }]);
    expect(session.canReadHistory).toBe(true);
    expect(session.canReadEngine).toBe(false);
    const snapshot = await device.get('/api/companion/homes/home-a/snapshot', { headers });
    expect(snapshot.status()).toBe(200);
    const home = await snapshot.json();
    expect(home.places.map((place: { placeId: string }) => place.placeId)).toEqual(['porch']);
    expect(home.places[0].readings[0].numberValue).toBe(21.5);
    expect(JSON.stringify(home)).not.toContain('private-wire-marker');
    const ownerAttempt = await device.get('/api/companion/owner/sessions', { headers });
    expect([401, 403]).toContain(ownerAttempt.status());
    await page.getByRole('button', { name: 'Refresh devices', exact: true }).click();
    await expect(page.getByText('Synthetic HTTPS tablet', { exact: true })).toBeVisible();
    await page.getByText('Exact approved access', { exact: true }).click();
    await expect(page.getByText('porch: Temperature', { exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Revoke Synthetic HTTPS tablet', exact: true }).click();
    await page.getByRole('button', { name: 'Confirm revocation', exact: true }).click();
    await expect(page.getByRole('status')).toContainText('Revoked');
    expect((await device.get('/api/companion/session', { headers })).status()).toBe(401);
    expect((await device.post('/api/companion/session/renew', { data: { refreshToken: credentials.refreshToken } })).status()).toBe(401);
  } finally { await device.dispose(); }
});
