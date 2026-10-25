import assert from 'node:assert/strict'
import test from 'node:test'
import { sharedLayoutProblem, validateOwnerLayout, validateSharedLayout } from '../src/companion-layout.ts'
import { fetchOwnerLayout, saveOwnerLayout } from '../src/companion-layout-api.ts'

const layout = () => ({ homeId: 'home-a', sourceBaseUrl: 'http://synthetic.invalid/', name: 'Our home',
  places: [{ id: 'front', name: 'Porch', icon: 'Door', sourcePlaceId: 'porch' }], connections: [],
  bindings: [{ placeId: 'front', sourcePlaceId: 'porch', sourceId: 'sensor.temperature', field: 'Temperature' }] })
const view = () => ({ homeId: 'home-a', sourceBaseUrl: 'http://synthetic.invalid', sourceMatches: true, revision: 'a'.repeat(32),
  layout: layout(), updatedAt: '2026-10-25T12:00:00Z', updatedBy: 'owner-a' })
const catalog = () => ({ homeId: 'home-a', capturedAt: '2026-10-25T12:00:00Z', places: [
  { placeId: 'porch', name: 'Porch', sources: [{ sourceId: 'sensor.temperature', name: 'Temperature', field: 'Temperature' }] },
] })

test('owner checked revisions use the storage GUID format, not the private member projection hash', () => {
  const value = view(); value.revision = 'aa41630f815344ed9d896e15ec3c8d71'
  assert.equal(validateOwnerLayout(value, false).snapshot.revision, value.revision)
  assert.throws(() => validateOwnerLayout({ ...value, revision: 'A'.repeat(64) }, false))
})

test('validated metadata is copied and the configured origin is canonicalized', () => {
  const original = view(); const result = validateOwnerLayout(original, false)
  original.layout.places[0].name = 'Other'; original.layout.bindings.length = 0
  assert.equal(result.sourceBaseUrl, 'http://synthetic.invalid/')
  assert.equal(result.snapshot.layout.places[0].name, 'Porch'); assert.equal(result.snapshot.layout.bindings.length, 1)
  assert.equal(sharedLayoutProblem(result.snapshot.layout, result, catalog()), null)
})

for (const defect of ['unknown-property', 'missing-bindings', 'blank', 'long-label', 'control', 'unicode', 'integer-icon', 'integer-field', 'duplicate-place', 'duplicate-role', 'orphan', 'self-edge', 'reversed-edge', 'provenance', 'bad-source', 'credentials', 'query', 'foreign-home', 'bad-revision', 'places-limit', 'bindings-limit', 'edges-limit']) {
  test(`invalid owner metadata is refused: ${defect}`, () => {
    const value = view(); const draft = value.layout
    switch (defect) {
      case 'unknown-property': draft.pins = ['front']; break
      case 'missing-bindings': delete draft.bindings; break
      case 'blank': draft.name = ' '; break
      case 'long-label': draft.name = 'a'.repeat(201); break
      case 'control': draft.name = 'Hidden\u202e'; break
      case 'unicode': draft.name = 'Bad\ud800'; break
      case 'integer-icon': draft.places[0].icon = 1; break
      case 'integer-field': draft.bindings[0].field = 1; break
      case 'duplicate-place': draft.places.push({ ...draft.places[0] }); break
      case 'duplicate-role': draft.bindings.push({ ...draft.bindings[0] }); break
      case 'orphan': draft.connections = [{ fromPlaceId: 'front', toPlaceId: 'elsewhere' }]; break
      case 'self-edge': draft.connections = [{ fromPlaceId: 'front', toPlaceId: 'front' }]; break
      case 'reversed-edge': draft.places.push({ ...draft.places[0], id: 'other' }); draft.connections = [{ fromPlaceId: 'front', toPlaceId: 'other' }, { fromPlaceId: 'other', toPlaceId: 'front' }]; break
      case 'provenance': draft.bindings[0].sourcePlaceId = 'elsewhere'; break
      case 'bad-source': draft.bindings[0].sourceId = 'friendly search term'; break
      case 'credentials': draft.sourceBaseUrl = 'http://owner:secret@synthetic.invalid'; break
      case 'query': draft.sourceBaseUrl = 'http://synthetic.invalid/?token=x'; break
      case 'foreign-home': draft.homeId = 'home-b'; break
      case 'bad-revision': value.revision = 'old'; break
      case 'places-limit': draft.places = Array.from({ length: 201 }, (_, index) => ({ ...draft.places[0], id: `place-${index}` })); break
      case 'bindings-limit': draft.bindings = Array.from({ length: 2001 }, () => ({ ...draft.bindings[0] })); break
      case 'edges-limit': draft.connections = Array.from({ length: 1001 }, () => ({ fromPlaceId: 'front', toPlaceId: 'other' })); break
    }
    assert.throws(() => validateOwnerLayout(value, false))
  })
}

