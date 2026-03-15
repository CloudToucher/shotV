export type WeaponType = 'machineGun' | 'grenade' | 'sniper'

export type WeaponSlot = 1 | 2 | 3

export interface WeaponDefinition {
  slot: WeaponSlot
  id: WeaponType
  label: string
  hint: string
  cooldown: number
  range: number
  effectWidth: number
  effectDuration: number
  splashRadius?: number
}

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

export type ItemCategory = 'resource' | 'intel' | 'boss' | 'consumable'

export interface ResourceBundle {
  salvage: number
  alloy: number
  research: number
}

export interface ItemDefinition {
  id: string
  label: string
  shortLabel: string
  description: string
  category: ItemCategory
  width: number
  height: number
  maxStack: number
  tint: number
  accent: number
  recoveredResources: ResourceBundle
  use?: {
    heals?: number
    explosionDamage?: number
    explosionRadius?: number
    refreshDash?: boolean
  }
}
