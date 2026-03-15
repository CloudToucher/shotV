import { Container, Graphics, Text } from 'pixi.js'

import type { ActiveRunSnapshotInput, GameStore } from '../../app/gameStore'
import { CombatEncounterManager, type CombatEncounterCallbacks, type CombatPlayerState } from '../combat/CombatEncounterManager'
import { CombatPlayerController, type CombatPlayerControllerCallbacks } from '../combat/CombatPlayerController'
import { GRENADE_DAMAGE, GRID_SIZE, MACHINE_GUN_DAMAGE, MACHINE_GUN_SPEED, PLAYER_MAX_HEALTH, SNIPER_DAMAGE, SNIPER_SPEED } from '../combat/constants'
import type { BurstRing, EnemyActor, GrenadeProjectile, NeedleProjectile } from '../combat/types'
import type { ArenaBounds, GameScene, InputSnapshot, ViewportSize } from '../core/contracts'
import { hostileByType } from '../data/hostiles'
import { itemById } from '../data/items'
import { weaponBySlot } from '../data/weapons'
import type { WeaponDefinition, WeaponType } from '../data/types'
import { PlayerAvatar } from '../entities/PlayerAvatar'
import {
  assignQuickSlotBinding,
  autoArrangeInventory,
  buildResourceLedgerFromItems,
  canPlaceItemAtPosition,
  consumeInventoryItemById,
  createInventoryItemRecord,
  findItemAtCell,
  getInventoryCapacity,
  getInventoryUsedCells,
  pickItemFromGridAtCell,
  placeItemAtPosition,
  placeItemInGrid,
  placeItemsInGrid,
  rotateInventoryItem,
  sanitizeQuickSlotBindings,
} from '../inventory/grid'
import { createInitialRunInventoryState, type GridInventoryState, type InventoryItemRecord } from '../inventory/types'
import { createInitialRunResourceLedger, type GroundLootDrop, type LootEntry, type RunResolutionOutcome, type RunState } from '../session/types'
import { palette } from '../theme/palette'
import { CombatHudController } from '../ui/CombatHudController'
import { drawFloatingInventoryItem, drawInventoryGrid, resolveInventoryCellAtPoint } from '../ui/inventorySurface'
import { createFocusedViewBounds, createTextStyle, drawCornerFrame, drawFullScreenPanelFrame, drawMapOverlayPanel, drawMinimap, drawWorldMarkers, drawWorldObstacles, drawWorldSurface } from '../ui/surface'
import { clipSegmentToWorld } from '../world/collision'
import { createCombatWorldLayout, type WorldMapLayout } from '../world/layout'
import { canExtractFromRunMap, createRunMapStateForRoute, getCurrentRunZone, getNextRunZone, getWorldRoute, isCurrentRunZoneCleared } from '../world/routes'

const RUN_SYNC_INTERVAL_SECONDS = 0.2
const DEFAULT_LOADOUT: WeaponType[] = ['machineGun', 'grenade', 'sniper']
const PANEL_GROUND_COLUMNS = 6
const PANEL_GROUND_ROWS = 3
const PANEL_GROUND_RADIUS = 128
const QUICK_SLOT_USE_LABELS = ['Z', 'X', 'C', 'V'] as const
const DROP_RELEASE_OFFSETS = [
  { x: -18, y: -8 },
  { x: 16, y: -4 },
  { x: -6, y: 18 },
  { x: 20, y: 14 },
]

interface NearbyGroundLootPanelState {
  totalCount: number
  hiddenCount: number
  items: InventoryItemRecord[]
  dropByItemId: Map<string, GroundLootDrop>
}

interface CombatInventoryPanelLayout {
  frame: {
    x: number
    y: number
    width: number
    height: number
    headerHeight: number
    footerHeight: number
  }
  summaryColumn: {
    x: number
    y: number
    width: number
    height: number
  }
  groundColumn: {
    x: number
    y: number
    width: number
    height: number
  }
  inventoryColumn: {
    x: number
    y: number
    width: number
    height: number
  }
  groundGrid: {
    x: number
    y: number
    columns: number
    rows: number
    cellSize: number
  }
  inventoryGrid: {
    x: number
    y: number
    columns: number
    rows: number
    cellSize: number
  }
}

export class CombatSandboxScene implements GameScene {
  readonly container = new Container()

  private readonly store: GameStore
  private readonly backdrop = new Graphics()
  private readonly cameraRoot = new Container()
  private readonly terrain = new Graphics()
  private readonly obstacleLayer = new Graphics()
  private readonly markerLayer = new Graphics()
  private readonly markerLabelLayer = new Container()
  private readonly world = new Container()
  private readonly enemyLayer = new Container()
  private readonly groundLootLayer = new Graphics()
  private readonly aimGuide = new Graphics()
  private readonly effects = new Graphics()
  private readonly reticle = new Graphics()
  private readonly minimap = new Graphics()
  private readonly minimapTitle = new Text({ text: '战区小地图', style: createTextStyle(12, palette.uiText, { fontWeight: '700', letterSpacing: 0.4 }) })
  private readonly overviewMap = new Graphics()
  private readonly overviewTitle = new Text({ text: '战区总地图', style: createTextStyle(18, palette.uiText, { fontWeight: '700' }) })
  private readonly overviewMeta = new Text({ text: '', style: createTextStyle(12, palette.uiMuted, { lineHeight: 20, align: 'left' }) })
  private readonly overviewHint = new Text({ text: '', style: createTextStyle(12, palette.uiMuted, { lineHeight: 20 }) })
  private readonly locationText = new Text({ text: '', style: createTextStyle(16, palette.uiText, { fontWeight: '700', letterSpacing: 0.4 }) })
  private readonly interactionPanel = new Graphics()
  private readonly interactionText = new Text({ text: '', style: createTextStyle(12, palette.uiText, { fontWeight: '700', letterSpacing: 0.3 }) })
  private readonly panel = new Graphics()
  private readonly panelTitle = new Text({ text: '行动面板', style: createTextStyle(22, palette.uiText, { fontWeight: '700', letterSpacing: 0.6 }) })
  private readonly panelBody = new Text({ text: '', style: createTextStyle(13, palette.uiMuted, { lineHeight: 23, wordWrap: true, wordWrapWidth: 214 }) })
  private readonly panelLootTitle = new Text({ text: '', style: createTextStyle(12, palette.uiText, { fontWeight: '700', letterSpacing: 0.8 }) })
  private readonly panelGridTitle = new Text({ text: '', style: createTextStyle(12, palette.uiText, { fontWeight: '700', letterSpacing: 0.8 }) })
  private readonly panelFooter = new Text({ text: 'Tab / I 关闭', style: createTextStyle(12, palette.uiMuted, { letterSpacing: 0.5 }) })

  private readonly player = new PlayerAvatar()
  private readonly playerController = new CombatPlayerController()
  private readonly hud = new CombatHudController()
  private readonly encounter = new CombatEncounterManager(this.enemyLayer)

  private readonly needleProjectiles: NeedleProjectile[] = []
  private readonly grenadeProjectiles: GrenadeProjectile[] = []
  private readonly burstRings: BurstRing[] = []

  private viewport: ViewportSize = { width: 0, height: 0 }
  private worldBounds: ArenaBounds = { left: 0, top: 0, right: 0, bottom: 0 }
  private layout: WorldMapLayout | null = null
  private visualTime = 0
  private shakeTrauma = 0
  private playerHealth = PLAYER_MAX_HEALTH
  private syncTimer = RUN_SYNC_INTERVAL_SECONDS
  private loadoutWeaponIds: WeaponType[] = [...DEFAULT_LOADOUT]
  private runElapsedSeconds = 0
  private runKills = 0
  private runHighestWave = 0
  private zoneHighestWave = 0
  private runInventory: GridInventoryState = createInitialRunInventoryState()
  private groundLoot: GroundLootDrop[] = []
  private runResources = createInitialRunResourceLedger()
  private lootEntries: LootEntry[] = []
  private shotsFired = 0
  private grenadesThrown = 0
  private dashesUsed = 0
  private damageTaken = 0
  private pendingOutcome: Extract<RunResolutionOutcome, 'boss-clear' | 'down'> | null = null
  private panelOpen = false
  private mapOpen = false
  private heldInventoryItem: InventoryItemRecord | null = null
  private heldItemOrigin: 'inventory' | 'ground' | null = null
  private heldInventoryRestoreItem: InventoryItemRecord | null = null
  private heldGroundRestoreDrop: GroundLootDrop | null = null
  private pointerScreen = { x: 0, y: 0, hasPointer: false }
  private storeUnsubscribe: (() => void) | null = null

  constructor(store: GameStore) {
    this.store = store
    this.locationText.anchor.set(1, 0)
    this.interactionText.anchor.set(0.5, 0)
    this.panelLootTitle.anchor.set(0, 0)
    this.panelGridTitle.anchor.set(0, 0)
    this.container.addChild(
      this.backdrop,
      this.cameraRoot,
      this.hud.container,
      this.minimap,
      this.minimapTitle,
      this.overviewMap,
      this.overviewTitle,
      this.overviewMeta,
      this.overviewHint,
      this.locationText,
      this.interactionPanel,
      this.interactionText,
      this.panel,
      this.panelTitle,
      this.panelBody,
      this.panelLootTitle,
      this.panelGridTitle,
      this.panelFooter,
    )
    this.cameraRoot.addChild(this.terrain, this.obstacleLayer, this.markerLayer, this.markerLabelLayer, this.world, this.groundLootLayer, this.aimGuide, this.effects, this.reticle)
    this.world.addChild(this.enemyLayer, this.player.container)
    this.storeUnsubscribe = this.store.subscribe((state, previousState) => {
      if (state.mode !== 'combat') {
        return
      }
      const nextKey = buildRunStructureKey(state.save.session.activeRun)
      const previousKey = buildRunStructureKey(previousState.save.session.activeRun)
      if (nextKey !== previousKey) {
        this.bootstrapFromStore()
      }
    })
    this.bootstrapFromStore()
  }

  resize(viewport: ViewportSize): void {
    this.viewport = viewport
    this.drawBackdrop()
    this.hud.setViewport(viewport)
    this.layoutUi()
    if (this.layout) {
      this.encounter.resize(this.layout.bounds, this.layout.obstacles, this.layout.enemySpawns, this.layout.bossSpawn)
      this.rebuildWorldVisuals()
    }
  }

