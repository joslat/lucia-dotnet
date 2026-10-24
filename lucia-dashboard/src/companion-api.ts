export const companionFields = ['Temperature', 'Humidity', 'Moisture', 'Light', 'Contact', 'Motion', 'Leak'] as const
export type CompanionField = typeof companionFields[number]
export interface CompanionPlaceGrant { placeId: string; fields: CompanionField[] }
export interface CompanionGrant {
  homeId: string
  memberId: string
  places: CompanionPlaceGrant[]
  canReadActivity: boolean
  canReadHistory: boolean
  canReadEngine: boolean
}
export interface CompanionDevice { id: string; deviceName: string; expiresAt: string; grant: CompanionGrant }
export interface CompanionReview { id: string; homeId: string; deviceName: string; expiresAt: string; status: 'Pending' | 'Approved' }

const base = '/api/companion/owner'
export const validCompanionId = (value: string) => /^[A-Za-z0-9_.:-]{1,128}$/.test(value)
export function normalizeCompanionCode(value: string): string | null {
  const code = value.trim().replaceAll('-', '').toUpperCase()
  return /^[0123456789ABCDEFGHJKMNPQRSTVWXYZ]{10}$/.test(code) ? `${code.slice(0, 5)}-${code.slice(5)}` : null
}

function failure(status: number): Error {
  const message = status === 401 || status === 403 ? 'Owner access or security verification is missing. Sign in as an owner, then retry deliberately.'
    : status === 404 ? 'Companion access is disabled, unavailable, or this request no longer exists. Check the host configuration and code.'
    : status === 409 || status === 410 ? 'This request expired or changed. Review the code or refresh connected devices before trying again.'
    : status === 429 ? 'Lucia is limiting requests. Wait a minute before trying again.'
    : status === 400 ? 'Use the HTTPS dashboard and valid exact identifiers. The host refused this request.'
    : 'Lucia could not confirm the operation. Refresh devices or review the code before retrying. No request was replayed.'
  return new Error(message)
}

async function request(path: string, init: RequestInit = {}, signal?: AbortSignal): Promise<Response> {
  let response: Response
  try {
    response = await fetch(`${base}${path}`, {
      ...init, credentials: 'same-origin', mode: 'same-origin', cache: 'no-store', redirect: 'error',
      signal: signal ? AbortSignal.any([signal, AbortSignal.timeout(15_000)]) : AbortSignal.timeout(15_000),
    })
  } catch {
    throw new Error('Lucia could not be reached securely. No operation was confirmed or automatically replayed.')
  }
  if (!response.ok) throw failure(response.status)
  return response
}
async function json(response: Response): Promise<unknown> {
  try { return await response.json() }
  catch { throw new Error('Lucia returned unreadable device metadata. Refresh or review again before choosing access.') }
}

function object(value: unknown): Record<string, unknown> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('Lucia returned invalid device metadata. Nothing was approved.')
  return value as Record<string, unknown>
}
function text(value: unknown, predicate: (value: string) => boolean): string {
  if (typeof value !== 'string' || !predicate(value)) throw new Error('Lucia returned invalid device metadata. Nothing was approved.')
  return value
}
const identifier = (value: unknown) => text(value, validCompanionId)
const deviceName = (value: unknown) => text(value, name => name.trim().length > 0 && name.length <= 80
  && [...name].every(character => { const code = character.charCodeAt(0); return code >= 32 && (code < 127 || code > 159) }))
const expiry = (value: unknown) => text(value, date => Number.isFinite(Date.parse(date)))
function flag(value: unknown): boolean { if (typeof value !== 'boolean') throw new Error('Lucia returned invalid access metadata.'); return value }
function grant(value: unknown): CompanionGrant {
  const item = object(value)
  if (!Array.isArray(item.places) || item.places.length > 200) throw new Error('Lucia returned invalid place access.')
  const places = item.places.map(value => {
    const place = object(value)
    if (!Array.isArray(place.fields) || place.fields.some(field => !companionFields.includes(field)) || new Set(place.fields).size !== place.fields.length) throw new Error('Lucia returned invalid observation access.')
    return { placeId: identifier(place.placeId), fields: place.fields as CompanionField[] }
  })
  if (new Set(places.map(place => place.placeId)).size !== places.length) throw new Error('Lucia returned duplicate place access.')
  return { homeId: identifier(item.homeId), memberId: identifier(item.memberId), places, canReadActivity: flag(item.canReadActivity), canReadHistory: flag(item.canReadHistory), canReadEngine: flag(item.canReadEngine) }
}

async function mutate(path: string, body?: unknown, method = 'POST', signal?: AbortSignal): Promise<Response> {
  const serialized = body === undefined ? undefined : JSON.stringify(body)
  if (serialized && new TextEncoder().encode(serialized).length > 16_000) throw new Error('Access selection is too large. Choose fewer places before approving.')
  const proof = object(await json(await request('/antiforgery', {}, signal)))
  const token = text(proof.requestToken, value => value.length > 0 && value.length <= 8192 && !/[\r\n]/.test(value))
  const header = text(proof.headerName, value => value === 'RequestVerificationToken' || value === 'X-CSRF-TOKEN')
  return request(path, { method, headers: { [header]: token, ...(serialized ? { 'Content-Type': 'application/json' } : {}) }, body: serialized }, signal)
}
export async function fetchCompanionDevices(signal?: AbortSignal): Promise<CompanionDevice[]> {
  const values = await json(await request('/sessions', {}, signal))
  if (!Array.isArray(values)) throw new Error('Lucia returned invalid device metadata. The device list is unavailable.')
  return values.map(value => {
    const item = object(value)
    return { id: identifier(item.id), deviceName: deviceName(item.deviceName), expiresAt: expiry(item.expiresAt), grant: grant(item.grant) }
  })
}
export async function reviewCompanionDevice(code: string, signal?: AbortSignal): Promise<CompanionReview> {
  const normalized = normalizeCompanionCode(code)
  if (!normalized) throw new Error('Enter the ten-character approval code shown on the intended device.')
  const item = object(await json(await mutate('/pairings/review', { userCode: normalized }, 'POST', signal)))
  if (item.status !== 'Pending' && item.status !== 'Approved') throw new Error('This request is no longer available for review.')
  return { id: identifier(item.id), homeId: identifier(item.homeId), deviceName: deviceName(item.deviceName), expiresAt: expiry(item.expiresAt), status: item.status }
}
export async function approveCompanionDevice(id: string, value: CompanionGrant, signal?: AbortSignal): Promise<void> {
  await mutate(`/pairings/${encodeURIComponent(identifier(id))}/approve`, { codeConfirmed: true, grant: grant(value) }, 'POST', signal)
}
export async function declineCompanionDevice(id: string, signal?: AbortSignal): Promise<void> {
  await mutate(`/pairings/${encodeURIComponent(identifier(id))}/cancel`, undefined, 'POST', signal)
}
export async function revokeCompanionDevice(id: string, signal?: AbortSignal): Promise<void> {
  await mutate(`/sessions/${encodeURIComponent(identifier(id))}`, undefined, 'DELETE', signal)
}
