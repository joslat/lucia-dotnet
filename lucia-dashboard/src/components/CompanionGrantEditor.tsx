import { useRef, useState } from 'react'
import Field from './Field'
import { companionFields, validCompanionId } from '../companion-api'
import type { CompanionField, CompanionGrant, CompanionReview } from '../companion-api'

interface PlaceDraft { key: number; placeId: string; fields: CompanionField[] }
interface Props {
  review: CompanionReview
  busy: boolean
  onApprove: (grant: CompanionGrant) => void
}
const inputStyle = 'min-h-12 w-full min-w-0 rounded-xl border border-stone bg-basalt px-3 py-2 text-sm text-light input-focus'
const secondaryStyle = 'min-h-12 rounded-xl border border-stone px-4 py-2 text-sm text-fog hover:border-amber hover:text-light focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-amber disabled:opacity-50'

export default function CompanionGrantEditor({ review, busy, onApprove }: Props) {
  const [memberId, setMemberId] = useState('')
  const [places, setPlaces] = useState<PlaceDraft[]>([])
  const [confirmed, setConfirmed] = useState(false)
  const [activity, setActivity] = useState(false)
  const [history, setHistory] = useState(false)
  const [engine, setEngine] = useState(false)
  const nextKey = useRef(0)
  const valid = validCompanionId(memberId) && places.every(place => validCompanionId(place.placeId) && place.fields.length > 0)
    && new Set(places.map(place => place.placeId)).size === places.length
  const checkboxStyle = 'h-5 w-5 shrink-0 accent-amber focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-amber'
  return (
    <form onSubmit={event => {
      event.preventDefault()
      if (!valid || !confirmed || busy) return
      onApprove({ homeId: review.homeId, memberId, places: places.map(({ placeId, fields }) => ({ placeId, fields })), canReadActivity: activity, canReadHistory: history, canReadEngine: engine })
    }} className="mt-6 space-y-6">
      <fieldset disabled={busy} className="min-w-0 space-y-6">
        <legend className="mb-3 font-display text-lg font-semibold text-light">Choose exact read access</legend>
        <Field label="Member ID" hint="Owner-assigned identity, not the device name or an administrator key. Use 1 to 128 ASCII letters, digits, dots, colons, underscores or hyphens.">
          {control => <input {...control} required maxLength={128} value={memberId} onChange={event => setMemberId(event.target.value)} className={inputStyle} autoComplete="off" spellCheck={false} />}
        </Field>
        <p className="max-w-prose text-sm text-fog">Pilot setup uses exact place IDs. Copy them from the intended home configuration. A place picker will follow authorized discovery. Names and physical connections grant no access.</p>
        {places.map((place, index) => (
          <fieldset key={place.key} className="min-w-0 space-y-3 border-t border-stone pt-4">
            <legend className="pr-3 text-sm font-semibold text-light">Place access {index + 1}</legend>
            <Field label="Place ID" hint="Exact and case-sensitive. Choose only the observations this person should read.">
              {control => <input {...control} required maxLength={128} value={place.placeId} onChange={event => setPlaces(current => current.map(item => item.key === place.key ? { ...item, placeId: event.target.value } : item))} className={inputStyle} autoComplete="off" spellCheck={false} />}
            </Field>
            <div className="flex flex-wrap gap-x-5 gap-y-1">
              {companionFields.map(field => <label key={field} className="flex min-h-12 items-center gap-2 text-sm text-cloud">
                <input type="checkbox" checked={place.fields.includes(field)} onChange={event => setPlaces(current => current.map(item => item.key === place.key ? { ...item, fields: event.target.checked ? companionFields.filter(value => value === field || item.fields.includes(value)) : item.fields.filter(value => value !== field) } : item))} className={checkboxStyle} />{field}
              </label>)}
            </div>
            <button type="button" onClick={() => setPlaces(current => current.filter(item => item.key !== place.key))} className={secondaryStyle} aria-label={`Remove place access ${index + 1}`}>Remove this place</button>
          </fieldset>
        ))}
        <button type="button" disabled={places.length >= 200} onClick={() => setPlaces(current => [...current, { key: nextKey.current++, placeId: '', fields: [] }])} className={secondaryStyle}>Add place access</button>
        {places.length === 0 && <p className="text-sm text-fog">No places selected. This device will receive no place observations.</p>}
        {places.length > 0 && !valid && <p className="text-sm text-fog">Use a valid member ID and unique place IDs, and choose at least one observation for each place.</p>}
        <fieldset className="space-y-2">
          <legend className="mb-2 text-sm font-semibold text-light">Additional read access, off by default</legend>
          <label className="flex min-h-12 items-center gap-3 text-sm"><input className={checkboxStyle} type="checkbox" checked={activity} onChange={event => setActivity(event.target.checked)} />Activity summaries</label>
          <label className="flex min-h-12 items-center gap-3 text-sm"><input className={checkboxStyle} type="checkbox" checked={history} onChange={event => setHistory(event.target.checked)} />Historical observations</label>
          <label className="flex min-h-12 items-center gap-3 text-sm"><input className={checkboxStyle} type="checkbox" checked={engine} onChange={event => setEngine(event.target.checked)} />Engine runtime summaries</label>
          <p className="max-w-prose text-sm text-fog">History and activity can reveal household routines. Engine access is limited to approved summaries, never appliance administration or raw conversations. These live views are not wired into the native preview yet.</p>
        </fieldset>
        <label className="flex min-h-12 items-start gap-3 text-sm text-light"><input type="checkbox" className={`${checkboxStyle} mt-0.5`} checked={confirmed} onChange={event => setConfirmed(event.target.checked)} />I matched this code and device with the intended person</label>
        <button type="submit" disabled={busy || !valid || !confirmed} className="min-h-12 rounded-xl bg-amber px-5 py-3 text-sm font-semibold text-on-accent hover:bg-amber-glow focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-amber disabled:opacity-50 disabled:cursor-not-allowed">{busy ? 'Confirming with Lucia...' : 'Approve read access'}</button>
      </fieldset>
    </form>
  )
}
