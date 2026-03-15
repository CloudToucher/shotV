import type { SaveState } from './types'
import type { WeaponType } from '../game/data/types'
import { createInitialInventoryState, createInitialRunInventoryState, RUN_QUICK_SLOT_COUNT, type InventoryItemRecord } from '../game/inventory/types'
import { createInitialBaseState } from '../game/meta/types'
import {
  createInitialRunResourceLedger,
  createInitialRunState,
  createInitialSessionState,
  type ExtractionResult,
  type LootCategory,
  type LootEntry,
  type RunResolutionOutcome,
  type RunState,
  type RunStateStatus,
} from '../game/session/types'
import { buildLayoutSeedFromText } from '../game/world/layout'
import { createRunMapStateForRoute } from '../game/world/routes'
import { createInitialWorldState } from '../game/world/types'

export const SAVE_VERSION = 6

export function createInitialSaveState(now = new Date()): SaveState {
  const timestamp = now.toISOString()

  return {
    version: SAVE_VERSION,
    createdAt: timestamp,
    updatedAt: timestamp,
    base: createInitialBaseState(),
    inventory: createInitialInventoryState(),
    world: createInitialWorldState(),
    session: createInitialSessionState(),
    settings: {
      developerMode: true,
    },
  }
}

export function hydrateSaveState(raw: unknown): SaveState {
  const initial = createInitialSaveState()
  const data = asObject(raw)

  if (!data) {
    return initial
  }

  const base = asObject(data.base)
  const baseResources = asObject(base?.resources)
  const inventory = asObject(data.inventory)
  const world = asObject(data.world)
  const session = asObject(data.session)
  const settings = asObject(data.settings)
  const hydratedWorld = {
    selectedRouteId: readString(world?.selectedRouteId, initial.world.selectedRouteId),
    selectedZoneId: readString(world?.selectedZoneId, initial.world.selectedZoneId),
    discoveredZones: readStringArray(world?.discoveredZones, initial.world.discoveredZones),
    activeRouteId: readNullableString(world?.activeRouteId, initial.world.activeRouteId),
  }

  const hydratedInventory = {
    stashColumns: readNumber(inventory?.stashColumns, initial.inventory.stashColumns),
    stashRows: readNumber(inventory?.stashRows, initial.inventory.stashRows),
    equippedWeaponIds: readWeaponTypeArray(inventory?.equippedWeaponIds, initial.inventory.equippedWeaponIds),
    equippedArmorId: readNullableString(inventory?.equippedArmorId, initial.inventory.equippedArmorId),
    storedItems: hydrateInventoryItems(inventory?.storedItems),
  }

  const hydratedSession = hydrateSessionState(
    session,
    hydratedInventory.equippedWeaponIds,
    hydratedWorld.selectedRouteId,
    initial.updatedAt,
  )

  return {
    version: typeof data.version === 'number' ? data.version : initial.version,
    createdAt: readString(data.createdAt, initial.createdAt),
    updatedAt: readString(data.updatedAt, initial.updatedAt),
    base: {
      facilityLevel: readNumber(base?.facilityLevel, initial.base.facilityLevel),
      deploymentCount: readNumber(base?.deploymentCount, initial.base.deploymentCount),
      resources: {
        salvage: readNumber(baseResources?.salvage, initial.base.resources.salvage),
        alloy: readNumber(baseResources?.alloy, initial.base.resources.alloy),
        research: readNumber(baseResources?.research, initial.base.resources.research),
      },
      unlockedStations: readStringArray(base?.unlockedStations, initial.base.unlockedStations),
    },
    inventory: hydratedInventory,
    world: hydratedWorld,
    session: hydratedSession,
    settings: {
      developerMode: typeof settings?.developerMode === 'boolean' ? settings.developerMode : initial.settings.developerMode,
    },
  }
}

function hydrateSessionState(
  session: Record<string, unknown> | null,
  fallbackLoadoutWeaponIds: WeaponType[],
  fallbackRouteId: string,
  fallbackTimestamp: string,
): SaveState['session'] {
  if (!session) {
    return createInitialSessionState()
  }

  const activeRun = asObject(session.activeRun)
  const lastExtraction = asObject(session.lastExtraction)
  const legacyLastResolvedRun = asObject(session.lastResolvedRun)

  return {
    activeRun: activeRun ? hydrateRunState(activeRun, fallbackLoadoutWeaponIds, fallbackRouteId, fallbackTimestamp) : null,
    lastExtraction: lastExtraction
      ? hydrateExtractionResult(lastExtraction, fallbackTimestamp)
      : legacyLastResolvedRun
        ? hydrateLegacyExtractionResult(legacyLastResolvedRun, fallbackTimestamp)
        : null,
  }
}

