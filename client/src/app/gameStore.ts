import type { GameMode } from './gameModes'
import { LocalSaveRepository } from './saveRepository'
import { createInitialSaveState } from './saveState'
import { createInitialSceneRuntimeState, type GameState, type SaveState, type SceneRuntimeState } from './types'
import type { WeaponType } from '../game/data/types'
import { weaponLoadout } from '../game/data/weapons'
import { buildResourceLedgerFromItems, placeItemsInGrid, sanitizeQuickSlotBindings } from '../game/inventory/grid'
import type { InventoryItemRecord } from '../game/inventory/types'
import {
  createInitialRunResourceLedger,
  createInitialRunState,
  type LootEntry,
  type RunResolutionOutcome,
  type RunState,
} from '../game/session/types'
import {
  advanceRunMapZone,
  canExtractFromRunMap,
  createRunMapStateForRoute,
  getCurrentRunZone,
  getNextWorldRouteId,
  isRunRouteComplete,
  markCurrentRunZoneCleared,
} from '../game/world/routes'
import type { WorldState } from '../game/world/types'

type StoreListener = (state: GameState, previousState: GameState) => void

export interface ActiveRunSnapshotInput {
  player: RunState['player']
  map: RunState['map']
  inventory: RunState['inventory']
  groundLoot: RunState['groundLoot']
  resources: RunState['resources']
  lootEntries: LootEntry[]
  stats: RunState['stats']
}

export class GameStore {
  private state: GameState
  private readonly listeners = new Set<StoreListener>()
  private readonly repository: LocalSaveRepository

  constructor(repository: LocalSaveRepository = new LocalSaveRepository()) {
    this.repository = repository
    const save = repository.load()

    this.state = {
      mode: save.session.activeRun ? 'combat' : 'base',
      save,
      hydrated: true,
      runtime: createInitialSceneRuntimeState(),
    }
  }

  getState(): GameState {
    return this.state
  }

  subscribe(listener: StoreListener): () => void {
    this.listeners.add(listener)

    return () => {
      this.listeners.delete(listener)
    }
  }

  selectNextWorldRoute(): void {
    this.selectWorldRoute(getNextWorldRouteId(this.state.save.world.selectedRouteId))
  }

  selectWorldRoute(routeId: string): void {
    this.commit(this.state.mode, (save) => {
      if (save.session.activeRun) {
        return save
      }

      const nextMap = createRunMapStateForRoute(routeId)

      return {
        ...save,
        updatedAt: new Date().toISOString(),
        world: {
          ...save.world,
          selectedRouteId: nextMap.routeId,
          selectedZoneId: nextMap.currentZoneId,
          discoveredZones: dedupeStrings([...save.world.discoveredZones, nextMap.currentZoneId]),
        },
      }
    })
  }

  deployCombat(): void {
    if (this.state.save.session.activeRun || !this.state.runtime.primaryActionReady) {
      return
    }

    const timestamp = new Date().toISOString()
    const nextRunId = buildRunId()
    const loadoutWeaponIds = buildRunLoadout(this.state.save)
    const mapState = createRunMapStateForRoute(this.state.save.world.selectedRouteId)
    const nextRun = createInitialRunState(nextRunId, timestamp, loadoutWeaponIds, mapState)

    this.commit('combat', (save) => ({
      ...save,
      updatedAt: timestamp,
      base: {
        ...save.base,
        deploymentCount: save.base.deploymentCount + 1,
      },
      session: {
        ...save.session,
        activeRun: nextRun,
      },
      world: mergeWorldStateWithRunMap(
        {
          ...save.world,
          activeRouteId: mapState.routeId,
        },
        mapState,
      ),
    }))
  }

