import { createContext, useContext } from 'react'
import type { RefObject } from 'react'
import type { OwnerLayout, SharedLayout } from '../companion-layout'

export interface KeptLayoutDraft { owner: OwnerLayout; draft: SharedLayout }
export const CompanionLayoutDraftContext = createContext<RefObject<KeptLayoutDraft | null> | null>(null)
export function useCompanionLayoutDraft() {
  const context = useContext(CompanionLayoutDraftContext)
  if (!context) throw new Error('Shared layout drafts require the authenticated dashboard scope.')
  return context
}