  update(deltaSeconds: number, elapsedSeconds: number, input: InputSnapshot): void {
    this.visualTime = elapsedSeconds
    this.pointerScreen = {
      x: input.pointerX,
      y: input.pointerY,
      hasPointer: input.hasPointer,
    }
    this.playerController.tick(deltaSeconds)
    this.hud.tick(deltaSeconds)
    this.shakeTrauma = Math.max(0, this.shakeTrauma - deltaSeconds * 2.4)

    if (input.panelTogglePressed) {
      if (this.panelOpen) {
        this.restoreHeldInventoryItem()
      }
      this.panelOpen = !this.panelOpen
      if (this.panelOpen) {
        this.mapOpen = false
      }
      this.hud.showToast(this.panelOpen ? '信息面板已展开' : '信息面板已关闭', this.panelOpen ? '查看搜刮记录、武器与背包占位' : '继续推进当前区域', 0.8)
    }

    if (input.mapTogglePressed) {
      if (!this.mapOpen) {
        this.restoreHeldInventoryItem()
      }
      this.mapOpen = !this.mapOpen
      if (this.mapOpen) {
        this.panelOpen = false
      }
      this.hud.showToast(this.mapOpen ? '总地图已展开' : '总地图已关闭', this.mapOpen ? '查看整张战区与出口位置' : '返回局部视野', 0.8)
    }

    if (!this.layout) {
      this.store.clearSceneRuntime()
      return
    }

    if (this.panelOpen && !this.mapOpen) {
      if (input.pointerPressed) {
        this.handleInventoryPanelPointerPressed(input.pointerX, input.pointerY)
      }

      if (input.pointerReleased) {
        this.handleInventoryPanelPointerReleased(input.pointerX, input.pointerY)
      }

      if (input.quickSlotBind) {
        this.handleQuickSlotBind(input.quickSlotBind)
      }

      if (input.rotatePressed) {
        this.rotateHeldInventoryItem()
      }

      if (input.sortPressed) {
        this.autoArrangeRunInventory()
      }
    }

    const encounterState = this.encounter.getEncounterState()
    const pointerWorld = input.hasPointer ? this.screenToWorld(input.pointerX, input.pointerY) : null
    const mappedInput = pointerWorld === null ? input : { ...input, pointerX: pointerWorld.x, pointerY: pointerWorld.y }
    const sceneBlocked = this.panelOpen || this.mapOpen

    if (!sceneBlocked && encounterState === 'active') {
      this.runElapsedSeconds += deltaSeconds
      this.playerController.handleInput(mappedInput, this.player, this.worldBounds, this.playerCallbacks)
      if (input.quickSlotUse) {
        this.tryUseQuickSlot(input.quickSlotUse)
      }
    } else {
      this.player.setMoveIntent(0, 0)
    }

    this.player.update(deltaSeconds, this.worldBounds, elapsedSeconds, this.layout.obstacles)
    if (!sceneBlocked && input.interactPressed) {
      this.tryPickupNearbyLoot()
    }
    if (!sceneBlocked) {
      this.encounter.update(deltaSeconds, elapsedSeconds, this.getPlayerState(), this.encounterCallbacks)
    }

    this.updateTransientEffects(deltaSeconds)
    this.applyCameraTransform()
    this.drawAimLayer(encounterState === 'active' && !this.panelOpen && pointerWorld !== null)
    this.drawEffects()
    this.drawGroundLoot()
    this.updateLocationLabel()
    this.updateInteractionPrompt()
    this.updateMarkerLabels(this.getFocusedMarkerId())
    this.drawPanel()
    this.drawOverviewMap()
    this.drawMinimap()
    this.syncSceneRuntime()
    this.hud.draw({
      elapsedSeconds: this.runElapsedSeconds,
      playerHealth: this.playerHealth,
      waveIndex: this.encounter.getWaveIndex(),
      killCount: this.runKills,
      enemyCount: this.encounter.getEnemies().length,
      pendingSpawns: this.encounter.getPendingSpawnCount(),
      currentWeaponId: this.playerController.getCurrentWeapon().id,
      boss: this.buildBossHudSnapshot(),
      encounterState,
      quickSlots: this.buildQuickSlotHudSnapshot(),
    })

    if (!sceneBlocked && encounterState === 'active') {
      this.syncTimer -= deltaSeconds
      if (this.syncTimer <= 0) {
        this.flushRunSnapshot()
        this.syncTimer = RUN_SYNC_INTERVAL_SECONDS
      }
    }
  }

  destroy(): void {
    this.storeUnsubscribe?.()
    this.storeUnsubscribe = null
    this.clearHeldInventoryState()
    this.store.clearSceneRuntime()
    this.encounter.destroy()
    this.container.destroy({ children: true })
  }

  private readonly playerCallbacks: CombatPlayerControllerCallbacks = {
    onWeaponChanged: (weapon, slot, silent) => {
      this.player.setWeaponStyle(weapon.id)
      this.player.triggerWeaponSwap()
      if (!silent) {
        this.hud.showToast(`武器槽 ${slot}`, `${weapon.label} / ${weapon.hint}`, 1.1)
      }
      this.hud.pulseWeapon(1)
      this.hud.pulseInfo(0.32)
    },
    onDash: (position) => {
      this.dashesUsed += 1
      this.spawnRing(position.x, position.y, 12, 64, 0.2, palette.dash, 3)
      this.hud.pulseWeapon(0.55)
      this.addShake(0.08)
    },
    onFire: (weapon, aimPoint) => {
      this.shotsFired += 1
      if (weapon.id === 'grenade') {
        this.grenadesThrown += 1
      }
      this.fireWeapon(weapon, aimPoint)
    },
  }

  private readonly encounterCallbacks: CombatEncounterCallbacks = {
    onWaveStarted: (waveIndex, hint) => {
      this.zoneHighestWave = Math.max(this.zoneHighestWave, waveIndex)
      this.runHighestWave = Math.max(this.runHighestWave, waveIndex)
      this.hud.showToast(`波次 ${String(waveIndex).padStart(2, '0')}`, hint, 1.15)
      this.hud.pulseInfo(0.9)
      this.flushRunSnapshot()
    },
    onEnemySpawned: (type) => {
      this.hud.pulseInfo(type === 'boss' ? 1 : 0.24)
    },
    onBossSpawned: (enemy) => {
      this.spawnRing(enemy.x, enemy.y, 18, 92, 0.28, palette.warning, 4)
      this.hud.showToast('区域主核出现', `${enemy.definition.label}正在接管战区`, 1.35)
      this.addShake(0.18)
      this.flushRunSnapshot()
    },
    onBossPhaseShift: (enemy) => {
      this.spawnRing(enemy.x, enemy.y, 24, 118, 0.34, palette.danger, 4)
      this.hud.showToast('第二阶段', `${enemy.definition.label}火力已升级`, 1.2)
      this.hud.pulseInfo(0.9)
      this.addShake(0.32)
      this.flushRunSnapshot()
    },
    onBossAttack: (pattern, enemy) => {
      this.spawnRing(enemy.x, enemy.y, 20, pattern === 'fan' ? 88 : 112, 0.24, palette.accentSoft, 3)
      this.addShake(pattern === 'fan' ? 0.08 : 0.16)
    },
    onEnemyHit: (enemy, amount, impactX, impactY) => {
      this.spawnRing(impactX, impactY, 6, 24, 0.12, palette.accentSoft, 2)
      this.hud.pulseWeapon(Math.min(1, 0.28 + amount / 48))
      this.hud.pulseInfo(0.2)
      this.addShake(enemy.type === 'boss' ? 0.08 : 0.03)
    },
    onEnemyKilled: (enemy) => {
      this.runKills = this.encounter.getKillCount()
      this.applyKillRewards(enemy)
      this.spawnRing(enemy.x, enemy.y, 14, enemy.type === 'boss' ? 74 : 42, 0.2, enemy.definition.colors.glow, 3)
      this.hud.pulseInfo(enemy.type === 'boss' ? 1 : 0.55)
      this.addShake(enemy.type === 'boss' ? 0.24 : 0.08)
      this.flushRunSnapshot()
    },
    onPlayerDamaged: (amount, sourceX, sourceY) => {
      this.applyPlayerDamage(amount, sourceX, sourceY)
    },
    onBossDefeated: (enemy) => {
      const baseMap = this.getLiveRunMap()
      const nextZone = getNextRunZone(baseMap)
      this.spawnRing(enemy.x, enemy.y, 24, 144, 0.42, palette.accent, 4)
      this.hud.showToast(nextZone ? '区域已压制' : '路线已完成', nextZone ? '前往出口后可推进下一区域' : '前往撤离出口后完成本次副本', 99)
      this.hud.pulseInfo(1)
      this.addShake(0.4)
      this.store.markCurrentZoneCleared(this.buildRunSnapshot())
    },
  }

  private bootstrapFromStore(): void {
    const activeRun = this.store.getState().save.session.activeRun
    this.clearEncounterState()

    if (!activeRun) {
      this.layout = null
      this.worldBounds = { left: 0, top: 0, right: 0, bottom: 0 }
      this.playerHealth = PLAYER_MAX_HEALTH
      this.player.reset()
      this.player.setLifeRatio(1)
      this.playerController.reset()
      this.playerController.notifySilentWeaponState(this.playerCallbacks)
      this.panelOpen = false
      this.mapOpen = false
      this.store.clearSceneRuntime()
      return
    }

    this.layout = this.resolveCombatLayout(activeRun)
    this.worldBounds = this.layout.bounds
    this.encounter.resize(this.layout.bounds, this.layout.obstacles, this.layout.enemySpawns, this.layout.bossSpawn)
    this.rebuildWorldVisuals()
    this.hydrateRun(activeRun)
  }

