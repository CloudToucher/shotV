import { Container, Graphics, Text } from 'pixi.js'

import type { GameStore } from '../../app/gameStore'
import type { ArenaBounds, GameScene, InputSnapshot, ViewportSize } from '../core/contracts'
import { itemById } from '../data/items'
import { weaponLoadout } from '../data/weapons'
import type { WeaponType } from '../data/types'
import { PlayerAvatar } from '../entities/PlayerAvatar'
import { autoArrangeInventory, canPlaceItemAtPosition, getInventoryCapacity, getInventoryUsedCells, pickItemFromGridAtCell, placeItemAtPosition, placeItemInGrid, rotateInventoryItem } from '../inventory/grid'
import type { InventoryItemRecord } from '../inventory/types'
import { palette } from '../theme/palette'
import { drawFloatingInventoryItem, drawInventoryGrid, resolveInventoryCellAtPoint } from '../ui/inventorySurface'
import { createFocusedViewBounds, createTextStyle, drawCornerFrame, drawFullScreenPanelFrame, drawMapOverlayPanel, drawMinimap, drawWorldMarkers, drawWorldObstacles, drawWorldSurface } from '../ui/surface'
import { createBaseWorldLayout } from '../world/layout'
import { getWorldRoute, worldRoutes } from '../world/routes'

type BasePanelMode = 'overview' | 'locker' | 'workshop' | 'command' | 'launch'

interface BaseStashPanelLayout {
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
  gridColumn: {
    x: number
    y: number
    width: number
    height: number
  }
  grid: {
    x: number
    y: number
    columns: number
    rows: number
    cellSize: number
  }
}

interface BasePanelActionRegion {
  id: string
  x: number
  y: number
  width: number
  height: number
}

export class BaseCampScene implements GameScene {
  readonly container = new Container()

  private readonly store: GameStore
  private readonly layout = createBaseWorldLayout()
  private readonly backdrop = new Graphics()
  private readonly cameraRoot = new Container()
  private readonly terrain = new Graphics()
  private readonly obstacleLayer = new Graphics()
  private readonly markerLayer = new Graphics()
  private readonly labels = new Container()
  private readonly minimap = new Graphics()
  private readonly minimapTitle = new Text({ text: '基地小地图', style: createTextStyle(12, palette.uiText, { fontWeight: '700' }) })
  private readonly overviewMap = new Graphics()
  private readonly overviewTitle = new Text({ text: '基地总地图', style: createTextStyle(18, palette.uiText, { fontWeight: '700' }) })
  private readonly overviewMeta = new Text({ text: '', style: createTextStyle(12, palette.uiMuted, { lineHeight: 20, align: 'left' }) })
  private readonly overviewHint = new Text({ text: '', style: createTextStyle(12, palette.uiMuted, { lineHeight: 20 }) })
  private readonly title = new Text({ text: '', style: createTextStyle(18, palette.uiText, { fontWeight: '700' }) })
  private readonly hintPanel = new Graphics()
  private readonly hint = new Text({ text: '', style: createTextStyle(12, palette.uiText, { fontWeight: '700', letterSpacing: 0.2 }) })
  private readonly panel = new Graphics()
  private readonly panelActionLayer = new Container()
  private readonly panelTitle = new Text({ text: '基地与背包', style: createTextStyle(22, palette.uiText, { fontWeight: '700', letterSpacing: 0.6 }) })
  private readonly panelBody = new Text({ text: '', style: createTextStyle(13, palette.uiMuted, { lineHeight: 23, wordWrap: true, wordWrapWidth: 214 }) })
  private readonly panelGridTitle = new Text({ text: '', style: createTextStyle(12, palette.uiText, { fontWeight: '700', letterSpacing: 0.8 }) })
  private readonly panelFooter = new Text({ text: 'Tab / I 关闭', style: createTextStyle(12, palette.uiMuted, { letterSpacing: 0.5 }) })
  private readonly player = new PlayerAvatar()

  private viewport: ViewportSize = { width: 0, height: 0 }
  private worldBounds: ArenaBounds = this.layout.bounds
  private panelOpen = false
  private panelMode: BasePanelMode = 'overview'
  private mapOpen = false
  private heldStashItem: InventoryItemRecord | null = null
  private heldStashRestoreItem: InventoryItemRecord | null = null
  private pointerScreen = { x: 0, y: 0, hasPointer: false }
  private panelActionRegions: BasePanelActionRegion[] = []
  private selectedWorkshopSlot = 0

  constructor(store: GameStore) {
    this.store = store
    this.title.anchor.set(0.5, 0)
    this.hint.anchor.set(0.5, 0)
    this.panelGridTitle.anchor.set(0, 0)
    this.container.addChild(
      this.backdrop,
      this.cameraRoot,
      this.minimap,
      this.minimapTitle,
      this.overviewMap,
      this.overviewTitle,
      this.overviewMeta,
      this.overviewHint,
      this.title,
      this.hintPanel,
      this.hint,
      this.panel,
      this.panelActionLayer,
      this.panelTitle,
      this.panelBody,
      this.panelGridTitle,
      this.panelFooter,
    )
    this.cameraRoot.addChild(this.terrain, this.obstacleLayer, this.markerLayer, this.labels, this.player.container)
    this.player.setPosition(this.layout.playerSpawn.x, this.layout.playerSpawn.y)
    this.player.setWeaponStyle('machineGun')
    this.rebuildWorld()
  }

  resize(viewport: ViewportSize): void {
    this.viewport = viewport
    this.drawBackdrop()
    this.layoutUi()
  }

