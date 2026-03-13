import { palette } from './theme/palette'

export type HostileType = 'melee' | 'ranged' | 'charger' | 'boss'

export type HostileMode = 'advance' | 'aim' | 'windup' | 'charge' | 'recover'

export interface HostileDefinition {
  type: HostileType
  label: string
  radius: number
  maxHealth: number
  moveSpeed: number
  contactDamage: number
  contactInterval: number
  attackCooldown: number
  attackWindup?: number
  attackRange?: number
  preferredDistance?: number
  projectileSpeed?: number
  projectileRadius?: number
  projectileDamage?: number
  chargeTriggerDistance?: number
  chargeSpeed?: number
  chargeDuration?: number
  recoverDuration?: number
  colors: {
    body: number
    edge: number
    glow: number
  }
}

export const hostileByType: Record<HostileType, HostileDefinition> = {
  melee: {
    type: 'melee',
    label: 'Pursuer',
    radius: 18,
    maxHealth: 34,
    moveSpeed: 142,
    contactDamage: 12,
    contactInterval: 0.58,
    attackCooldown: 0.24,
    colors: {
      body: palette.enemyMelee,
      edge: palette.enemyEdge,
      glow: palette.enemyMeleeGlow,
    },
  },
  ranged: {
    type: 'ranged',
    label: 'Marksman',
    radius: 17,
    maxHealth: 30,
    moveSpeed: 96,
    contactDamage: 10,
    contactInterval: 0.8,
    attackCooldown: 1.45,
    attackWindup: 0.42,
    attackRange: 420,
    preferredDistance: 250,
    projectileSpeed: 320,
    projectileRadius: 7,
    projectileDamage: 12,
    colors: {
      body: palette.enemyRanged,
      edge: palette.enemyEdge,
      glow: palette.enemyRangedGlow,
    },
  },
  charger: {
    type: 'charger',
    label: 'Breaker',
    radius: 20,
    maxHealth: 48,
    moveSpeed: 94,
    contactDamage: 20,
    contactInterval: 0.72,
    attackCooldown: 2.4,
    attackWindup: 0.56,
    chargeTriggerDistance: 240,
    chargeSpeed: 560,
    chargeDuration: 0.28,
    recoverDuration: 0.5,
    colors: {
      body: palette.enemyCharger,
      edge: palette.enemyEdge,
      glow: palette.enemyChargerGlow,
    },
  },
  boss: {
    type: 'boss',
    label: 'Aegis Prime',
    radius: 34,
    maxHealth: 520,
    moveSpeed: 82,
    contactDamage: 26,
    contactInterval: 0.8,
    attackCooldown: 1.5,
    attackWindup: 0.72,
    attackRange: 480,
    preferredDistance: 250,
    projectileSpeed: 290,
    projectileRadius: 8,
    projectileDamage: 16,
    colors: {
      body: palette.enemyBoss,
      edge: palette.enemyEdge,
      glow: palette.enemyBossGlow,
    },
  },
}
