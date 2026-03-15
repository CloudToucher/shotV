import type { EnemyAvatar } from '../entities/EnemyAvatar'
import type { HostileDefinition, HostileMode, HostileType, WeaponType } from '../data/types'

export interface NeedleProjectile {
  startX: number
  startY: number
  endX: number
  endY: number
  dirX: number
  dirY: number
  age: number
  duration: number
  length: number
  width: number
  color: number
  coreColor: number
}

export interface BurstRing {
  x: number
  y: number
  age: number
  duration: number
  startRadius: number
  endRadius: number
  color: number
  width: number
}

export interface MuzzleFlash {
  x: number
  y: number
  angle: number
  age: number
  duration: number
  size: number
  weaponType: WeaponType
}

export interface GrenadeProjectile {
  startX: number
  startY: number
  endX: number
  endY: number
  age: number
  duration: number
}

export interface GrenadeExplosion {
  x: number
  y: number
  age: number
  duration: number
  radius: number
}

export interface ImpactParticle {
  x: number
  y: number
  vx: number
  vy: number
  rotation: number
  spin: number
  age: number
  duration: number
  length: number
  width: number
  color: number
  alpha: number
  drag: number
}

export interface DashAfterimage {
  x: number
  y: number
  aimAngle: number
  weaponType: WeaponType
  age: number
  duration: number
  scale: number
}

export interface EnemyProjectile {
  x: number
  y: number
  vx: number
  vy: number
  radius: number
  damage: number
  age: number
  duration: number
  color: number
  glowColor: number
}

export interface EnemyActor {
  id: number
  type: HostileType
  definition: HostileDefinition
  avatar: EnemyAvatar
  x: number
  y: number
  health: number
  contactCooldown: number
  attackCooldown: number
  mode: HostileMode
  modeTimer: number
  chargeDirX: number
  chargeDirY: number
  facingAngle: number
  phase: 1 | 2
  pattern: 'fan' | 'nova'
  phaseShifted: boolean
}

export interface SegmentHit {
  enemy: EnemyActor
  t: number
  pointX: number
  pointY: number
}

export type EncounterState = 'active' | 'down' | 'clear'
