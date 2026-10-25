import { expect, test } from '@playwright/test';

test('production owner editor saves and conflicts through actual isolated HTTPS, antiforgery, SQLite and HA protocol', async ({ page, context }) => {
  test.skip(!process.env.COMPANION_HTTPS_URL, 'Requires the isolated CompanionBrowserHost --owner-home fixture.');
  const origin = new URL(process.env.COMPANION_HTTPS_URL!);
  expect(origin.protocol).toBe('https:'); expect(['localhost', '127.0.0.1']).toContain(origin.hostname);
  await context.addCookies([{ name: '.lucia.session', value: 'synthetic-cookie', url: origin.origin, secure: true, httpOnly: true, sameSite: 'Strict' }]);
  await page.goto(`${origin.origin}/companion-devices`);
  const editor = page.getByRole('region', { name: 'Shared home layout', exact: true });
  await editor.getByRole('button', { name: 'Edit shared layout', exact: true }).click();
  await expect(editor.getByLabel('Home display name')).toBeEnabled();
  await editor.getByLabel('Home display name').fill('Synthetic shared wire home');
  await editor.getByLabel('Available place').selectOption('porch'); await editor.getByRole('button', { name: 'Add selected place', exact: true }).click();
  await editor.getByLabel('Place display name').fill('Shared wire porch');
  await editor.getByRole('checkbox', { name: 'Bind Porch temperature · Temperature', exact: true }).check();
  await editor.getByRole('button', { name: 'Review shared changes', exact: true }).click();
  await editor.getByRole('button', { name: 'Save shared layout', exact: true }).click();
  await expect(editor.getByRole('status')).toContainText('Shared layout saved');
  const current = await context.request.get(`${origin.origin}/api/companion/owner/layout`); expect(current.status()).toBe(200);
  const stored = await current.json(); expect(stored.layout.name).toBe('Synthetic shared wire home'); expect(stored.layout.bindings).toHaveLength(1);
  const noProof = await context.request.put(`${origin.origin}/api/companion/owner/layout`, { data: { expectedRevision: stored.revision, layout: stored.layout } }); expect(noProof.status()).toBe(403);
  const proofResponse = await context.request.get(`${origin.origin}/api/companion/owner/antiforgery`); const proof = await proofResponse.json();
  const competing = await context.request.put(`${origin.origin}/api/companion/owner/layout`, { headers: { [proof.headerName]: proof.requestToken },
    data: { expectedRevision: stored.revision, layout: { ...stored.layout, name: 'Other owner wire update' } } }); expect(competing.status()).toBe(200);
  await editor.getByLabel('Home display name').fill('My retained wire draft');
  await editor.getByRole('button', { name: 'Review shared changes', exact: true }).click();
  await editor.getByRole('button', { name: 'Save shared layout', exact: true }).click();
  await expect(editor.getByRole('alert')).toContainText('draft is kept');
  await editor.getByRole('button', { name: 'Read latest shared setup', exact: true }).click();
  await expect(editor.getByText('Other owner wire update', { exact: true })).toBeVisible();
  await expect(editor.getByText('My retained wire draft', { exact: true })).toBeVisible();
  await editor.getByRole('button', { name: 'Review my draft against this version', exact: true }).click();
  await editor.getByRole('button', { name: 'Save shared layout', exact: true }).click();
  await expect(editor.getByRole('status')).toContainText('Shared layout saved');
  const final = await context.request.get(`${origin.origin}/api/companion/owner/layout`); expect((await final.json()).layout.name).toBe('My retained wire draft');
});