  update(deltaSeconds: number, elapsedSeconds: number, input: InputSnapshot): void {
    this.pointerScreen = {
      x: input.pointerX,
      y: input.pointerY,
      hasPointer: input.hasPointer,
    }

    if (input.panelTogglePressed) {
      if (!this.panelOpen) {
        this.panelOpen = true
        this.panelMode = 'overview'
        this.mapOpen = false
      } else if (this.panelMode !== 'overview') {
        this.restoreHeldStashItem()
        this.panelMode = 'overview'
        this.mapOpen = false
      } else {
        this.restoreHeldStashItem()
        this.panelOpen = false
      }
    }

    if (input.mapTogglePressed) {
      this.mapOpen = !this.mapOpen
      if (this.mapOpen) {
        this.restoreHeldStashItem()
        this.panelOpen = false
      }
    }

    if (input.interactPressed) {
      this.handleNearbyInteraction()
    }

    if (this.panelOpen && !this.mapOpen) {
      if (this.isStashGridVisible()) {
        if (input.pointerPressed) {
          this.handlePanelPointerPressed(input.pointerX, input.pointerY)
        }

        if (input.pointerReleased) {
          this.handlePanelPointerReleased(input.pointerX, input.pointerY)
        }

        if (input.rotatePressed) {
          this.rotateHeldStashItem()
        }

        if (input.sortPressed) {
          this.autoArrangeStash()
        }
      } else if (input.pointerPressed) {
        this.handleActionPanelPointerPressed(input.pointerX, input.pointerY)
      }
    }

    const pointerWorld = input.hasPointer ? this.screenToWorld(input.pointerX, input.pointerY) : null
    if (pointerWorld) {
      const position = this.player.getPosition()
      this.player.setAimAngle(Math.atan2(pointerWorld.y - position.y, pointerWorld.x - position.x))
    }

    if (this.panelOpen || this.mapOpen) {
      this.player.setMoveIntent(0, 0)
    } else {
      this.player.setMoveIntent(input.moveX, input.moveY)
    }

    this.player.update(deltaSeconds, this.worldBounds, elapsedSeconds, this.layout.obstacles)
    this.applyCamera()
    const focusedMarkerId = this.getFocusedMarkerId()
    drawWorldMarkers(this.markerLayer, this.layout.markers, elapsedSeconds, focusedMarkerId)
    this.updateTitle()
    this.updateHint()
    this.updateMarkerLabels(focusedMarkerId)
    this.drawPanel()
    this.drawOverviewMap()
    this.drawMinimap()
    this.syncSceneRuntime()
  }

  destroy(): void {
    this.restoreHeldStashItem()
    this.store.clearSceneRuntime()
    this.container.destroy({ children: true })
  }

  private rebuildWorld(): void {
    drawWorldSurface(this.terrain, { bounds: this.worldBounds, gridSize: 80, mode: 'base' })
    drawWorldObstacles(this.obstacleLayer, this.layout.obstacles)
    drawWorldMarkers(this.markerLayer, this.layout.markers, 0)
    this.labels.removeChildren().forEach((child) => child.destroy())
    for (const marker of this.layout.markers) {
      const label = new Text({ text: marker.label, style: createTextStyle(12, palette.uiText, { fontWeight: '700', letterSpacing: 0.5 }) })
      label.anchor.set(0.5, 0)
      label.position.set(marker.x, marker.y + 22)
      this.labels.addChild(label)
    }
    this.updateMarkerLabels(null)
  }

  private applyCamera(): void {
    const position = this.player.getPosition()
    const desiredX = this.viewport.width * 0.5 - position.x
    const desiredY = this.viewport.height * 0.5 - position.y
    const offsetX = clamp(desiredX, this.viewport.width - this.worldBounds.right, -this.worldBounds.left)
    const offsetY = clamp(desiredY, this.viewport.height - this.worldBounds.bottom, -this.worldBounds.top)
    this.cameraRoot.position.set(offsetX, offsetY)
  }

  private screenToWorld(screenX: number, screenY: number): { x: number; y: number } {
    return { x: screenX - this.cameraRoot.position.x, y: screenY - this.cameraRoot.position.y }
  }

  private updateTitle(): void {
    const state = this.store.getState()
    const route = getWorldRoute(state.save.world.selectedRouteId)
    const equipped = normalizeLoadout(state.save.inventory.equippedWeaponIds)
    this.player.setWeaponStyle(equipped[0] ?? 'machineGun')
    this.title.text = `基地待命 // 当前路线：${route.label}`
  }

  private updateHint(): void {
    const marker = this.findNearbyMarker()

    if (!marker) {
      this.hint.text = '靠近站点后按 E 交互，或前往出击闸门打开部署确认。'
    } else if (marker.id === 'launch') {
      this.hint.text = `接近 ${marker.label}：按 E 打开出击确认`
    } else {
      const mode = resolveBasePanelMode(marker.id)
      const closing = this.panelOpen && mode !== null && this.panelMode === mode
      if (marker.id === 'workshop') {
        this.hint.text = `接近 ${marker.label}：按 E ${closing ? '关闭' : '交互'}，可编排武器槽位顺序`
      } else if (marker.id === 'command') {
        this.hint.text = `接近 ${marker.label}：按 E ${closing ? '关闭' : '交互'}，可切换待部署路线`
      } else {
        this.hint.text = `接近 ${marker.label}：按 E ${closing ? '关闭' : '交互'}，Tab 查看基地总览`
      }
    }

    this.drawHintPanel()
  }

