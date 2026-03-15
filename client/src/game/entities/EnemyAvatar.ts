import { Container, Graphics } from 'pixi.js'

import { hostileByType } from '../data/hostiles'
import type { HostileDefinition, HostileMode, HostileType } from '../data/types'
import { palette } from '../theme/palette'

export class EnemyAvatar {
  readonly container = new Container()

  private readonly glow = new Graphics()
  private readonly shell = new Graphics()
  private readonly edge = new Graphics()
  private readonly core = new Graphics()
  private readonly aimAnchor = new Container()
  private readonly aimBlade = new Graphics()
  private readonly healthBack = new Graphics()
  private readonly healthFill = new Graphics()

  private readonly definition: HostileDefinition
  private readonly seed: number
  private readonly type: HostileType

  private aimAngle = 0
  private lifeRatio = 1
  private motion = 0
  private mode: HostileMode = 'advance'
  private damageFlash = 0
  private attackPulse = 0

  constructor(type: HostileType, seed: number) {
    this.definition = hostileByType[type]
    this.seed = seed
    this.type = type

    this.drawBody(type)
    this.container.addChild(
      this.glow,
      this.shell,
      this.edge,
      this.core,
      this.aimAnchor,
      this.healthBack,
      this.healthFill,
    )
    this.aimAnchor.addChild(this.aimBlade)
  }

  setPosition(x: number, y: number): void {
    this.container.position.set(x, y)
  }

  setAimAngle(angle: number): void {
    this.aimAngle = angle
  }

  setLifeRatio(ratio: number): void {
    this.lifeRatio = clamp(ratio, 0, 1)
  }

  setMotion(amount: number): void {
    this.motion = clamp(amount, 0, 1)
  }

  setMode(mode: HostileMode): void {
    this.mode = mode
  }

  triggerDamageFlash(intensity = 1): void {
    this.damageFlash = Math.max(this.damageFlash, intensity)
  }

  triggerAttackPulse(intensity = 1): void {
    this.attackPulse = Math.max(this.attackPulse, intensity)
  }

  update(deltaSeconds: number, elapsedSeconds: number): void {
    this.damageFlash = Math.max(0, this.damageFlash - deltaSeconds * 5.5)
    this.attackPulse = Math.max(0, this.attackPulse - deltaSeconds * 4.4)

    const modePulse = modeStrengthByType[this.mode]
    const sway = Math.sin(elapsedSeconds * 4.4 + this.seed) * 0.035
    const shellScale = 1 + this.attackPulse * 0.06 + modePulse * 0.04
    const glowScale = 1 + this.motion * 0.08 + modePulse * 0.16 + this.damageFlash * 0.12

    this.glow.alpha = 0.12 + modePulse * 0.16 + this.damageFlash * 0.18
    this.glow.scale.set(glowScale)

    this.shell.scale.set(shellScale)
    this.edge.scale.set(shellScale)
    this.core.scale.set(1 + this.damageFlash * 0.24 + this.attackPulse * 0.14)

    const bodyFacing = this.type === 'charger' || this.type === 'boss' ? this.aimAngle : 0
    const bodyWobble = this.type === 'charger' || this.type === 'boss' ? sway * 0.35 : sway + (this.motion - 0.5) * 0.05

    this.glow.rotation = bodyFacing + bodyWobble * 0.4
    this.shell.rotation = bodyFacing + bodyWobble
    this.edge.rotation = this.shell.rotation
    this.core.rotation = bodyFacing + bodyWobble * 1.18
    this.container.rotation = 0
    this.edge.alpha = 0.72 + this.damageFlash * 0.18 + modePulse * 0.12
    this.core.alpha = 0.76 + this.attackPulse * 0.14 + this.damageFlash * 0.18
    this.shell.alpha = 0.9 + this.lifeRatio * 0.1
    this.container.alpha = 0.7 + this.lifeRatio * 0.3

    const aimDistance = this.definition.radius + 10 + modePulse * 6
    this.aimAnchor.position.set(Math.cos(this.aimAngle) * aimDistance, Math.sin(this.aimAngle) * aimDistance)
    this.aimAnchor.rotation = this.aimAngle
    this.aimAnchor.alpha = 0.28 + modePulse * 0.32 + this.attackPulse * 0.18
    this.aimAnchor.scale.set(1 + modePulse * 0.12 + this.attackPulse * 0.08)

    this.drawHealthBar()
  }

