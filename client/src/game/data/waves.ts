import type { HostileType } from './types'

export interface SpawnOrder {
  type: HostileType
  delay: number
}

export function buildWaveOrders(wave: number): SpawnOrder[] {
  if (wave >= 5) {
    return [
      {
        type: 'boss',
        delay: 0.35,
      },
    ]
  }

  const orders: SpawnOrder[] = []
  const meleeCount = 2 + wave
  const rangedCount = wave >= 2 ? 1 + Math.floor((wave - 2) / 2) : 0
  const chargerCount = wave >= 3 ? 1 + Math.floor((wave - 3) / 2) : 0

  pushOrders(orders, meleeCount, 'melee')
  pushOrders(orders, rangedCount, 'ranged')
  pushOrders(orders, chargerCount, 'charger')

  if (wave >= 4 && wave % 2 === 0) {
    pushOrders(orders, 1, 'ranged')
  }

  return shuffle(orders)
}

export function buildWaveHint(wave: number): string {
  if (wave >= 5) {
    return '区域主核正在接管战场'
  }

  if (wave < 2) {
    return '追猎体开始从外围压近'
  }

  if (wave < 3) {
    return '射击体加入火力线'
  }

  return '冲锋体开始压迫位移节奏'
}

function pushOrders(target: SpawnOrder[], count: number, type: HostileType): void {
  for (let index = 0; index < count; index += 1) {
    target.push({
      type,
      delay: 0.28 + Math.random() * 0.18,
    })
  }
}

function shuffle<T>(items: T[]): T[] {
  for (let index = items.length - 1; index > 0; index -= 1) {
    const swapIndex = Math.floor(Math.random() * (index + 1))
    const next = items[index]

    items[index] = items[swapIndex]
    items[swapIndex] = next
  }

  return items
}