  private drawPanel(): void {
    this.panel.visible = this.panelOpen
    this.panelActionLayer.visible = this.panelOpen
    this.panelTitle.visible = this.panelOpen
    this.panelBody.visible = this.panelOpen
    this.panelFooter.visible = this.panelOpen
    this.panelActionRegions = []
    this.clearPanelActionLayer()
    if (!this.panelOpen) {
      this.panelGridTitle.visible = false
      return
    }

    const state = this.store.getState()
    const route = getWorldRoute(state.save.world.selectedRouteId)
    const last = state.save.session.lastExtraction
    const equipped = state.save.inventory.equippedWeaponIds.length > 0 ? state.save.inventory.equippedWeaponIds : ['machineGun', 'grenade', 'sniper']
    const names = equipped.map((id) => weaponLoadout.find((weapon) => weapon.id === id)?.label ?? id).join(' / ')
    const storedLines =
      state.save.inventory.storedItems
        .slice(-6)
        .map((item) => `- ${(itemById[item.itemId]?.shortLabel ?? item.itemId)} x${item.quantity}`)
        .join('\n') || '仓库里还没有回收物资'
    const showStashGrid = this.panelMode === 'overview' || this.panelMode === 'locker'
    const layout = showStashGrid ? this.getStashPanelLayout() : null
    const frame = layout?.frame ?? this.getPanelFrame()

    drawFullScreenPanelFrame(this.panel, {
      screenWidth: this.viewport.width,
      screenHeight: this.viewport.height,
      x: frame.x,
      y: frame.y,
      width: frame.width,
      height: frame.height,
      headerHeight: frame.headerHeight,
      footerHeight: frame.footerHeight,
    })
    this.panelTitle.text = getPanelTitle(this.panelMode)
    this.panelTitle.position.set(frame.x + 26, frame.y + 24)
    this.panelGridTitle.visible = showStashGrid
    if (showStashGrid) {
      const stashLayout = layout!

      this.drawPanelSection(stashLayout.summaryColumn)
      this.drawPanelSection(stashLayout.gridColumn)
      this.panel.rect(stashLayout.summaryColumn.x + stashLayout.summaryColumn.width + 11, stashLayout.summaryColumn.y + 16, 1, stashLayout.summaryColumn.height - 32).fill({ color: palette.frameSoft, alpha: 0.16 })
      this.panelBody.position.set(stashLayout.summaryColumn.x + 16, stashLayout.summaryColumn.y + 58)
      this.panelBody.style.wordWrapWidth = stashLayout.summaryColumn.width - 32
      this.panelGridTitle.position.set(stashLayout.gridColumn.x + 16, stashLayout.gridColumn.y + 14)
      this.panelGridTitle.text = `仓储网格 ${getInventoryUsedCells(state.save.inventory.storedItems)} / ${getInventoryCapacity(state.save.inventory.stashColumns, state.save.inventory.stashRows)}`
      drawInventoryGrid(this.panel, {
        ...stashLayout.grid,
        items: state.save.inventory.storedItems,
      })
      this.drawHeldStashPreview(stashLayout.grid)
    } else {
      const contentX = frame.x + 26
      const contentY = frame.y + frame.headerHeight + 24
      const contentWidth = frame.width - 52
      const contentHeight = frame.height - frame.headerHeight - frame.footerHeight - 48
      this.drawPanelSection({ x: contentX, y: contentY, width: contentWidth, height: contentHeight })
      this.panelBody.position.set(contentX + 16, contentY + 58)
      this.panelBody.style.wordWrapWidth = contentWidth - 32
      if (this.panelMode === 'workshop') {
        this.drawWorkshopPanel(contentX, contentY, contentWidth)
      } else if (this.panelMode === 'command') {
        this.drawCommandPanel(contentX, contentY, contentWidth, contentHeight)
      } else if (this.panelMode === 'launch') {
        this.drawLaunchPanel(contentX, contentY, contentWidth, contentHeight)
      }
    }
    this.panelFooter.position.set(frame.x + 26, frame.y + frame.height - frame.footerHeight + 16)
    this.panelFooter.text = showStashGrid
      ? '左键按住拖拽 · 松开放下 · R 旋转 · F 自动整理 · Tab / I 关闭'
      : this.panelMode === 'workshop'
        ? '左键选择槽位或武器 · Tab / I 关闭'
        : this.panelMode === 'command'
          ? '左键切换路线 · 前往闸门部署 · Tab / I 关闭'
          : this.panelMode === 'launch'
            ? '左键确认部署或返回选线 · Tab / I 关闭'
          : 'Tab / I 关闭'
    this.panelBody.text = buildPanelBody({
      mode: this.panelMode,
      routeLabel: route.label,
      resourceLine: `基地资源：废料 ${state.save.base.resources.salvage} / 合金 ${state.save.base.resources.alloy} / 研究 ${state.save.base.resources.research}`,
      facilitiesLine: `设施：${state.save.base.unlockedStations.join(' / ')}`,
      deploymentLine: `已部署次数：${state.save.base.deploymentCount}`,
      weaponLine: `已装备武器：${names}`,
      stashLine: `仓储占位：${getInventoryUsedCells(state.save.inventory.storedItems)} / ${getInventoryCapacity(state.save.inventory.stashColumns, state.save.inventory.stashRows)}`,
      lastSummaryLine: last ? `上次结算：${last.summaryLabel} / 击杀 ${last.kills} / 最高波次 ${last.highestWave}` : '上次结算：暂无',
      lastRecoveryLine: last ? `回收：废料 ${last.resourcesRecovered.salvage} / 合金 ${last.resourcesRecovered.alloy} / 研究 ${last.resourcesRecovered.research}` : '回收：暂无',
      storedLines,
      routeSummary: getWorldRoute(state.save.world.selectedRouteId).summary,
    })
  }

