import { useEffect, useState } from 'react'
import Field from './Field'
import CompanionPlacePicker from './CompanionPlacePicker'
import { companionApprovalFits, fetchCompanionCatalog, validCompanionId } from '../companion-api'
import type { CompanionCatalog, CompanionPlaceGrant, CompanionGrant, CompanionReview } from '../companion-api'

interface Props {
  review: CompanionReview
  busy: boolean
  onApprove: (grant: CompanionGrant) => void
}
const inputStyle = 'min-h-12 w-full min-w-0 rounded-xl border border-stone bg-basalt px-3 py-2 text-sm text-light input-focus'
const secondaryStyle = 'min-h-12 rounded-xl border border-stone px-4 py-2 text-sm text-fog hover:border-amber hover:text-light focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-amber disabled:opacity-50'

export default function CompanionGrantEditor({ review, busy, onApprove }: Props) {
  const [memberId, setMemberId] = useState('')
  const [places, setPlaces] = useState<CompanionPlaceGrant[]>([])
  const [confirmed, setConfirmed] = useState(false)
  const [activity, setActivity] = useState(false)
  const [history, setHistory] = useState(false)
  const [engine, setEngine] = useState(false)
  const [catalog, setCatalog] = useState<CompanionCatalog | null>(null)
  const [catalogError, setCatalogError] = useState('')
  const [loading, setLoading] = useState(true)
  const [revision, setRevision] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    void fetchCompanionCatalog(review.homeId, controller.signal).then(value => {
      if (!controller.signal.aborted) { setCatalog(value); setCatalogError('') }
    }).catch((error: unknown) => {
      if (!controller.signal.aborted) setCatalogError(error instanceof Error ? error.message : 'Available places could not be loaded. Reload before approving.')
    }).finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [review.homeId, revision])
  function reload() {
    setCatalog(null); setCatalogError(''); setLoading(true); setPlaces([]); setConfirmed(false); setRevision(value => value + 1)
  }
  const draft: CompanionGrant = { homeId: review.homeId, memberId, places, canReadActivity: activity, canReadHistory: history, canReadEngine: engine }
  const oversized = !companionApprovalFits(draft)
  const valid = !!catalog && catalog.homeId === review.homeId && !loading && !catalogError && !oversized && validCompanionId(memberId) && places.length <= 200
    && places.every(place => place.fields.length > 0 && catalog.places.some(available => available.placeId === place.placeId && place.fields.every(field => available.fields.includes(field))))
    && new Set(places.map(place => place.placeId)).size === places.length
  const checkboxStyle = 'h-5 w-5 shrink-0 accent-amber focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-amber'
  return (
    <form onSubmit={event => {
      event.preventDefault()
      if (!valid || !confirmed || busy) return
      onApprove(draft)
    }} className="mt-6 space-y-6">
      <fieldset disabled={busy} className="min-w-0 space-y-6">
        <legend className="mb-3 font-display text-lg font-semibold text-light">Choose exact read access</legend>
        <Field label="Member ID" hint="Owner-assigned identity, not the device name or an administrator key. Use 1 to 128 ASCII letters, digits, dots, colons, underscores or hyphens.">
          {control => <input {...control} required maxLength={128} value={memberId} onChange={event => setMemberId(event.target.value)} className={inputStyle} autoComplete="off" spellCheck={false} />}
        </Field>
        <div className="space-y-4 border-t border-stone pt-4">
          <div className="flex flex-wrap items-center justify-between gap-3"><h4 className="font-display text-lg font-semibold text-light">Choose places and observations</h4><button type="button" disabled={loading} onClick={reload} className={secondaryStyle}>Reload available places</button></div>
          {loading && <p role="status" className="text-sm text-fog">Reading available places from the authorized home catalog...</p>}
          {catalogError && <p role="alert" className="max-w-prose text-sm text-rose">{catalogError}</p>}
          {catalog && <CompanionPlacePicker catalog={catalog} selected={places} onChange={setPlaces} />}
        </div>
        {places.length === 0 && <p className="text-sm text-fog">No places selected. This device will receive no place observations.</p>}
        {places.length > 0 && !valid && <p className="text-sm text-fog">Use a valid member ID and choose at least one available observation for each selected place.</p>}
        <fieldset className="space-y-2">
          <legend className="mb-2 text-sm font-semibold text-light">Additional read access, off by default</legend>
          <label className="flex min-h-12 items-center gap-3 text-sm"><input className={checkboxStyle} type="checkbox" checked={activity} onChange={event => setActivity(event.target.checked)} />Activity summaries</label>
          <label className="flex min-h-12 items-center gap-3 text-sm"><input className={checkboxStyle} type="checkbox" checked={history} onChange={event => setHistory(event.target.checked)} />Historical observations</label>
          <label className="flex min-h-12 items-center gap-3 text-sm"><input className={checkboxStyle} type="checkbox" checked={engine} onChange={event => setEngine(event.target.checked)} />Engine runtime summaries</label>
          <p className="max-w-prose text-sm text-fog">History and activity can reveal household routines. Engine access is limited to approved summaries, never appliance administration or raw conversations. These live views are not wired into the native preview yet.</p>
        </fieldset>
        {oversized && <p role="alert" className="max-w-prose text-sm text-rose">This access selection is too large. Deselect some places or observations; your choices are kept.</p>}
        <label className="flex min-h-12 items-start gap-3 text-sm text-light"><input type="checkbox" className={`${checkboxStyle} mt-0.5`} checked={confirmed} onChange={event => setConfirmed(event.target.checked)} />I matched this code and device with the intended person</label>
        <button type="submit" disabled={busy || !valid || !confirmed} className="min-h-12 rounded-xl bg-amber px-5 py-3 text-sm font-semibold text-on-accent hover:bg-amber-glow focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-amber disabled:opacity-50 disabled:cursor-not-allowed">{busy ? 'Confirming with Lucia...' : 'Approve read access'}</button>
      </fieldset>
    </form>
  )
}