function hydrateRunState(
  run: Record<string, unknown>,
  fallbackLoadoutWeaponIds: WeaponType[],
  fallbackRouteId: string,
  fallbackTimestamp: string,
): RunState {
  const runId = readString(run.id, '未命名行动')
  const enteredAt = readString(run.enteredAt, fallbackTimestamp)
  const player = asObject(run.player)
  const map = asObject(run.map)
  const boss = asObject(map?.boss)
  const resources = asObject(run.resources)
  const stats = asObject(run.stats)
  const inventory = asObject(run.inventory)
  const routeId = readString(map?.routeId, fallbackRouteId)
  const loadoutWeaponIds = readWeaponTypeArray(player?.loadoutWeaponIds, fallbackLoadoutWeaponIds)
  const initialMap = createRunMapStateForRoute(routeId)
  const initialRun = createInitialRunState(runId, enteredAt, loadoutWeaponIds, initialMap)
  const initialInventory = createInitialRunInventoryState()
  const hydratedRunItems = hydrateInventoryItems(inventory?.items)
  const hydratedCurrentZoneId = readString(map?.currentZoneId, initialMap.currentZoneId)
  const zones = hydrateRunZones(map?.zones, initialMap.zones, hydratedCurrentZoneId)
  const currentZoneId = zones.some((zone) => zone.id === hydratedCurrentZoneId) ? hydratedCurrentZoneId : initialMap.currentZoneId

  return {
    ...initialRun,
    status: readRunStateStatus(run.status),
    pendingOutcome: readNullableResolutionOutcome(run.pendingOutcome),
    player: {
      ...initialRun.player,
      health: readNumber(player?.health, initialRun.player.health),
      maxHealth: readNumber(player?.maxHealth, initialRun.player.maxHealth),
      currentWeaponId: readWeaponType(player?.currentWeaponId, initialRun.player.currentWeaponId),
      loadoutWeaponIds,
      shotsFired: readNumber(player?.shotsFired, initialRun.player.shotsFired),
      grenadesThrown: readNumber(player?.grenadesThrown, initialRun.player.grenadesThrown),
      dashesUsed: readNumber(player?.dashesUsed, initialRun.player.dashesUsed),
      damageTaken: readNumber(player?.damageTaken, initialRun.player.damageTaken),
    },
    map: {
      ...initialMap,
      sceneId: 'combat-sandbox',
      routeId: initialMap.routeId,
      currentZoneId,
      layoutSeed: readNumber(map?.layoutSeed, buildLayoutSeedFromText(`${routeId}:${currentZoneId}`)),
      zones,
      currentWave: readNumber(map?.currentWave, initialRun.map.currentWave),
      highestWave: readNumber(map?.highestWave, initialRun.map.highestWave),
      hostilesRemaining: readNumber(map?.hostilesRemaining, initialRun.map.hostilesRemaining),
      boss: {
        ...initialRun.map.boss,
        spawned: typeof boss?.spawned === 'boolean' ? boss.spawned : initialRun.map.boss.spawned,
        defeated: typeof boss?.defeated === 'boolean' ? boss.defeated : initialRun.map.boss.defeated,
        label: readNullableString(boss?.label, initialRun.map.boss.label),
        phase: readNullableBossPhase(boss?.phase, initialRun.map.boss.phase),
        health: readNullableNumber(boss?.health, initialRun.map.boss.health),
        maxHealth: readNullableNumber(boss?.maxHealth, initialRun.map.boss.maxHealth),
      },
    },
    inventory: {
      columns: readNumber(inventory?.columns, initialInventory.columns),
      rows: readNumber(inventory?.rows, initialInventory.rows),
      items: hydratedRunItems,
      quickSlots: hydrateQuickSlots(inventory?.quickSlots, hydratedRunItems),
    },
    groundLoot: hydrateGroundLoot(run.groundLoot),
    resources: hydrateRunResourceLedger(resources),
    lootEntries: hydrateLootEntries(run.lootEntries),
    stats: {
      elapsedSeconds: readNumber(stats?.elapsedSeconds, initialRun.stats.elapsedSeconds),
      kills: readNumber(stats?.kills, initialRun.stats.kills),
      highestWave: readNumber(stats?.highestWave, initialRun.stats.highestWave),
      extracted: typeof stats?.extracted === 'boolean' ? stats.extracted : initialRun.stats.extracted,
      bossDefeated: typeof stats?.bossDefeated === 'boolean' ? stats.bossDefeated : initialRun.stats.bossDefeated,
    },
  }
}