  private drawMinimap(): void {
    const focusedMarkerId = this.getFocusedMarkerId()
    drawMinimap(this.minimap, {
      x: 20,
      y: 12,
      width: 196,
      height: 196,
      bounds: this.layout.bounds,
      viewBounds: createFocusedViewBounds(this.layout.bounds, this.player.getPosition(), 760, 760),
      obstacles: this.layout.obstacles,
      player: this.player.getPosition(),
      markers: this.layout.markers,
      highlightedMarkerId: focusedMarkerId,
    })
  }

  private drawOverviewMap(): void {
    this.minimap.visible = !this.mapOpen
    this.minimapTitle.visible = !this.mapOpen
    this.title.visible = !this.mapOpen
    this.hintPanel.visible = !this.mapOpen && !this.panelOpen && this.hint.text.length > 0
    this.hint.visible = !this.mapOpen
    this.overviewMap.visible = this.mapOpen
    this.overviewTitle.visible = this.mapOpen
    this.overviewMeta.visible = this.mapOpen
    this.overviewHint.visible = this.mapOpen

    if (!this.mapOpen) {
      this.overviewMap.clear()
      return
    }

    const width = this.viewport.width
    const height = this.viewport.height
    const x = 0
    const y = 0
    const route = getWorldRoute(this.store.getState().save.world.selectedRouteId)
    const focusedMarker = this.findNearbyMarker() ?? this.layout.markers.find((marker) => marker.id === 'launch') ?? this.layout.markers[0] ?? null

    drawMinimap(this.overviewMap, {
      x,
      y,
      width,
      height,
      bounds: this.layout.bounds,
      obstacles: this.layout.obstacles,
      player: this.player.getPosition(),
      markers: this.layout.markers,
      cameraBounds: this.buildCameraBounds(),
      highlightedMarkerId: focusedMarker?.id ?? null,
    })
    drawMapOverlayPanel(this.overviewMap, 24, 18, 340, 78, palette.frame)
    drawMapOverlayPanel(this.overviewMap, width - 320, 18, 296, 88, palette.minimapMarker)
    drawMapOverlayPanel(this.overviewMap, 24, height - 124, 460, 92, palette.panelWarm)

    this.overviewTitle.position.set(40, 38)
    this.overviewMeta.position.set(width - 304, 38)
    this.overviewHint.position.set(40, height - 102)
    this.overviewTitle.text = `基地总地图 / 当前副本：${route.label}`
    this.overviewMeta.text = `焦点站点：${focusedMarker?.label ?? '无'}\n图例：橙=角色 · 蓝=镜头 · 绿=站点`
    this.overviewHint.text = '基地常态尽量保持场景完整可见。\n前往下方出击闸门后，才能进入当前选择的副本。'
  }

  private syncSceneRuntime(): void {
    const marker = this.findNearbyMarker()
    const atLaunch = marker?.id === 'launch'

    this.store.updateSceneRuntime({
      primaryActionReady: Boolean(atLaunch),
      primaryActionHint: atLaunch ? '已到达出击闸门，按 E 打开出击确认。' : '前往出击闸门后才能出发。',
      nearbyMarkerId: marker?.id ?? null,
      nearbyMarkerLabel: marker?.label ?? null,
      nearbyMarkerKind: marker?.kind ?? null,
      mapOverlayOpen: this.mapOpen,
    })
  }