  syncActiveRun(snapshot: ActiveRunSnapshotInput): void {
    this.commit('combat', (save) => {
      const activeRun = save.session.activeRun

      if (!activeRun) {
        return save
      }

      const mergedRun = mergeRunSnapshot(activeRun, snapshot)

      return {
        ...save,
        updatedAt: new Date().toISOString(),
        session: {
          ...save.session,
          activeRun: mergedRun,
        },
        world: mergeWorldStateWithRunMap(save.world, mergedRun.map),
      }
    })
  }

  markCurrentZoneCleared(snapshot?: ActiveRunSnapshotInput): void {
    this.commit('combat', (save) => {
      const activeRun = save.session.activeRun

      if (!activeRun || activeRun.status !== 'active') {
        return save
      }

      const mergedRun = mergeRunSnapshot(activeRun, snapshot)
      const clearedMap = markCurrentRunZoneCleared(mergedRun.map)
      const routeComplete = isRunRouteComplete(clearedMap)
      const highestWave = Math.max(mergedRun.stats.highestWave, clearedMap.highestWave, clearedMap.currentWave)

      return {
        ...save,
        updatedAt: new Date().toISOString(),
        session: {
          ...save.session,
          activeRun: {
            ...mergedRun,
            status: 'active',
            pendingOutcome: null,
            map: clearedMap,
            stats: {
              ...mergedRun.stats,
              highestWave,
              bossDefeated: routeComplete ? true : mergedRun.stats.bossDefeated,
            },
          },
        },
        world: mergeWorldStateWithRunMap(save.world, clearedMap),
      }
    })
  }

  advanceActiveRunZone(): void {
    this.commit('combat', (save) => {
      const activeRun = save.session.activeRun

      if (!activeRun || activeRun.status !== 'active' || !this.state.runtime.primaryActionReady) {
        return save
      }

      const nextMap = advanceRunMapZone(activeRun.map)

      if (!nextMap) {
        return save
      }

      return {
        ...save,
        updatedAt: new Date().toISOString(),
        session: {
          ...save.session,
          activeRun: {
            ...activeRun,
            pendingOutcome: null,
            map: nextMap,
          },
        },
        world: mergeWorldStateWithRunMap(save.world, nextMap),
      }
    })
  }

  markRunOutcome(outcome: Extract<RunResolutionOutcome, 'boss-clear' | 'down'>, snapshot?: ActiveRunSnapshotInput): void {
    this.commit('combat', (save) => {
      const activeRun = save.session.activeRun

      if (!activeRun) {
        return save
      }

      const mergedRun = mergeRunSnapshot(activeRun, snapshot)
      const highestWave = Math.max(mergedRun.stats.highestWave, mergedRun.map.highestWave, mergedRun.map.currentWave)

      return {
        ...save,
        updatedAt: new Date().toISOString(),
        session: {
          ...save.session,
          activeRun: {
            ...mergedRun,
            status: 'awaiting-settlement',
            pendingOutcome: outcome,
            player: {
              ...mergedRun.player,
              health: outcome === 'down' ? 0 : mergedRun.player.health,
            },
            map: {
              ...mergedRun.map,
              hostilesRemaining: outcome === 'down' ? mergedRun.map.hostilesRemaining : 0,
              boss: {
                ...mergedRun.map.boss,
                defeated: outcome === 'boss-clear' ? true : mergedRun.map.boss.defeated,
                health: outcome === 'boss-clear' ? 0 : mergedRun.map.boss.health,
              },
            },
            stats: {
              ...mergedRun.stats,
              highestWave,
              bossDefeated: outcome === 'boss-clear' ? true : mergedRun.stats.bossDefeated,
            },
          },
        },
        world: mergeWorldStateWithRunMap(save.world, mergedRun.map),
      }
    })
  }

