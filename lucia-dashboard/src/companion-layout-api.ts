import { mutateCompanionOwner, requestCompanionOwner } from './companion-api.ts'
import { validOwnerLayoutRevision, validateOwnerLayout, validateSharedLayout } from './companion-layout.ts'
import type { OwnerLayout, SharedLayout } from './companion-layout.ts'

async function document(response: Response): Promise<unknown> {
  const reader = response.body?.getReader()
  if (!reader || response.headers.get('Content-Type')?.split(';')[0].trim().toLowerCase() !== 'application/json')
    throw new Error('Shared setup could not be read safely. No saved setup was replaced.')
  let body = ''; let bytes = 0
  const decoder = new TextDecoder('utf-8', { fatal: true })
  try {
    while (true) {
      const chunk = await reader.read(); if (chunk.done) break
      bytes += chunk.value.byteLength
      if (bytes > 1_048_576) throw new Error('Shared setup is too large to inspect safely.')
      body += decoder.decode(chunk.value, { stream: true })
    }
    return JSON.parse(body + decoder.decode())
  } catch { throw new Error('Shared setup could not be read safely. No saved setup was replaced.') }
  finally { await reader.cancel().catch(() => {}); reader.releaseLock() }
}

export async function fetchOwnerLayout(signal?: AbortSignal): Promise<OwnerLayout> {
  const response = await requestCompanionOwner('/layout', {}, signal, [404])
  return validateOwnerLayout(await document(response), response.status === 404)
}

export async function saveOwnerLayout(draft: SharedLayout, expectedRevision: string | null, signal?: AbortSignal): Promise<OwnerLayout> {
  const layout = validateSharedLayout(draft)
  if (expectedRevision !== null && !validOwnerLayoutRevision(expectedRevision)) throw new Error('Read the saved setup before choosing a save baseline.')
  const response = await mutateCompanionOwner('/layout', { expectedRevision, layout }, 'PUT', signal, [409])
  if (response.status === 409) throw new Error('The shared layout changed on Lucia. Your draft is kept. Read the latest shared setup and compare before choosing a save baseline.')
  const result = validateOwnerLayout(await document(response), false)
  if (!result.sourceMatches || result.homeId !== layout.homeId || result.sourceBaseUrl !== layout.sourceBaseUrl
    || JSON.stringify(result.snapshot?.layout) !== JSON.stringify(layout)) throw new Error('The saved setup response could not be verified.')
  return result
}
