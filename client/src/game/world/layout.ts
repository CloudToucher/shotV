import type { ArenaBounds, WorldMarker, WorldObstacle } from '../core/contracts'

export interface WorldMapLayout {
  id: string
  seed: number
  bounds: ArenaBounds
  playerSpawn: { x: number; y: number }
  bossSpawn: { x: number; y: number }
  enemySpawns: Array<{ x: number; y: number }>
  obstacles: WorldObstacle[]
  markers: WorldMarker[]
}

export interface CombatLayoutInput {
  routeId: string
  zoneId: string
  zoneLabel: string
  threatLevel: number
  allowsExtraction: boolean
  seed: number
}

const WORLD_MARGIN = 140

export function createCombatWorldLayout(input: CombatLayoutInput): WorldMapLayout {
  const width = 2800 + input.threatLevel * 260
  const height = 2200 + input.threatLevel * 220
  const bounds: ArenaBounds = {
    left: 0,
    top: 0,
    right: width,
    bottom: height,
  }
  const playerSpawn = { x: width * 0.5, y: height - 220 }
  const bossSpawn = { x: width * 0.5, y: 220 }
  const exitPoint = { x: width - 240, y: height - 260 }
  const random = createRng(input.seed)
  const obstacles: WorldObstacle[] = []
  const safetyRects = [
    createRect(playerSpawn.x - 180, playerSpawn.y - 160, 360, 260),
    createRect(bossSpawn.x - 220, bossSpawn.y - 160, 440, 260),
    createRect(exitPoint.x - 180, exitPoint.y - 150, 360, 240),
    createRect(width * 0.5 - 120, 0, 240, height),
    createRect(playerSpawn.x - 120, playerSpawn.y - 110, exitPoint.x - playerSpawn.x + 240, 220),
  ]
  const obstacleTarget = 24 + input.threatLevel * 6
  let index = 0
  let attempts = 0

  while (obstacles.length < obstacleTarget && attempts < obstacleTarget * 24) {
    attempts += 1

    const obstacle: WorldObstacle = {
      id: `combat-obstacle-${index}`,
      x: WORLD_MARGIN + random() * (width - WORLD_MARGIN * 2 - 240),
      y: WORLD_MARGIN + random() * (height - WORLD_MARGIN * 2 - 220),
      width: 90 + random() * (input.threatLevel >= 3 ? 210 : 170),
      height: 72 + random() * (input.threatLevel >= 2 ? 180 : 140),
      kind: random() > 0.55 ? 'wall' : 'cover' as const,
    }

    if (safetyRects.some((rect) => intersectsRect(rect, obstacle))) {
      continue
    }

    if (obstacles.some((existing) => intersectsRect(expandRect(existing, 48), obstacle))) {
      continue
    }

    obstacles.push(obstacle)
    index += 1
  }

  const enemySpawns = createCombatSpawnPoints(bounds, obstacles, playerSpawn)
  const markers: WorldMarker[] = [
    { id: 'entry', x: playerSpawn.x, y: playerSpawn.y + 90, label: '\u6295\u9001\u70b9', kind: 'entry' },
    {
      id: 'objective',
      x: bossSpawn.x,
      y: bossSpawn.y,
      label: `${input.zoneLabel}核心`,
      kind: 'objective',
    },
    {
      id: 'exit',
      x: exitPoint.x,
      y: exitPoint.y,
      label: input.allowsExtraction ? '撤离出口' : '区域出口',
      kind: 'extraction',
    },
  ]

  return {
    id: `${input.routeId}:${input.zoneId}`,
    seed: input.seed,
    bounds,
    playerSpawn,
    bossSpawn,
    enemySpawns,
    obstacles,
    markers,
  }
}