  private findNearbyMarker(radius = 120) {
    const playerPosition = this.player.getPosition()
    let nearestMarker: typeof this.layout.markers[number] | null = null
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

  private buildCameraBounds(): ArenaBounds {
    const left = clamp(-this.cameraRoot.position.x, this.worldBounds.left, Math.max(this.worldBounds.left, this.worldBounds.right - this.viewport.width))
    const top = clamp(-this.cameraRoot.position.y, this.worldBounds.top, Math.max(this.worldBounds.top, this.worldBounds.bottom - this.viewport.height))
    return { left, top, right: Math.min(this.worldBounds.right, left + this.viewport.width), bottom: Math.min(this.worldBounds.bottom, top + this.viewport.height) }
  }

  private drawBackdrop(): void {
    this.backdrop.clear().rect(0, 0, this.viewport.width, this.viewport.height).fill({ color: palette.bgOuter })
  }

  private layoutUi(): void {
    this.minimapTitle.position.set(38, 20)
    this.title.position.set(this.viewport.width * 0.5, 18)
    this.hint.position.set(this.viewport.width * 0.5, this.viewport.height - 62)
    this.drawHintPanel()
  }

  private updateMarkerLabels(highlightedMarkerId: string | null): void {
    this.layout.markers.forEach((marker, index) => {
      const label = this.labels.children[index] as Text | undefined

      if (!label) {
        return
      }

      const focused = marker.id === highlightedMarkerId
      label.tint = focused ? getMarkerEmphasisTint(marker.kind) : palette.uiText
      label.alpha = focused ? 0.98 : 0.78
      label.scale.set(focused ? 1.04 : 1)
      label.position.set(marker.x, marker.y + (focused ? 26 : 22))
    })
  }

  private handleNearbyInteraction(): void {
    const marker = this.findNearbyMarker()
    const mode = resolveBasePanelMode(marker?.id ?? null)

    if (!mode) {
      return
    }

    if (this.panelOpen && this.panelMode === mode) {
      this.restoreHeldStashItem()
      this.panelOpen = false
      return
    }

    if (this.heldStashItem && !isStashPanelMode(mode)) {
      this.restoreHeldStashItem()
    }

    this.panelMode = mode
    if (mode === 'workshop') {
      this.selectedWorkshopSlot = clamp(this.selectedWorkshopSlot, 0, weaponLoadout.length - 1)
    }
    this.panelOpen = true
    this.mapOpen = false
  }

  private handleActionPanelPointerPressed(pointerX: number, pointerY: number): void {
    const target = this.panelActionRegions.find(
      (region) =>
        pointerX >= region.x &&
        pointerX <= region.x + region.width &&
        pointerY >= region.y &&
        pointerY <= region.y + region.height,
    )

    if (!target) {
      return
    }

    if (target.id.startsWith('workshop-slot:')) {
      this.selectedWorkshopSlot = Number(target.id.split(':')[1]) || 0
      return
    }

    if (target.id.startsWith('workshop-weapon:')) {
      const weaponId = target.id.split(':')[1] as WeaponType
      const current = [...this.store.getState().save.inventory.equippedWeaponIds]
      const next = assignWeaponToLoadoutSlot(current, this.selectedWorkshopSlot, weaponId)

      this.store.updateEquippedWeapons(next)
      this.player.setWeaponStyle(next[0] ?? 'machineGun')
      return
    }

    if (target.id.startsWith('command-route:')) {
      const routeId = target.id.slice('command-route:'.length)
      this.store.selectWorldRoute(routeId)
      return
    }

    if (target.id === 'launch-confirm') {
      this.store.deployCombat()
      return
    }

    if (target.id === 'launch-route') {
      this.panelMode = 'command'
    }
  }

  private drawWorkshopPanel(contentX: number, contentY: number, contentWidth: number): void {
    const state = this.store.getState()
    const equipped = normalizeLoadout(state.save.inventory.equippedWeaponIds)
    const infoHeight = 138
    const sectionY = contentY + infoHeight + 26
    const sectionWidth = contentWidth - 32
    const slotGap = 12
    const slotWidth = Math.floor((sectionWidth - slotGap * 2) / 3)
    const slotHeight = 92
    const weaponCardHeight = 84
    const selectedSlotIndex = clamp(this.selectedWorkshopSlot, 0, equipped.length - 1)
    const selectedWeapon = weaponLoadout.find((weapon) => weapon.id === equipped[selectedSlotIndex]) ?? weaponLoadout[selectedSlotIndex] ?? weaponLoadout[0]

    this.panelBody.text = [
      `当前主手序列：${equipped.map((weaponId, index) => `${index + 1}.${weaponLoadout.find((weapon) => weapon.id === weaponId)?.label ?? weaponId}`).join(' / ')}`,
      '说明：点选下方槽位，再点备选武器，即可把对应武器换到该槽位；若武器已在其它槽位，会自动交换位置。',
      `当前编辑槽位：${selectedSlotIndex + 1} · ${selectedWeapon.label}`,
    ].join('\n')

    const slotY = sectionY + 18

    for (let index = 0; index < equipped.length; index += 1) {
      const weaponId = equipped[index]
      const weapon = weaponLoadout.find((entry) => entry.id === weaponId) ?? weaponLoadout[index]
      const x = contentX + 16 + index * (slotWidth + slotGap)
      const hovered = this.isPointerInside(x, slotY, slotWidth, slotHeight)
      const active = index === selectedSlotIndex

      this.drawActionCard(
        {
          x,
          y: slotY,
          width: slotWidth,
          height: slotHeight,
        },
        {
          title: `槽位 ${index + 1} / ${weapon.label}`,
          body: weapon.hint,
          active,
          hovered,
          accentColor: palette.frame,
        },
      )
      this.panelActionRegions.push({ id: `workshop-slot:${index}`, x, y: slotY, width: slotWidth, height: slotHeight })
    }

    const weaponY = slotY + slotHeight + 28
    const weaponSectionWidth = Math.floor((sectionWidth - slotGap * 2) / 3)

    for (let index = 0; index < weaponLoadout.length; index += 1) {
      const weapon = weaponLoadout[index]
      const x = contentX + 16 + index * (weaponSectionWidth + slotGap)
      const hovered = this.isPointerInside(x, weaponY, weaponSectionWidth, weaponCardHeight)
      const active = weapon.id === equipped[selectedSlotIndex]

      this.drawActionCard(
        {
          x,
          y: weaponY,
          width: weaponSectionWidth,
          height: weaponCardHeight,
        },
        {
          title: weapon.label,
          body: `槽位 ${weapon.slot} 预设 / ${weapon.hint}`,
          active,
          hovered,
          accentColor: palette.accent,
        },
      )
      this.panelActionRegions.push({ id: `workshop-weapon:${weapon.id}`, x, y: weaponY, width: weaponSectionWidth, height: weaponCardHeight })
    }
  }

  private drawCommandPanel(contentX: number, contentY: number, contentWidth: number, contentHeight: number): void {
    const state = this.store.getState()
    const selectedRoute = getWorldRoute(state.save.world.selectedRouteId)
    const infoHeight = 150
    const cardsY = contentY + infoHeight + 20
    const cardsHeight = contentHeight - infoHeight - 36
    const columnGap = 12
    const rowGap = 12
    const columns = 2
    const cardWidth = Math.floor((contentWidth - 32 - columnGap) / columns)
    const cardHeight = Math.max(86, Math.floor((cardsHeight - rowGap * 2) / 3))
    const routeZones = selectedRoute.zones.map((zone) => `${zone.label} / 威胁 ${zone.threatLevel} / 回收 x${zone.rewardMultiplier.toFixed(2)}`).join('\n')

    this.panelBody.text = [
      `当前路线：${selectedRoute.label}`,
      `路线说明：${selectedRoute.summary}`,
      `区域结构：${selectedRoute.zones.length} 段`,
      `部署提示：先在这里选线，再前往出击闸门执行部署。`,
      '',
      routeZones,
    ].join('\n')

    for (let index = 0; index < worldRoutes.length; index += 1) {
      const route = worldRoutes[index]
      const column = index % columns
      const row = Math.floor(index / columns)
      const x = contentX + 16 + column * (cardWidth + columnGap)
      const y = cardsY + row * (cardHeight + rowGap)
      const hovered = this.isPointerInside(x, y, cardWidth, cardHeight)
      const active = route.id === selectedRoute.id
      const riskLabel = route.zones.map((zone) => zone.threatLevel).join('-')

      this.drawActionCard(
        {
          x,
          y,
          width: cardWidth,
          height: cardHeight,
        },
        {
          title: route.label,
          body: `${route.summary}\n威胁梯度 ${riskLabel} · 区域 ${route.zones.length} 段`,
          active,
          hovered,
          accentColor: palette.minimapMarker,
        },
      )
      this.panelActionRegions.push({ id: `command-route:${route.id}`, x, y, width: cardWidth, height: cardHeight })
    }
  }

  private drawLaunchPanel(contentX: number, contentY: number, contentWidth: number, contentHeight: number): void {
    const route = getWorldRoute(this.store.getState().save.world.selectedRouteId)
    const confirmWidth = Math.min(360, contentWidth - 32)
    const confirmHeight = 96
    const secondaryWidth = confirmWidth
    const secondaryHeight = 82
    const centerX = contentX + Math.floor((contentWidth - confirmWidth) * 0.5)
    const confirmY = contentY + Math.max(76, Math.floor(contentHeight * 0.38))
    const secondaryY = confirmY + confirmHeight + 18
    const hoveredConfirm = this.isPointerInside(centerX, confirmY, confirmWidth, confirmHeight)
    const hoveredRoute = this.isPointerInside(centerX, secondaryY, secondaryWidth, secondaryHeight)

    this.drawActionCard(
      {
        x: centerX,
        y: confirmY,
        width: confirmWidth,
        height: confirmHeight,
      },
      {
        title: `确认部署 / ${route.label}`,
        body: `从出击闸门进入当前副本。\n${route.summary}`,
        active: true,
        hovered: hoveredConfirm,
        accentColor: palette.accent,
      },
    )
    this.panelActionRegions.push({ id: 'launch-confirm', x: centerX, y: confirmY, width: confirmWidth, height: confirmHeight })

    this.drawActionCard(
      {
        x: centerX,
        y: secondaryY,
        width: secondaryWidth,
        height: secondaryHeight,
      },
      {
        title: '返回选线',
        body: '切回指挥台界面，调整当前副本路线与风险梯度。',
        active: false,
        hovered: hoveredRoute,
        accentColor: palette.frame,
      },
    )
    this.panelActionRegions.push({ id: 'launch-route', x: centerX, y: secondaryY, width: secondaryWidth, height: secondaryHeight })
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

  private drawActionCard(
    bounds: { x: number; y: number; width: number; height: number },
    input: { title: string; body: string; active: boolean; hovered: boolean; accentColor: number },
  ): void {
    const fillAlpha = input.active ? 0.94 : input.hovered ? 0.86 : 0.76
    const strokeAlpha = input.active ? 0.78 : input.hovered ? 0.44 : 0.22

    this.panel.roundRect(bounds.x, bounds.y, bounds.width, bounds.height, 16).fill({
      color: input.active ? palette.uiActive : palette.uiPanel,
      alpha: fillAlpha,
    })
    this.panel.roundRect(bounds.x, bounds.y, bounds.width, bounds.height, 16).stroke({
      width: 1.2,
      color: input.active ? input.accentColor : palette.frame,
      alpha: strokeAlpha,
      alignment: 0.5,
    })
    this.panel.roundRect(bounds.x + 12, bounds.y + 14, 34, 3, 999).fill({
      color: input.accentColor,
      alpha: input.active ? 0.9 : input.hovered ? 0.58 : 0.34,
    })
    drawCornerFrame(this.panel, bounds.x + 8, bounds.y + 8, bounds.width - 16, bounds.height - 16, 10, input.accentColor, 0.22, 1)

    this.addPanelActionText(bounds.x + 14, bounds.y + 12, input.title, 13, palette.uiText, true)
    this.addPanelActionText(bounds.x + 14, bounds.y + 34, input.body, 11, palette.uiMuted, false, bounds.width - 28, 18)
  }

  private addPanelActionText(
    x: number,
    y: number,
    text: string,
    fontSize: number,
    fill: number,
    bold: boolean,
    wordWrapWidth?: number,
    lineHeight?: number,
  ): void {
    const label = new Text({
      text,
      style: createTextStyle(fontSize, fill, {
        fontWeight: bold ? '700' : '500',
        lineHeight,
        wordWrap: wordWrapWidth !== undefined,
        wordWrapWidth,
      }),
    })
    label.position.set(x, y)
    this.panelActionLayer.addChild(label)
  }

  private clearPanelActionLayer(): void {
    this.panelActionLayer.removeChildren().forEach((child) => child.destroy())
  }

  private drawHintPanel(): void {
    this.hintPanel.clear()
    this.hintPanel.visible = !this.mapOpen && !this.panelOpen && this.hint.text.length > 0

    if (!this.hintPanel.visible) {
      return
    }

    const maxWidth = Math.max(220, this.viewport.width - 40)
    const width = Math.min(maxWidth, Math.max(320, this.hint.width + 40))
    const height = 42
    const x = Math.round((this.viewport.width - width) * 0.5)
    const y = Math.round(this.viewport.height - 82)

    this.hintPanel.roundRect(x + 4, y + 6, width, height, 12).fill({ color: palette.obstacleShadow, alpha: 0.08 })
    this.hintPanel.roundRect(x, y, width, height, 12).fill({ color: palette.uiPanel, alpha: 0.78 })
    this.hintPanel.roundRect(x + 12, y + 10, width - 24, 16, 8).fill({ color: palette.arenaCore, alpha: 0.16 })
    this.hintPanel.rect(x + 16, y + 19, 12, 2).fill({ color: palette.panelWarm, alpha: 0.82 })
  }

  private getFocusedMarkerId(): string | null {
    return this.findNearbyMarker()?.id ?? 'launch'
  }

  private isPointerInside(x: number, y: number, width: number, height: number): boolean {
    return (
      this.pointerScreen.hasPointer &&
      this.pointerScreen.x >= x &&
      this.pointerScreen.x <= x + width &&
      this.pointerScreen.y >= y &&
      this.pointerScreen.y <= y + height
    )
  }

  private handlePanelPointerPressed(pointerX: number, pointerY: number): void {
    if (this.heldStashItem) {
      return
    }

    const state = this.store.getState()
    const layout = this.getStashPanelLayout().grid
    const targetCell = resolveInventoryCellAtPoint(layout, pointerX, pointerY)

    if (!targetCell) {
      return
    }

    const extraction = pickItemFromGridAtCell(state.save.inventory.storedItems, targetCell.x, targetCell.y)

    if (!extraction.item) {
      return
    }

    this.heldStashItem = extraction.item
    this.heldStashRestoreItem = { ...extraction.item }
    this.store.updateStashItems(extraction.items)
  }

  private handlePanelPointerReleased(pointerX: number, pointerY: number): void {
    if (!this.heldStashItem) {
      return
    }

    const state = this.store.getState()
    const layout = this.getStashPanelLayout().grid
    const targetCell = resolveInventoryCellAtPoint(layout, pointerX, pointerY)

    if (!targetCell) {
      this.restoreHeldStashItem()
      return
    }

    const placement = placeItemAtPosition(layout.columns, layout.rows, state.save.inventory.storedItems, this.heldStashItem, targetCell.x, targetCell.y)

    if (!placement.placed) {
      this.restoreHeldStashItem()
      return
    }

    this.store.updateStashItems(placement.items)
    this.clearHeldStashItem()
  }

  private rotateHeldStashItem(): void {
    if (!this.heldStashItem || this.heldStashItem.width === this.heldStashItem.height) {
      return
    }

    this.heldStashItem = rotateInventoryItem(this.heldStashItem)
  }

  private restoreHeldStashItem(): void {
    if (!this.heldStashItem) {
      return
    }

    const state = this.store.getState()
    const restoreItem = this.heldStashRestoreItem ?? this.heldStashItem
    const exactRestore = placeItemAtPosition(
      state.save.inventory.stashColumns,
      state.save.inventory.stashRows,
      state.save.inventory.storedItems,
      restoreItem,
      restoreItem.x,
      restoreItem.y,
    )
    const fallbackRestore = exactRestore.placed
      ? exactRestore
      : placeItemInGrid(state.save.inventory.stashColumns, state.save.inventory.stashRows, state.save.inventory.storedItems, restoreItem)

    if (fallbackRestore.placed) {
      this.store.updateStashItems(fallbackRestore.items)
    }

    this.clearHeldStashItem()
  }

  private clearHeldStashItem(): void {
    this.heldStashItem = null
    this.heldStashRestoreItem = null
  }

  private autoArrangeStash(): void {
    const state = this.store.getState()
    const items = this.heldStashItem ? [...state.save.inventory.storedItems, this.heldStashItem] : state.save.inventory.storedItems
    const arrangement = autoArrangeInventory(state.save.inventory.stashColumns, state.save.inventory.stashRows, items)

    if (!arrangement.arranged) {
      return
    }

    this.store.updateStashItems(arrangement.items)
    this.clearHeldStashItem()
  }

  private getPanelFrame() {
    const width = Math.max(320, Math.min(this.viewport.width - 32, Math.round(this.viewport.width * 0.68)))
    const height = Math.max(320, Math.min(this.viewport.height - 32, Math.round(this.viewport.height * 0.72)))

    return {
      x: Math.round((this.viewport.width - width) * 0.5),
      y: Math.round((this.viewport.height - height) * 0.5),
      width,
      height,
      headerHeight: 84,
      footerHeight: 50,
    }
  }

  private getStashPanelLayout(): BaseStashPanelLayout {
    const frame = this.getPanelFrame()
    const state = this.store.getState()
    const contentX = frame.x + 26
    const contentY = frame.y + frame.headerHeight + 24
    const contentWidth = frame.width - 52
    const contentHeight = frame.height - frame.headerHeight - frame.footerHeight - 48
    let summaryWidth = Math.max(220, Math.min(420, Math.floor(contentWidth * 0.42)))

    if (contentWidth - summaryWidth - 24 < 220) {
      summaryWidth = Math.max(180, contentWidth - 244)
    }

    const gridColumnX = contentX + summaryWidth + 24
    const gridColumnWidth = frame.x + frame.width - 26 - gridColumnX
    const cellSize = Math.max(
      18,
      Math.min(
        34,
        Math.floor((gridColumnWidth - 48) / state.save.inventory.stashColumns),
        Math.floor((contentHeight - 84) / state.save.inventory.stashRows),
      ),
    )
    const gridWidth = state.save.inventory.stashColumns * cellSize

    return {
      frame,
      summaryColumn: {
        x: contentX,
        y: contentY,
        width: summaryWidth,
        height: contentHeight,
      },
      gridColumn: {
        x: gridColumnX,
        y: contentY,
        width: gridColumnWidth,
        height: contentHeight,
      },
      grid: {
        x: gridColumnX + Math.max(16, Math.floor((gridColumnWidth - gridWidth) * 0.5)),
        y: contentY + 58,
        columns: state.save.inventory.stashColumns,
        rows: state.save.inventory.stashRows,
        cellSize,
      },
    }
  }

  private drawHeldStashPreview(gridLayout: { x: number; y: number; columns: number; rows: number; cellSize: number }): void {
    if (!this.heldStashItem || !this.pointerScreen.hasPointer) {
      return
    }

    const state = this.store.getState()
    const cell = resolveInventoryCellAtPoint(gridLayout, this.pointerScreen.x, this.pointerScreen.y)
    const valid = cell
      ? canPlaceItemAtPosition(gridLayout.columns, gridLayout.rows, state.save.inventory.storedItems, this.heldStashItem, cell.x, cell.y)
      : false

    drawFloatingInventoryItem(this.panel, {
      item: this.heldStashItem,
      cellSize: gridLayout.cellSize,
      x: cell ? gridLayout.x + cell.x * gridLayout.cellSize : this.pointerScreen.x - this.heldStashItem.width * gridLayout.cellSize * 0.5,
      y: cell ? gridLayout.y + cell.y * gridLayout.cellSize : this.pointerScreen.y - this.heldStashItem.height * gridLayout.cellSize * 0.5,
      valid,
    })
  }

  private isStashGridVisible(): boolean {
    return this.panelMode === 'overview' || this.panelMode === 'locker'
  }
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value))
}

