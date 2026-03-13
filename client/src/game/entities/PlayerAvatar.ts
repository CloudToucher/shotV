import { Container, Graphics } from 'pixi.js'

import type { ArenaBounds } from '../core/contracts'
import { palette } from '../theme/palette'
import type { WeaponType } from '../weapons/weaponConfig'

const MAX_SPEED = 260
const ACCELERATION = 7.25
const BRAKE = 9.5
const DASH_SPEED = 760
const DASH_DURATION = 0.14
const DASH_COOLDOWN = 0.65

export class PlayerAvatar {
  readonly container = new Container()

  private readonly glow = new Graphics()
  private readonly shell = new Graphics()
  private readonly edge = new Graphics()
  private readonly core = new Graphics()
  private readonly arrowAnchor = new Container()
  private readonly arrow = new Graphics()
  private readonly arrowGlow = new Graphics()

  private readonly position = { x: 0, y: 0 }
  private readonly velocity = { x: 0, y: 0 }
  private readonly moveIntent = { x: 0, y: 0 }
  private readonly dashDirection = { x: 0, y: -1 }

  private aimAngle = -Math.PI / 2
  private dashTime = 0
  private dashCooldown = 0
  private shotKick = 0
  private dashPulse = 0
  private arrowDistanceBase = 38

  constructor() {
    this.glow
      .roundRect(-22, -22, 44, 44, 14)
      .fill({ color: palette.playerEdge, alpha: 0.12 })

    this.shell
      .roundRect(-14, -14, 28, 28, 6)
      .fill({ color: palette.playerBody, alpha: 0.98 })

    this.edge
      .roundRect(-14, -14, 28, 28, 6)
      .stroke({ width: 2, color: palette.playerEdge, alpha: 0.88, alignment: 0.5 })

    this.core
      .roundRect(-5, -5, 10, 10, 2)
      .fill({ color: palette.playerCore, alpha: 0.92 })

    this.setWeaponStyle('machineGun')
    this.arrowAnchor.addChild(this.arrowGlow, this.arrow)
    this.container.addChild(this.glow, this.shell, this.edge, this.core, this.arrowAnchor)
  }

  setPosition(x: number, y: number): void {
    this.position.x = x
    this.position.y = y
    this.container.position.set(x, y)
  }

  getPosition(): { x: number; y: number } {
    return { x: this.position.x, y: this.position.y }
  }

  setMoveIntent(x: number, y: number): void {
    const magnitude = Math.hypot(x, y)

    if (magnitude <= 0.0001) {
      this.moveIntent.x = 0
      this.moveIntent.y = 0
      return
    }

    this.moveIntent.x = x / magnitude
    this.moveIntent.y = y / magnitude
  }

  setAimAngle(angle: number): void {
    this.aimAngle = angle
  }

  setWeaponStyle(weaponType: WeaponType): void {
    const shape = arrowShapeByWeapon[weaponType]

    this.arrowDistanceBase = shape.distance
    this.arrow.clear()
    this.arrowGlow.clear()

    this.arrowGlow
      .poly(shape.glowPoints)
      .fill({ color: palette.accentSoft, alpha: 0.28 })

    this.arrow
      .poly(shape.points)
      .fill({ color: palette.accent, alpha: 0.98 })
  }

  requestDash(): boolean {
    if (this.dashCooldown > 0 || this.dashTime > 0) {
      return false
    }

    const magnitude = Math.hypot(this.moveIntent.x, this.moveIntent.y)

    if (magnitude > 0.0001) {
      this.dashDirection.x = this.moveIntent.x
      this.dashDirection.y = this.moveIntent.y
    } else {
      this.dashDirection.x = Math.cos(this.aimAngle)
      this.dashDirection.y = Math.sin(this.aimAngle)
    }

    this.dashTime = DASH_DURATION
    this.dashCooldown = DASH_COOLDOWN
    this.dashPulse = 1

    return true
  }

  triggerShot(intensity = 1): void {
    this.shotKick = Math.max(this.shotKick, intensity)
  }

  triggerWeaponSwap(): void {
    this.dashPulse = Math.max(this.dashPulse, 0.36)
  }

  getShotOrigin(): { x: number; y: number } {
    const reach = this.arrowDistanceBase + this.shotKick * 6 + this.dashPulse * 5

    return {
      x: this.position.x + Math.cos(this.aimAngle) * reach,
      y: this.position.y + Math.sin(this.aimAngle) * reach,
    }
  }