  private hydrateRun(run: RunState): void {
    const currentZone = getCurrentRunZone(run.map)
    this.loadoutWeaponIds = run.player.loadoutWeaponIds.length > 0 ? [...run.player.loadoutWeaponIds] : [...DEFAULT_LOADOUT]
    this.playerHealth = run.player.health
    this.runElapsedSeconds = run.stats.elapsedSeconds
    this.runKills = run.stats.kills
    this.runHighestWave = run.stats.highestWave
    this.zoneHighestWave = run.map.highestWave
    this.runInventory = {
      columns: run.inventory.columns,
      rows: run.inventory.rows,
      items: run.inventory.items.map((item) => ({ ...item })),
      quickSlots: sanitizeQuickSlotBindings(run.inventory.quickSlots, run.inventory.items.map((item) => item.id)),
    }
    this.groundLoot = run.groundLoot.map((drop) => ({
      ...drop,
      item: { ...drop.item },
    }))
    this.runResources = run.inventory.items.length > 0 ? buildResourceLedgerFromItems(run.inventory.items) : { salvage: run.resources.salvage, alloy: run.resources.alloy, research: run.resources.research }
    this.lootEntries = [...run.lootEntries]
    this.shotsFired = run.player.shotsFired
    this.grenadesThrown = run.player.grenadesThrown
    this.dashesUsed = run.player.dashesUsed
    this.damageTaken = run.player.damageTaken
    this.pendingOutcome = run.status === 'awaiting-settlement' && run.pendingOutcome !== 'extracted' ? run.pendingOutcome : null
    this.panelOpen = false
    this.mapOpen = false
    this.clearHeldInventoryState()
    this.player.reset()
    this.player.setPosition(this.layout?.playerSpawn.x ?? this.viewport.width * 0.5, this.layout?.playerSpawn.y ?? this.viewport.height * 0.5)
    this.player.setLifeRatio(this.playerHealth / PLAYER_MAX_HEALTH)
    this.playerController.reset()
    this.playerController.restoreWeapon(run.player.currentWeaponId, this.playerCallbacks)

    if (run.status === 'awaiting-settlement') {
      if (run.pendingOutcome === 'down') {
        this.encounter.markPlayerDown()
        this.hud.showToast('行动中止', '本局等待结算，返回基地查看结果', 99)
      } else {
        this.encounter.markEncounterClear()
        this.hud.showToast('路线完成', '本局等待结算，返回基地领取回收物资', 99)
      }
      return
    }

    if (currentZone?.status === 'cleared') {
      this.encounter.markEncounterClear()
      this.hud.showToast('区域已压制', getNextRunZone(run.map) ? '前往出口后可继续推进到下一区域' : '终点区域已完成，前往撤离出口完成副本', 99)
      return
    }

    if (run.map.currentWave > 0 || run.map.hostilesRemaining > 0 || run.map.boss.spawned) {
      this.encounter.restoreCheckpoint(
        {
          currentWave: run.map.currentWave,
          hostilesRemaining: run.map.hostilesRemaining,
          boss: { spawned: run.map.boss.spawned, defeated: run.map.boss.defeated, phase: run.map.boss.phase, health: run.map.boss.health },
          kills: run.stats.kills,
        },
        this.encounterCallbacks,
      )
      this.hud.showToast(currentZone?.label ?? '行动已恢复', currentZone?.description ?? '继续肃清当前区域', 1.2)
      return
    }

    this.hud.showToast(currentZone?.label ?? '战区接入完成', currentZone?.description ?? '探索当前区域，肃清威胁后再决定推进或撤离', 1.2)
  }

  private resolveCombatLayout(run: RunState): WorldMapLayout {
    const currentZone = getCurrentRunZone(run.map)
    return createCombatWorldLayout({
      routeId: run.map.routeId,
      zoneId: run.map.currentZoneId,
      zoneLabel: currentZone?.label ?? '未知区域',
      threatLevel: currentZone?.threatLevel ?? 1,
      allowsExtraction: currentZone?.allowsExtraction ?? false,
      seed: run.map.layoutSeed,
    })
  }

  private rebuildWorldVisuals(): void {
    if (!this.layout) {
      return
    }

    drawWorldSurface(this.terrain, { bounds: this.layout.bounds, gridSize: GRID_SIZE, mode: 'combat' })
    drawWorldObstacles(this.obstacleLayer, this.layout.obstacles)
    const focusedMarkerId = this.getFocusedMarkerId()
    drawWorldMarkers(this.markerLayer, this.layout.markers, this.visualTime, focusedMarkerId)
    this.markerLabelLayer.removeChildren().forEach((child) => child.destroy())
    for (const marker of this.layout.markers) {
      const label = new Text({ text: marker.label, style: createTextStyle(12, palette.uiText, { fontWeight: '700', letterSpacing: 0.5 }) })
      label.anchor.set(0.5, 0)
      label.position.set(marker.x, marker.y + 22)
      this.markerLabelLayer.addChild(label)
    }
    this.updateMarkerLabels(focusedMarkerId)
  }

  private clearEncounterState(): void {
    this.encounter.reset()
    this.needleProjectiles.length = 0
    this.grenadeProjectiles.length = 0
    this.burstRings.length = 0
    this.shakeTrauma = 0
    this.cameraRoot.position.set(0, 0)
    this.syncTimer = RUN_SYNC_INTERVAL_SECONDS
    this.runElapsedSeconds = 0
    this.runKills = 0
    this.runHighestWave = 0
    this.zoneHighestWave = 0
    this.runInventory = createInitialRunInventoryState()
    this.groundLoot = []
    this.runResources = createInitialRunResourceLedger()
    this.lootEntries = []
    this.shotsFired = 0
    this.grenadesThrown = 0
    this.dashesUsed = 0
    this.damageTaken = 0
    this.pendingOutcome = null
    this.clearHeldInventoryState()
  }

  private getPlayerState(): CombatPlayerState {
    const position = this.player.getPosition()
    return { x: position.x, y: position.y, radius: this.player.getCollisionRadius(), isDashing: this.player.isDashing() }
  }

  private getLiveRunMap(): RunState['map'] {
    const state = this.store.getState()
    const activeRun = state.save.session.activeRun
    return activeRun?.map ?? createRunMapStateForRoute(state.save.world.selectedRouteId)
  }

  private buildBossHudSnapshot() {
    const boss = this.encounter.getBoss()
    if (!boss) {
      return null
    }
    return { label: boss.definition.label, health: boss.health, maxHealth: boss.definition.maxHealth, phase: boss.phase }
  }

  private buildQuickSlotHudSnapshot() {
    return this.runInventory.quickSlots.map((itemId, index) => {
      const item = itemId ? this.runInventory.items.find((entry) => entry.id === itemId) : null
      const definition = item ? itemById[item.itemId] : null

      return {
        keyLabel: QUICK_SLOT_USE_LABELS[index] ?? String(index + 1),
        itemLabel: definition?.shortLabel ?? '空',
        quantity: item?.quantity ?? 0,
        available: Boolean(item && definition?.use),
      }
    })
  }

  private drawGroundLoot(): void {
    this.groundLootLayer.clear()
    const nearestDrop = this.findNearbyGroundLoot()

    for (const drop of this.groundLoot) {
      const definition = itemById[drop.item.itemId]
      const tint = definition?.tint ?? palette.warning
      const accent = definition?.accent ?? palette.accent
      const pulse = (Math.sin(this.visualTime * 4.2 + drop.x * 0.01) + 1) * 0.5
      const width = 18 + drop.item.width * 7
      const height = 12 + drop.item.height * 7
      const x = drop.x - width * 0.5
      const y = drop.y - height * 0.5 - 10
      const highlighted = nearestDrop?.id === drop.id
      const frameWidth = width + (highlighted ? 12 : 8)
      const frameHeight = height + (highlighted ? 18 : 12)
      const frameX = drop.x - frameWidth * 0.5
      const frameY = y - (highlighted ? 8 : 4)

      this.groundLootLayer.circle(drop.x, drop.y + 6, 12 + pulse * 4 + (highlighted ? 4 : 0)).fill({
        color: tint,
        alpha: highlighted ? 0.1 : 0.05,
      })
      this.groundLootLayer.circle(drop.x, drop.y, 16 + pulse * 7 + (highlighted ? 6 : 0)).stroke({
        width: highlighted ? 1.5 : 1.2,
        color: tint,
        alpha: highlighted ? 0.34 : 0.16,
        alignment: 0.5,
      })
      this.groundLootLayer.roundRect(x, y, width, height, 6).fill({ color: palette.uiPanel, alpha: highlighted ? 0.92 : 0.82 })
      this.groundLootLayer.roundRect(x, y, width, height, 6).stroke({ width: 1.2, color: tint, alpha: highlighted ? 0.72 : 0.36, alignment: 0.5 })
      this.groundLootLayer.rect(x + 4, y + 4, width - 8, 4).fill({ color: accent, alpha: highlighted ? 0.52 : 0.3 })
      drawCornerFrame(this.groundLootLayer, frameX, frameY, frameWidth, frameHeight, 8, tint, highlighted ? 0.36 : 0.16, highlighted ? 1.2 : 1)

      if (highlighted) {
        this.groundLootLayer.moveTo(frameX - 10, drop.y - 10)
        this.groundLootLayer.lineTo(frameX - 2, drop.y - 2)
        this.groundLootLayer.lineTo(frameX - 10, drop.y + 6)
        this.groundLootLayer.moveTo(frameX + frameWidth + 10, drop.y - 10)
        this.groundLootLayer.lineTo(frameX + frameWidth + 2, drop.y - 2)
        this.groundLootLayer.lineTo(frameX + frameWidth + 10, drop.y + 6)
        this.groundLootLayer.stroke({ width: 1.4, color: tint, alpha: 0.42, cap: 'round', join: 'round' })
      }

      if (drop.item.quantity > 1) {
        this.groundLootLayer.roundRect(x + width - 22, y + height - 16, 16, 12, 6).fill({ color: accent, alpha: 0.94 })
      }
    }
  }

  private fireWeapon(weapon: WeaponDefinition, aimPoint: { x: number; y: number }): void {
    if (!this.layout) {
      return
    }

    const playerPosition = this.player.getPosition()
    const aimAngle = Math.atan2(aimPoint.y - playerPosition.y, aimPoint.x - playerPosition.x)
    const dirX = Math.cos(aimAngle)
    const dirY = Math.sin(aimAngle)
    const origin = this.player.getShotOrigin()

    if (weapon.id === 'grenade') {
      this.player.triggerShot(0.78)
      this.grenadeProjectiles.push({ startX: origin.x, startY: origin.y, endX: aimPoint.x, endY: aimPoint.y, age: 0, duration: 0.34 })
      this.spawnRing(origin.x, origin.y, 8, 26, 0.16, palette.accentSoft, 3)
      this.hud.pulseWeapon(0.72)
      this.addShake(0.07)
      return
    }

    const range = weapon.id === 'sniper' ? 100000 : weapon.range
    const target = { x: origin.x + dirX * range, y: origin.y + dirY * range }
    const clipped = clipSegmentToWorld(origin, target, this.worldBounds, this.layout.obstacles, 2)
    const hits = this.encounter.resolveSegmentHits(origin, clipped, weapon.effectWidth * 0.65)
    let trailEndX = clipped.x
    let trailEndY = clipped.y

    if (weapon.id === 'machineGun' && hits.length > 0) {
      trailEndX = hits[0].pointX
      trailEndY = hits[0].pointY
      this.encounter.damageEnemy(hits[0].enemy, MACHINE_GUN_DAMAGE, hits[0].pointX, hits[0].pointY, this.encounterCallbacks)
    }

    if (weapon.id === 'sniper' && hits.length > 0) {
      for (const hit of hits) {
        this.encounter.damageEnemy(hit.enemy, SNIPER_DAMAGE, hit.pointX, hit.pointY, this.encounterCallbacks)
      }
    }

    const distance = Math.hypot(trailEndX - origin.x, trailEndY - origin.y)
    const speed = weapon.id === 'sniper' ? SNIPER_SPEED : MACHINE_GUN_SPEED

    this.player.triggerShot(weapon.id === 'sniper' ? 1.45 : 0.9)
    this.needleProjectiles.push({
      startX: origin.x,
      startY: origin.y,
      endX: trailEndX,
      endY: trailEndY,
      dirX,
      dirY,
      age: 0,
      duration: clamp(distance / speed, 0.08, weapon.id === 'sniper' ? 0.28 : 0.18),
      length: weapon.id === 'sniper' ? 26 : 14,
      width: weapon.id === 'sniper' ? 5 : 3,
      color: weapon.id === 'sniper' ? palette.playerEdge : palette.accent,
      coreColor: weapon.id === 'sniper' ? palette.accentSoft : palette.arenaCore,
    })
    this.hud.pulseWeapon(weapon.id === 'sniper' ? 1 : 0.45)
    this.addShake(weapon.id === 'sniper' ? 0.14 : 0.04)
  }