  resolveActiveRunToBase(outcome: RunResolutionOutcome = 'extracted'): void {
    this.commit('base', (save) => {
      const activeRun = save.session.activeRun

      if (!activeRun) {
        return save
      }

      if (activeRun.status === 'active' && (!canExtractFromRunMap(activeRun.map) || !this.state.runtime.primaryActionReady)) {
        return save
      }

      const timestamp = new Date().toISOString()
      const resolvedOutcome =
        activeRun.status === 'awaiting-settlement'
          ? (activeRun.pendingOutcome ?? outcome)
          : isRunRouteComplete(activeRun.map)
            ? 'boss-clear'
            : outcome
      const normalizedRun = finalizeRunForOutcome(activeRun, resolvedOutcome)
      const settlement = settleRunInventoryInBase(save.inventory, normalizedRun, resolvedOutcome)
      const extraction = buildExtractionResult(normalizedRun, resolvedOutcome, timestamp, settlement)

      return {
        ...save,
        updatedAt: timestamp,
        base: {
          ...save.base,
          resources: {
            salvage: save.base.resources.salvage + settlement.resourcesRecovered.salvage,
            alloy: save.base.resources.alloy + settlement.resourcesRecovered.alloy,
            research: save.base.resources.research + settlement.resourcesRecovered.research,
          },
        },
        inventory: settlement.inventory,
        session: {
          ...save.session,
          activeRun: null,
          lastExtraction: extraction,
        },
        world: {
          ...mergeWorldStateWithRunMap(save.world, normalizedRun.map),
          activeRouteId: null,
        },
      }
    })
  }

  resetSave(): void {
    const save = createInitialSaveState()

    this.repository.clear()
    this.state = {
      mode: 'base',
      save,
      hydrated: true,
      runtime: createInitialSceneRuntimeState(),
    }
    this.repository.save(save)
    this.emit(this.state, this.state)
  }

  updateSceneRuntime(patch: Partial<SceneRuntimeState>): void {
    const previousState = this.state
    const nextRuntime = {
      ...previousState.runtime,
      ...patch,
    }

    if (isSameRuntimeState(previousState.runtime, nextRuntime)) {
      return
    }

    this.state = {
      ...previousState,
      runtime: nextRuntime,
    }

    this.emit(this.state, previousState)
  }

  clearSceneRuntime(): void {
    this.updateSceneRuntime(createInitialSceneRuntimeState())
  }

  updateStashItems(items: InventoryItemRecord[]): void {
    this.commit(this.state.mode, (save) => ({
      ...save,
      updatedAt: new Date().toISOString(),
      inventory: {
        ...save.inventory,
        storedItems: items.map((item) => ({ ...item })),
      },
    }))
  }

  updateEquippedWeapons(weaponIds: WeaponType[]): void {
    this.commit(this.state.mode, (save) => ({
      ...save,
      updatedAt: new Date().toISOString(),
      inventory: {
        ...save.inventory,
        equippedWeaponIds: sanitizeEquippedWeaponIds(weaponIds),
      },
    }))
  }

  setDeveloperMode(enabled: boolean): void {
    this.commit(this.state.mode, (save) => ({
      ...save,
      updatedAt: new Date().toISOString(),
      settings: {
        ...save.settings,
        developerMode: enabled,
      },
    }))
  }

  private commit(mode: GameMode, mutate: (save: SaveState) => SaveState): void {
    const previousState = this.state
    const nextSave = mutate(previousState.save)

    if (nextSave === previousState.save) {
      return
    }

    this.state = {
      mode,
      save: nextSave,
      hydrated: true,
      runtime: mode === previousState.mode ? previousState.runtime : createInitialSceneRuntimeState(),
    }

    this.repository.save(nextSave)
    this.emit(this.state, previousState)
  }

  private emit(state: GameState, previousState: GameState): void {
    for (const listener of this.listeners) {
      listener(state, previousState)
    }
  }
}

export const gameStore = new GameStore()

