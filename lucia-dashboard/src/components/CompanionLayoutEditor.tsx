import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { House } from 'lucide-react'
import Field from './Field'
import CompanionLayoutPlaceEditor, { layoutInput, layoutSecondary } from './CompanionLayoutPlaceEditor'
import CompanionLayoutSummary from './CompanionLayoutSummary'
import { useCompanionLayoutDraft } from './companion-layout-draft-context'
import { fetchCompanionSourceCatalog } from '../companion-api'
import type { CompanionSourceCatalog } from '../companion-api'
import { fetchOwnerLayout, saveOwnerLayout } from '../companion-layout-api'
import { sharedLayoutProblem } from '../companion-layout'
import type { OwnerLayout, SharedLayout } from '../companion-layout'

const primary = 'min-h-12 rounded-xl bg-amber px-4 py-2 text-sm font-semibold text-on-accent hover:bg-amber-glow focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-amber disabled:opacity-50'
const errorMessage = (error: unknown) => error instanceof Error ? error.message : 'Lucia could not confirm this operation.'
type Comparison = { owner: OwnerLayout; catalog: CompanionSourceCatalog }

export default function CompanionLayoutEditor() {
  const kept = useCompanionLayoutDraft()
  const initial = useRef(kept.current).current
  const [open, setOpen] = useState(!!initial)
  const [owner, setOwner] = useState<OwnerLayout | null>(initial?.owner ?? null)
  const [catalog, setCatalog] = useState<CompanionSourceCatalog | null>(null)
  const [draft, setDraft] = useState<SharedLayout | null>(initial?.draft ?? null)
  const [selected, setSelected] = useState('')
  const [availableId, setAvailableId] = useState('')
  const [step, setStep] = useState<'edit' | 'review' | 'recovery'>(initial ? 'recovery' : 'edit')
  const [comparison, setComparison] = useState<Comparison | null>(null)
  const [busy, setBusy] = useState(false)
  const [notice, setNotice] = useState(initial ? 'Unsaved draft kept after dashboard navigation. Read the latest shared setup before choosing a save baseline.' : '')
  const [error, setError] = useState('')
  const [remove, setRemove] = useState<string | null>(null)
  const [closing, setClosing] = useState(false)
  const lifetime = useRef<AbortController | null>(null)
  const operation = useRef(false)
  const statusRef = useRef<HTMLParagraphElement>(null)
  const reviewRef = useRef<HTMLHeadingElement>(null)
  const keepRef = useRef<HTMLButtonElement>(null)
  const openRef = useRef<HTMLButtonElement>(null)
  const removeInvoker = useRef<HTMLButtonElement | null>(null)
  const closeRef = useRef<HTMLButtonElement>(null)
  const reviewButton = useRef<HTMLButtonElement>(null)
  const availableRef = useRef<HTMLSelectElement>(null)
  const placeRef = useRef<HTMLSelectElement>(null)
  const draftBaseline = owner?.snapshot?.layout ?? null
  const dirty = draft !== null && JSON.stringify(draft) !== JSON.stringify(draftBaseline)
  const problem = draft && owner ? sharedLayoutProblem(draft, owner, catalog) : 'Read the shared setup first.'
  const place = draft?.places.find(item => item.id === selected) ?? draft?.places[0]
  const available = catalog?.places.filter(item => !draft?.places.some(place => place.sourcePlaceId === item.placeId)) ?? []

  useEffect(() => { const controller = new AbortController(); lifetime.current = controller; return () => controller.abort() }, [])
  useLayoutEffect(() => { kept.current = open && dirty && owner && draft ? { owner, draft } : null }, [open, dirty, owner, draft, kept])
  useEffect(() => { if (remove || closing) keepRef.current?.focus() }, [remove, closing])

  async function perform(action: (signal: AbortSignal) => Promise<void>, saving = false) {
    if (operation.current || !lifetime.current) return
    const signal = lifetime.current.signal
    operation.current = true; setBusy(true); setError(''); setNotice('Checking shared setup with Lucia...')
    try { await action(signal) }
    catch (error) {
      if (!signal.aborted) {
        setNotice(''); setError(`${errorMessage(error)}${saving ? ' Your draft is kept. Read the latest shared setup before any new save.' : step === 'recovery' ? ' Your draft is kept.' : ''}`)
        if (saving) { setStep('recovery'); setComparison(null) }
      }
    } finally {
      operation.current = false
      if (!signal.aborted) { setBusy(false); requestAnimationFrame(() => statusRef.current?.focus()) }
    }
  }
  function applyOwner(value: OwnerLayout) {
    setOwner(value); setDraft(value.snapshot?.layout ?? { homeId: value.homeId, sourceBaseUrl: value.sourceBaseUrl, name: 'Your home', places: [], connections: [], bindings: [] })
    setSelected(value.snapshot?.layout.places[0]?.id ?? ''); setAvailableId(''); setStep('edit'); setComparison(null); setRemove(null); setClosing(false)
  }
  function load() {
    setOpen(true); setOwner(null); setDraft(null); setCatalog(null)
    void perform(async signal => {
      const value = await fetchOwnerLayout(signal); if (signal.aborted) return
      applyOwner(value)
      const sources = await fetchCompanionSourceCatalog(value.homeId, signal); if (signal.aborted) return
      setCatalog(sources); setNotice(value.snapshot ? 'Shared setup loaded. Edits stay in this draft until reviewed and saved.' : 'No shared setup yet. Choose available places, then review before saving.')
    })
  }
  function change(value: SharedLayout) { setDraft(value); setStep('edit'); setError(''); setNotice('Unsaved shared-layout draft. Review before saving.') }
  function review() { setStep('review'); setNotice('Review the complete shared metadata below. Nothing has been saved yet.'); requestAnimationFrame(() => reviewRef.current?.focus()) }
  function addPlace() {
    if (!draft) return
    const source = available.find(item => item.placeId === availableId); if (!source || draft.places.length >= 200) return
    const id = `place-${crypto.randomUUID()}`
    change({ ...draft, places: [...draft.places, { id, name: source.name, icon: 'Home', sourcePlaceId: source.placeId }] }); setSelected(id); setAvailableId('')
  }
  function move(delta: number) {
    if (!draft || !place) return
    const places = [...draft.places]; const index = places.findIndex(item => item.id === place.id); const next = index + delta
    if (next < 0 || next >= places.length) return
    ;[places[index], places[next]] = [places[next], places[index]]; change({ ...draft, places })
  }
  function close() { setOpen(false); setClosing(false); setDraft(null); setOwner(null); setCatalog(null); requestAnimationFrame(() => openRef.current?.focus()) }
  function keepPlace() { setRemove(null); requestAnimationFrame(() => removeInvoker.current?.focus()) }
  function keepEditing() { setClosing(false); requestAnimationFrame(() => closeRef.current?.focus()) }

  return <section aria-labelledby="shared-layout-heading" className="rounded-xl border border-stone bg-charcoal p-5 sm:p-6">
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div className="min-w-0 space-y-3"><h2 id="shared-layout-heading" className="flex items-center gap-3 font-display text-xl font-semibold text-light"><House className="h-6 w-6 shrink-0 text-fog" aria-hidden="true" />Shared home layout</h2>
        <p className="max-w-prose text-sm text-fog">Arrange names, icons, source bindings and physical neighbors for companion clients. This changes Lucia presentation only, not Home Assistant or device permissions.</p></div>
      {!open && <button type="button" ref={openRef} className={layoutSecondary} onClick={load}>Edit shared layout</button>}
    </div>
    {open && <div className="mt-6 space-y-6">
      <p ref={statusRef} tabIndex={-1} role={error ? 'alert' : 'status'} className={`max-w-prose break-words text-sm outline-offset-4 ${error ? 'text-rose' : 'text-fog'}`}>{error || notice}</p>
      {!draft && !busy && <button type="button" className={layoutSecondary} onClick={load}>Read shared setup</button>}
      {owner && <p className="break-words text-sm text-fog">Host: {window.location.origin}<br />Configured source: {owner.sourceBaseUrl}<br />Home: {owner.homeId}</p>}
      {draft && owner && step === 'edit' && <>
        <fieldset disabled={busy || remove !== null || closing} className="min-w-0 space-y-6">
          <Field label="Home display name">{control => <input {...control} className={layoutInput} value={draft.name} maxLength={200} onChange={event => change({ ...draft, name: event.target.value })} />}</Field>
          <div className="flex flex-wrap items-end gap-3">
            <Field label="Available place" hint="Places come from the configured source. No connections or sensor bindings are assumed." className="min-w-0 flex-1 basis-64">{control =>
              <select {...control} ref={availableRef} className={layoutInput} value={availableId} onChange={event => setAvailableId(event.target.value)}><option value="">Choose a place</option>{available.map(item => <option key={item.placeId} value={item.placeId}>{item.name}</option>)}</select>}</Field>
            <button type="button" className={layoutSecondary} disabled={!availableId || draft.places.length >= 200} onClick={addPlace}>Add selected place</button>
          </div>
          <button type="button" className={layoutSecondary} onClick={() => void perform(async signal => {
            setCatalog(null); const current = await fetchCompanionSourceCatalog(owner.homeId, signal); if (!signal.aborted) { setCatalog(current); setNotice('Available sources reloaded. Your draft is unchanged.') }
          })}>Reload available sources</button>
          {draft.places.length === 0 ? <p className="text-sm text-fog">No places in this draft. Add a room, porch or garden from the available source places.</p> : <div className="grid min-w-0 gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(0,1.6fr)]">
            <div className="min-w-0 space-y-3">
              <Field label="Place to edit">{control => <select {...control} ref={placeRef} className={layoutInput} value={place?.id ?? ''} onChange={event => setSelected(event.target.value)}>{draft.places.map(item => <option key={item.id} value={item.id}>{item.name || 'Unnamed place'}</option>)}</select>}</Field>
              <div className="flex flex-wrap gap-3"><button type="button" className={layoutSecondary} disabled={place === draft.places[0]} onClick={() => move(-1)}>Move place earlier</button><button type="button" className={layoutSecondary} disabled={place === draft.places.at(-1)} onClick={() => move(1)}>Move place later</button></div>
              <p className="text-sm text-fog">This order stays stable. Personal pins and section order remain on each client.</p>
            </div>
            {place && <CompanionLayoutPlaceEditor draft={draft} place={place} catalog={catalog} onChange={change} onRemove={button => { removeInvoker.current = button; setRemove(place.id) }} />}
          </div>}
          {problem && <p className="max-w-prose text-sm text-rose">{problem}</p>}
          <button type="button" ref={reviewButton} className={primary} disabled={!!problem || !dirty} onClick={review}>Review shared changes</button>
        </fieldset>
        {remove && <div className="space-y-3 border-t border-stone pt-4" onKeyDown={event => { if (event.key === 'Escape' && !busy) { event.preventDefault(); event.stopPropagation(); keepPlace() } }}><p className="break-words text-sm text-light">Remove {draft.places.find(item => item.id === remove)?.name} and its bindings/connections from this draft?</p><div className="flex flex-wrap gap-3">
          <button type="button" ref={keepRef} className={layoutSecondary} onClick={keepPlace}>Keep place</button>
          <button type="button" className={`${layoutSecondary} text-rose`} onClick={() => { const places = draft.places.filter(item => item.id !== remove); change({ ...draft, places, bindings: draft.bindings.filter(item => item.placeId !== remove), connections: draft.connections.filter(edge => edge.fromPlaceId !== remove && edge.toPlaceId !== remove) }); setSelected(''); setRemove(null); requestAnimationFrame(() => (places.length ? placeRef.current : availableRef.current)?.focus()) }}>Confirm removal from draft</button>
        </div></div>}
      </>}
      {draft && owner && step === 'review' && <div className="space-y-5">
        <h3 ref={reviewRef} tabIndex={-1} className="font-display text-xl font-semibold text-light outline-offset-4">Review your shared layout</h3>
        <p className="max-w-prose text-sm text-fog">Save replaces the shared presentation metadata at the inspected version. It grants no access, changes no physical device and leaves personal pins, section order and local drafts alone. Clients must adopt the shared setup explicitly.</p>
        <CompanionLayoutSummary layout={draft} />
        {problem && <p className="text-sm text-rose">{problem}</p>}
        <div className="flex flex-wrap gap-3"><button type="button" className={layoutSecondary} disabled={busy || closing} onClick={() => { setStep('edit'); setNotice('Draft kept. Nothing was saved.'); requestAnimationFrame(() => reviewButton.current?.focus()) }}>Back to editing</button><button type="button" className={primary} disabled={busy || closing || !!problem} onClick={() => void perform(async signal => {
          const result = await saveOwnerLayout(draft, owner.snapshot?.revision ?? null, signal); if (!signal.aborted) { applyOwner(result); setNotice('Shared layout saved. Clients can review and adopt their permitted view. Personal settings and device access were not changed.') }
        }, true)}>Save shared layout</button></div>
      </div>}
      {draft && owner && step === 'recovery' && <div className="space-y-6">
        <div className="space-y-4"><h3 className="font-display text-xl font-semibold text-light">Your kept draft</h3><CompanionLayoutSummary layout={draft} /></div>
        <button type="button" className={layoutSecondary} disabled={busy || closing} onClick={() => void perform(async signal => {
          setComparison(null); const latest = await fetchOwnerLayout(signal); const sources = await fetchCompanionSourceCatalog(latest.homeId, signal)
          if (!signal.aborted) { setComparison({ owner: latest, catalog: sources }); setNotice('Latest setup read. Compare both versions before choosing. Nothing was saved or discarded.') }
        })}>Read latest shared setup</button>
        {comparison && <div className="space-y-4 border-t border-stone pt-5">
          <h3 className="font-display text-xl font-semibold text-light">Latest setup on Lucia</h3>
          {comparison.owner.snapshot ? <CompanionLayoutSummary layout={comparison.owner.snapshot.layout} /> : <p className="text-sm text-fog">No shared layout is currently configured.</p>}
          <div className="flex flex-wrap gap-3"><button type="button" className={layoutSecondary} disabled={busy || closing || comparison.owner.homeId !== draft.homeId || comparison.owner.sourceBaseUrl !== draft.sourceBaseUrl || !comparison.owner.sourceMatches}
            onClick={() => { setOwner(comparison.owner); setCatalog(comparison.catalog); setComparison(null); if (sharedLayoutProblem(draft, comparison.owner, comparison.catalog)) { setStep('edit'); setNotice('Draft kept. Review unavailable places or bindings before saving.'); requestAnimationFrame(() => statusRef.current?.focus()) } else review() }}>Review my draft against this version</button>
            <button type="button" className={layoutSecondary} disabled={busy || closing} onClick={() => { applyOwner(comparison.owner); setCatalog(comparison.catalog); setNotice('Draft discarded explicitly. The latest shared setup is loaded.'); requestAnimationFrame(() => statusRef.current?.focus()) }}>Discard my draft and use latest</button></div>
        </div>}
      </div>}
      {closing ? <div className="space-y-3 border-t border-stone pt-4" onKeyDown={event => { if (event.key === 'Escape' && !busy) { event.preventDefault(); event.stopPropagation(); keepEditing() } }}><p className="text-sm text-light">Discard this unsaved draft and close the editor?</p><div className="flex flex-wrap gap-3"><button type="button" ref={keepRef} className={layoutSecondary} onClick={keepEditing}>Keep editing</button><button type="button" className={`${layoutSecondary} text-rose`} onClick={close}>Discard draft and close</button></div></div>
        : <button type="button" ref={closeRef} className={layoutSecondary} disabled={busy || remove !== null} onClick={() => dirty ? setClosing(true) : close()}>Close layout editor</button>}
      <p className="max-w-prose text-xs text-fog">Drafts stay in memory during this signed-in dashboard session, including route navigation. Reload, sign-out or closing the dashboard loses them. Returning to the editor requires a fresh comparison before saving; no draft is written to browser storage.</p>
    </div>}
  </section>
}