  private updateTransientEffects(deltaSeconds: number): void {
    for (const projectile of this.needleProjectiles) {
      projectile.age += deltaSeconds
    }
    for (const projectile of this.grenadeProjectiles) {
      projectile.age += deltaSeconds
    }
    for (const ring of this.burstRings) {
      ring.age += deltaSeconds
    }

    for (let index = this.grenadeProjectiles.length - 1; index >= 0; index -= 1) {
      const projectile = this.grenadeProjectiles[index]
      if (projectile.age < projectile.duration) {
        continue
      }
      const radius = weaponBySlot[2].splashRadius ?? 66
      this.grenadeProjectiles.splice(index, 1)
      this.encounter.applyExplosionDamage(projectile.endX, projectile.endY, radius, GRENADE_DAMAGE, this.encounterCallbacks)
      this.spawnRing(projectile.endX, projectile.endY, 14, radius, 0.24, palette.accent, 4)
      this.spawnRing(projectile.endX, projectile.endY, 8, radius * 0.68, 0.2, palette.dash, 2)
      this.hud.pulseInfo(0.8)
      this.addShake(0.18)
    }

    removeExpired(this.needleProjectiles)
    removeExpired(this.burstRings)
  }

  private applyPlayerDamage(amount: number, sourceX: number, sourceY: number): void {
    if (this.player.isDashing() || this.encounter.getEncounterState() !== 'active') {
      return
    }
    this.playerHealth = Math.max(0, this.playerHealth - amount)
    this.damageTaken += amount
    this.player.setLifeRatio(this.playerHealth / PLAYER_MAX_HEALTH)
    this.player.flashDamage(amount >= 16 ? 1 : 0.72)
    this.spawnRing(sourceX, sourceY, 10, 34, 0.16, palette.danger, 3)
    this.hud.pulseHealth(1)
    this.hud.pulseInfo(0.5)
    this.addShake(amount >= 16 ? 0.2 : 0.12)
    this.flushRunSnapshot()
    if (this.playerHealth === 0) {
      this.encounter.markPlayerDown()
      this.hud.showToast('行动中止', '本局待结算，请返回基地', 99)
      this.finalizeRun('down')
    }
  }

  private applyPlayerHeal(amount: number): number {
    if (amount <= 0 || this.playerHealth >= PLAYER_MAX_HEALTH) {
      return 0
    }

    const previousHealth = this.playerHealth
    this.playerHealth = Math.min(PLAYER_MAX_HEALTH, this.playerHealth + amount)
    this.player.setLifeRatio(this.playerHealth / PLAYER_MAX_HEALTH)
    this.spawnRing(this.player.getPosition().x, this.player.getPosition().y, 12, 42, 0.18, palette.minimapMarker, 3)
    this.hud.pulseHealth(0.65)
    this.hud.pulseInfo(0.36)
    return this.playerHealth - previousHealth
  }

  private applyKillRewards(enemy: EnemyActor): void {
    const currentZone = getCurrentRunZone(this.getLiveRunMap())
    const rewardMultiplier = currentZone?.rewardMultiplier ?? 1
    const rewardByType: Record<EnemyActor['type'], Array<{ itemId: string; quantity: number }>> = {
      melee: [{ itemId: 'salvage-scrap', quantity: 4 }],
      ranged: [
        { itemId: 'salvage-scrap', quantity: 3 },
        { itemId: 'telemetry-cache', quantity: 1 },
      ],
      charger: [
        { itemId: 'salvage-scrap', quantity: 5 },
        { itemId: 'alloy-plate', quantity: 1 },
      ],
      boss: [{ itemId: 'aegis-core', quantity: 1 }],
    }
    const utilityDropByType: Partial<Record<EnemyActor['type'], Array<{ itemId: string; chance: number }>>> = {
      melee: [{ itemId: 'dash-cell', chance: 0.12 }],
      ranged: [{ itemId: 'med-injector', chance: 0.15 }],
      charger: [{ itemId: 'shock-charge', chance: 0.18 }],
      boss: [{ itemId: 'field-kit', chance: 1 }],
    }
    const offsetPattern = [
      { x: -18, y: -6 },
      { x: 16, y: 2 },
      { x: -8, y: 16 },
    ]
    const nextDrops: GroundLootDrop[] = []

    rewardByType[enemy.type].forEach((reward, index) => {
      const quantity = Math.max(1, Math.round(reward.quantity * rewardMultiplier))
      const item = createInventoryItemRecord(reward.itemId, quantity, `drop-${enemy.type}-${this.runKills}-${Math.floor(this.runElapsedSeconds * 1000)}-${index}`)

      if (!item) {
        return
      }

      const offset = offsetPattern[index % offsetPattern.length]
      nextDrops.push({
        id: item.id,
        item,
        x: enemy.x + offset.x,
        y: enemy.y + offset.y,
        source: enemy.type === 'boss' ? 'boss' : 'enemy',
      })
    })

    utilityDropByType[enemy.type]?.forEach((reward, index) => {
      if (Math.random() > reward.chance) {
        return
      }

      const item = createInventoryItemRecord(reward.itemId, 1, `utility-${enemy.type}-${this.runKills}-${Math.floor(this.runElapsedSeconds * 1000)}-${index}`)

      if (!item) {
        return
      }

      const offset = offsetPattern[(rewardByType[enemy.type].length + index) % offsetPattern.length]
      nextDrops.push({
        id: item.id,
        item,
        x: enemy.x + offset.x,
        y: enemy.y + offset.y,
        source: enemy.type === 'boss' ? 'boss' : 'enemy',
      })
    })

    this.groundLoot = [...this.groundLoot, ...nextDrops]
  }

  private tryPickupNearbyLoot(): void {
    const activeRun = this.store.getState().save.session.activeRun

    if (!activeRun || activeRun.status !== 'active') {
      return
    }

    const nearestDrop = this.findNearbyGroundLoot()

    if (!nearestDrop) {
      this.hud.showToast('附近无可拾取物资', '靠近掉落物后按 E 才能装入携行网格', 0.8)
      return
    }

    const placement = placeItemInGrid(this.runInventory.columns, this.runInventory.rows, this.runInventory.items, nearestDrop.item)
    const definition = itemById[nearestDrop.item.itemId]

    if (!placement.placed) {
      this.hud.showToast('携行网格已满', `${definition?.label ?? '该物资'} 当前无法装入`, 1)
      return
    }

    this.updateRunInventoryItems(placement.items, null)
    this.groundLoot = this.groundLoot.filter((drop) => drop.id !== nearestDrop.id)
    this.recordGroundLootPickup(nearestDrop)
    this.flushRunSnapshot()
  }

  private finalizeRun(outcome: Extract<RunResolutionOutcome, 'boss-clear' | 'down'>): void {
    if (this.pendingOutcome) {
      return
    }
    this.pendingOutcome = outcome
    this.store.markRunOutcome(outcome, this.buildRunSnapshot())
  }

  private flushRunSnapshot(): void {
    if (!this.pendingOutcome) {
      this.store.syncActiveRun(this.buildRunSnapshot())
    }
  }

  private buildRunSnapshot(): ActiveRunSnapshotInput {
    const baseMap = this.getLiveRunMap()
    const encounterState = this.encounter.getEncounterState()
    const boss = this.encounter.getBoss()
    const currentWave = this.encounter.getWaveIndex()
    const mapHighestWave = Math.max(this.zoneHighestWave, currentWave)
    const statsHighestWave = Math.max(this.runHighestWave, currentWave)

    return {
      inventory: {
        columns: this.runInventory.columns,
        rows: this.runInventory.rows,
        items: this.runInventory.items.map((item) => ({ ...item })),
        quickSlots: [...this.runInventory.quickSlots],
      },
      groundLoot: this.groundLoot.map((drop) => ({
        ...drop,
        item: { ...drop.item },
      })),
      player: {
        health: this.playerHealth,
        maxHealth: PLAYER_MAX_HEALTH,
        currentWeaponId: this.playerController.getCurrentWeapon().id,
        loadoutWeaponIds: [...this.loadoutWeaponIds],
        shotsFired: this.shotsFired,
        grenadesThrown: this.grenadesThrown,
        dashesUsed: this.dashesUsed,
        damageTaken: this.damageTaken,
      },
      map: {
        ...baseMap,
        currentWave,
        highestWave: mapHighestWave,
        hostilesRemaining: encounterState === 'clear' ? 0 : this.encounter.getEnemies().length + this.encounter.getPendingSpawnCount(),
        boss: boss
          ? { spawned: true, defeated: false, label: boss.definition.label, phase: boss.phase, health: boss.health, maxHealth: boss.definition.maxHealth }
          : {
              spawned: encounterState === 'clear' || baseMap.boss.spawned || this.pendingOutcome === 'boss-clear',
              defeated: encounterState === 'clear' || baseMap.boss.defeated || this.pendingOutcome === 'boss-clear',
              label: encounterState === 'clear' || baseMap.boss.spawned || this.pendingOutcome === 'boss-clear' ? baseMap.boss.label ?? hostileByType.boss.label : null,
              phase: encounterState === 'clear' ? baseMap.boss.phase ?? 2 : baseMap.boss.phase,
              health: encounterState === 'clear' ? 0 : baseMap.boss.health,
              maxHealth: encounterState === 'clear' || baseMap.boss.spawned || this.pendingOutcome === 'boss-clear' ? baseMap.boss.maxHealth ?? hostileByType.boss.maxHealth : null,
            },
      },
      resources: buildResourceLedgerFromItems(this.runInventory.items),
      lootEntries: [...this.lootEntries],
      stats: {
        elapsedSeconds: Number(this.runElapsedSeconds.toFixed(2)),
        kills: this.runKills,
        highestWave: statsHighestWave,
        extracted: false,
        bossDefeated: encounterState === 'clear' || this.pendingOutcome === 'boss-clear',
      },
    }
  }

  private spawnRing(x: number, y: number, startRadius: number, endRadius: number, duration: number, color: number, width: number): void {
    this.burstRings.push({ x, y, age: 0, duration, startRadius, endRadius, color, width })
  }

  private addShake(intensity: number): void {
    this.shakeTrauma = Math.min(1, this.shakeTrauma + intensity)
  }