function hydrateExtractionResult(result: Record<string, unknown>, fallbackTimestamp: string): ExtractionResult {
  const outcome = readResolutionOutcome(result.outcome)

  return {
    runId: readString(result.runId, '未命名行动'),
    sceneId: 'combat-sandbox',
    outcome,
    success: typeof result.success === 'boolean' ? result.success : outcome !== 'down',
    resolvedAt: readString(result.resolvedAt, fallbackTimestamp),
    durationSeconds: readNumber(result.durationSeconds, 0),
    kills: readNumber(result.kills, 0),
    highestWave: readNumber(result.highestWave, 0),
    bossDefeated: typeof result.bossDefeated === 'boolean' ? result.bossDefeated : outcome === 'boss-clear',
    resourcesRecovered: hydrateRunResourceLedger(asObject(result.resourcesRecovered)),
    resourcesLost: hydrateRunResourceLedger(asObject(result.resourcesLost)),
    lootRecovered: hydrateLootEntries(result.lootRecovered),
    lootLost: hydrateLootEntries(result.lootLost),
    summaryLabel: readString(result.summaryLabel, buildSummaryLabel(outcome)),
  }
}

function hydrateLegacyExtractionResult(result: Record<string, unknown>, fallbackTimestamp: string): ExtractionResult {
  const outcome = readLegacyResolutionOutcome(result.outcome)

  return {
    runId: readString(result.runId, '未命名行动'),
    sceneId: 'combat-sandbox',
    outcome,
    success: outcome !== 'down',
    resolvedAt: readString(result.resolvedAt, fallbackTimestamp),
    durationSeconds: 0,
    kills: 0,
    highestWave: 0,
    bossDefeated: outcome === 'boss-clear',
    resourcesRecovered: createInitialRunResourceLedger(),
    resourcesLost: createInitialRunResourceLedger(),
    lootRecovered: [],
    lootLost: [],
    summaryLabel: buildSummaryLabel(outcome),
  }
}

function hydrateRunResourceLedger(value: Record<string, unknown> | null) {
  const initial = createInitialRunResourceLedger()

  return {
    salvage: readNumber(value?.salvage, initial.salvage),
    alloy: readNumber(value?.alloy, initial.alloy),
    research: readNumber(value?.research, initial.research),
  }
}

function hydrateInventoryItems(value: unknown): InventoryItemRecord[] {
  if (!Array.isArray(value)) {
    return []
  }

  return value
    .map((entry) => asObject(entry))
    .filter((entry): entry is Record<string, unknown> => Boolean(entry))
    .map((entry, index) => hydrateInventoryItem(entry, `item-${index}`))
}

function hydrateQuickSlots(value: unknown, items: readonly InventoryItemRecord[]): (string | null)[] {
  const validIds = new Set(items.map((item) => item.id))
  const seen = new Set<string>()

  return Array.from({ length: RUN_QUICK_SLOT_COUNT }, (_, index) => {
    const itemId = Array.isArray(value) ? readNullableString(value[index], null) : null

    if (!itemId || !validIds.has(itemId) || seen.has(itemId)) {
      return null
    }

    seen.add(itemId)
    return itemId
  })
}

function hydrateGroundLoot(value: unknown): RunState['groundLoot'] {
  if (!Array.isArray(value)) {
    return []
  }

  return value
    .map((entry) => asObject(entry))
    .filter((entry): entry is Record<string, unknown> => Boolean(entry))
    .map((entry, index) => ({
      id: readString(entry.id, `ground-${index}`),
      item: hydrateInventoryItem(asObject(entry.item), `ground-item-${index}`),
      x: readNumber(entry.x, 0),
      y: readNumber(entry.y, 0),
      source: readLootSource(entry.source),
    }))
}

function hydrateInventoryItem(value: Record<string, unknown> | null, fallbackId: string): InventoryItemRecord {
  return {
    id: readString(value?.id, fallbackId),
    itemId: readString(value?.itemId, 'unknown-item'),
    quantity: readNumber(value?.quantity, 1),
    x: readNumber(value?.x, 0),
    y: readNumber(value?.y, 0),
    width: readNumber(value?.width, 1),
    height: readNumber(value?.height, 1),
    rotated: typeof value?.rotated === 'boolean' ? value.rotated : false,
  }
}

function hydrateLootEntries(value: unknown): LootEntry[] {
  if (!Array.isArray(value)) {
    return []
  }

  return value
    .map((entry) => asObject(entry))
    .filter((entry): entry is Record<string, unknown> => Boolean(entry))
    .map((entry) => ({
      id: readString(entry.id, `loot-${Math.random().toString(36).slice(2, 8)}`),
      definitionId: readString(entry.definitionId, 'unknown-loot'),
      label: readString(entry.label, '未知物资'),
      category: readLootCategory(entry.category),
      quantity: readNumber(entry.quantity, 1),
      source: readLootSource(entry.source),
      acquiredAtWave: readNumber(entry.acquiredAtWave, 0),
      acquiredAtSeconds: readNumber(entry.acquiredAtSeconds, 0),
    }))
}

