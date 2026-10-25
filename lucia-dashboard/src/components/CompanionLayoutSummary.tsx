import { BedDouble, CookingPot, DoorOpen, House, Sofa, Trees } from 'lucide-react'
import type { SharedLayout } from '../companion-layout'

const icons = { Home: House, Sofa, Garden: Trees, Kitchen: CookingPot, Door: DoorOpen, Bed: BedDouble }

export default function CompanionLayoutSummary({ layout }: { layout: SharedLayout }) {
  return <div className="space-y-4">
    <h4 className="break-words font-display text-lg font-semibold text-light">{layout.name}</h4>
    <p className="text-sm text-fog">{layout.places.length} places · {layout.connections.length} physical connections · {layout.bindings.length} source bindings</p>
    {layout.places.length === 0 && <p className="text-sm text-fog">No shared places. This does not revoke device access or delete personal setup.</p>}
    <ul className="divide-y divide-stone border-y border-stone">
      {layout.places.map(place => {
        const Icon = icons[place.icon]
        const neighbors = layout.connections.filter(edge => edge.fromPlaceId === place.id || edge.toPlaceId === place.id)
          .map(edge => layout.places.find(other => other.id === (edge.fromPlaceId === place.id ? edge.toPlaceId : edge.fromPlaceId))!.name)
        return <li key={place.id} className="flex min-w-0 gap-3 py-4">
          <Icon className="mt-0.5 h-6 w-6 shrink-0 text-fog" aria-hidden="true" />
          <div className="min-w-0 space-y-2 break-words text-sm">
            <p className="font-semibold text-light">{place.name}</p>
            <p className="text-fog">Icon: {place.icon}. Connected to: {neighbors.length ? neighbors.join(', ') : 'No configured neighbors'}.</p>
            <details><summary className="min-h-12 cursor-pointer py-3 text-fog focus-visible:outline-2 focus-visible:outline-amber">Sources for {place.name}</summary>
              <p className="text-fog">Source place: {place.sourcePlaceId}</p>
              <ul className="space-y-2 pb-2 text-cloud">{layout.bindings.filter(binding => binding.placeId === place.id).map(binding =>
                <li key={`${binding.sourceId}:${binding.field}`}>{binding.sourceId} · {binding.field}</li>)}</ul>
              {!layout.bindings.some(binding => binding.placeId === place.id) && <p className="text-fog">No source bindings.</p>}
            </details>
          </div>
        </li>
      })}
    </ul>
  </div>
}