test('valid surrogate pairs remain readable, while an unrelated configured origin is recovery metadata, not permission to save', () => {
  const value = view(); value.layout.name = 'Our home 🏡'; value.layout.sourceBaseUrl = 'http://old.invalid/'; value.sourceMatches = false
  const owner = validateOwnerLayout(value, false)
  assert.equal(owner.snapshot.layout.name, 'Our home 🏡')
  assert.match(sharedLayoutProblem(owner.snapshot.layout, owner, catalog()), /configured source changed/)
})

test('missing layout needs exact owner context; an ordinary or member 404 cannot create a blank draft', () => {
  const missing = { error: 'layout_not_configured', homeId: 'home-a', sourceBaseUrl: 'http://synthetic.invalid', sourceMatches: true }
  assert.equal(validateOwnerLayout(missing, true).snapshot, null)
  assert.throws(() => validateOwnerLayout({ error: 'layout_not_configured' }, true))
  assert.throws(() => validateOwnerLayout({ ...missing, token: 'private' }, true))
})

test('missing/moved source roles and a foreign catalog block review without changing metadata', () => {
  const owner = validateOwnerLayout(view(), false); const draft = layout()
  const missing = catalog(); missing.places[0].sources = []
  assert.match(sharedLayoutProblem(draft, owner, missing), /explicitly remove/)
  assert.equal(draft.bindings.length, 1)
  assert.match(sharedLayoutProblem(draft, owner, { ...catalog(), homeId: 'other' }), /Load the current/)
  assert.match(sharedLayoutProblem(draft, owner, null), /Load the current/)
})

test('owner transport uses same-origin cookie security, fresh CSRF and checked revision without native authority', async () => {
  const original = globalThis.fetch; const calls = []
  globalThis.fetch = async (url, init) => {
    calls.push({ url, init })
    assert.equal(init.credentials, 'same-origin'); assert.equal(init.mode, 'same-origin'); assert.equal(init.redirect, 'error'); assert.equal(init.cache, 'no-store')
    assert.equal(init.headers?.Authorization, undefined); assert.equal(init.headers?.['X-API-Key'], undefined)
    return Response.json(url.endsWith('/antiforgery') ? { headerName: 'RequestVerificationToken', requestToken: 'synthetic-proof' } : view())
  }
  try {
    await saveOwnerLayout(layout(), 'a'.repeat(32)); await saveOwnerLayout(layout(), 'a'.repeat(32))
    assert.deepEqual(calls.map(call => call.url), ['/api/companion/owner/antiforgery', '/api/companion/owner/layout', '/api/companion/owner/antiforgery', '/api/companion/owner/layout'])
    assert.equal(calls[1].init.method, 'PUT'); assert.equal(calls[1].init.headers.RequestVerificationToken, 'synthetic-proof')
    assert.equal(JSON.parse(calls[1].init.body).expectedRevision, 'a'.repeat(32))
  } finally { globalThis.fetch = original }
})

test('corrupt, oversized or owner-missing responses are safe errors and are not echoed', async () => {
  const original = globalThis.fetch
  try {
    for (const response of [new Response('PRIVATE_MARKER', { headers: { 'Content-Type': 'application/json' } }),
      new Response('x'.repeat(1_048_577), { headers: { 'Content-Type': 'application/json' } }), Response.json({ error: 'layout_not_configured' }, { status: 404 })]) {
      globalThis.fetch = async () => response
      await assert.rejects(fetchOwnerLayout(), error => !String(error).includes('PRIVATE_MARKER'))
    }
  } finally { globalThis.fetch = original }
})

test('layout validation does not mutate topology or allow an unknown owner field', () => {
  const value = layout(); const parsed = validateSharedLayout(value); parsed.connections.push({ fromPlaceId: 'front', toPlaceId: 'other' })
  assert.equal(value.connections.length, 0)
  assert.throws(() => validateSharedLayout({ ...value, canReadActivity: true }))
})