function hydrateRunZones(
  value: unknown,
  fallbackZones: RunState['map']['zones'],
  currentZoneId: string,
): RunState['map']['zones'] {
  if (!Array.isArray(value)) {
    return fallbackZones.map((zone) =>
      zone.id === currentZoneId
        ? {
            ...zone,
            status: 'active',
          }
        : zone,
    )
  }

  const rawZones = value
    .map((entry) => asObject(entry))
    .filter((entry): entry is Record<string, unknown> => Boolean(entry))
  const fallbackById = Object.fromEntries(fallbackZones.map((zone) => [zone.id, zone])) as Record<string, RunState['map']['zones'][number]>

  return fallbackZones.map((zone) => {
    const rawZone = rawZones.find((entry) => readString(entry.id, zone.id) === zone.id)

    if (!rawZone) {
      return zone.id === currentZoneId ? { ...zone, status: 'active' } : zone
    }

    return {
      ...zone,
      label: readString(rawZone.label, zone.label),
      status: readRunZoneStatus(rawZone.status, zone.id === currentZoneId ? 'active' : zone.status),
      threatLevel: readNumber(rawZone.threatLevel, zone.threatLevel),
      rewardMultiplier: readNumber(rawZone.rewardMultiplier, zone.rewardMultiplier),
      allowsExtraction: typeof rawZone.allowsExtraction === 'boolean' ? rawZone.allowsExtraction : zone.allowsExtraction,
      description: readString(rawZone.description, fallbackById[zone.id]?.description ?? zone.description),
    }
  })
}

function asObject(value: unknown): Record<string, unknown> | null {
  return typeof value === 'object' && value !== null ? (value as Record<string, unknown>) : null
}

function readString(value: unknown, fallback: string): string {
  return typeof value === 'string' ? value : fallback
}

function readNullableString(value: unknown, fallback: string | null): string | null {
  if (value === null) {
    return null
  }

  return typeof value === 'string' ? value : fallback
}

function readNumber(value: unknown, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback
}

function readNullableNumber(value: unknown, fallback: number | null): number | null {
  if (value === null) {
    return null
  }

  return typeof value === 'number' && Number.isFinite(value) ? value : fallback
}

function readStringArray(value: unknown, fallback: string[]): string[] {
  return Array.isArray(value) ? value.filter((entry): entry is string => typeof entry === 'string') : fallback
}

function readWeaponTypeArray(value: unknown, fallback: WeaponType[]): WeaponType[] {
  if (!Array.isArray(value)) {
    return fallback
  }

  const result = value
    .map((entry) => readWeaponType(entry, null))
    .filter((entry): entry is WeaponType => entry !== null)

  return result.length > 0 ? result : fallback
}

function readWeaponType(value: unknown, fallback: WeaponType): WeaponType
function readWeaponType(value: unknown, fallback: null): WeaponType | null
function readWeaponType(value: unknown, fallback: WeaponType | null): WeaponType | null {
  return value === 'machineGun' || value === 'grenade' || value === 'sniper' ? value : fallback
}

function readRunStateStatus(value: unknown): RunStateStatus {
  return value === 'awaiting-settlement' ? 'awaiting-settlement' : 'active'
}

function readRunZoneStatus(value: unknown, fallback: RunState['map']['zones'][number]['status']): RunState['map']['zones'][number]['status'] {
  return value === 'locked' || value === 'cleared' || value === 'active' ? value : fallback
}

function readNullableResolutionOutcome(value: unknown): RunResolutionOutcome | null {
  if (value === null) {
    return null
  }

  return readResolutionOutcome(value)
}

function readResolutionOutcome(value: unknown): RunResolutionOutcome {
  return value === 'boss-clear' || value === 'down' ? value : 'extracted'
}

function readLegacyResolutionOutcome(value: unknown): RunResolutionOutcome {
  if (value === 'sandbox-clear') {
    return 'boss-clear'
  }

  if (value === 'sandbox-down') {
    return 'down'
  }

  return 'extracted'
}

function readNullableBossPhase(value: unknown, fallback: 1 | 2 | null): 1 | 2 | null {
  if (value === null) {
    return null
  }

  return value === 2 ? 2 : value === 1 ? 1 : fallback
}

function readLootCategory(value: unknown): LootCategory {
  return value === 'intel' || value === 'boss' || value === 'consumable' ? value : 'resource'
}

function readLootSource(value: unknown): LootEntry['source'] {
  return value === 'boss' || value === 'wave' || value === 'manual' ? value : 'enemy'
}

function buildSummaryLabel(outcome: RunResolutionOutcome): string {
  if (outcome === 'boss-clear') {
    return '路线清空'
  }

  if (outcome === 'down') {
    return '行动失败'
  }

  return '成功撤离'
}
