import { clamp, clampToDistance } from '../combat/combatMath'
import type { ArenaBounds, InputSnapshot, ViewportSize } from '../core/contracts'
import type { PlayerAvatar } from '../entities/PlayerAvatar'
import { weaponBySlot, weaponLoadout } from '../data/weapons'
import type { WeaponDefinition, WeaponSlot, WeaponType } from '../data/types'

export interface CombatPlayerControllerCallbacks {
  onWeaponChanged: (weapon: WeaponDefinition, slot: WeaponSlot, silent: boolean) => void
  onDash: (position: { x: number; y: number }) => void
  onFire: (weapon: WeaponDefinition, aimPoint: { x: number; y: number }) => void
}

export class CombatPlayerController {
  private currentWeapon = weaponBySlot[1]
  private lastAimPoint = { x: 0, y: 0 }
  private shotCooldown = 0

  getCurrentWeapon(): WeaponDefinition {
    return this.currentWeapon
  }

  getLastAimPoint(): { x: number; y: number } {
    return this.lastAimPoint
  }

  tick(deltaSeconds: number): void {
    this.shotCooldown = Math.max(0, this.shotCooldown - deltaSeconds)
  }

  reset(viewport?: ViewportSize): void {
    this.currentWeapon = weaponBySlot[1]
    this.shotCooldown = 0

    if (viewport) {
      this.setDefaultAim(viewport)
    }
  }

  setDefaultAim(viewport: ViewportSize): void {
    this.lastAimPoint = {
      x: viewport.width / 2,
      y: viewport.height / 2 - 120,
    }
  }

  handleInput(
    input: InputSnapshot,
    player: PlayerAvatar,
    arenaBounds: ArenaBounds,
    callbacks: CombatPlayerControllerCallbacks,
  ): void {
    if (input.weaponSwitch && input.weaponSwitch !== this.currentWeapon.slot) {
      this.applyWeapon(input.weaponSwitch, callbacks, false)
    }

    const aimPoint = this.resolveAimPoint(input, player, arenaBounds)
    const playerPosition = player.getPosition()
    const aimAngle = Math.atan2(aimPoint.y - playerPosition.y, aimPoint.x - playerPosition.x)

    this.lastAimPoint = aimPoint
    player.setAimAngle(aimAngle)
    player.setMoveIntent(input.moveX, input.moveY)

    if (input.dashPressed && player.requestDash()) {
      callbacks.onDash(playerPosition)
    }

    if (input.shootHeld && this.shotCooldown === 0 && input.hasPointer) {
      callbacks.onFire(this.currentWeapon, aimPoint)
      this.shotCooldown = this.currentWeapon.cooldown
    }
  }

  notifySilentWeaponState(callbacks: CombatPlayerControllerCallbacks): void {
    callbacks.onWeaponChanged(this.currentWeapon, this.currentWeapon.slot, true)
  }

  restoreWeapon(weaponId: WeaponType, callbacks: CombatPlayerControllerCallbacks): void {
    const nextWeapon = weaponLoadout.find((weapon) => weapon.id === weaponId) ?? weaponBySlot[1]

    this.currentWeapon = nextWeapon
    this.shotCooldown = 0
    callbacks.onWeaponChanged(this.currentWeapon, this.currentWeapon.slot, true)
  }

  private resolveAimPoint(input: InputSnapshot, player: PlayerAvatar, arenaBounds: ArenaBounds): { x: number; y: number } {
    const playerPosition = player.getPosition()

    if (input.hasPointer) {
      const clamped = {
        x: clamp(input.pointerX, arenaBounds.left, arenaBounds.right),
        y: clamp(input.pointerY, arenaBounds.top, arenaBounds.bottom),
      }

      return this.currentWeapon.id === 'grenade' ? clampToDistance(clamped, playerPosition, this.currentWeapon.range) : clamped
    }

    const aimAngle = player.getAimAngle()
    const idleDistance = this.currentWeapon.id === 'grenade' ? Math.min(this.currentWeapon.range, 120) : 120

    return {
      x: playerPosition.x + Math.cos(aimAngle) * idleDistance,
      y: playerPosition.y + Math.sin(aimAngle) * idleDistance,
    }
  }

  private applyWeapon(slot: WeaponSlot, callbacks: CombatPlayerControllerCallbacks, silent: boolean): void {
    this.currentWeapon = weaponBySlot[slot]
    this.shotCooldown = 0
    callbacks.onWeaponChanged(this.currentWeapon, slot, silent)
  }
}
