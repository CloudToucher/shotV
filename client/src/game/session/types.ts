import type { ItemCategory, ResourceBundle, WeaponType } from '../data/types'
import { createInitialRunInventoryState, type GridInventoryState, type InventoryItemRecord } from '../inventory/types'
import type { WorldZoneKind } from '../world/types'

export type RunResolutionOutcome = 'extracted' | 'boss-clear' | 'down'

export type RunStateStatus = 'active' | 'awaiting-settlement'

export type LootCategory = ItemCategory

export interface LootEntry {
  id: string
  definitionId: string
  label: string
  category: LootCategory
  quantity: number
  source: 'enemy' | 'boss' | 'wave' | 'manual'
  acquiredAtWave: number
  acquiredAtSeconds: number
}

export interface PlayerRunState {
  health: number
  maxHealth: number
  currentWeaponId: WeaponType
  loadoutWeaponIds: WeaponType[]
  shotsFired: number
  grenadesThrown: number
  dashesUsed: number
  damageTaken: number
}

export interface GroundLootDrop {
  id: string
  item: InventoryItemRecord
  x: number
  y: number
  source: LootEntry['source']
}

export interface RunBossState {
  spawned: boolean
  defeated: boolean
  label: string | null
  phase: 1 | 2 | null
  health: number | null
  maxHealth: number | null
}

export type RunZoneStatus = 'locked' | 'active' | 'cleared'

export interface RunZoneState {
  id: string
  label: string
  kind: WorldZoneKind
  status: RunZoneStatus
  threatLevel: number
  rewardMultiplier: number
  allowsExtraction: boolean
  description: string
}

export interface RunMapState {
  sceneId: 'combat-sandbox'
  routeId: string
  currentZoneId: string
  layoutSeed: number
  zones: RunZoneState[]
  currentWave: number
  highestWave: number
  hostilesRemaining: number
  boss: RunBossState
}

export type RunResourceLedger = ResourceBundle

export interface RunStats {
  elapsedSeconds: number
  kills: number
  highestWave: number
  extracted: boolean
  bossDefeated: boolean
}

export interface RunState {
  id: string
  sceneId: 'combat-sandbox'
  enteredAt: string
  status: RunStateStatus
  pendingOutcome: RunResolutionOutcome | null
  player: PlayerRunState
  map: RunMapState
  inventory: GridInventoryState
  groundLoot: GroundLootDrop[]
  resources: RunResourceLedger
  lootEntries: LootEntry[]
  stats: RunStats
}

export interface ExtractionResult {
  runId: string
  sceneId: 'combat-sandbox'
  outcome: RunResolutionOutcome
  success: boolean
  resolvedAt: string
  durationSeconds: number
  kills: number
  highestWave: number
  bossDefeated: boolean
  resourcesRecovered: RunResourceLedger
  resourcesLost: RunResourceLedger
  lootRecovered: LootEntry[]
  lootLost: LootEntry[]
  summaryLabel: string
}

export interface SessionState {
  activeRun: RunState | null
  lastExtraction: ExtractionResult | null
}

export function createInitialPlayerRunState(): PlayerRunState {
  return {
    health: 100,
    maxHealth: 100,
    currentWeaponId: 'machineGun',
    loadoutWeaponIds: ['machineGun', 'grenade', 'sniper'],
    shotsFired: 0,
    grenadesThrown: 0,
    dashesUsed: 0,
    damageTaken: 0,
  }
}

export function createInitialRunMapState(): RunMapState {
  return {
    sceneId: 'combat-sandbox',
    routeId: 'combat-sandbox-route',
    currentZoneId: 'perimeter-dock',
    layoutSeed: 1,
    zones: [],
    currentWave: 0,
    highestWave: 0,
    hostilesRemaining: 0,
    boss: {
      spawned: false,
      defeated: false,
      label: null,
      phase: null,
      health: null,
      maxHealth: null,
    },
  }
}

export function createInitialRunResourceLedger(): RunResourceLedger {
  return {
    salvage: 0,
    alloy: 0,
    research: 0,
  }
}

export function createInitialRunState(
  runId: string,
  enteredAt: string,
  loadoutWeaponIds: WeaponType[] = ['machineGun', 'grenade', 'sniper'],
  mapState: RunMapState = createInitialRunMapState(),
): RunState {
  return {
    id: runId,
    sceneId: 'combat-sandbox',
    enteredAt,
    status: 'active',
    pendingOutcome: null,
    player: {
      ...createInitialPlayerRunState(),
      currentWeaponId: loadoutWeaponIds[0] ?? 'machineGun',
      loadoutWeaponIds,
    },
    map: mapState,
    inventory: createInitialRunInventoryState(),
    groundLoot: [],
    resources: createInitialRunResourceLedger(),
    lootEntries: [],
    stats: {
      elapsedSeconds: 0,
      kills: 0,
      highestWave: 0,
      extracted: false,
      bossDefeated: false,
    },
  }
}

export function createInitialSessionState(): SessionState {
  return {
    activeRun: null,
    lastExtraction: null,
  }
}
