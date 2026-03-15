import type { WeaponDefinition, WeaponSlot } from './types'

export const weaponLoadout: WeaponDefinition[] = [
  {
    slot: 1,
    id: 'machineGun',
    label: '机枪',
    hint: '中距离压制主武器，射速高，持续火力稳定。',
    cooldown: 0.085,
    range: 560,
    effectWidth: 4.5,
    effectDuration: 0.09,
  },
  {
    slot: 2,
    id: 'grenade',
    label: '榴弹',
    hint: '短抛物线投射，落点爆炸，适合清理密集敌群。',
    cooldown: 0.46,
    range: 360,
    effectWidth: 0,
    effectDuration: 0,
    splashRadius: 66,
  },
  {
    slot: 3,
    id: 'sniper',
    label: '狙击',
    hint: '高穿透精确射击，单线爆发高，适合点杀目标。',
    cooldown: 0.72,
    range: 100000,
    effectWidth: 9,
    effectDuration: 0.16,
  },
]

export const weaponBySlot: Record<WeaponSlot, WeaponDefinition> = {
  1: weaponLoadout[0],
  2: weaponLoadout[1],
  3: weaponLoadout[2],
}
