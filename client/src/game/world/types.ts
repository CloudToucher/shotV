export type WorldZoneKind = 'perimeter' | 'high-risk' | 'high-value' | 'extraction'

export interface WorldRouteZoneDefinition {
  id: string
  label: string
  kind: WorldZoneKind
  description: string
  threatLevel: number
  rewardMultiplier: number
  allowsExtraction: boolean
}

export interface WorldRouteDefinition {
  id: string
  label: string
  summary: string
  zones: WorldRouteZoneDefinition[]
}

export interface WorldState {
  selectedRouteId: string
  selectedZoneId: string
  discoveredZones: string[]
  activeRouteId: string | null
}

export function createInitialWorldState(): WorldState {
  return {
    selectedRouteId: 'combat-sandbox-route',
    selectedZoneId: 'perimeter-dock',
    discoveredZones: ['perimeter-dock'],
    activeRouteId: null,
  }
}
