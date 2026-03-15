import type { GameMode } from './gameModes'
import type { InventoryState } from '../game/inventory/types'
import type { BaseState } from '../game/meta/types'
import type { SessionState } from '../game/session/types'
import type { WorldState } from '../game/world/types'
import type { WorldMarker } from '../game/core/contracts'

export interface GameSettingsState {
  developerMode: boolean
}

export interface SaveState {
  version: number
  createdAt: string
  updatedAt: string
  base: BaseState
  inventory: InventoryState
  world: WorldState
  session: SessionState
  settings: GameSettingsState
}

export interface SceneRuntimeState {
  primaryActionReady: boolean
  primaryActionHint: string
  nearbyMarkerId: string | null
  nearbyMarkerLabel: string | null
  nearbyMarkerKind: WorldMarker['kind'] | null
  mapOverlayOpen: boolean
}

export interface GameState {
  mode: GameMode
  save: SaveState
  hydrated: boolean
  runtime: SceneRuntimeState
}

export function createInitialSceneRuntimeState(): SceneRuntimeState {
  return {
    primaryActionReady: false,
    primaryActionHint: '',
    nearbyMarkerId: null,
    nearbyMarkerLabel: null,
    nearbyMarkerKind: null,
    mapOverlayOpen: false,
  }
}