  private applyCameraTransform(): void {
    const playerPosition = this.player.getPosition()
    const desiredX = this.viewport.width * 0.5 - playerPosition.x
    const desiredY = this.viewport.height * 0.5 - playerPosition.y
    const clampedX = clamp(desiredX, this.viewport.width - this.worldBounds.right, -this.worldBounds.left)
    const clampedY = clamp(desiredY, this.viewport.height - this.worldBounds.bottom, -this.worldBounds.top)
    const shakePower = this.shakeTrauma * this.shakeTrauma * 14
    const shakeX = this.shakeTrauma > 0.0001 ? Math.sin(this.visualTime * 48) * shakePower * 0.72 : 0
    const shakeY = this.shakeTrauma > 0.0001 ? Math.cos(this.visualTime * 44) * shakePower : 0
    this.cameraRoot.position.set(clampedX + shakeX, clampedY + shakeY)
  }

  private buildCameraBounds(): ArenaBounds {
    const left = clamp(-this.cameraRoot.position.x, this.worldBounds.left, Math.max(this.worldBounds.left, this.worldBounds.right - this.viewport.width))
    const top = clamp(-this.cameraRoot.position.y, this.worldBounds.top, Math.max(this.worldBounds.top, this.worldBounds.bottom - this.viewport.height))
    return { left, top, right: Math.min(this.worldBounds.right, left + this.viewport.width), bottom: Math.min(this.worldBounds.bottom, top + this.viewport.height) }
  }

  private screenToWorld(screenX: number, screenY: number): { x: number; y: number } {
    return { x: screenX - this.cameraRoot.position.x, y: screenY - this.cameraRoot.position.y }
  }

  private drawAimLayer(showReticle: boolean): void {
    const playerPosition = this.player.getPosition()
    const weapon = this.playerController.getCurrentWeapon()
    const aimPoint = this.playerController.getLastAimPoint()
    this.aimGuide.clear()
    this.reticle.clear()

    if (!showReticle) {
      return
    }

    this.aimGuide.moveTo(playerPosition.x, playerPosition.y).lineTo(aimPoint.x, aimPoint.y).stroke({ width: weapon.id === 'sniper' ? 1.6 : 1.2, color: palette.reticle, alpha: 0.2, cap: 'round' })

    if (weapon.id === 'grenade') {
      this.aimGuide.circle(playerPosition.x, playerPosition.y, weapon.range).stroke({ width: 1.5, color: palette.frame, alpha: 0.18, alignment: 0.5 })
      this.reticle.circle(aimPoint.x, aimPoint.y, weapon.splashRadius ?? 66).stroke({ width: 2, color: palette.accent, alpha: 0.38, alignment: 0.5 }).circle(aimPoint.x, aimPoint.y, 5).fill({ color: palette.accent, alpha: 0.94 })
      return
    }

    const outerRadius = weapon.id === 'sniper' ? 16 : 11
    this.reticle.circle(aimPoint.x, aimPoint.y, outerRadius).stroke({ width: 2, color: palette.accentSoft, alpha: 0.72, alignment: 0.5 }).circle(aimPoint.x, aimPoint.y, 2).fill({ color: palette.accent, alpha: 0.96 })
  }

  private drawEffects(): void {
    this.effects.clear()
    for (const ring of this.burstRings) {
      const progress = ring.age / ring.duration
      this.effects.circle(ring.x, ring.y, lerp(ring.startRadius, ring.endRadius, progress)).stroke({ width: Math.max(1, ring.width - progress * 2), color: ring.color, alpha: 1 - progress, alignment: 0.5 })
    }
    for (const projectile of this.needleProjectiles) {
      const progress = projectile.age / projectile.duration
      const alpha = 1 - progress
      const headX = lerp(projectile.startX, projectile.endX, progress)
      const headY = lerp(projectile.startY, projectile.endY, progress)
      const tailX = headX - projectile.dirX * projectile.length
      const tailY = headY - projectile.dirY * projectile.length
      this.effects.moveTo(tailX, tailY).lineTo(headX, headY).stroke({ width: projectile.width, color: projectile.color, alpha, cap: 'round' })
      this.effects.moveTo(tailX, tailY).lineTo(headX, headY).stroke({ width: Math.max(1, projectile.width * 0.34), color: projectile.coreColor, alpha: alpha * 0.68, cap: 'round' })
    }
    for (const projectile of this.encounter.getEnemyProjectiles()) {
      this.effects.circle(projectile.x, projectile.y, projectile.radius * 1.5).fill({ color: projectile.glowColor, alpha: 0.16 })
      this.effects.circle(projectile.x, projectile.y, projectile.radius).fill({ color: projectile.color, alpha: 0.94 })
    }
    for (const projectile of this.grenadeProjectiles) {
      const progress = projectile.age / projectile.duration
      const travelX = lerp(projectile.startX, projectile.endX, progress)
      const travelY = lerp(projectile.startY, projectile.endY, progress) - Math.sin(progress * Math.PI) * 28
      this.effects.circle(travelX, travelY, 7).fill({ color: palette.accent, alpha: 0.96 })
      this.effects.circle(travelX, travelY, 3).fill({ color: palette.arenaCore, alpha: 0.9 })
    }

    if (this.layout) {
      drawWorldMarkers(this.markerLayer, this.layout.markers, this.visualTime, this.getFocusedMarkerId())
    }
  }

  private drawBackdrop(): void {
    this.backdrop.clear().rect(0, 0, this.viewport.width, this.viewport.height).fill({ color: palette.bgOuter })
  }

  private layoutUi(): void {
    this.minimapTitle.position.set(38, 20)
    this.locationText.position.set(this.viewport.width - 26, 18)
    this.interactionText.position.set(this.viewport.width * 0.5, this.viewport.height - 59)
    this.drawInteractionPromptPanel()
  }

  private updateMarkerLabels(highlightedMarkerId: string | null): void {
    this.layout?.markers.forEach((marker, index) => {
      const label = this.markerLabelLayer.children[index] as Text | undefined

      if (!label) {
        return
      }

      const focused = marker.id === highlightedMarkerId
      label.tint = focused ? getMarkerEmphasisTint(marker.kind) : palette.uiText
      label.alpha = focused ? 0.98 : 0.76
      label.scale.set(focused ? 1.04 : 1)
      label.position.set(marker.x, marker.y + (focused ? 26 : 22))
    })
  }

  private drawPanel(): void {
    this.panel.visible = this.panelOpen
    this.panelTitle.visible = this.panelOpen
    this.panelBody.visible = this.panelOpen
    this.panelLootTitle.visible = this.panelOpen
    this.panelGridTitle.visible = this.panelOpen
    this.panelFooter.visible = this.panelOpen
    if (!this.panelOpen) {
      return
    }

    const layout = this.getCombatPanelLayout()
    const nearbyGroundLoot = this.getNearbyGroundLootGridState(layout.groundGrid)
    const visibleGroundCount = nearbyGroundLoot.totalCount - nearbyGroundLoot.hiddenCount

    drawFullScreenPanelFrame(this.panel, {
      screenWidth: this.viewport.width,
      screenHeight: this.viewport.height,
      x: layout.frame.x,
      y: layout.frame.y,
      width: layout.frame.width,
      height: layout.frame.height,
      headerHeight: layout.frame.headerHeight,
      footerHeight: layout.frame.footerHeight,
    })
    this.drawPanelSection(layout.summaryColumn)
    this.drawPanelSection(layout.groundColumn)
    this.drawPanelSection(layout.inventoryColumn)
    this.panelTitle.position.set(layout.frame.x + 26, layout.frame.y + 24)
    this.panelBody.position.set(layout.summaryColumn.x + 16, layout.summaryColumn.y + 58)
    this.panelBody.style.wordWrapWidth = layout.summaryColumn.width - 32
    this.panelLootTitle.position.set(layout.groundColumn.x + 16, layout.groundColumn.y + 14)
    this.panelLootTitle.text = nearbyGroundLoot.hiddenCount > 0 ? `附近掉落 ${visibleGroundCount} / ${nearbyGroundLoot.totalCount}` : `附近掉落 ${nearbyGroundLoot.totalCount}`
    this.panelGridTitle.position.set(layout.inventoryColumn.x + 16, layout.inventoryColumn.y + 14)
    this.panelGridTitle.text = `携行网格 ${getInventoryUsedCells(this.runInventory.items)} / ${getInventoryCapacity(this.runInventory.columns, this.runInventory.rows)}`
    drawInventoryGrid(this.panel, {
      ...layout.groundGrid,
      items: nearbyGroundLoot.items,
    })
    drawInventoryGrid(this.panel, {
      ...layout.inventoryGrid,
      items: this.runInventory.items,
    })
    this.drawHeldInventoryPreview(layout.inventoryGrid, layout.groundGrid)
    this.panelFooter.position.set(layout.frame.x + 26, layout.frame.y + layout.frame.height - layout.frame.footerHeight + 16)
    this.panelFooter.text = '左键按住拖拽 · 松开释放 · 1-4 绑定快捷栏 · R 旋转 · F 自动整理 · Tab / I 关闭'
    this.panelBody.text = this.buildPanelText(nearbyGroundLoot)
  }

  private buildPanelText(nearbyGroundLoot: NearbyGroundLootPanelState): string {
    const activeRun = this.store.getState().save.session.activeRun
    const currentZone = activeRun ? getCurrentRunZone(activeRun.map) : null
    const lootLines = this.lootEntries.slice(-5).map((entry) => `- ${entry.label}`).join('\n') || '暂无拾取记录'
    const inventoryLines = this.runInventory.items.slice(0, 6).map((item) => `- ${(itemById[item.itemId]?.shortLabel ?? item.itemId)} x${item.quantity}`).join('\n') || '暂无已装入物资'
    const heldLabel = this.heldInventoryItem ? `${itemById[this.heldInventoryItem.itemId]?.label ?? this.heldInventoryItem.itemId} x${this.heldInventoryItem.quantity} / ${this.heldItemOrigin === 'ground' ? '来自地面' : '来自携行格'}` : '无'
    const nearbyLabel = nearbyGroundLoot.hiddenCount > 0 ? `${nearbyGroundLoot.totalCount} 份（面板显示 ${nearbyGroundLoot.totalCount - nearbyGroundLoot.hiddenCount} 份）` : `${nearbyGroundLoot.totalCount} 份`
    const quickSlotLines = this.runInventory.quickSlots.map((itemId, index) => `- ${index + 1}: ${this.getQuickSlotLabel(itemId)}`).join('\n')
    return [
      `当前路线：${activeRun ? getWorldRoute(activeRun.map.routeId).label : '未部署'}`,
      `当前区域：${currentZone?.label ?? '未进入'}`,
      `状态：${activeRun?.status === 'awaiting-settlement' ? '等待结算' : '行动中'}`,
      `背包占位：${getInventoryUsedCells(this.runInventory.items)} / ${getInventoryCapacity(this.runInventory.columns, this.runInventory.rows)}`,
      `附近掉落：${nearbyLabel}`,
      `手持物资：${heldLabel}`,
      `本局资源：废料 ${this.runResources.salvage} / 合金 ${this.runResources.alloy} / 研究 ${this.runResources.research}`,
      '',
      '操作 //',
      '- 左键按住物资并拖到目标格，松开后完成放置',
      '- 悬停可用物资或拿起已装入的可用物资后，按 1-4 绑定快捷栏',
      '- 战斗中按 Z / X / C / V 使用已绑定的快捷栏物资',
      '- F 自动整理携行网格，尽量把手中物资一起归位',
      '- 将手中物资拖到掉落区后松开，可丢回地面',
      '',
      '快捷栏 //',
      quickSlotLines,
      '',
      '已装入 //',
      inventoryLines,
      '',
      '最近拾取 //',
      lootLines,
    ].join('\n')
  }