  getAimAngle(): number {
    return this.aimAngle
  }

  update(deltaSeconds: number, arena: ArenaBounds, elapsedSeconds: number): void {
    this.dashCooldown = Math.max(0, this.dashCooldown - deltaSeconds)
    this.shotKick = Math.max(0, this.shotKick - deltaSeconds * 9)
    this.dashPulse = Math.max(0, this.dashPulse - deltaSeconds * 4.5)

    if (this.dashTime > 0) {
      this.dashTime = Math.max(0, this.dashTime - deltaSeconds)
      this.velocity.x = this.dashDirection.x * DASH_SPEED
      this.velocity.y = this.dashDirection.y * DASH_SPEED
    } else {
      const hasIntent = this.moveIntent.x !== 0 || this.moveIntent.y !== 0
      const desiredVelocityX = this.moveIntent.x * MAX_SPEED
      const desiredVelocityY = this.moveIntent.y * MAX_SPEED
      const response = hasIntent ? ACCELERATION : BRAKE
      const blend = Math.min(1, response * deltaSeconds)

      this.velocity.x += (desiredVelocityX - this.velocity.x) * blend
      this.velocity.y += (desiredVelocityY - this.velocity.y) * blend
    }

    this.position.x += this.velocity.x * deltaSeconds
    this.position.y += this.velocity.y * deltaSeconds

    this.position.x = clamp(this.position.x, arena.left, arena.right)
    this.position.y = clamp(this.position.y, arena.top, arena.bottom)

    if ((this.position.x === arena.left && this.velocity.x < 0) || (this.position.x === arena.right && this.velocity.x > 0)) {
      this.velocity.x = 0
    }

    if ((this.position.y === arena.top && this.velocity.y < 0) || (this.position.y === arena.bottom && this.velocity.y > 0)) {
      this.velocity.y = 0
    }

    this.container.position.set(this.position.x, this.position.y)

    const speed = Math.hypot(this.velocity.x, this.velocity.y)
    const speedFactor = Math.min(1, speed / MAX_SPEED)
    const dashFactor = this.dashTime > 0 ? 1 : this.dashPulse
    const aimX = Math.cos(this.aimAngle)
    const aimY = Math.sin(this.aimAngle)
    const arrowDistance = this.arrowDistanceBase + this.shotKick * 8 + dashFactor * 10

    this.glow.alpha = 0.12 + speedFactor * 0.1 + dashFactor * 0.18 + (Math.sin(elapsedSeconds * 5.2) * 0.02 + 0.02)
    this.glow.scale.set(1 + speedFactor * 0.2 + dashFactor * 0.16)

    this.shell.scale.set(1 + dashFactor * 0.05, 1 + dashFactor * 0.05)
    this.edge.alpha = 0.74 + dashFactor * 0.26
    this.shell.rotation = this.velocity.x * 0.0009
    this.edge.rotation = this.shell.rotation
    this.core.rotation = -this.shell.rotation * 1.4

    this.arrowAnchor.position.set(aimX * arrowDistance, aimY * arrowDistance)
    this.arrowAnchor.rotation = this.aimAngle
    this.arrowAnchor.scale.set(1 + this.shotKick * 0.16 + dashFactor * 0.16)
    this.arrowGlow.alpha = 0.22 + dashFactor * 0.24
  }

  destroy(): void {
    this.container.destroy({ children: true })
  }
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value))
}

const arrowShapeByWeapon: Record<WeaponType, { distance: number; points: number[]; glowPoints: number[] }> = {
  machineGun: {
    distance: 38,
    points: [12, 0, -10, -9, -2, 0, -10, 9],
    glowPoints: [16, 0, -7, -11, 1, 0, -7, 11],
  },
  grenade: {
    distance: 42,
    points: [13, 0, 4, -11, -10, -5, -5, 0, -10, 5, 4, 11],
    glowPoints: [17, 0, 6, -13, -13, -6, -7, 0, -13, 6, 6, 13],
  },
  sniper: {
    distance: 48,
    points: [19, 0, -13, -6, -4, 0, -13, 6],
    glowPoints: [24, 0, -15, -8, -2, 0, -15, 8],
  },
}
