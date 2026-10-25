import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { ShieldCheck, Smartphone } from 'lucide-react'
import Field from '../components/Field'
import CompanionGrantEditor from '../components/CompanionGrantEditor'
import CompanionLayoutEditor from '../components/CompanionLayoutEditor'
import { approveCompanionDevice, companionApprovalFits, declineCompanionDevice, fetchCompanionDevices, normalizeCompanionCode, reviewCompanionDevice, revokeCompanionDevice } from '../companion-api'
import type { CompanionDevice, CompanionGrant, CompanionReview } from '../companion-api'

type Confirmation = { kind: 'decline'; review: CompanionReview } | { kind: 'revoke'; device: CompanionDevice }
const secondaryStyle = 'min-h-12 rounded-xl border border-stone bg-basalt px-4 py-2 text-sm text-fog hover:border-amber hover:text-light focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-amber disabled:opacity-50'
const dangerStyle = `${secondaryStyle} text-rose`
const message = (error: unknown) => error instanceof Error ? error.message : 'Lucia could not confirm the operation. Refresh before trying again.'

export default function CompanionDevicesPage() {
  const [code, setCode] = useState('')
  const [review, setReview] = useState<CompanionReview | null>(null)
  const [reviewedCode, setReviewedCode] = useState('')
  const [devices, setDevices] = useState<CompanionDevice[] | null>(null)
  const [listBusy, setListBusy] = useState(true)
  const [listError, setListError] = useState('')
  const [checkedAt, setCheckedAt] = useState<Date | null>(null)
  const [busy, setBusy] = useState(false)
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')
  const [confirmation, setConfirmation] = useState<Confirmation | null>(null)
  const operation = useRef(false)
  const lifetime = useRef<AbortController | null>(null)
  const noticeRef = useRef<HTMLParagraphElement>(null)
  const reviewRef = useRef<HTMLHeadingElement>(null)
  const keepRef = useRef<HTMLButtonElement>(null)
  const invoker = useRef<HTMLButtonElement | null>(null)
  const focusNotice = () => requestAnimationFrame(() => noticeRef.current?.focus())

  const readDevices = useCallback(async (signal: AbortSignal) => {
    setListBusy(true); setDevices(null); setListError('')
    try {
      const result = await fetchCompanionDevices(signal)
      if (!signal.aborted) { setDevices(result); setCheckedAt(new Date()) }
    } catch (error) { if (!signal.aborted) setListError(message(error)) }
    finally { if (!signal.aborted) setListBusy(false) }
  }, [])
  useEffect(() => {
    const controller = new AbortController()
    lifetime.current = controller
    void readDevices(controller.signal)
    return () => controller.abort()
  }, [readDevices])
  useEffect(() => {
    if (!review) return
    const expiry = Date.parse(review.expiresAt) - Date.now()
    const timer = window.setTimeout(() => {
      setReview(null); setConfirmation(null); setError('This approval code expired. Ask the person to request a new one.'); focusNotice()
    }, Math.max(0, expiry))
    return () => window.clearTimeout(timer)
  }, [review])
  useLayoutEffect(() => { if (confirmation) keepRef.current?.focus() }, [confirmation])

  async function perform(action: (signal: AbortSignal) => Promise<void>, focusReview = false) {
    if (operation.current || !lifetime.current) return
    operation.current = true; setBusy(true); setError(''); setNotice('Checking with Lucia...')
    const signal = lifetime.current.signal
    try { await action(signal) }
    catch (error) { if (!signal.aborted) { setError(message(error)); setNotice('') } }
    finally {
      operation.current = false
      if (!signal.aborted) {
        setBusy(false)
        requestAnimationFrame(() => (focusReview && reviewRef.current ? reviewRef.current : noticeRef.current)?.focus())
      }
    }
  }
  function closeConfirmation() {
    setConfirmation(null)
    invoker.current?.focus()
  }
  async function approve(grant: CompanionGrant) {
    const current = review
    if (!current || current.status !== 'Pending' || Date.parse(current.expiresAt) <= Date.now()) return
    if (!companionApprovalFits(grant)) {
      setError('This access selection is too large. Deselect some places or observations; your choices are kept.'); setNotice(''); focusNotice(); return
    }
    await perform(async signal => {
      // A lost response is ambiguous. Clear the editor before sending, never offer a blind replay.
      setReview(null); setConfirmation(null)
      await approveCompanionDevice(current.id, grant, signal)
      setNotice('Approved. The intended device must complete its check. Refresh devices to see sessions actually issued by Lucia.')
    })
  }
  async function confirmRemoval() {
    const current = confirmation
    if (!current) return
    await perform(async signal => {
      setConfirmation(null)
      if (current.kind === 'decline') {
        setReview(null)
        await declineCompanionDevice(current.review.id, signal)
        setNotice('Declined. This request can no longer redeem a device session.')
      } else {
        await revokeCompanionDevice(current.device.id, signal)
        setDevices(items => items?.filter(item => item.id !== current.device.id) ?? null)
        setNotice(`Revoked access for ${current.device.deviceName}. This does not remove local setup metadata from that device.`)
      }
    })
  }
  function removalQuestion() {
    if (!confirmation) return null
    return <div className="mt-4 space-y-3 border-t border-stone pt-4" onKeyDown={event => {
      if (event.key === 'Escape' && !busy) { event.preventDefault(); closeConfirmation() }
    }}>
      <p className="text-sm text-light">{confirmation.kind === 'decline' ? `Decline access for ${confirmation.review.deviceName}?` : `Revoke access for ${confirmation.device.deviceName}?`}</p>
      <p className="max-w-prose text-sm text-fog">{confirmation.kind === 'decline' ? 'A request already redeemed cannot be declined. Refresh connected devices and revoke its session instead.' : 'The host will reject this session on its next request, including renewal. Offline screens cannot be remotely erased.'}</p>
      <div className="flex flex-wrap gap-3">
        <button type="button" ref={keepRef} disabled={busy} onClick={closeConfirmation} className={secondaryStyle}>{confirmation.kind === 'decline' ? 'Keep request' : 'Keep device access'}</button>
        <button type="button" disabled={busy} onClick={() => void confirmRemoval()} className={dangerStyle}>{confirmation.kind === 'decline' ? 'Confirm decline' : 'Confirm revocation'}</button>
      </div>
    </div>
  }

  return <div className="mx-auto max-w-5xl space-y-8">
    <header className="space-y-3">
      <div className="flex items-center gap-3"><ShieldCheck className="h-6 w-6 shrink-0 text-amber" aria-hidden="true" /><h1 className="font-display text-2xl font-semibold text-light">Companion devices</h1></div>
      <p className="max-w-prose text-sm text-fog">Approve read access for an intended phone or tablet. Keep your owner key in this dashboard, never in the companion app. Use the trusted HTTPS dashboard for these operations.</p>
    </header>
    <p ref={noticeRef} tabIndex={-1} role={error ? 'alert' : 'status'} className={`max-w-prose break-words text-sm outline-offset-4 ${error ? 'text-rose' : 'text-fog'}`}>{error || notice}</p>
    <section aria-labelledby="approve-heading" className="rounded-xl border border-stone bg-charcoal p-5 sm:p-6">
      <h2 id="approve-heading" className="font-display text-xl font-semibold text-light">Approve a device</h2>
      <form className="mt-4 flex flex-wrap items-end gap-3" onSubmit={event => {
        event.preventDefault()
        setReview(null); setConfirmation(null)
        void perform(async signal => {
          const normalized = normalizeCompanionCode(code)
          if (!normalized) throw new Error('Enter the ten-character approval code shown on the intended device.')
          const result = await reviewCompanionDevice(normalized, signal)
          if (Date.parse(result.expiresAt) <= Date.now()) throw new Error('This approval code expired. Ask for a new code.')
          setReview(result); setReviewedCode(normalized); setNotice('Check the code, device and exact access before deciding.')
        }, true)
      }}>
        <Field label="Approval code" hint="Read it from the intended device, not an unsolicited message." className="min-w-0 flex-1 basis-64">
          {control => <input {...control} value={code} maxLength={20} disabled={busy} onChange={event => { setCode(event.target.value); setReview(null); setConfirmation(null); setError(''); setNotice('') }} className="min-h-12 w-full min-w-0 rounded-xl border border-stone bg-basalt px-3 py-2 text-sm text-light input-focus" autoComplete="off" autoCapitalize="characters" spellCheck={false} />}
        </Field>
        <button type="submit" disabled={busy || !normalizeCompanionCode(code)} className={secondaryStyle}>{busy ? 'Checking...' : 'Review device'}</button>
      </form>
      {review && <div className="mt-6 border-t border-stone pt-5">
        <div className="space-y-3">
          <div className="flex items-start gap-3"><Smartphone className="mt-1 h-6 w-6 shrink-0 text-amber" aria-hidden="true" /><h3 ref={reviewRef} tabIndex={-1} className="min-w-0 break-words font-display text-lg font-semibold text-light outline-offset-4">{review.deviceName}</h3></div>
          <p className="font-display text-2xl font-semibold text-amber" aria-label="Code to match"><span className="inline-block">{reviewedCode.slice(0, 6)}</span><wbr /><span className="inline-block">{reviewedCode.slice(6)}</span></p>
          <p className="break-words text-sm text-fog">Home ID: {review.homeId}<br />Host: {window.location.origin}<br />Expires: {new Date(review.expiresAt).toLocaleString()}</p>
          <p className="max-w-prose text-sm text-fog">Device names are untrusted labels, not proof of who is holding the device. Match the code with the intended person.</p>
        </div>
        {review.status === 'Pending' ? <CompanionGrantEditor key={review.id} review={review} busy={busy} onApprove={grant => void approve(grant)} /> : <p className="mt-5 text-sm text-fog">Already approved. Its grant cannot be changed here. Let the device finish, or decline before redemption. To change an issued grant, revoke and pair again.</p>}
        <button type="button" disabled={busy} className={`${dangerStyle} mt-5`} onClick={event => { invoker.current = event.currentTarget; setConfirmation({ kind: 'decline', review }) }}>Decline this request</button>
        {confirmation?.kind === 'decline' && removalQuestion()}
      </div>}
    </section>
    <CompanionLayoutEditor />
    <section aria-labelledby="devices-heading" className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3"><h2 id="devices-heading" className="font-display text-xl font-semibold text-light">Connected devices</h2><button type="button" disabled={busy || listBusy} onClick={() => void readDevices(lifetime.current!.signal)} className={secondaryStyle}>Refresh devices</button></div>
      <p className="max-w-prose text-sm text-fog">Issued, unexpired sessions, not proof that a device is online. Revocation requires a response from Lucia.{checkedAt && ` Last checked ${checkedAt.toLocaleTimeString()}.`}</p>
      {listBusy && <p className="text-sm text-fog" aria-live="polite">Reading device sessions...</p>}
      {listError && <p role="alert" className="max-w-prose text-sm text-rose">{listError}</p>}
      {devices?.length === 0 && <p className="text-sm text-fog">No active companion devices.</p>}
      {devices && <ul className="divide-y divide-stone border-y border-stone">
        {devices.map(device => <li key={device.id} className="space-y-3 py-5">
          <div className="flex flex-wrap items-start justify-between gap-3"><div className="min-w-0"><h3 className="break-words font-display text-lg font-semibold text-light">{device.deviceName}</h3><p className="break-words text-sm text-fog">Member: {device.grant.memberId}<br />Home: {device.grant.homeId}<br />Expires: {new Date(device.expiresAt).toLocaleString()}</p></div><button type="button" disabled={busy} aria-label={`Revoke ${device.deviceName}`} className={dangerStyle} onClick={event => { invoker.current = event.currentTarget; setConfirmation({ kind: 'revoke', device }) }}>Revoke access</button></div>
          <details className="text-sm"><summary className="min-h-12 cursor-pointer py-3 text-fog focus-visible:outline-2 focus-visible:outline-amber">Exact approved access</summary><ul className="space-y-2 break-words pb-3 text-cloud">{device.grant.places.length === 0 && <li>No place observations</li>}{device.grant.places.map(place => <li key={place.placeId}>{place.placeId}: {place.fields.length ? place.fields.join(', ') : 'No observations'}</li>)}<li>Activity: {device.grant.canReadActivity ? 'Allowed' : 'Not allowed'}</li><li>History: {device.grant.canReadHistory ? 'Allowed' : 'Not allowed'}</li><li>Engine summaries: {device.grant.canReadEngine ? 'Allowed' : 'Not allowed'}</li><li>Session ID: {device.id}</li></ul></details>
          {confirmation?.kind === 'revoke' && confirmation.device.id === device.id && removalQuestion()}
        </li>)}
      </ul>}
    </section>
  </div>
}
