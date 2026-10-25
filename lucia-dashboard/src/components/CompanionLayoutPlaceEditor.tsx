import { useRef } from 'react'
import Field from './Field'
import { layoutIcons } from '../companion-layout'
import type { LayoutPlace, SharedLayout } from '../companion-layout'
import type { CompanionSourceCatalog } from '../companion-api'

export const layoutSecondary = 'min-h-12 rounded-xl border border-stone bg-basalt px-4 py-2 text-sm text-fog hover:border-amber hover:text-light focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-amber disabled:opacity-50'
export const layoutInput = 'min-h-12 w-full min-w-0 rounded-xl border border-stone bg-basalt px-3 py-2 text-sm text-light input-focus'

export default function CompanionLayoutPlaceEditor({ draft, place, catalog, onChange, onRemove }: {
  draft: SharedLayout; place: LayoutPlace; catalog: CompanionSourceCatalog | null; onChange: (draft: SharedLayout) => void; onRemove: (button: HTMLButtonElement) => void
}) {
  const sourcesRef = useRef<HTMLFieldSetElement>(null)
  const available = catalog?.places.find(item => item.placeId === place.sourcePlaceId)
  const bindings = draft.bindings.filter(binding => binding.placeId === place.id)
  const unavailable = bindings.filter(binding => !available?.sources.some(source => source.sourceId === binding.sourceId && source.field === binding.field))
  const changePlace = (change: Partial<LayoutPlace>) => onChange({ ...draft, places: draft.places.map(item => item.id === place.id ? { ...item, ...change } : item) })
  return <div className="min-w-0 space-y-5">
    <Field label="Place display name" hint="Presentation only. This does not rename a Home Assistant area.">{control =>
      <input {...control} className={layoutInput} maxLength={200} value={place.name} onChange={event => changePlace({ name: event.target.value })} />}</Field>
    <Field label="Place icon">{control => <select {...control} className={layoutInput} value={place.icon} onChange={event => changePlace({ icon: event.target.value as LayoutPlace['icon'] })}>
      {layoutIcons.map(icon => <option key={icon}>{icon}</option>)}
    </select>}</Field>
    <p className="break-words text-sm text-fog">Source place: {place.sourcePlaceId}{!available && '. No longer available. Remove this place explicitly before saving.'}</p>
    <fieldset ref={sourcesRef} tabIndex={-1} className="min-w-0 space-y-2 outline-offset-4"><legend className="mb-2 max-w-full break-words font-semibold text-light">Sources to display</legend>
      <p className="break-words text-sm text-fog">Bindings do not grant access. Each device still receives only its approved fields.</p>
      {available?.sources.map(source => {
        const checked = bindings.some(binding => binding.sourceId === source.sourceId && binding.field === source.field)
        const assignedElsewhere = draft.bindings.some(binding => binding.placeId !== place.id && binding.sourceId === source.sourceId && binding.field === source.field)
        return <label key={`${source.sourceId}:${source.field}`} className="flex min-h-12 min-w-0 items-start gap-3 py-3 text-sm text-cloud">
          <input className="mt-1 shrink-0 accent-amber" type="checkbox" checked={checked} disabled={assignedElsewhere}
            aria-label={`Bind ${source.name} · ${source.field}`} onChange={event => onChange({ ...draft, bindings: event.target.checked
              ? [...draft.bindings, { placeId: place.id, sourcePlaceId: place.sourcePlaceId, sourceId: source.sourceId, field: source.field }]
              : draft.bindings.filter(binding => !(binding.placeId === place.id && binding.sourceId === source.sourceId && binding.field === source.field)) })} />
          <span className="min-w-0 break-words">{source.name} · {source.field}<span className="block break-all text-xs text-fog">{source.sourceId}{assignedElsewhere && ' · Already bound to another configured place'}</span></span>
        </label>
      })}
      {available?.sources.length === 0 && <p className="text-sm text-fog">This place has no supported sources in the current catalog.</p>}
      {unavailable.map(binding => <div key={`${binding.sourceId}:${binding.field}`} className="space-y-2 border-t border-stone py-3">
        <p className="break-all text-sm text-rose">{binding.sourceId} · {binding.field}. Missing or moved; no reassignment was inferred.</p>
        <button type="button" className={layoutSecondary} onClick={() => { onChange({ ...draft, bindings: draft.bindings.filter(item => item !== binding) }); requestAnimationFrame(() => sourcesRef.current?.focus()) }}
          aria-label={`Remove unavailable binding ${binding.sourceId} · ${binding.field}`}>Remove unavailable binding</button>
      </div>)}
    </fieldset>
    <fieldset className="min-w-0 space-y-2"><legend className="mb-2 max-w-full break-words font-semibold text-light">Physical connections</legend>
      <p className="text-sm text-fog">Choose actual neighboring places. These lines are not a measured floor plan or sensor ownership.</p>
      {draft.places.filter(other => other.id !== place.id).map(other => {
        const connected = draft.connections.some(edge => edge.fromPlaceId === place.id && edge.toPlaceId === other.id || edge.toPlaceId === place.id && edge.fromPlaceId === other.id)
        return <label key={other.id} className="flex min-h-12 items-start gap-3 py-3 text-sm text-cloud">
          <input type="checkbox" className="mt-1 shrink-0 accent-amber" checked={connected} aria-label={`Connect to ${other.name}`} onChange={event => onChange({ ...draft,
            connections: event.target.checked ? [...draft.connections, { fromPlaceId: place.id, toPlaceId: other.id }]
              : draft.connections.filter(edge => !(edge.fromPlaceId === place.id && edge.toPlaceId === other.id || edge.toPlaceId === place.id && edge.fromPlaceId === other.id)) })} />
          <span className="min-w-0 break-words">{other.name}</span>
        </label>
      })}
      {draft.places.length < 2 && <p className="text-sm text-fog">Add another place to configure a connection.</p>}
    </fieldset>
    <button type="button" className={`${layoutSecondary} text-rose`} onClick={event => onRemove(event.currentTarget)}>Remove selected place</button>
  </div>
}