function resolveBasePanelMode(markerId: string | null): BasePanelMode | null {
  if (markerId === 'locker') {
    return 'locker'
  }
  if (markerId === 'workshop') {
    return 'workshop'
  }
  if (markerId === 'command') {
    return 'command'
  }
  if (markerId === 'launch') {
    return 'launch'
  }
  return null
}

function getPanelTitle(mode: BasePanelMode): string {
  if (mode === 'locker') {
    return '储物柜 / 仓储'
  }
  if (mode === 'workshop') {
    return '工坊台 / 改装'
  }
  if (mode === 'command') {
    return '指挥台 / 行动'
  }
  if (mode === 'launch') {
    return '出击闸门 / 部署确认'
  }
  return '基地与背包'
}

function getMarkerEmphasisTint(kind: 'entry' | 'objective' | 'extraction' | 'locker' | 'station'): number {
  if (kind === 'locker') {
    return palette.warning
  }
  if (kind === 'station') {
    return palette.frame
  }
  if (kind === 'extraction') {
    return palette.minimapMarker
  }
  return palette.accent
}

function isStashPanelMode(mode: BasePanelMode): boolean {
  return mode === 'overview' || mode === 'locker'
}

function buildPanelBody(input: {
  mode: BasePanelMode
  routeLabel: string
  routeSummary: string
  resourceLine: string
  facilitiesLine: string
  deploymentLine: string
  weaponLine: string
  stashLine: string
  lastSummaryLine: string
  lastRecoveryLine: string
  storedLines: string
}): string {
  if (input.mode === 'locker') {
    return [
      input.resourceLine,
      input.stashLine,
      input.lastSummaryLine,
      input.lastRecoveryLine,
      '操作：左键按住拖拽 / 松开放下 / R 旋转 / F 自动整理',
      '',
      '最近入库 //',
      input.storedLines,
    ].join('\n')
  }

  if (input.mode === 'workshop') {
    return [
      input.resourceLine,
      input.weaponLine,
      input.facilitiesLine,
      '',
      '工坊状态 //',
      '当前已接入武器槽位编排，可直接调整 1/2/3 号武器顺序。',
      '后续会继续把维修、模块改装和消耗品制造接进同一面板。',
    ].join('\n')
  }

  if (input.mode === 'command') {
    return [
      `当前路线：${input.routeLabel}`,
      `路线说明：${input.routeSummary}`,
      input.deploymentLine,
      input.lastSummaryLine,
      '',
      '指挥台状态 //',
      '当前已接入路线卡片选择，选定后前往出击闸门即可部署。',
      '后续会继续把合同、风险标签和推荐掉落补进这里。',
    ].join('\n')
  }

  if (input.mode === 'launch') {
    return [
      input.resourceLine,
      `当前路线：${input.routeLabel}`,
      `路线说明：${input.routeSummary}`,
      input.deploymentLine,
      '',
      '部署确认 //',
      '当前界面只负责最终出发确认，不再在基地常态长期挂出右侧栏。',
      '确认后将立即从出击闸门进入当前选择的副本。',
    ].join('\n')
  }

  return [
    input.resourceLine,
    `当前路线：${input.routeLabel}`,
    input.facilitiesLine,
    input.deploymentLine,
    input.weaponLine,
    input.stashLine,
    '操作：左键按住拖拽 / 松开放下 / R 旋转 / F 自动整理',
    '',
    input.lastSummaryLine,
    input.lastRecoveryLine,
    '',
    '最近入库 //',
    input.storedLines,
  ].join('\n')
}

function normalizeLoadout(weaponIds: readonly WeaponType[]): WeaponType[] {
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

function assignWeaponToLoadoutSlot(weaponIds: readonly WeaponType[], slotIndex: number, weaponId: WeaponType): WeaponType[] {
  const next = normalizeLoadout(weaponIds)
  const targetIndex = clamp(slotIndex, 0, next.length - 1)
  const existingIndex = next.findIndex((entry) => entry === weaponId)

  if (existingIndex === -1) {
    next[targetIndex] = weaponId
    return normalizeLoadout(next)
  }

  const previousWeapon = next[targetIndex]
  next[targetIndex] = weaponId
  next[existingIndex] = previousWeapon
  return next
}