  private getGroundLootGridLayout() {
    return this.getCombatPanelLayout().groundGrid
  }

  private getInventoryGridLayout() {
    return this.getCombatPanelLayout().inventoryGrid
  }

  private handleInventoryPanelPointerPressed(pointerX: number, pointerY: number): void {
    if (this.heldInventoryItem) {
      return
    }

    const groundLayout = this.getGroundLootGridLayout()
    const groundCell = resolveInventoryCellAtPoint(groundLayout, pointerX, pointerY)

    if (groundCell) {
      this.handleGroundLootGridPointerPressed(groundCell, groundLayout)
      return
    }

    const inventoryLayout = this.getInventoryGridLayout()
    const inventoryCell = resolveInventoryCellAtPoint(inventoryLayout, pointerX, pointerY)

    if (!inventoryCell) {
      return
    }

    this.handleRunInventoryGridPointerPressed(inventoryCell)
  }

  private handleInventoryPanelPointerReleased(pointerX: number, pointerY: number): void {
    if (!this.heldInventoryItem) {
      return
    }

    const inventoryLayout = this.getInventoryGridLayout()
    const inventoryCell = resolveInventoryCellAtPoint(inventoryLayout, pointerX, pointerY)

    if (inventoryCell) {
      if (this.tryPlaceHeldInventoryItemAtInventoryCell(inventoryCell)) {
        return
      }

      this.restoreHeldInventoryItem()
      return
    }

    const groundLayout = this.getGroundLootGridLayout()
    const groundCell = resolveInventoryCellAtPoint(groundLayout, pointerX, pointerY)

    if (groundCell) {
      this.releaseHeldInventoryItemToGround()
      return
    }

    this.restoreHeldInventoryItem()
  }

  private handleGroundLootGridPointerPressed(targetCell: { x: number; y: number }, groundLayout: ReturnType<CombatSandboxScene['getGroundLootGridLayout']>): void {
    const nearbyGroundLoot = this.getNearbyGroundLootGridState(groundLayout)
    const targetItem = findItemAtCell(nearbyGroundLoot.items, targetCell.x, targetCell.y)

    if (!targetItem) {
      return
    }

    const drop = nearbyGroundLoot.dropByItemId.get(targetItem.id)

    if (!drop) {
      return
    }

    this.heldInventoryItem = { ...drop.item }
    this.heldItemOrigin = 'ground'
    this.heldInventoryRestoreItem = null
    this.heldGroundRestoreDrop = {
      ...drop,
      item: { ...drop.item },
    }
    this.groundLoot = this.groundLoot.filter((entry) => entry.id !== drop.id)
  }

  private handleRunInventoryGridPointerPressed(targetCell: { x: number; y: number }): void {
    const extraction = pickItemFromGridAtCell(this.runInventory.items, targetCell.x, targetCell.y)

    if (!extraction.item) {
      return
    }

    this.heldInventoryItem = extraction.item
    this.heldItemOrigin = 'inventory'
    this.heldInventoryRestoreItem = { ...extraction.item }
    this.heldGroundRestoreDrop = null
    this.updateRunInventoryItems(extraction.items, extraction.item.id)
  }

  private tryPlaceHeldInventoryItemAtInventoryCell(targetCell: { x: number; y: number }): boolean {
    if (!this.heldInventoryItem) {
      return false
    }

    const placement = placeItemAtPosition(
      this.runInventory.columns,
      this.runInventory.rows,
      this.runInventory.items,
      this.heldInventoryItem,
      targetCell.x,
      targetCell.y,
    )

    if (!placement.placed) {
      return false
    }

    this.updateRunInventoryItems(placement.items)

    if (this.heldItemOrigin === 'ground' && this.heldGroundRestoreDrop) {
      this.recordGroundLootPickup(this.heldGroundRestoreDrop)
    }

    this.clearHeldInventoryState()
    this.flushRunSnapshot()
    return true
  }

  private rotateHeldInventoryItem(): void {
    if (!this.heldInventoryItem || this.heldInventoryItem.width === this.heldInventoryItem.height) {
      return
    }

    this.heldInventoryItem = rotateInventoryItem(this.heldInventoryItem)
  }

  private restoreHeldInventoryItem(): void {
    if (!this.heldInventoryItem) {
      return
    }

    if (this.heldItemOrigin === 'ground' && this.heldGroundRestoreDrop) {
      this.groundLoot = [
        ...this.groundLoot,
        {
          ...this.heldGroundRestoreDrop,
          item: { ...this.heldGroundRestoreDrop.item },
        },
      ]
      this.clearHeldInventoryState()
      return
    }

    const restoreItem = this.heldInventoryRestoreItem ?? this.heldInventoryItem
    const exactRestore = placeItemAtPosition(
      this.runInventory.columns,
      this.runInventory.rows,
      this.runInventory.items,
      restoreItem,
      restoreItem.x,
      restoreItem.y,
    )
    const fallbackRestore = exactRestore.placed
      ? exactRestore
      : placeItemInGrid(this.runInventory.columns, this.runInventory.rows, this.runInventory.items, restoreItem)

    if (fallbackRestore.placed) {
      this.updateRunInventoryItems(fallbackRestore.items)
    }

    this.clearHeldInventoryState()
  }

  private drawHeldInventoryPreview(
    inventoryLayout: { x: number; y: number; columns: number; rows: number; cellSize: number },
    groundLayout: { x: number; y: number; columns: number; rows: number; cellSize: number },
  ): void {
    if (!this.heldInventoryItem || !this.pointerScreen.hasPointer) {
      return
    }

    const inventoryCell = resolveInventoryCellAtPoint(inventoryLayout, this.pointerScreen.x, this.pointerScreen.y)

    if (inventoryCell) {
      const valid = canPlaceItemAtPosition(
        inventoryLayout.columns,
        inventoryLayout.rows,
        this.runInventory.items,
        this.heldInventoryItem,
        inventoryCell.x,
        inventoryCell.y,
      )

      drawFloatingInventoryItem(this.panel, {
        item: this.heldInventoryItem,
        cellSize: inventoryLayout.cellSize,
        x: inventoryLayout.x + inventoryCell.x * inventoryLayout.cellSize,
        y: inventoryLayout.y + inventoryCell.y * inventoryLayout.cellSize,
        valid,
      })
      return
    }

    const groundCell = resolveInventoryCellAtPoint(groundLayout, this.pointerScreen.x, this.pointerScreen.y)

    if (groundCell) {
      drawFloatingInventoryItem(this.panel, {
        item: this.heldInventoryItem,
        cellSize: groundLayout.cellSize,
        x: groundLayout.x + groundCell.x * groundLayout.cellSize,
        y: groundLayout.y + groundCell.y * groundLayout.cellSize,
        valid: true,
      })
      return
    }

    drawFloatingInventoryItem(this.panel, {
      item: this.heldInventoryItem,
      cellSize: inventoryLayout.cellSize,
      x: this.pointerScreen.x - this.heldInventoryItem.width * inventoryLayout.cellSize * 0.5,
      y: this.pointerScreen.y - this.heldInventoryItem.height * inventoryLayout.cellSize * 0.5,
      valid: false,
    })
  }

  private getNearbyGroundLootGridState(
    layout = this.getGroundLootGridLayout(),
    radius = PANEL_GROUND_RADIUS,
  ): NearbyGroundLootPanelState {
    const nearbyDrops = this.findNearbyGroundLootDrops(radius)
    const placement = placeItemsInGrid(
      layout.columns,
      layout.rows,
      [],
      nearbyDrops.map((drop) => ({ ...drop.item })),
    )
    const visibleIds = new Set(placement.placedIds)
    const dropByItemId = new Map<string, GroundLootDrop>()

    for (const drop of nearbyDrops) {
      if (visibleIds.has(drop.item.id)) {
        dropByItemId.set(drop.item.id, drop)
      }
    }

    return {
      totalCount: nearbyDrops.length,
      hiddenCount: nearbyDrops.length - placement.placedIds.length,
      items: placement.items,
      dropByItemId,
    }
  }

  private recordGroundLootPickup(drop: GroundLootDrop): void {
    const definition = itemById[drop.item.itemId]

    this.lootEntries = dedupeLootEntries([
      ...this.lootEntries,
      {
        id: drop.item.id,
        definitionId: drop.item.itemId,
        label: `${definition?.label ?? '未知物资'} x${drop.item.quantity}`,
        category: definition?.category ?? 'resource',
        quantity: drop.item.quantity,
        source: drop.source,
        acquiredAtWave: Math.max(0, this.encounter.getWaveIndex()),
        acquiredAtSeconds: Number(this.runElapsedSeconds.toFixed(2)),
      },
    ])
    this.hud.showToast('已拾取物资', `${definition?.label ?? '未知物资'} x${drop.item.quantity}`, 0.95)
  }

  private releaseHeldInventoryItemToGround(): void {
    if (!this.heldInventoryItem) {
      return
    }

    if (this.heldItemOrigin === 'ground' && this.heldGroundRestoreDrop) {
      this.groundLoot = [
        ...this.groundLoot,
        {
          ...this.heldGroundRestoreDrop,
          item: { ...this.heldGroundRestoreDrop.item },
        },
      ]
      this.clearHeldInventoryState()
      return
    }

    const playerPosition = this.player.getPosition()
    const offset = DROP_RELEASE_OFFSETS[this.groundLoot.length % DROP_RELEASE_OFFSETS.length]
    const definition = itemById[this.heldInventoryItem.itemId]

    this.groundLoot = [
      ...this.groundLoot,
      {
        id: this.heldInventoryItem.id,
        item: { ...this.heldInventoryItem },
        x: playerPosition.x + offset.x,
        y: playerPosition.y + offset.y,
        source: 'manual',
      },
    ]
    this.hud.showToast('已丢回地面', `${definition?.label ?? '未知物资'} x${this.heldInventoryItem.quantity}`, 0.9)
    this.updateRunInventoryItems(this.runInventory.items, null)
    this.clearHeldInventoryState()
    this.flushRunSnapshot()
  }

