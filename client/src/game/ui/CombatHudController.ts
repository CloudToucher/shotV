import { Container, Graphics, Text } from 'pixi.js'

import { PLAYER_MAX_HEALTH } from '../combat/constants'
import type { EncounterState } from '../combat/types'
import type { ViewportSize } from '../core/contracts'
import { weaponLoadout } from '../data/weapons'
import type { WeaponType } from '../data/types'
import { palette } from '../theme/palette'
import { createTextStyle } from './surface'

export interface CombatHudBossSnapshot {
  label: string
  health: number
  maxHealth: number
  phase: 1 | 2
}

export interface CombatHudQuickSlotSnapshot {
  keyLabel: string
  itemLabel: string
  quantity: number
  available: boolean
}

export interface CombatHudSnapshot {
  elapsedSeconds: number
  playerHealth: number
  waveIndex: number
  killCount: number
  enemyCount: number
  pendingSpawns: number
  currentWeaponId: WeaponType
  boss: CombatHudBossSnapshot | null
  encounterState: EncounterState
  quickSlots: CombatHudQuickSlotSnapshot[]
}

export class CombatHudController {
  readonly container = new Container()

  private readonly hudGraphics = new Graphics()
  private readonly toastTitle = new Text({
    text: '',
    style: createTextStyle(20, palette.uiText, { fontWeight: '700', letterSpacing: 0.7 }),
  })
  private readonly toastHint = new Text({
    text: '',
    style: createTextStyle(12, palette.uiMuted, { letterSpacing: 0.4 }),
  })
  private readonly healthText = new Text({
    text: '',
    style: createTextStyle(15, palette.uiText, { fontWeight: '700', letterSpacing: 0.4 }),
  })
  private readonly waveText = new Text({
    text: '',
    style: createTextStyle(15, palette.uiText, { fontWeight: '700', letterSpacing: 0.4 }),
  })
  private readonly enemyText = new Text({
    text: '',
    style: createTextStyle(12, palette.uiMuted, { letterSpacing: 0.3 }),
  })
  private readonly bossText = new Text({
    text: '',
    style: createTextStyle(13, palette.uiText, { fontWeight: '700', letterSpacing: 0.4 }),
  })
  private readonly centerTitle = new Text({
    text: '',
    style: createTextStyle(30, palette.uiText, { fontWeight: '700', letterSpacing: 0.6 }),
  })
  private readonly centerHint = new Text({
    text: '',
    style: createTextStyle(13, palette.uiMuted, { letterSpacing: 0.35 }),
  })
  private readonly quickSlotLabels = Array.from({ length: 4 }, () =>
    new Text({
      text: '',
      style: createTextStyle(12, palette.uiText, { fontWeight: '700', letterSpacing: 0.3 }),
    }),
  )
  private readonly weaponLabels = weaponLoadout.map(
    (weapon) =>
      new Text({
        text: `${weapon.slot} ${weapon.label}`,
        style: createTextStyle(15, palette.uiText, { fontWeight: '700', letterSpacing: 0.45 }),
      }),
  )

  private viewport: ViewportSize = { width: 0, height: 0 }
  private messageTimer = 0
  private healthHudPulse = 0
  private infoHudPulse = 0
  private weaponHudPulse = 0

  constructor() {
    this.toastTitle.anchor.set(0.5)
    this.toastHint.anchor.set(0.5)
    this.bossText.anchor.set(0.5, 0)
    this.centerTitle.anchor.set(0.5)
    this.centerHint.anchor.set(0.5)

    this.container.addChild(
      this.hudGraphics,
      ...this.weaponLabels,
      this.toastTitle,
      this.toastHint,
      this.healthText,
      this.waveText,
      this.enemyText,
      this.bossText,
      this.centerTitle,
      this.centerHint,
      ...this.quickSlotLabels,
    )
  }

  setViewport(viewport: ViewportSize): void {
    this.viewport = viewport
    this.layoutHud()
  }

  tick(deltaSeconds: number): void {
    this.messageTimer = Math.max(0, this.messageTimer - deltaSeconds)
    this.healthHudPulse = Math.max(0, this.healthHudPulse - deltaSeconds * 3.1)
    this.infoHudPulse = Math.max(0, this.infoHudPulse - deltaSeconds * 2.8)
    this.weaponHudPulse = Math.max(0, this.weaponHudPulse - deltaSeconds * 4.2)
  }

  destroy(): void {
    this.container.destroy({ children: true })
  }