function mergeRunSnapshot(run: RunState, snapshot?: ActiveRunSnapshotInput): RunState {
  if (!snapshot) {
    return run
  }

  return {
    ...run,
    player: {
      ...snapshot.player,
      loadoutWeaponIds: snapshot.player.loadoutWeaponIds.length > 0 ? snapshot.player.loadoutWeaponIds : run.player.loadoutWeaponIds,
      currentWeaponId: snapshot.player.currentWeaponId ?? run.player.currentWeaponId,
    },
    map: {
      ...run.map,
      ...snapshot.map,
      routeId: snapshot.map.routeId || run.map.routeId,
      currentZoneId: snapshot.map.currentZoneId || run.map.currentZoneId,
      zones: snapshot.map.zones.length > 0 ? snapshot.map.zones.map((zone) => ({ ...zone })) : run.map.zones,
      highestWave: Math.max(snapshot.map.highestWave, snapshot.map.currentWave, run.map.highestWave),
      boss: {
        ...snapshot.map.boss,
      },
    },
    inventory: {
      columns: snapshot.inventory.columns,
      rows: snapshot.inventory.rows,
      items: snapshot.inventory.items.map((item) => ({ ...item })),
      quickSlots: sanitizeQuickSlotBindings(snapshot.inventory.quickSlots, snapshot.inventory.items.map((item) => item.id)),
    },
    groundLoot: snapshot.groundLoot.map((drop) => ({
      ...drop,
      item: { ...drop.item },
    })),
    resources: buildResourceLedgerFromItems(snapshot.inventory.items),
    lootEntries: dedupeLootEntries(snapshot.lootEntries),
    stats: {
      ...snapshot.stats,
      highestWave: Math.max(snapshot.stats.highestWave, snapshot.map.highestWave, snapshot.map.currentWave, run.stats.highestWave),
      bossDefeated: snapshot.stats.bossDefeated || snapshot.map.boss.defeated,
    },
  }
}

function finalizeRunForOutcome(run: RunState, outcome: RunResolutionOutcome): RunState {
  const highestWave = Math.max(run.stats.highestWave, run.map.highestWave, run.map.currentWave)

  return {
    ...run,
    status: 'awaiting-settlement',
    pendingOutcome: outcome,
    player: {
      ...run.player,
      health: outcome === 'down' ? 0 : run.player.health,
    },
    map: {
      ...run.map,
      highestWave,
      boss: {
        ...run.map.boss,
        defeated: outcome === 'boss-clear' ? true : run.map.boss.defeated,
        health: outcome === 'boss-clear' ? 0 : run.map.boss.health,
      },
    },
    stats: {
      ...run.stats,
      highestWave,
      extracted: outcome === 'extracted',
      bossDefeated: outcome === 'boss-clear' ? true : run.stats.bossDefeated,
    },
  }
}

function buildExtractionResult(
  run: RunState,
  outcome: RunResolutionOutcome,
  resolvedAt: string,
  settlement: RunInventorySettlement,
) {
  const currentZone = getCurrentRunZone(run.map)

  return {
    runId: run.id,
    sceneId: run.sceneId,
    outcome,
    success: outcome !== 'down',
    resolvedAt,
    durationSeconds: Math.round(run.stats.elapsedSeconds),
    kills: run.stats.kills,
    highestWave: Math.max(run.stats.highestWave, run.map.highestWave, run.map.currentWave),
    bossDefeated: run.stats.bossDefeated || outcome === 'boss-clear',
    resourcesRecovered: settlement.resourcesRecovered,
    resourcesLost: settlement.resourcesLost,
    lootRecovered: settlement.lootRecovered,
    lootLost: settlement.lootLost,
    summaryLabel: buildExtractionSummaryLabel(outcome, currentZone?.label),
  }
}

interface RunInventorySettlement {
  inventory: SaveState['inventory']
  resourcesRecovered: RunState['resources']
  resourcesLost: RunState['resources']
  lootRecovered: LootEntry[]
  lootLost: LootEntry[]
}

