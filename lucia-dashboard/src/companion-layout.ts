import { companionFields, validCompanionId } from './companion-api.ts'
import type { CompanionField, CompanionSourceCatalog } from './companion-api.ts'

export const layoutIcons = ['Home', 'Sofa', 'Garden', 'Kitchen', 'Door', 'Bed'] as const
export type LayoutIcon = typeof layoutIcons[number]
export interface LayoutPlace { id: string; name: string; icon: LayoutIcon; sourcePlaceId: string }
export interface LayoutConnection { fromPlaceId: string; toPlaceId: string }
export interface LayoutBinding { placeId: string; sourcePlaceId: string; sourceId: string; field: CompanionField }
export interface SharedLayout { homeId: string; sourceBaseUrl: string; name: string; places: LayoutPlace[]; connections: LayoutConnection[]; bindings: LayoutBinding[] }
export interface OwnerLayout { homeId: string; sourceBaseUrl: string; sourceMatches: boolean; snapshot: { revision: string; layout: SharedLayout; updatedAt: string; updatedBy: string } | null }
export const validOwnerLayoutRevision = (value: string) => /^[a-f0-9]{32}$/.test(value)

const invalid = () => new Error('Shared layout metadata is invalid or too large. No saved setup was replaced.')
export function layoutObject(value: unknown): Record<string, unknown> {
  if (!value || typeof value !== 'object' || Array.isArray(value)) throw invalid()
  return value as Record<string, unknown>
}
function exact(value: unknown, keys: string[]) {
  const item = layoutObject(value)
  if (Object.keys(item).length !== keys.length || keys.some(key => !Object.hasOwn(item, key))) throw invalid()
  return item
}
function id(value: unknown): string { if (typeof value !== 'string' || !validCompanionId(value)) throw invalid(); return value }
function label(value: unknown): string {
  if (typeof value !== 'string' || !value || value !== value.trim() || value.length > 200 || /[\p{Cc}\p{Cf}\uD800-\uDFFF]/u.test(value)) throw invalid()
  return value
}
export function layoutOrigin(value: unknown): string {
  if (typeof value !== 'string') throw invalid()
  const uri = new URL(value)
  if (!['http:', 'https:'].includes(uri.protocol) || uri.username || uri.password || uri.pathname !== '/' || uri.search || uri.hash) throw invalid()
  return uri.href
}
function list(value: unknown, maximum: number): unknown[] { if (!Array.isArray(value) || value.length > maximum) throw invalid(); return value }

export function validateSharedLayout(value: unknown): SharedLayout {
  const item = exact(value, ['homeId', 'sourceBaseUrl', 'name', 'places', 'connections', 'bindings'])
  const places = list(item.places, 200).map(value => {
    const place = exact(value, ['id', 'name', 'icon', 'sourcePlaceId'])
    if (!layoutIcons.some(icon => icon === place.icon)) throw invalid()
    return { id: id(place.id), name: label(place.name), icon: place.icon as LayoutIcon, sourcePlaceId: id(place.sourcePlaceId) }
  })
  if (new Set(places.map(place => place.id)).size !== places.length) throw invalid()
  const seenEdges = new Set<string>()
  const connections = list(item.connections, 1000).map(value => {
    const edge = exact(value, ['fromPlaceId', 'toPlaceId']); const from = id(edge.fromPlaceId); const to = id(edge.toPlaceId)
    const key = [from, to].sort().join('\u0000')
    if (from === to || !places.some(place => place.id === from) || !places.some(place => place.id === to) || seenEdges.has(key)) throw invalid()
    seenEdges.add(key); return { fromPlaceId: from, toPlaceId: to }
  })
  const seenRoles = new Set<string>()
  const bindings = list(item.bindings, 2000).map(value => {
    const binding = exact(value, ['placeId', 'sourcePlaceId', 'sourceId', 'field'])
    const placeId = id(binding.placeId); const sourcePlaceId = id(binding.sourcePlaceId)
    if (typeof binding.sourceId !== 'string' || binding.sourceId.length > 256 || !/^[a-z0-9_]+\.[a-z0-9_]+$/.test(binding.sourceId)
      || !companionFields.some(field => field === binding.field) || !places.some(place => place.id === placeId && place.sourcePlaceId === sourcePlaceId)) throw invalid()
    const key = `${binding.sourceId}\u0000${binding.field}`
    if (seenRoles.has(key)) throw invalid()
    seenRoles.add(key)
    return { placeId, sourcePlaceId, sourceId: binding.sourceId, field: binding.field as CompanionField }
  })
  return { homeId: id(item.homeId), sourceBaseUrl: layoutOrigin(item.sourceBaseUrl), name: label(item.name), places, connections, bindings }
}

export function validateOwnerLayout(value: unknown, missing: boolean): OwnerLayout {
  const item = exact(value, missing ? ['error', 'homeId', 'sourceBaseUrl', 'sourceMatches']
    : ['homeId', 'sourceBaseUrl', 'sourceMatches', 'revision', 'layout', 'updatedAt', 'updatedBy'])
  const homeId = id(item.homeId); const sourceBaseUrl = layoutOrigin(item.sourceBaseUrl)
  if (typeof item.sourceMatches !== 'boolean' || missing && item.error !== 'layout_not_configured') throw invalid()
  if (missing) return { homeId, sourceBaseUrl, sourceMatches: item.sourceMatches, snapshot: null }
  if (typeof item.revision !== 'string' || !validOwnerLayoutRevision(item.revision) || typeof item.updatedAt !== 'string'
    || !Number.isFinite(Date.parse(item.updatedAt))) throw invalid()
  const layout = validateSharedLayout(item.layout)
  if (layout.homeId !== homeId) throw invalid()
  return { homeId, sourceBaseUrl, sourceMatches: item.sourceMatches,
    snapshot: { revision: item.revision, layout, updatedAt: item.updatedAt, updatedBy: id(item.updatedBy) } }
}

export function sharedLayoutProblem(draft: SharedLayout, owner: OwnerLayout, catalog: CompanionSourceCatalog | null): string | null {
  try { validateSharedLayout(draft) } catch { return 'Use names of 1 to 200 visible characters and a valid layout before reviewing.' }
  if (!owner.sourceMatches || draft.homeId !== owner.homeId || layoutOrigin(draft.sourceBaseUrl) !== owner.sourceBaseUrl) return 'The configured source changed. Ask the operator to correct its binding before editing this layout.'
  if (!catalog || catalog.homeId !== owner.homeId) return 'Load the current available places before reviewing.'
  if (draft.places.some(place => !catalog.places.some(current => current.placeId === place.sourcePlaceId))
    || draft.bindings.some(binding => !catalog.places.some(place => place.placeId === binding.sourcePlaceId
      && place.sources.some(source => source.sourceId === binding.sourceId && source.field === binding.field))))
    return 'Some places or bindings are no longer available at their saved source. Review and explicitly remove them before saving.'
  if (new TextEncoder().encode(JSON.stringify({ expectedRevision: owner.snapshot?.revision ?? null, layout: draft })).length > 524_288)
    return 'Shared setup is too large. Remove places, connections or bindings before reviewing again.'
  return null
}