  showToast(title: string, hint: string, duration: number): void {
    this.toastTitle.text = title
    this.toastHint.text = hint
    this.messageTimer = duration
  }

  pulseHealth(intensity: number): void {
    this.healthHudPulse = Math.max(this.healthHudPulse, intensity)
  }

  pulseInfo(intensity: number): void {
    this.infoHudPulse = Math.max(this.infoHudPulse, intensity)
  }

  pulseWeapon(intensity: number): void {
    this.weaponHudPulse = Math.max(this.weaponHudPulse, intensity)
  }

  getWeaponPulse(): number {
    return this.weaponHudPulse
  }

  draw(snapshot: CombatHudSnapshot): void {
    this.hudGraphics.clear()

    const playerHealthRatio = snapshot.playerHealth / PLAYER_MAX_HEALTH
    const displayWave = Math.max(1, snapshot.waveIndex)
    const healthPulse = this.healthHudPulse + (playerHealthRatio < 0.35 ? (Math.sin(snapshot.elapsedSeconds * 8.2) + 1) * 0.18 : 0)
    const infoLift = this.infoHudPulse * 5
    const beltRise = this.weaponHudPulse * 7
    const totalWidth = weaponLoadout.length * 136 + (weaponLoadout.length - 1) * 12
    const bottomMargin = 24
    const leftMargin = 24
    const rightMargin = 24
    const beltBaseY = this.viewport.height - bottomMargin - 46
    const beltX = leftMargin
    const beltY = beltBaseY - beltRise
    const quickSlotWidth = 108
    const quickSlotGap = 10
    const quickSlotTotalWidth = snapshot.quickSlots.length * quickSlotWidth + Math.max(0, snapshot.quickSlots.length - 1) * quickSlotGap
    const quickSlotX = this.viewport.width - quickSlotTotalWidth - rightMargin
    const quickSlotY = this.viewport.height - bottomMargin - 28

    const healthX = leftMargin
    const panelY = beltBaseY - 102
    const healthWidth = 304
    const infoWidth = 280
    const infoX = this.viewport.width - infoWidth - rightMargin
    const infoY = 24 - infoLift

    this.healthText.position.set(healthX + 24 + healthPulse * 2, panelY + 18)
    this.waveText.position.set(infoX + 20, infoY + 18)
    this.enemyText.position.set(infoX + 20, infoY + 44)
    this.bossText.position.set(this.viewport.width / 2, 112)

    this.healthText.text = `生命模组 ${Math.ceil(snapshot.playerHealth)} / ${PLAYER_MAX_HEALTH}`
    this.waveText.text = `波次 ${String(displayWave).padStart(2, '0')} · 击杀 ${snapshot.killCount}`
    this.enemyText.text = `活动目标 ${snapshot.enemyCount} · 待增援 ${snapshot.pendingSpawns}`
    this.bossText.text = snapshot.boss ? `区域主核 ${snapshot.boss.label} · 阶段 ${snapshot.boss.phase}` : ''

    drawHudPanel(this.hudGraphics, healthX, panelY, healthWidth, 86, 0.16 + healthPulse * 0.06)
    this.hudGraphics.poly([
      infoX + 8 + 8, infoY + 10,
      infoX + 8 + infoWidth - 16, infoY + 10,
      infoX + 8 + infoWidth - 16, infoY + 10 + 58 - 8,
      infoX + 8 + infoWidth - 16 - 8, infoY + 10 + 58,
      infoX + 8, infoY + 10 + 58,
      infoX + 8, infoY + 10 + 8
    ]).fill({
      color: palette.uiPanel,
      alpha: 0.4 + this.infoHudPulse * 0.1,
    }).stroke({ width: 1, color: palette.frame, alpha: 0.2 })
    
    this.hudGraphics.rect(infoX + 14, infoY + 14, infoWidth - 28, 2).fill({
      color: palette.arenaCore,
      alpha: 0.3,
    })

    // Segmented health bar
    const segments = 10
    const segmentWidth = (healthWidth - 44 - (segments - 1) * 4) / segments
    for (let i = 0; i < segments; i++) {
      const segX = healthX + 22 + i * (segmentWidth + 4)
      const segRatio = (i + 1) / segments
      const isActive = playerHealthRatio >= segRatio - (1 / segments) / 2
      
      this.hudGraphics.poly([
        segX + 4, panelY + 52,
        segX + segmentWidth, panelY + 52,
        segX + segmentWidth - 4, panelY + 62,
        segX, panelY + 62
      ]).fill({ 
        color: isActive ? (playerHealthRatio > 0.3 ? palette.dash : palette.danger) : palette.uiText, 
        alpha: isActive ? 0.9 + healthPulse * 0.1 : 0.15 
      })
      
      if (isActive && healthPulse > 0) {
        this.hudGraphics.poly([
          segX + 4, panelY + 52,
          segX + segmentWidth, panelY + 52,
          segX + segmentWidth - 4, panelY + 62,
          segX, panelY + 62
        ]).stroke({ width: 1, color: palette.arenaCore, alpha: healthPulse * 0.5 })
      }
    }

    this.hudGraphics.rect(healthX + 22, panelY + 34, 64, 3).fill({ color: palette.panelWarm, alpha: 0.42 })
    this.hudGraphics.rect(infoX + 20, infoY + 34, 58, 3).fill({ color: palette.panelLine, alpha: 0.4 })

    drawHudPanel(this.hudGraphics, beltX - 16, beltY - 12, totalWidth + 32, 74, 0.09)
    drawHudPanel(this.hudGraphics, quickSlotX - 12, quickSlotY - 10, quickSlotTotalWidth + 24, 44, 0.07)

    let slotX = quickSlotX
    for (let index = 0; index < this.quickSlotLabels.length; index += 1) {
      const slot = snapshot.quickSlots[index]
      const label = this.quickSlotLabels[index]
      const available = slot?.available ?? false

      label.text = slot ? `${slot.keyLabel}  ${slot.itemLabel}${slot.quantity > 1 ? ` x${slot.quantity}` : ''}` : ''
      label.tint = available ? palette.uiText : palette.uiMuted
      label.alpha = available ? 0.98 : 0.68
      label.position.set(slotX + 14, quickSlotY + 12)

      const cut = 6
      this.hudGraphics
        .poly([
          slotX + cut, quickSlotY,
          slotX + quickSlotWidth, quickSlotY,
          slotX + quickSlotWidth, quickSlotY + 28 - cut,
          slotX + quickSlotWidth - cut, quickSlotY + 28,
          slotX, quickSlotY + 28,
          slotX, quickSlotY + cut
        ])
        .fill({ color: available ? palette.uiActive : palette.uiPanel, alpha: 0.9 })
        .stroke({ width: 1.2, color: available ? palette.frame : palette.frameSoft, alpha: available ? 0.8 : 0.3, alignment: 0.5 })

      slotX += quickSlotWidth + quickSlotGap
    }

    let weaponX = beltX
    for (let index = 0; index < weaponLoadout.length; index += 1) {
      const weapon = weaponLoadout[index]
      const active = weapon.id === snapshot.currentWeaponId
      const label = this.weaponLabels[index]
      const rise = active ? 6 + this.weaponHudPulse * 6 : 0
      label.tint = active ? palette.uiText : palette.uiMuted
      label.alpha = active ? 1 : 0.78
      label.position.set(weaponX + 18, beltY + 15 - rise)

      const wCut = 8
      const wH = 46 + rise
      const wY = beltY - rise
      
      this.hudGraphics
        .poly([
          weaponX + wCut, wY,
          weaponX + 136, wY,
          weaponX + 136, wY + wH - wCut,
          weaponX + 136 - wCut, wY + wH,
          weaponX, wY + wH,
          weaponX, wY + wCut
        ])
        .fill({ color: active ? palette.uiActive : palette.uiPanel, alpha: 0.9 })
        .stroke({ width: 1.5, color: active ? palette.frame : palette.frameSoft, alpha: active ? 0.9 : 0.3, alignment: 0.5 })

      this.hudGraphics.rect(weaponX + 16, beltY + 9 - rise, 30, 2).fill({ color: active ? palette.accent : palette.frameSoft, alpha: active ? 0.54 : 0.18 })
      weaponX += 148
    }

    if (snapshot.boss) {
      const ratio = snapshot.boss.health / snapshot.boss.maxHealth
      const bossWidth = 420
      const bossX = (this.viewport.width - bossWidth) / 2
      const bossY = 98

      this.bossText.alpha = 1
      drawHudPanel(this.hudGraphics, bossX, bossY, bossWidth, 48, 0.12)
      this.hudGraphics
        .rect(bossX + 18, bossY + 28, bossWidth - 36, 4)
        .fill({ color: palette.uiText, alpha: 0.15 })
        .rect(bossX + 18, bossY + 28, (bossWidth - 36) * ratio, 4)
        .fill({ color: snapshot.boss.phase === 1 ? palette.warning : palette.danger, alpha: 0.96 })
        
      this.hudGraphics.poly([
        bossX + 18 + (bossWidth - 36) * ratio, bossY + 24,
        bossX + 18 + (bossWidth - 36) * ratio + 4, bossY + 28,
        bossX + 18 + (bossWidth - 36) * ratio, bossY + 32
      ]).fill({ color: palette.arenaCore, alpha: 0.9 })
    } else {
      this.bossText.alpha = 0
    }

    if (this.messageTimer > 0) {
      const alpha = Math.min(1, this.messageTimer * 1.6)
      const toastWidth = 340
      const toastX = (this.viewport.width - toastWidth) / 2

      drawHudPanel(this.hudGraphics, toastX, 24, toastWidth, 64, 0.18 * alpha)
      this.toastTitle.alpha = alpha
      this.toastHint.alpha = alpha * 0.86
    } else {
      this.toastTitle.alpha = 0
      this.toastHint.alpha = 0
    }

    if (snapshot.encounterState === 'down') {
      this.centerTitle.text = '战术链路中断'
      this.centerHint.text = '返回基地完成结算，然后重新部署。'
      this.centerTitle.alpha = 1
      this.centerHint.alpha = 0.92

      drawHudPanel(this.hudGraphics, this.viewport.width / 2 - 220, this.viewport.height / 2 - 84, 440, 164, 0.18)
      this.hudGraphics.rect(this.viewport.width / 2 - 180, this.viewport.height / 2 - 30, 360, 2).fill({
        color: palette.danger,
        alpha: 0.24,
      })
    } else {
      this.centerTitle.alpha = 0
      this.centerHint.alpha = 0
    }
  }