function settleRunInventoryInBase(
  inventory: SaveState['inventory'],
  run: RunState,
  outcome: RunResolutionOutcome,
): RunInventorySettlement {
  const carriedIds = new Set(run.inventory.items.map((item) => item.id))

  if (outcome === 'down') {
    return {
      inventory,
      resourcesRecovered: createInitialRunResourceLedger(),
      resourcesLost: buildResourceLedgerFromItems(run.inventory.items),
      lootRecovered: [],
      lootLost: run.lootEntries.filter((entry) => carriedIds.has(entry.id)),
    }
  }

  const placement = placeItemsInGrid(inventory.stashColumns, inventory.stashRows, inventory.storedItems, run.inventory.items)
  const recoveredIds = new Set(placement.placedIds)
  const recoveredItems = run.inventory.items.filter((item) => recoveredIds.has(item.id))
  const lostItems = run.inventory.items.filter((item) => !recoveredIds.has(item.id))

  return {
    inventory: {
      ...inventory,
      storedItems: placement.items,
    },
    resourcesRecovered: buildResourceLedgerFromItems(recoveredItems),
    resourcesLost: buildResourceLedgerFromItems(lostItems),
    lootRecovered: run.lootEntries.filter((entry) => recoveredIds.has(entry.id)),
    lootLost: run.lootEntries.filter((entry) => carriedIds.has(entry.id) && !recoveredIds.has(entry.id)),
  }
}

function mergeWorldStateWithRunMap(world: WorldState, map: RunState['map']): WorldState {
  const discoveredZoneIds = map.zones.filter((zone) => zone.status !== 'locked').map((zone) => zone.id)

  return {
    ...world,
    selectedRouteId: map.routeId,
    selectedZoneId: map.currentZoneId,
    discoveredZones: dedupeStrings([...world.discoveredZones, ...discoveredZoneIds]),
    activeRouteId: world.activeRouteId ?? map.routeId,
  }
}

function buildRunLoadout(save: SaveState): WeaponType[] {
  return save.inventory.equippedWeaponIds.length > 0 ? save.inventory.equippedWeaponIds : ['machineGun', 'grenade', 'sniper']
}

function sanitizeEquippedWeaponIds(weaponIds: WeaponType[]): WeaponType[] {
  const fallback = weaponLoadout.map((weapon) => weapon.id)
  const seen = new Set<WeaponType>()
  const result: WeaponType[] = []

  for (const weaponId of weaponIds) {
    if (seen.has(weaponId)) {
      continue
    }

    seen.add(weaponId)
    result.push(weaponId)
  }

  for (const weaponId of fallback) {
    if (!seen.has(weaponId)) {
      seen.add(weaponId)
      result.push(weaponId)
    }
  }

  return result.slice(0, fallback.length)
}

function dedupeLootEntries(entries: LootEntry[]): LootEntry[] {
  const seen = new Set<string>()

  return entries.filter((entry) => {
    if (seen.has(entry.id)) {
      return false
    }

    seen.add(entry.id)
    return true
  })
}

function dedupeStrings(values: string[]): string[] {
  return [...new Set(values)]
}

function buildExtractionSummaryLabel(outcome: RunResolutionOutcome, zoneLabel?: string | null): string {
  if (outcome === 'boss-clear') {
    return zoneLabel ? `${zoneLabel}已肃清` : '路线已肃清'
  }

  if (outcome === 'down') {
    return '行动失败'
  }

  return zoneLabel ? `已从${zoneLabel}撤离` : '成功撤离'
}

function buildRunId(): string {
  return `run-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`
}

function isSameRuntimeState(previous: SceneRuntimeState, next: SceneRuntimeState): boolean {
  return (
    previous.primaryActionReady === next.primaryActionReady &&
    previous.primaryActionHint === next.primaryActionHint &&
    previous.nearbyMarkerId === next.nearbyMarkerId &&
    previous.nearbyMarkerLabel === next.nearbyMarkerLabel &&
    previous.nearbyMarkerKind === next.nearbyMarkerKind &&
    previous.mapOverlayOpen === next.mapOverlayOpen
  )
}
