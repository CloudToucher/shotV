import type { WeaponType } from '../data/types'

export const RUN_QUICK_SLOT_COUNT = 4

export interface InventoryItemRecord {
  id: string
  itemId: string
  quantity: number
  x: number
  y: number
  width: number
  height: number
  rotated: boolean
}

export interface GridInventoryState {
  columns: number
  rows: number
  items: InventoryItemRecord[]
  quickSlots: (string | null)[]
}

export interface InventoryState {
  stashColumns: number
  stashRows: number
  equippedWeaponIds: WeaponType[]
  equippedArmorId: string | null
  storedItems: InventoryItemRecord[]
}

export function createInitialInventoryState(): InventoryState {
  return {
    stashColumns: 8,
    stashRows: 6,
    equippedWeaponIds: ['machineGun', 'grenade', 'sniper'],
    equippedArmorId: null,
    storedItems: [],
  }
}

export function createInitialRunInventoryState(): GridInventoryState {
  return {
    columns: 6,
    rows: 4,
    items: [],
    quickSlots: Array.from({ length: RUN_QUICK_SLOT_COUNT }, () => null),
  }
}