  private clearHeldInventoryState(): void {
    this.heldInventoryItem = null
    this.heldItemOrigin = null
    this.heldInventoryRestoreItem = null
    this.heldGroundRestoreDrop = null
  }

  private autoArrangeRunInventory(): void {
    const items = this.heldInventoryItem ? [...this.runInventory.items, this.heldInventoryItem] : this.runInventory.items
    const arrangement = autoArrangeInventory(this.runInventory.columns, this.runInventory.rows, items)

    if (!arrangement.arranged) {
      this.hud.showToast('自动整理失败', this.heldItemOrigin === 'ground' ? '当前背包仍装不下手中物资' : '当前布局无法完成重排', 0.95)
      return
    }

    this.updateRunInventoryItems(arrangement.items)

    if (this.heldItemOrigin === 'ground' && this.heldGroundRestoreDrop) {
      this.recordGroundLootPickup(this.heldGroundRestoreDrop)
    }

    this.clearHeldInventoryState()
    this.hud.showToast('已自动整理', '携行网格已按占位重新压缩', 0.9)
    this.flushRunSnapshot()
  }

  private updateRunInventoryItems(items: InventoryItemRecord[], preservedItemId: string | null = this.getHeldInventoryBindingId()): void {
    const nextItems = items.map((item) => ({ ...item }))
    const validItemIds = new Set(nextItems.map((item) => item.id))

    if (preservedItemId) {
      validItemIds.add(preservedItemId)
    }

    this.runInventory = {
      ...this.runInventory,
      items: nextItems,
      quickSlots: sanitizeQuickSlotBindings(this.runInventory.quickSlots, validItemIds),
    }
    this.runResources = buildResourceLedgerFromItems(this.runInventory.items)
  }

  private handleQuickSlotBind(slotNumber: 1 | 2 | 3 | 4): void {
    const slotIndex = slotNumber - 1
    const targetItem = this.resolveQuickSlotBindingTarget()

    if (!targetItem) {
      if (this.runInventory.quickSlots[slotIndex]) {
        this.runInventory = {
          ...this.runInventory,
          quickSlots: assignQuickSlotBinding(this.runInventory.quickSlots, slotIndex, null),
        }
        this.hud.showToast(`快捷栏 ${slotNumber}`, '已清空绑定', 0.8)
      } else {
        this.hud.showToast(`快捷栏 ${slotNumber}`, '悬停可直接使用的物资或先拿起对应物资后再绑定', 0.95)
      }
      return
    }

    const definition = itemById[targetItem.itemId]

    if (!definition?.use) {
      this.hud.showToast(`快捷栏 ${slotNumber}`, '当前只允许绑定可直接使用的消耗品', 0.95)
      return
    }

    const nextQuickSlots = assignQuickSlotBinding(this.runInventory.quickSlots, slotIndex, targetItem.id)
    const wasBound = nextQuickSlots[slotIndex] === targetItem.id

    this.runInventory = {
      ...this.runInventory,
      quickSlots: nextQuickSlots,
    }
    this.hud.showToast(
      `快捷栏 ${slotNumber}`,
      wasBound ? `已绑定 ${itemById[targetItem.itemId]?.label ?? targetItem.itemId}` : '已清空绑定',
      0.9,
    )
  }

  private tryUseQuickSlot(slotNumber: 1 | 2 | 3 | 4): void {
    const slotIndex = slotNumber - 1
    const itemId = this.runInventory.quickSlots[slotIndex]

    if (!itemId) {
      return
    }

    const item = this.runInventory.items.find((entry) => entry.id === itemId)

    if (!item) {
      this.runInventory = {
        ...this.runInventory,
        quickSlots: assignQuickSlotBinding(this.runInventory.quickSlots, slotIndex, null),
      }
      return
    }

    const definition = itemById[item.itemId]
    const use = definition?.use

    if (!definition || !use) {
      this.hud.showToast(`快捷栏 ${slotNumber}`, `${definition?.label ?? '该物资'} 不能直接使用`, 0.9)
      return
    }

    let used = false
    const effectLines: string[] = []

    if (use.heals) {
      const healed = this.applyPlayerHeal(use.heals)

      if (healed > 0) {
        used = true
        effectLines.push(`恢复 ${Math.round(healed)} 生命`)
      }
    }

    if (use.explosionDamage && use.explosionRadius) {
      const position = this.player.getPosition()
      this.encounter.applyExplosionDamage(position.x, position.y, use.explosionRadius, use.explosionDamage, this.encounterCallbacks)
      this.spawnRing(position.x, position.y, 18, use.explosionRadius, 0.24, palette.warning, 4)
      this.hud.pulseInfo(0.75)
      this.addShake(0.12)
      used = true
      effectLines.push(`震爆半径 ${Math.round(use.explosionRadius)}`)
    }

    if (use.refreshDash) {
      this.player.refreshDashCharge()
      this.hud.pulseWeapon(0.4)
      used = true
      effectLines.push('冲刺已刷新')
    }

    if (!used) {
      this.hud.showToast(`快捷栏 ${slotNumber}`, `${definition.label} 当前无需使用`, 0.8)
      return
    }

    const consumeResult = consumeInventoryItemById(this.runInventory.items, item.id)

    if (!consumeResult.consumed) {
      return
    }

    this.updateRunInventoryItems(consumeResult.items, null)
    this.hud.showToast(`快捷栏 ${slotNumber}`, `${definition.label} 已使用 · ${effectLines.join(' / ')}`, 0.95)
    this.flushRunSnapshot()
  }

  private resolveQuickSlotBindingTarget(): InventoryItemRecord | null {
    if (this.heldInventoryItem && this.heldItemOrigin === 'inventory') {
      return this.heldInventoryItem
    }

    const inventoryCell = resolveInventoryCellAtPoint(this.getInventoryGridLayout(), this.pointerScreen.x, this.pointerScreen.y)

    if (!inventoryCell) {
      return null
    }

    return findItemAtCell(this.runInventory.items, inventoryCell.x, inventoryCell.y)
  }

  private getQuickSlotLabel(itemId: string | null): string {
    if (!itemId) {
      return '空'
    }

    const item = this.runInventory.items.find((entry) => entry.id === itemId) ?? (this.heldInventoryItem?.id === itemId ? this.heldInventoryItem : null)

    if (!item) {
      return '空'
    }

    return `${itemById[item.itemId]?.shortLabel ?? item.itemId} x${item.quantity}`
  }

  private getHeldInventoryBindingId(): string | null {
    return this.heldItemOrigin === 'inventory' ? this.heldInventoryItem?.id ?? null : null
  }

  private getPanelFrame() {
    const margin = Math.max(16, Math.min(24, Math.floor(Math.min(this.viewport.width, this.viewport.height) * 0.03)))

    return {
      x: margin,
      y: margin,
      width: Math.max(320, this.viewport.width - margin * 2),
      height: Math.max(320, this.viewport.height - margin * 2),
      headerHeight: 84,
      footerHeight: 50,
    }
  }

  private getCombatPanelLayout(): CombatInventoryPanelLayout {
    const frame = this.getPanelFrame()
    const contentX = frame.x + 26
    const contentY = frame.y + frame.headerHeight + 24
    const contentWidth = frame.width - 52
    const contentHeight = frame.height - frame.headerHeight - frame.footerHeight - 48
    let summaryWidth = Math.max(240, Math.min(380, Math.floor(contentWidth * 0.34)))
    const columnGap = 24
    let columnWidth = Math.floor((contentWidth - summaryWidth - columnGap) / 2)

    if (columnWidth < 200) {
      summaryWidth = Math.max(180, contentWidth - columnGap - 400)
      columnWidth = Math.floor((contentWidth - summaryWidth - columnGap) / 2)
    }

    const groundColumnX = contentX + summaryWidth + columnGap
    const inventoryColumnX = groundColumnX + columnWidth + columnGap
    const inventoryColumnWidth = frame.x + frame.width - 26 - inventoryColumnX
    const cellSize = Math.max(
      18,
      Math.min(
        38,
        Math.floor((Math.min(columnWidth, inventoryColumnWidth) - 48) / PANEL_GROUND_COLUMNS),
        Math.floor((contentHeight - 96) / this.runInventory.rows),
      ),
    )
    const groundGridWidth = PANEL_GROUND_COLUMNS * cellSize
    const inventoryGridWidth = this.runInventory.columns * cellSize

    return {
      frame,
      summaryColumn: {
        x: contentX,
        y: contentY,
        width: summaryWidth,
        height: contentHeight,
      },
      groundColumn: {
        x: groundColumnX,
        y: contentY,
        width: columnWidth,
        height: contentHeight,
      },
      inventoryColumn: {
        x: inventoryColumnX,
        y: contentY,
        width: inventoryColumnWidth,
        height: contentHeight,
      },
      groundGrid: {
        x: groundColumnX + Math.max(16, Math.floor((columnWidth - groundGridWidth) * 0.5)),
        y: contentY + 58,
        columns: PANEL_GROUND_COLUMNS,
        rows: PANEL_GROUND_ROWS,
        cellSize,
      },
      inventoryGrid: {
        x: inventoryColumnX + Math.max(16, Math.floor((inventoryColumnWidth - inventoryGridWidth) * 0.5)),
        y: contentY + 58,
        columns: this.runInventory.columns,
        rows: this.runInventory.rows,
        cellSize,
      },
    }
  }

  private drawPanelSection(bounds: { x: number; y: number; width: number; height: number }): void {
    this.panel.roundRect(bounds.x, bounds.y, bounds.width, bounds.height, 14).fill({
      color: palette.uiActive,
      alpha: 0.26,
    })
    this.panel.roundRect(bounds.x, bounds.y, bounds.width, bounds.height, 14).stroke({
      width: 1.1,
      color: palette.frame,
      alpha: 0.24,
      alignment: 0.5,
    })
    this.panel.roundRect(bounds.x + 2, bounds.y + 2, bounds.width - 4, bounds.height - 4, 12).stroke({
      width: 1,
      color: palette.frameSoft,
      alpha: 0.12,
      alignment: 0.5,
    })
    this.panel.rect(bounds.x + 16, bounds.y + 42, bounds.width - 32, 1).fill({ color: palette.panelLine, alpha: 0.22 })
    drawCornerFrame(this.panel, bounds.x + 10, bounds.y + 10, bounds.width - 20, bounds.height - 20, 12, palette.frame, 0.18, 1)
  }

  private updateLocationLabel(): void {
    const activeRun = this.store.getState().save.session.activeRun
    if (!activeRun) {
      this.locationText.text = ''
      return
    }
    const zone = getCurrentRunZone(activeRun.map)
    this.locationText.text = `${getWorldRoute(activeRun.map.routeId).label} // ${zone?.label ?? '未知区域'}`
  }