export function createBaseWorldLayout(seed = 20260314): WorldMapLayout {
  const bounds: ArenaBounds = {
    left: 0,
    top: 0,
    right: 2240,
    bottom: 1680,
  }
  const playerSpawn = { x: 1120, y: 1320 }
  const markers: WorldMarker[] = [
    { id: 'command', x: 1120, y: 320, label: '\u6307\u6325\u53f0', kind: 'station' },
    { id: 'locker', x: 740, y: 720, label: '\u50a8\u7269\u67dc', kind: 'locker' },
    { id: 'workshop', x: 1490, y: 760, label: '\u5de5\u574a\u53f0', kind: 'station' },
    { id: 'launch', x: 1120, y: 1460, label: '\u51fa\u51fb\u95f8\u95e8', kind: 'entry' },
  ]
  const obstacles: WorldObstacle[] = [
    createObstacle('north-wall-left', 468, 176, 220, 42, 'wall', '\u6307\u6325\u5317\u5899'),
    createObstacle('north-wall-right', 1552, 176, 220, 42, 'wall', '\u89c2\u5bdf\u5317\u5899'),
    createObstacle('command-console', 1038, 368, 164, 70, 'station', '\u6307\u6325\u7ec8\u7aef'),
    createObstacle('command-rack-left', 952, 452, 82, 56, 'station', '\u526f\u63a7\u5236\u67dc'),
    createObstacle('command-rack-right', 1206, 452, 82, 56, 'station', '\u526f\u63a7\u5236\u67dc'),
    createObstacle('locker-bank-left', 654, 762, 84, 60, 'locker', '\u50a8\u7269\u67dc A'),
    createObstacle('locker-bank-right', 748, 762, 84, 60, 'locker', '\u50a8\u7269\u67dc B'),
    createObstacle('workbench-main', 1404, 800, 154, 62, 'station', '\u5de5\u574a\u53f0'),
    createObstacle('workbench-rack', 1580, 786, 78, 78, 'station', '\u7ef4\u4fee\u67dc'),
    createObstacle('cargo-crate-left', 886, 1106, 96, 58, 'cover', '\u8f6c\u8fd0\u7bb1'),
    createObstacle('cargo-crate-right', 1258, 1106, 96, 58, 'cover', '\u8f6c\u8fd0\u7bb1'),
    createObstacle('launch-pillar-left', 1030, 1492, 60, 74, 'wall', '\u95f8\u95e8\u4fa7\u67f1'),
    createObstacle('launch-pillar-right', 1150, 1492, 60, 74, 'wall', '\u95f8\u95e8\u4fa7\u67f1'),
    createObstacle('launch-console', 1088, 1386, 64, 42, 'station', '\u51fa\u51fb\u63a7\u5236\u7ec8\u7aef'),
  ]

  return {
    id: 'base-camp',
    seed,
    bounds,
    playerSpawn,
    bossSpawn: { x: 1120, y: 280 },
    enemySpawns: [],
    obstacles,
    markers,
  }
}
export function buildLayoutSeedFromText(text: string): number {
  let hash = 2166136261

  for (let index = 0; index < text.length; index += 1) {
    hash ^= text.charCodeAt(index)
    hash = Math.imul(hash, 16777619)
  }

  return Math.abs(hash >>> 0)
}

function createCombatSpawnPoints(
  bounds: ArenaBounds,
  obstacles: readonly WorldObstacle[],
  playerSpawn: { x: number; y: number },
): Array<{ x: number; y: number }> {
  const candidates = [
    { x: bounds.left + 160, y: bounds.top + 180 },
    { x: bounds.right - 160, y: bounds.top + 180 },
    { x: bounds.left + 160, y: bounds.bottom - 180 },
    { x: bounds.right - 160, y: bounds.bottom - 180 },
    { x: bounds.left + 120, y: bounds.bottom * 0.55 },
    { x: bounds.right - 120, y: bounds.bottom * 0.55 },
    { x: bounds.left + bounds.right * 0.28, y: bounds.top + 120 },
    { x: bounds.left + bounds.right * 0.72, y: bounds.top + 120 },
    { x: bounds.left + bounds.right * 0.22, y: bounds.bottom - 120 },
    { x: bounds.left + bounds.right * 0.78, y: bounds.bottom - 120 },
  ]

  return candidates.filter((point) => {
    if (Math.hypot(point.x - playerSpawn.x, point.y - playerSpawn.y) < 380) {
      return false
    }

    return !obstacles.some((obstacle) => pointInRect(point.x, point.y, expandRect(obstacle, 36)))
  })
}

function createObstacle(
  id: string,
  x: number,
  y: number,
  width: number,
  height: number,
  kind: WorldObstacle['kind'],
  label?: string,
): WorldObstacle {
  return { id, x, y, width, height, kind, label }
}

function createRect(x: number, y: number, width: number, height: number): Pick<WorldObstacle, 'x' | 'y' | 'width' | 'height'> {
  return { x, y, width, height }
}

function createRng(seed: number): () => number {
  let state = seed >>> 0

  return () => {
    state = Math.imul(state ^ (state >>> 15), 1 | state)
    state ^= state + Math.imul(state ^ (state >>> 7), 61 | state)

    return ((state ^ (state >>> 14)) >>> 0) / 4294967296
  }
}

function expandRect(rect: Pick<WorldObstacle, 'x' | 'y' | 'width' | 'height'>, padding: number) {
  return {
    x: rect.x - padding,
    y: rect.y - padding,
    width: rect.width + padding * 2,
    height: rect.height + padding * 2,
  }
}

function intersectsRect(
  left: Pick<WorldObstacle, 'x' | 'y' | 'width' | 'height'>,
  right: Pick<WorldObstacle, 'x' | 'y' | 'width' | 'height'>,
): boolean {
  return (
    left.x < right.x + right.width &&
    left.x + left.width > right.x &&
    left.y < right.y + right.height &&
    left.y + left.height > right.y
  )
}

function pointInRect(x: number, y: number, rect: Pick<WorldObstacle, 'x' | 'y' | 'width' | 'height'>): boolean {
  return x >= rect.x && x <= rect.x + rect.width && y >= rect.y && y <= rect.y + rect.height
}