  destroy(): void {
    this.container.destroy({ children: true })
  }

  private drawBody(type: HostileType): void {
    const colors = this.definition.colors

    if (type === 'melee') {
      this.glow.poly([0, -20, 18, 0, 0, 20, -18, 0]).fill({ color: colors.glow, alpha: 0.28 })
      this.shell.poly([0, -15, 14, 0, 0, 15, -14, 0]).fill({ color: colors.body, alpha: 0.96 })
      this.edge.poly([0, -15, 14, 0, 0, 15, -14, 0]).stroke({
        width: 2,
        color: colors.edge,
        alpha: 0.82,
        alignment: 0.5,
        join: 'round',
      })
      this.core.rect(-4, -4, 8, 8).fill({ color: palette.arenaCore, alpha: 0.92 })
      this.aimBlade.poly([10, 0, -4, -4, -1, 0, -4, 4]).fill({ color: colors.edge, alpha: 0.88 })
      return
    }

    if (type === 'ranged') {
      this.glow.roundRect(-18, -18, 36, 36, 10).fill({ color: colors.glow, alpha: 0.24 })
      this.shell.roundRect(-12, -12, 24, 24, 7).fill({ color: colors.body, alpha: 0.95 })
      this.edge.roundRect(-12, -12, 24, 24, 7).stroke({
        width: 2,
        color: colors.edge,
        alpha: 0.82,
        alignment: 0.5,
      })
      this.core.roundRect(-5, -5, 10, 10, 3).fill({ color: palette.arenaCore, alpha: 0.92 })
      this.aimBlade.poly([12, 0, -5, -3, -2, 0, -5, 3]).fill({ color: colors.edge, alpha: 0.88 })
      return
    }

    if (type === 'charger') {
      this.glow.poly([18, 0, -4, -19, -12, -8, -12, 8, -4, 19]).fill({ color: colors.glow, alpha: 0.26 })
      this.shell.poly([15, 0, -6, -14, -10, -6, -10, 6, -6, 14]).fill({ color: colors.body, alpha: 0.95 })
      this.edge.poly([15, 0, -6, -14, -10, -6, -10, 6, -6, 14]).stroke({
        width: 2,
        color: colors.edge,
        alpha: 0.84,
        alignment: 0.5,
        join: 'round',
      })
      this.core.poly([6, 0, -4, -5, -1, 0, -4, 5]).fill({ color: palette.arenaCore, alpha: 0.92 })
      this.aimBlade.poly([13, 0, -5, -4, -1, 0, -5, 4]).fill({ color: colors.edge, alpha: 0.88 })
      return
    }

    this.glow.poly([22, 0, 10, -22, -12, -22, -24, 0, -12, 22, 10, 22]).fill({ color: colors.glow, alpha: 0.24 })
    this.shell.poly([19, 0, 8, -18, -10, -18, -20, 0, -10, 18, 8, 18]).fill({ color: colors.body, alpha: 0.96 })
    this.edge.poly([19, 0, 8, -18, -10, -18, -20, 0, -10, 18, 8, 18]).stroke({
      width: 2.2,
      color: colors.edge,
      alpha: 0.88,
      alignment: 0.5,
      join: 'round',
    })
    this.core.poly([10, 0, -6, -7, -1, 0, -6, 7]).fill({ color: palette.arenaCore, alpha: 0.92 })
    this.aimBlade.poly([18, 0, -6, -5, 0, 0, -6, 5]).fill({ color: colors.edge, alpha: 0.9 })
  }

  private drawHealthBar(): void {
    const visible = this.type === 'boss' || this.lifeRatio < 0.999 || this.damageFlash > 0.08

    this.healthBack.clear()
    this.healthFill.clear()

    if (!visible) {
      return
    }

    const width = this.type === 'boss' ? 62 : 30
    const y = -this.definition.radius - 14

    this.healthBack
      .roundRect(-width / 2, y, width, 4, 2)
      .fill({ color: palette.uiText, alpha: 0.18 })

    this.healthFill
      .roundRect(-width / 2, y, width * this.lifeRatio, 4, 2)
      .fill({ color: palette.dash, alpha: 0.92 })
  }
}

const modeStrengthByType: Record<HostileMode, number> = {
  advance: 0,
  aim: 0.48,
  windup: 0.74,
  charge: 1,
  recover: 0.24,
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value))
}
