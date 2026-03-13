export type WeaponType = 'machineGun' | 'grenade' | 'sniper'

export interface WeaponDefinition {
  slot: 1 | 2 | 3
  id: WeaponType
  label: string
  hint: string
  cooldown: number
  range: number
  effectWidth: number
  effectDuration: number
  splashRadius?: number
}

export const weaponLoadout: WeaponDefinition[] = [
  {
    slot: 1,
    id: 'machineGun',
    label: 'Machine Gun',
    hint: 'High cadence suppression with stable mid-range damage.',
    cooldown: 0.085,
    range: 560,
    effectWidth: 4.5,
    effectDuration: 0.09,
  },
  {
    slot: 2,
    id: 'grenade',
    label: 'Grenade',
    hint: 'Short throw arc with blast damage at the landing point.',
    cooldown: 0.46,
    range: 360,
    effectWidth: 0,
    effectDuration: 0,
    splashRadius: 66,
  },
  {
    slot: 3,
    id: 'sniper',
    label: 'Sniper',
    hint: 'Piercing precision shot with heavy single-line damage.',
    cooldown: 0.72,
    range: 100000,
    effectWidth: 9,
    effectDuration: 0.16,
  },
]

export const weaponBySlot = {
  1: weaponLoadout[0],
  2: weaponLoadout[1],
  3: weaponLoadout[2],
} as const
