import { useState } from 'react'
import { MapPin } from 'lucide-react'
import Field from './Field'
import type { CompanionCatalog, CompanionPlaceGrant } from '../companion-api'

interface Props {
  catalog: CompanionCatalog
  selected: CompanionPlaceGrant[]
  onChange: (places: CompanionPlaceGrant[]) => void
}
const checkboxStyle = 'h-5 w-5 shrink-0 accent-amber focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-amber'

/** Exact metadata choices only. Names explain an ID; they never grant access by themselves. */
export default function CompanionPlacePicker({ catalog, selected, onChange }: Props) {
  const [query, setQuery] = useState('')
  const [limit, setLimit] = useState(25)
  const selectedIds = new Set(selected.map(place => place.placeId))
  const chosen = catalog.places.filter(place => selectedIds.has(place.placeId))
  const matching = catalog.places.filter(place => !selectedIds.has(place.placeId) && `${place.name} ${place.placeId}`.toLocaleLowerCase().includes(query.trim().toLocaleLowerCase()))
  const visible = [...chosen, ...matching.slice(0, limit)]
  return <section aria-label="Available places" className="min-w-0 space-y-4">
    <Field label="Find a place" hint="Search a name or exact ID. Chosen places stay visible while searching.">
      {control => <input {...control} type="search" value={query} onChange={event => { setQuery(event.target.value); setLimit(25) }} className="min-h-12 w-full min-w-0 rounded-xl border border-stone bg-basalt px-3 py-2 text-sm text-light input-focus" />}
    </Field>
    <p role="status" className="text-sm text-fog">{selected.length} of 200 places chosen. Select observations explicitly; nothing is selected for you.</p>
    {catalog.places.length === 0 && <p className="text-sm text-fog">No supported places are available in this home.</p>}
    {catalog.places.length > 0 && matching.length === 0 && <p className="text-sm text-fog">No other places match this search.</p>}
    <div className="divide-y divide-stone border-y border-stone">
      {visible.map(place => {
        const grant = selected.find(item => item.placeId === place.placeId)
        return <div key={place.placeId} className="min-w-0 space-y-3 py-4">
          <label className="flex min-h-12 items-start gap-3 text-sm text-light">
            <input type="checkbox" className={`${checkboxStyle} mt-0.5`} aria-label={`Choose ${place.name} (${place.placeId})`} checked={!!grant} disabled={place.fields.length === 0 || (!grant && selected.length >= 200)} onChange={event => onChange(event.target.checked ? [...selected, { placeId: place.placeId, fields: [] }] : selected.filter(item => item.placeId !== place.placeId))} />
            <MapPin className="h-5 w-5 shrink-0 text-fog" aria-hidden="true" />
            <span className="min-w-0 break-words"><span className="font-semibold">{place.name}</span><span className="mt-1 block text-fog">{place.placeId} · {place.sourceCount} {place.sourceCount === 1 ? 'source' : 'sources'}</span></span>
          </label>
          {place.fields.length === 0 && <p className="text-sm text-fog">No supported observation fields to grant.</p>}
          {grant && <fieldset className="min-w-0 space-y-2 pl-8">
            <legend className="break-words text-sm font-semibold text-light">{place.name} observations</legend>
            <div className="flex flex-wrap gap-x-5 gap-y-1">
              {place.fields.map(field => <label key={field} className="flex min-h-12 items-center gap-2 text-sm text-cloud">
                <input type="checkbox" className={checkboxStyle} checked={grant.fields.includes(field)} onChange={event => onChange(selected.map(item => item.placeId === place.placeId
                  ? { ...item, fields: event.target.checked ? place.fields.filter(value => value === field || item.fields.includes(value)) : item.fields.filter(value => value !== field) } : item))} />{field}
              </label>)}
            </div>
            {grant.fields.length === 0 && <p className="text-sm text-fog">Choose at least one observation, or deselect this place.</p>}
          </fieldset>}
        </div>
      })}
    </div>
    {matching.length > limit && <button type="button" onClick={() => setLimit(value => value + 25)} className="min-h-12 rounded-xl border border-stone px-4 py-2 text-sm text-fog hover:border-amber hover:text-light focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-amber">Show more places</button>}
    <p className="text-sm text-fog">Names and room connections grant no access. Catalog captured {new Date(catalog.capturedAt).toLocaleString()}; the host still checks current owner authority when approving.</p>
  </section>
}