  private updateInteractionPrompt(): void {
    if (!this.layout) {
      this.interactionText.text = ''
      this.drawInteractionPromptPanel()
      return
    }
    const nearestDrop = this.findNearbyGroundLoot()
    const nearestMarker = this.findNearbyMarker()
    const activeRun = this.store.getState().save.session.activeRun

    if (nearestDrop) {
      const definition = itemById[nearestDrop.item.itemId]
      const canPlace = placeItemInGrid(this.runInventory.columns, this.runInventory.rows, this.runInventory.items, nearestDrop.item).placed
      this.interactionText.text = canPlace
        ? `接近 ${definition?.label ?? '物资'}：按 E 拾取并装入携行网格`
        : `接近 ${definition?.label ?? '物资'}：携行网格已满，暂时无法拾取`
    } else if (!nearestMarker || !activeRun) {
      this.interactionText.text = ''
    } else if (nearestMarker.id === 'exit') {
      const zoneCleared = isCurrentRunZoneCleared(activeRun.map)

      if (zoneCleared || canExtractFromRunMap(activeRun.map)) {
        this.interactionText.text = `接近 ${nearestMarker.label}：出口已联通，可在右上角推进或撤离`
      } else {
        this.interactionText.text = `接近 ${nearestMarker.label}：出口锁定中，先清理当前区域`
      }
    } else if (nearestMarker.kind === 'objective') {
      this.interactionText.text = isCurrentRunZoneCleared(activeRun.map)
        ? `接近 ${nearestMarker.label}：区域已压制，转向出口完成推进或撤离`
        : `接近 ${nearestMarker.label}：压制当前区域核心后，再转向出口`
    } else {
      this.interactionText.text = `接近 ${nearestMarker.label}：这里会继续接入更细的场景交互`
    }

    this.drawInteractionPromptPanel()
  }

  private drawOverviewMap(): void {
    this.minimap.visible = !this.mapOpen
    this.minimapTitle.visible = !this.mapOpen
    this.locationText.visible = !this.mapOpen
    this.interactionPanel.visible = !this.mapOpen && !this.panelOpen && this.interactionText.text.length > 0
    this.interactionText.visible = !this.mapOpen
    this.hud.container.visible = !this.mapOpen
    this.overviewMap.visible = this.mapOpen
    this.overviewTitle.visible = this.mapOpen
    this.overviewMeta.visible = this.mapOpen
    this.overviewHint.visible = this.mapOpen

    if (!this.mapOpen || !this.layout) {
      this.overviewMap.clear()
      return
    }

    const width = this.viewport.width
    const height = this.viewport.height
    const x = 0
    const y = 0
    const activeRun = this.store.getState().save.session.activeRun
    const currentZone = activeRun ? getCurrentRunZone(activeRun.map) : null
    const focusedMarker = this.layout.markers.find((marker) => marker.id === this.getFocusedMarkerId()) ?? null

    drawMinimap(this.overviewMap, {
      x,
      y,
      width,
      height,
      bounds: this.layout.bounds,
      obstacles: this.layout.obstacles,
      player: this.player.getPosition(),
      enemies: this.encounter.getEnemies(),
      markers: this.layout.markers,
      cameraBounds: this.buildCameraBounds(),
      highlightedMarkerId: focusedMarker?.id ?? null,
    })
    drawMapOverlayPanel(this.overviewMap, 24, 18, 360, 78, palette.frame)
    drawMapOverlayPanel(this.overviewMap, width - 332, 18, 308, 92, focusedMarker?.kind === 'objective' ? palette.warning : palette.minimapMarker)
    drawMapOverlayPanel(this.overviewMap, 24, height - 132, 500, 100, palette.panelWarm)

    this.overviewTitle.position.set(40, 38)
    this.overviewMeta.position.set(width - 316, 38)
    this.overviewHint.position.set(40, height - 108)
    this.overviewTitle.text = `${getWorldRoute(activeRun?.map.routeId ?? this.getLiveRunMap().routeId).label} / 战区总地图`
    this.overviewMeta.text = `焦点目标：${focusedMarker?.label ?? '无'}\n图例：橙=你 · 红=敌人 · 绿=出口 · 黄=核心`
    this.overviewHint.text = currentZone
      ? `当前区域：${currentZone.label}\n蓝框为当前镜头，优先看焦点目标，再决定推进或撤离。`
      : '蓝框为当前镜头，优先看焦点目标，再决定推进或撤离。'
  }

  private drawMinimap(): void {
    if (!this.layout) {
      return
    }
    const focusedMarkerId = this.getFocusedMarkerId()
    drawMinimap(this.minimap, {
      x: 20,
      y: 12,
      width: 196,
      height: 196,
      bounds: this.layout.bounds,
      viewBounds: createFocusedViewBounds(this.layout.bounds, this.player.getPosition(), 820, 820),
      obstacles: this.layout.obstacles,
      player: this.player.getPosition(),
      enemies: this.encounter.getEnemies(),
      markers: this.layout.markers,
      highlightedMarkerId: focusedMarkerId,
    })
  }

  private syncSceneRuntime(): void {
    const activeRun = this.store.getState().save.session.activeRun

    if (!activeRun || !this.layout) {
      this.store.clearSceneRuntime()
      return
    }

    const nearestMarker = this.findNearbyMarker()
    const atExit = nearestMarker?.id === 'exit'
    const canExtract = activeRun.status === 'active' && canExtractFromRunMap(activeRun.map)
    const canAdvance = activeRun.status === 'active' && isCurrentRunZoneCleared(activeRun.map) && Boolean(getNextRunZone(activeRun.map))
    const ready = activeRun.status === 'awaiting-settlement' ? true : atExit && (canExtract || canAdvance)
    let hint = ''

    if (activeRun.status === 'awaiting-settlement') {
      hint = '本局已结束，可直接结算返回基地。'
    } else if (ready && canAdvance) {
      hint = `已到达 ${nearestMarker?.label ?? '出口'}，可推进下一区域。`
    } else if (ready && canExtract) {
      hint = `已到达 ${nearestMarker?.label ?? '撤离出口'}，可执行撤离。`
    } else if (canAdvance) {
      hint = '当前区域已清理，前往出口后才能推进。'
    } else if (canExtract) {
      hint = '前往撤离出口后才能离开当前副本。'
    } else if (atExit) {
      hint = '出口尚未启用，先完成当前区域的压制。'
    } else if (nearestMarker?.kind === 'objective') {
      hint = '核心位置只用于战斗目标，推进或撤离必须去真正出口。'
    } else if (this.findNearbyGroundLoot()) {
      hint = '靠近掉落物后按 E，可装入携行网格。'
    } else {
      hint = '继续探索，找到出口并决定推进或撤离。'
    }

    this.store.updateSceneRuntime({
      primaryActionReady: ready,
      primaryActionHint: hint,
      nearbyMarkerId: nearestMarker?.id ?? null,
      nearbyMarkerLabel: nearestMarker?.label ?? null,
      nearbyMarkerKind: nearestMarker?.kind ?? null,
      mapOverlayOpen: this.mapOpen,
    })
  }

  private findNearbyMarker(radius = 126) {
    if (!this.layout) {
      return null
    }

    const playerPosition = this.player.getPosition()
    let nearestMarker: WorldMapLayout['markers'][number] | null = null
    let nearestDistance = radius

    for (const marker of this.layout.markers) {
      const distance = Math.hypot(marker.x - playerPosition.x, marker.y - playerPosition.y)

      if (distance > nearestDistance) {
        continue
      }

      nearestMarker = marker
      nearestDistance = distance
    }

    return nearestMarker
  }

  private findNearbyGroundLoot(radius = 92): GroundLootDrop | null {
    return this.findNearbyGroundLootDrops(radius)[0] ?? null
  }

  private findNearbyGroundLootDrops(radius = 92): GroundLootDrop[] {
    const playerPosition = this.player.getPosition()
    return this.groundLoot
      .map((drop) => ({
        drop,
        distance: Math.hypot(drop.x - playerPosition.x, drop.y - playerPosition.y),
      }))
      .filter((entry) => entry.distance <= radius)
      .sort((left, right) => left.distance - right.distance)
      .map((entry) => entry.drop)
  }

  private drawInteractionPromptPanel(): void {
    this.interactionPanel.clear()
    this.interactionPanel.visible = !this.mapOpen && !this.panelOpen && this.interactionText.text.length > 0

    if (!this.interactionPanel.visible) {
      return
    }

    const maxWidth = Math.max(240, this.viewport.width - 48)
    const width = Math.min(maxWidth, Math.max(420, this.interactionText.width + 44))
    const height = 42
    const x = Math.round((this.viewport.width - width) * 0.5)
    const y = Math.round(this.viewport.height - 78)

    this.interactionPanel.roundRect(x + 4, y + 6, width, height, 12).fill({ color: palette.obstacleShadow, alpha: 0.08 })
    this.interactionPanel.roundRect(x, y, width, height, 12).fill({ color: palette.uiPanel, alpha: 0.78 })
    this.interactionPanel.roundRect(x + 12, y + 10, width - 24, 16, 8).fill({ color: palette.arenaCore, alpha: 0.16 })
    this.interactionPanel.rect(x + 16, y + 19, 12, 2).fill({ color: palette.panelWarm, alpha: 0.82 })
  }

  private getFocusedMarkerId(): string | null {
    if (!this.layout) {
      return null
    }

    const nearestMarker = this.findNearbyMarker()

    if (nearestMarker) {
      return nearestMarker.id
    }

    const activeRun = this.store.getState().save.session.activeRun

    if (!activeRun) {
      return this.layout.markers[0]?.id ?? null
    }

    if (activeRun.status === 'awaiting-settlement' || isCurrentRunZoneCleared(activeRun.map) || canExtractFromRunMap(activeRun.map)) {
      return 'exit'
    }

    return 'objective'
  }
}

function buildRunStructureKey(run: RunState | null): string {
  if (!run) {
    return 'none'
  }
  return [run.id, run.map.routeId, run.map.currentZoneId, run.map.layoutSeed, run.status, run.pendingOutcome ?? 'active'].join('|')
}

function removeExpired<T extends { age: number; duration: number }>(items: T[]): void {
  for (let index = items.length - 1; index >= 0; index -= 1) {
    if (items[index].age >= items[index].duration) {
      items.splice(index, 1)
    }
  }
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value))
}

function lerp(start: number, end: number, t: number): number {
  return start + (end - start) * t
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

function getMarkerEmphasisTint(kind: 'entry' | 'objective' | 'extraction' | 'locker' | 'station'): number {
  if (kind === 'objective' || kind === 'locker') {
    return palette.warning
  }
  if (kind === 'extraction') {
    return palette.minimapMarker
  }
  if (kind === 'station') {
    return palette.frame
  }
  return palette.accent
}
