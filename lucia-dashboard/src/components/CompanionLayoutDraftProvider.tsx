import { useEffect, useRef } from 'react'
import type { ReactNode } from 'react'
import { CompanionLayoutDraftContext } from './companion-layout-draft-context'
import type { KeptLayoutDraft } from './companion-layout-draft-context'

/** One in-memory draft for this authenticated shell. No storage, credentials or permission cache. */
export default function CompanionLayoutDraftProvider({ children }: { children: ReactNode }) {
  const draft = useRef<KeptLayoutDraft | null>(null)
  useEffect(() => {
    const prevent = (event: BeforeUnloadEvent) => { if (draft.current) { event.preventDefault(); event.returnValue = '' } }
    window.addEventListener('beforeunload', prevent)
    return () => window.removeEventListener('beforeunload', prevent)
  }, [])
  return <CompanionLayoutDraftContext.Provider value={draft}>{children}</CompanionLayoutDraftContext.Provider>
}
