import type { ArenaBounds } from '../core/contracts'

export function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value))
}

export function lerp(start: number, end: number, t: number): number {
  return start + (end - start) * t
}

export function easeOutCubic(value: number): number {
  return 1 - Math.pow(1 - value, 3)
}

export function clampToDistance(
  point: { x: number; y: number },
  origin: { x: number; y: number },
  maxDistance: number,
): { x: number; y: number } {
  const deltaX = point.x - origin.x
  const deltaY = point.y - origin.y
  const distance = Math.hypot(deltaX, deltaY)

  if (distance <= maxDistance || distance <= 0.0001) {
    return point
  }

  const scale = maxDistance / distance

  return {
    x: origin.x + deltaX * scale,
    y: origin.y + deltaY * scale,
  }
}

export function clipToArena(origin: { x: number; y: number }, target: { x: number; y: number }, arena: ArenaBounds): { x: number; y: number } {
  let bestT = 1

  if (target.x !== origin.x) {
    bestT = resolveIntersection(bestT, (arena.left - origin.x) / (target.x - origin.x), origin, target, arena, 'x')
    bestT = resolveIntersection(bestT, (arena.right - origin.x) / (target.x - origin.x), origin, target, arena, 'x')
  }

  if (target.y !== origin.y) {
    bestT = resolveIntersection(bestT, (arena.top - origin.y) / (target.y - origin.y), origin, target, arena, 'y')
    bestT = resolveIntersection(bestT, (arena.bottom - origin.y) / (target.y - origin.y), origin, target, arena, 'y')
  }

  return {
    x: origin.x + (target.x - origin.x) * bestT,
    y: origin.y + (target.y - origin.y) * bestT,
  }
}

export function segmentCircleIntersection(
  origin: { x: number; y: number },
  target: { x: number; y: number },
  circle: { x: number; y: number },
  radius: number,
): number | null {
  const deltaX = target.x - origin.x
  const deltaY = target.y - origin.y
  const offsetX = origin.x - circle.x
  const offsetY = origin.y - circle.y
  const a = deltaX * deltaX + deltaY * deltaY
  const b = 2 * (offsetX * deltaX + offsetY * deltaY)
  const c = offsetX * offsetX + offsetY * offsetY - radius * radius
  const discriminant = b * b - 4 * a * c

  if (a <= 0.0001 || discriminant < 0) {
    return null
  }

  const root = Math.sqrt(discriminant)
  const near = (-b - root) / (2 * a)
  const far = (-b + root) / (2 * a)

  if (near >= 0 && near <= 1) {
    return near
  }

  if (far >= 0 && far <= 1) {
    return far
  }

  return null
}

function resolveIntersection(
  currentBest: number,
  candidateT: number,
  origin: { x: number; y: number },
  target: { x: number; y: number },
  arena: ArenaBounds,
  axis: 'x' | 'y',
): number {
  if (candidateT <= 0 || candidateT > currentBest) {
    return currentBest
  }

  const pointX = origin.x + (target.x - origin.x) * candidateT
  const pointY = origin.y + (target.y - origin.y) * candidateT

  if (axis === 'x' && pointY >= arena.top && pointY <= arena.bottom) {
    return candidateT
  }

  if (axis === 'y' && pointX >= arena.left && pointX <= arena.right) {
    return candidateT
  }

  return currentBest
}
