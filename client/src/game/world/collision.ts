import type { ArenaBounds, WorldObstacle } from '../core/contracts'

export function resolveCircleWorldMovement(
  position: { x: number; y: number },
  nextX: number,
  nextY: number,
  radius: number,
  bounds: ArenaBounds,
  obstacles: readonly WorldObstacle[],
): { x: number; y: number } {
  const startX = clamp(position.x, bounds.left + radius, bounds.right - radius)
  const startY = clamp(position.y, bounds.top + radius, bounds.bottom - radius)
  let x = clamp(nextX, bounds.left + radius, bounds.right - radius)

  if (collidesAnyObstacle(x, startY, radius, obstacles)) {
    x = startX
  }

  let y = clamp(nextY, bounds.top + radius, bounds.bottom - radius)

  if (collidesAnyObstacle(x, y, radius, obstacles)) {
    y = startY
  }

  return { x, y }
}

export function clipSegmentToWorld(
  origin: { x: number; y: number },
  target: { x: number; y: number },
  bounds: ArenaBounds,
  obstacles: readonly WorldObstacle[],
  padding = 0,
): { x: number; y: number } {
  let bestT = clipSegmentToBoundsT(origin, target, bounds)

  for (const obstacle of obstacles) {
    const expanded = expandObstacle(obstacle, padding)
    const hit = segmentRectIntersection(origin, target, expanded)

    if (hit !== null && hit >= 0 && hit < bestT) {
      bestT = hit
    }
  }

  return {
    x: origin.x + (target.x - origin.x) * bestT,
    y: origin.y + (target.y - origin.y) * bestT,
  }
}

export function pointInsideObstacle(x: number, y: number, obstacle: Pick<WorldObstacle, 'x' | 'y' | 'width' | 'height'>): boolean {
  return x >= obstacle.x && x <= obstacle.x + obstacle.width && y >= obstacle.y && y <= obstacle.y + obstacle.height
}

function clipSegmentToBoundsT(origin: { x: number; y: number }, target: { x: number; y: number }, bounds: ArenaBounds): number {
  let bestT = 1

  if (target.x !== origin.x) {
    bestT = resolveIntersection(bestT, (bounds.left - origin.x) / (target.x - origin.x), origin, target, bounds, 'x')
    bestT = resolveIntersection(bestT, (bounds.right - origin.x) / (target.x - origin.x), origin, target, bounds, 'x')
  }

  if (target.y !== origin.y) {
    bestT = resolveIntersection(bestT, (bounds.top - origin.y) / (target.y - origin.y), origin, target, bounds, 'y')
    bestT = resolveIntersection(bestT, (bounds.bottom - origin.y) / (target.y - origin.y), origin, target, bounds, 'y')
  }

  return bestT
}

function segmentRectIntersection(
  origin: { x: number; y: number },
  target: { x: number; y: number },
  rect: { left: number; top: number; right: number; bottom: number },
): number | null {
  const deltaX = target.x - origin.x
  const deltaY = target.y - origin.y
  let entry = 0
  let exit = 1

  const checks: Array<[number, number]> = [
    [-deltaX, origin.x - rect.left],
    [deltaX, rect.right - origin.x],
    [-deltaY, origin.y - rect.top],
    [deltaY, rect.bottom - origin.y],
  ]

  for (const [p, q] of checks) {
    if (p === 0) {
      if (q < 0) {
        return null
      }

      continue
    }

    const ratio = q / p

    if (p < 0) {
      entry = Math.max(entry, ratio)
    } else {
      exit = Math.min(exit, ratio)
    }

    if (entry > exit) {
      return null
    }
  }

  return entry > 0 ? entry : exit >= 0 ? 0 : null
}

function resolveIntersection(
  currentBest: number,
  candidateT: number,
  origin: { x: number; y: number },
  target: { x: number; y: number },
  bounds: ArenaBounds,
  axis: 'x' | 'y',
): number {
  if (candidateT <= 0 || candidateT > currentBest) {
    return currentBest
  }

  const pointX = origin.x + (target.x - origin.x) * candidateT
  const pointY = origin.y + (target.y - origin.y) * candidateT

  if (axis === 'x' && pointY >= bounds.top && pointY <= bounds.bottom) {
    return candidateT
  }

  if (axis === 'y' && pointX >= bounds.left && pointX <= bounds.right) {
    return candidateT
  }

  return currentBest
}

function expandObstacle(obstacle: Pick<WorldObstacle, 'x' | 'y' | 'width' | 'height'>, padding: number) {
  return {
    left: obstacle.x - padding,
    top: obstacle.y - padding,
    right: obstacle.x + obstacle.width + padding,
    bottom: obstacle.y + obstacle.height + padding,
    width: obstacle.width + padding * 2,
    height: obstacle.height + padding * 2,
  }
}

function collidesAnyObstacle(
  x: number,
  y: number,
  padding: number,
  obstacles: readonly WorldObstacle[],
): boolean {
  for (const obstacle of obstacles) {
    if (pointInBounds(x, y, expandObstacle(obstacle, padding))) {
      return true
    }
  }

  return false
}

function pointInBounds(x: number, y: number, bounds: { left: number; top: number; right: number; bottom: number }): boolean {
  return x >= bounds.left && x <= bounds.right && y >= bounds.top && y <= bounds.bottom
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value))
}