  private layoutHud(): void {
    this.toastTitle.position.set(this.viewport.width / 2, 46)
    this.toastHint.position.set(this.viewport.width / 2, 68)
    const bottomMargin = 24
    const leftMargin = 24
    const beltBaseY = this.viewport.height - bottomMargin - 46
    this.healthText.position.set(leftMargin + 24, beltBaseY - 84)
    this.waveText.position.set(this.viewport.width - 288, 42)
    this.enemyText.position.set(this.viewport.width - 288, 68)
    this.bossText.position.set(this.viewport.width / 2, 112)
    this.centerTitle.position.set(this.viewport.width / 2, this.viewport.height / 2 - 20)
    this.centerHint.position.set(this.viewport.width / 2, this.viewport.height / 2 + 16)

    let x = leftMargin
    const y = beltBaseY

    for (const label of this.weaponLabels) {
      label.position.set(x + 18, y + 15)
      x += 148
    }
  }
}

function drawHudPanel(graphics: Graphics, x: number, y: number, width: number, height: number, accentAlpha: number): void {
  const cut = 12
  graphics.poly([
    x + cut, y,
    x + width, y,
    x + width, y + height - cut,
    x + width - cut, y + height,
    x, y + height,
    x, y + cut
  ]).fill({ color: palette.uiPanel, alpha: 0.85 })
  
  graphics.poly([
    x + cut, y,
    x + width, y,
    x + width, y + height - cut,
    x + width - cut, y + height,
    x, y + height,
    x, y + cut
  ]).stroke({ width: 1.5, color: palette.frame, alpha: 0.3, alignment: 0.5 })
  
  graphics.rect(x + 16, y + 8, 30, 2).fill({ color: palette.panelWarm, alpha: 0.5 + accentAlpha * 0.5 })
  drawCornerTicks(graphics, x + 8, y + 8, width - 16, height - 16)
}

function drawCornerTicks(graphics: Graphics, x: number, y: number, width: number, height: number): void {
  const size = 14
  graphics.moveTo(x, y + size)
  graphics.lineTo(x, y)
  graphics.lineTo(x + size, y)
  graphics.moveTo(x + width - size, y)
  graphics.lineTo(x + width, y)
  graphics.lineTo(x + width, y + size)
  graphics.moveTo(x + width, y + height - size)
  graphics.lineTo(x + width, y + height)
  graphics.lineTo(x + width - size, y + height)
  graphics.moveTo(x + size, y + height)
  graphics.lineTo(x, y + height)
  graphics.lineTo(x, y + height - size)
  graphics.stroke({ width: 1.1, color: palette.panelLine, alpha: 0.18, cap: 'round', join: 'round' })
}
