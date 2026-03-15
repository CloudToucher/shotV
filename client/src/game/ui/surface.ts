import { Graphics, TextStyle, type ColorSource } from 'pixi.js'

import type { ArenaBounds, WorldMarker, WorldObstacle } from '../core/contracts'
import { palette } from '../theme/palette'

export const UI_FONT_FAMILY = 'Bahnschrift, Segoe UI Variable Display, Segoe UI, PingFang SC, Microsoft YaHei UI, Noto Sans SC, sans-serif'

export function createTextStyle(
  fontSize: number,
  fill: ColorSource,
  options: Partial<TextStyle> = {},
): TextStyle {
  return new TextStyle({
    fontFamily: UI_FONT_FAMILY,
    fontSize,
    fill,
    ...options,
  })
}

export function drawGlassPanel(graphics: Graphics, x: number, y: number, width: number, height: number, accentAlpha = 0.16): void {
  const radius = 18
  graphics.clear()
  graphics.roundRect(x + 8, y + 10, width - 6, height - 6, radius + 1).fill({ color: palette.obstacleShadow, alpha: 0.1 })
  graphics.roundRect(x, y, width, height, radius).fill({ color: palette.uiPanel, alpha: 0.5 + accentAlpha * 0.2 })
  graphics.roundRect(x + 2, y + 2, width - 4, height - 4, radius - 2).stroke({ width: 1, color: palette.arenaCore, alpha: 0.28, alignment: 0.5 })
  graphics.roundRect(x + 12, y + 10, width - 24, 16, 8).fill({ color: palette.arenaCore, alpha: 0.18 })
}

export interface FullScreenPanelOptions {
  screenWidth: number
  screenHeight: number
  x: number
  y: number
  width: number
  height: number
  headerHeight?: number
  footerHeight?: number
}

export function drawFullScreenPanelFrame(graphics: Graphics, options: FullScreenPanelOptions): void {
  const {
    screenWidth,
    screenHeight,
    x,
    y,
    width,
    height,
    headerHeight = 76,
    footerHeight = 48,
  } = options

  graphics.clear()
  graphics.rect(0, 0, screenWidth, screenHeight).fill({ color: palette.bgOuter, alpha: 0.7 })
  graphics.rect(0, 0, screenWidth, screenHeight).fill({ color: palette.frame, alpha: 0.035 })
  graphics.roundRect(x, y, width, height, 16).fill({ color: palette.uiPanel, alpha: 0.98 })
  graphics.roundRect(x, y, width, height, 16).stroke({ width: 1.8, color: palette.frame, alpha: 0.42, alignment: 0.5 })
  graphics.roundRect(x + 6, y + 6, width - 12, height - 12, 12).stroke({ width: 1, color: palette.frameSoft, alpha: 0.22, alignment: 0.5 })
  graphics.rect(x, y, width, headerHeight).fill({ color: palette.uiActive, alpha: 0.88 })
  graphics.rect(x + 20, y + 18, 52, 3).fill({ color: palette.panelWarm, alpha: 0.92 })
  graphics.rect(x + 20, y + headerHeight - 1, width - 40, 1).fill({ color: palette.panelLine, alpha: 0.42 })
  graphics.rect(x, y + height - footerHeight, width, footerHeight).fill({ color: palette.uiActive, alpha: 0.68 })
  graphics.rect(x + 20, y + height - footerHeight, width - 40, 1).fill({ color: palette.frame, alpha: 0.18 })
  drawCornerFrame(graphics, x + 10, y + 10, width - 20, height - 20, 20, palette.frame, 0.36, 1.4)
  drawCornerFrame(graphics, x + 24, y + 24, width - 48, height - 48, 14, palette.panelLine, 0.16, 1.1)
}

export function drawMapOverlayPanel(
  graphics: Graphics,
  x: number,
  y: number,
  width: number,
  height: number,
  accentColor: number = palette.frame,
): void {
  graphics.roundRect(x, y, width, height, 14).fill({ color: palette.uiPanel, alpha: 0.9 })
  graphics.roundRect(x, y, width, height, 14).stroke({ width: 1.2, color: accentColor, alpha: 0.24, alignment: 0.5 })
  graphics.roundRect(x + 2, y + 2, width - 4, height - 4, 12).stroke({ width: 1, color: palette.frameSoft, alpha: 0.12, alignment: 0.5 })
  graphics.rect(x + 14, y + 14, 34, 3).fill({ color: accentColor, alpha: 0.82 })
  drawCornerFrame(graphics, x + 8, y + 8, width - 16, height - 16, 10, accentColor, 0.18, 1)
}

export interface WorldSurfaceOptions {
  bounds: ArenaBounds
  gridSize: number
  mode?: 'combat' | 'base'
}

export function drawWorldSurface(graphics: Graphics, options: WorldSurfaceOptions): void {
  const { bounds, gridSize, mode = 'combat' } = options
  const width = bounds.right - bounds.left
  const height = bounds.bottom - bounds.top
  const majorStep = gridSize * 4

  graphics.clear()
  graphics.rect(bounds.left, bounds.top, width, height).fill({ color: palette.worldFloor })
  graphics.rect(bounds.left, bounds.top, width, height).stroke({ width: 1, color: palette.worldLineStrong, alpha: 0.03, alignment: 0.5 })

  for (let x = bounds.left + majorStep; x < bounds.right; x += majorStep) {
    graphics.moveTo(x, bounds.top)
    graphics.lineTo(x, bounds.bottom)
  }
  for (let y = bounds.top + majorStep; y < bounds.bottom; y += majorStep) {
    graphics.moveTo(bounds.left, y)
    graphics.lineTo(bounds.right, y)
  }
  graphics.stroke({ width: 1, color: palette.worldLineStrong, alpha: mode === 'combat' ? 0.02 : 0.015 })

  if (mode === 'base') {
    for (let y = bounds.top + 180; y < bounds.bottom; y += 320) {
      graphics.rect(bounds.left + 120, y, width - 240, 1).fill({ color: palette.worldLineStrong, alpha: 0.025 })
    }
  } else {
    for (let index = 0; index < 2; index += 1) {
      const laneInset = 96 + index * 54
      graphics.roundRect(bounds.left + laneInset, bounds.top + laneInset, width - laneInset * 2, height - laneInset * 2, 32).stroke({
        width: 1,
        color: palette.worldLineStrong,
        alpha: index === 0 ? 0.025 : 0.015,
        alignment: 0.5,
      })
    }
  }
}

export function drawWorldObstacles(graphics: Graphics, obstacles: readonly WorldObstacle[]): void {
  graphics.clear()

  for (const obstacle of obstacles) {
    const style = resolveObstacleStyle(obstacle.kind)
    const accentColor = style.accent ?? style.edge
    const radius = Math.max(10, Math.min(18, Math.min(obstacle.width, obstacle.height) * 0.2))
    const liftX = Math.max(8, Math.min(15, Math.min(obstacle.width, obstacle.height) * 0.12))
    const liftY = Math.max(10, Math.min(18, Math.min(obstacle.width, obstacle.height) * 0.15))
    const inset = Math.max(10, Math.min(18, Math.min(obstacle.width, obstacle.height) * 0.16))
    const faceX = obstacle.x + inset
    const faceY = obstacle.y + inset
    const faceWidth = Math.max(18, obstacle.width - inset * 2)
    const faceHeight = Math.max(18, obstacle.height - inset * 2)
    const faceRadius = Math.max(6, radius - 5)
    const accentWidth = Math.min(faceWidth * 0.26, 40)
    const handleY = faceY + Math.max(9, faceHeight * 0.22)

    drawCastShadow(graphics, obstacle.x, obstacle.y, obstacle.width, obstacle.height, radius, liftX, liftY)
    drawExtrudedFaces(graphics, obstacle.x, obstacle.y, obstacle.width, obstacle.height, radius, liftX, liftY, style.side, style.sideDark)
    graphics.roundRect(obstacle.x, obstacle.y, obstacle.width, obstacle.height, radius).fill({ color: style.fill, alpha: 1 })
    graphics.roundRect(obstacle.x, obstacle.y, obstacle.width, obstacle.height, radius).stroke({ width: 1.5, color: style.edge, alpha: 0.88, alignment: 0.5 })
    graphics.roundRect(obstacle.x + 2, obstacle.y + 2, obstacle.width - 4, obstacle.height - 4, Math.max(8, radius - 2)).stroke({
      width: 1,
      color: palette.arenaCore,
      alpha: 0.34,
      alignment: 0.5,
    })
    graphics.roundRect(obstacle.x + 8, obstacle.y + 6, obstacle.width - 16, Math.max(10, obstacle.height * 0.18), Math.max(7, radius - 4)).fill({
      color: palette.arenaCore,
      alpha: 0.26,
    })
    graphics.roundRect(faceX, faceY, faceWidth, faceHeight, faceRadius).fill({ color: style.inner, alpha: 1 })
    graphics.roundRect(faceX, faceY, faceWidth, faceHeight, faceRadius).stroke({ width: 1, color: style.edge, alpha: 0.22, alignment: 0.5 })
    graphics.roundRect(faceX + 3, faceY + 3, faceWidth - 6, Math.max(8, faceHeight * 0.16), Math.max(5, faceRadius - 3)).fill({ color: palette.arenaCore, alpha: 0.58 })
    graphics.roundRect(faceX + 8, faceY + faceHeight - 18, faceWidth - 16, 10, 999).fill({ color: style.sideDark, alpha: 0.12 })
    drawEmbossEdge(graphics, obstacle.x, obstacle.y, obstacle.width, obstacle.height, radius, palette.arenaCore, style.sideDark)
    drawEmbossEdge(graphics, faceX, faceY, faceWidth, faceHeight, faceRadius, palette.arenaCore, style.side)

    if (style.accent !== null) {
      graphics.roundRect(faceX, handleY, accentWidth, 5, 999).fill({ color: accentColor, alpha: 0.44 })
    }

    if (obstacle.kind === 'locker') {
      const dividerLeft = faceX + faceWidth * 0.34
      const dividerRight = faceX + faceWidth * 0.68
      graphics.moveTo(dividerLeft, faceY + 12)
      graphics.lineTo(dividerLeft, faceY + faceHeight - 12)
      graphics.moveTo(dividerRight, faceY + 12)
      graphics.lineTo(dividerRight, faceY + faceHeight - 12)
      graphics.stroke({ width: 1, color: style.edge, alpha: 0.38 })
      graphics.circle(faceX + accentWidth + 8, handleY + 2.5, 2.4).fill({ color: accentColor, alpha: 0.44 })
      continue
    }

    if (obstacle.kind === 'station') {
      const centerY = faceY + faceHeight * 0.5
      graphics.moveTo(faceX + 16, centerY)
      graphics.lineTo(faceX + faceWidth - 16, centerY)
      graphics.stroke({ width: 1.2, color: style.edge, alpha: 0.26, cap: 'round' })
      graphics.circle(faceX + faceWidth * 0.28, centerY, 2.8).fill({ color: accentColor, alpha: 0.42 })
      graphics.circle(faceX + faceWidth * 0.72, centerY, 2.8).fill({ color: accentColor, alpha: 0.42 })
      continue
    }

    if (obstacle.kind === 'cover') {
      graphics.moveTo(faceX + 14, faceY + faceHeight - 14)
      graphics.lineTo(faceX + faceWidth - 14, faceY + 14)
      graphics.stroke({ width: 1.1, color: style.edge, alpha: 0.24, cap: 'round' })
      continue
    }

    if (style.accent !== null) {
      const dividerX = faceX + faceWidth * 0.5
      graphics.moveTo(dividerX, faceY + 12)
      graphics.lineTo(dividerX, faceY + faceHeight - 12)
      graphics.stroke({ width: 1, color: style.edge, alpha: 0.2 })
    }
  }
}

function drawCastShadow(graphics: Graphics, x: number, y: number, width: number, height: number, radius: number, offsetX: number, offsetY: number): void {
  graphics.roundRect(x + offsetX * 0.7, y + offsetY * 0.8, width, height, radius + 1).fill({ color: palette.obstacleShadow, alpha: 0.22 })
  graphics.roundRect(x + offsetX * 1.15, y + offsetY * 1.25, width, height, radius + 3).fill({ color: palette.obstacleShadow, alpha: 0.13 })
  graphics.roundRect(x + offsetX * 1.5, y + offsetY * 1.75, width, height, radius + 6).fill({ color: palette.obstacleShadow, alpha: 0.07 })
}

function drawExtrudedFaces(
  graphics: Graphics,
  x: number,
  y: number,
  width: number,
  height: number,
  radius: number,
  offsetX: number,
  offsetY: number,
  bottomColor: number,
  sideColor: number,
): void {
  const rightFaceTop = y + radius
  const rightFaceBottom = y + height - radius
  const bottomFaceLeft = x + radius
  const bottomFaceRight = x + width - radius

  graphics.poly([
    width + x,
    rightFaceTop,
    width + x + offsetX,
    rightFaceTop + offsetY,
    width + x + offsetX,
    rightFaceBottom + offsetY,
    width + x,
    rightFaceBottom,
  ]).fill({ color: sideColor, alpha: 1 })

  graphics.poly([
    bottomFaceLeft,
    y + height,
    bottomFaceRight,
    y + height,
    bottomFaceRight + offsetX,
    y + height + offsetY,
    bottomFaceLeft + offsetX,
    y + height + offsetY,
  ]).fill({ color: bottomColor, alpha: 1 })

  graphics.poly([
    width + x - radius,
    y + height,
    width + x,
    y + height - radius,
    width + x + offsetX,
    y + height - radius + offsetY,
    width + x + offsetX,
    y + height + offsetY,
  ]).fill({ color: sideColor, alpha: 1 })
}

function drawEmbossEdge(
  graphics: Graphics,
  x: number,
  y: number,
  width: number,
  height: number,
  radius: number,
  highlightColor: number,
  shadowColor: number,
): void {
  const inset = 1.5
  const edgeInset = Math.max(6, radius * 0.58)

  graphics.moveTo(x + edgeInset, y + inset)
  graphics.lineTo(x + width - edgeInset, y + inset)
  graphics.moveTo(x + inset, y + edgeInset)
  graphics.lineTo(x + inset, y + height - edgeInset)
  graphics.stroke({ width: 1.1, color: highlightColor, alpha: 0.58, cap: 'round' })

  graphics.moveTo(x + width - inset, y + edgeInset)
  graphics.lineTo(x + width - inset, y + height - edgeInset)
  graphics.moveTo(x + edgeInset, y + height - inset)
  graphics.lineTo(x + width - edgeInset, y + height - inset)
  graphics.stroke({ width: 1.1, color: shadowColor, alpha: 0.3, cap: 'round' })
}

function resolveObstacleStyle(kind: WorldObstacle['kind']) {
  if (kind === 'cover') {
    return {
      fill: palette.obstacleCover,
      inner: 0xf5f9fb,
      edge: 0x5d7280,
      side: 0xc4d3dc,
      sideDark: 0xa9bcc9,
      accent: 0x5d7280,
    }
  }

  if (kind === 'station') {
    return {
      fill: palette.obstacleStation,
      inner: 0xf6faf6,
      edge: 0x6b7e74,
      side: 0xc9d5cc,
      sideDark: 0xb2c2b7,
      accent: 0x6b7e74,
    }
  }

  if (kind === 'locker') {
    return {
      fill: palette.obstacleLocker,
      inner: 0xf9f6f0,
      edge: 0x8d7759,
      side: 0xd9cfc0,
      sideDark: 0xc2b39d,
      accent: 0x8d7759,
    }
  }

  return {
    fill: palette.obstacleWall,
    inner: palette.obstacleInner,
    edge: 0x707d88,
    side: 0xc8d0d8,
    sideDark: 0xafbac5,
    accent: null,
  }
}

function getMinimapObstacleColor(kind: WorldObstacle['kind']): number {
  if (kind === 'station') {
    return 0xb8c5bd
  }
  if (kind === 'locker') {
    return 0xd2bc98
  }
  if (kind === 'cover') {
    return 0xb8c8d1
  }
  return palette.minimapObstacle
}

export function drawWorldMarkers(graphics: Graphics, markers: readonly WorldMarker[], elapsedSeconds: number, highlightedMarkerId: string | null = null): void {
  graphics.clear()

  markers.forEach((marker, index) => {
    const color = getMarkerColor(marker.kind)
    const baseRadius = getMarkerRadius(marker.kind)
    const pulse = (Math.sin(elapsedSeconds * 2.6 + index * 0.9) + 1) * 0.5
    const focused = marker.id === highlightedMarkerId
    const glowRadius = baseRadius + 8 + pulse * 8 + (focused ? 8 : 0)
    const ringRadius = baseRadius + pulse * 4 + (focused ? 4 : 0)

    graphics.circle(marker.x, marker.y, glowRadius).stroke({ width: focused ? 1.5 : 1.2, color, alpha: (focused ? 0.2 : 0.12) + pulse * 0.12, alignment: 0.5 })
    graphics.circle(marker.x, marker.y, ringRadius).stroke({ width: focused ? 2.6 : 2.2, color, alpha: (focused ? 0.52 : 0.38) + pulse * 0.14, alignment: 0.5 })
    graphics.circle(marker.x, marker.y, Math.max(5, baseRadius * 0.26)).fill({ color: palette.arenaCore, alpha: 0.9 })
    drawMarkerGlyph(graphics, marker.x, marker.y, marker.kind, Math.max(8, baseRadius * 0.5), color)

    if (focused) {
      const frameSize = baseRadius * 2.4 + pulse * 8
      drawCornerFrame(graphics, marker.x - frameSize * 0.5, marker.y - frameSize * 0.5, frameSize, frameSize, 8, color, 0.28, 1.2)
      drawMarkerChevron(graphics, marker.x, marker.y, baseRadius + 20 + pulse * 4, color)
    }
  })
}

export interface MinimapOptions {
  x: number
  y: number
  width: number
  height: number
  bounds: ArenaBounds
  viewBounds?: ArenaBounds | null
  obstacles: readonly WorldObstacle[]
  player: { x: number; y: number }
  enemies?: readonly { x: number; y: number }[]
  markers?: readonly WorldMarker[]
  cameraBounds?: ArenaBounds | null
  highlightedMarkerId?: string | null
}

export function drawMinimap(graphics: Graphics, options: MinimapOptions): void {
  const { x, y, width, height, bounds, viewBounds = null, obstacles, player, enemies = [], markers = [], cameraBounds = null, highlightedMarkerId = null } = options
  const padding = Math.max(12, Math.min(20, width * 0.08))
  const mapX = x + padding
  const mapY = y + padding + 18
  const mapWidth = width - padding * 2
  const mapHeight = height - padding * 2 - 18
  const view = viewBounds ?? bounds
  const scaleX = mapWidth / Math.max(1, view.right - view.left)
  const scaleY = mapHeight / Math.max(1, view.bottom - view.top)

  drawGlassPanel(graphics, x, y, width, height, 0.1)

  graphics.roundRect(mapX, mapY, mapWidth, mapHeight, 16).fill({ color: palette.minimapBg, alpha: 0.96 })
  graphics.roundRect(mapX, mapY, mapWidth, mapHeight, 16).stroke({ width: 1, color: palette.frameSoft, alpha: 0.1, alignment: 0.5 })

  for (let guide = 1; guide < 6; guide += 1) {
    const gx = mapX + (mapWidth / 6) * guide
    const gy = mapY + (mapHeight / 6) * guide
    graphics.moveTo(gx, mapY + 10)
    graphics.lineTo(gx, mapY + mapHeight - 10)
    graphics.moveTo(mapX + 10, gy)
    graphics.lineTo(mapX + mapWidth - 10, gy)
  }
  graphics.stroke({ width: 1, color: palette.grid, alpha: 0.06 })

  for (const obstacle of obstacles) {
    const clipped = intersectRect(view, {
      left: obstacle.x,
      top: obstacle.y,
      right: obstacle.x + obstacle.width,
      bottom: obstacle.y + obstacle.height,
    })

    if (!clipped) {
      continue
    }

    const ox = mapX + (clipped.left - view.left) * scaleX
    const oy = mapY + (clipped.top - view.top) * scaleY
    const ow = Math.max(2, (clipped.right - clipped.left) * scaleX)
    const oh = Math.max(2, (clipped.bottom - clipped.top) * scaleY)

    graphics.roundRect(ox, oy, ow, oh, Math.min(6, Math.min(ow, oh) * 0.35)).fill({ color: getMinimapObstacleColor(obstacle.kind), alpha: 0.92 })
  }

  if (cameraBounds) {
    const clipped = intersectRect(view, cameraBounds)

    if (clipped) {
      const cx = mapX + (clipped.left - view.left) * scaleX
      const cy = mapY + (clipped.top - view.top) * scaleY
      const cw = Math.max(8, (clipped.right - clipped.left) * scaleX)
      const ch = Math.max(8, (clipped.bottom - clipped.top) * scaleY)

      drawCornerFrame(graphics, cx, cy, cw, ch, 10, palette.minimapBorder, 0.7, 1.5)
      graphics.rect(cx, cy, cw, ch).stroke({ width: 1, color: palette.minimapBorder, alpha: 0.18, alignment: 0.5 })
    }
  }

  for (const marker of markers) {
    if (!containsPoint(view, marker.x, marker.y)) {
      continue
    }

    const px = mapX + (marker.x - view.left) * scaleX
    const py = mapY + (marker.y - view.top) * scaleY
    const color = getMarkerColor(marker.kind)
    const focused = marker.id === highlightedMarkerId

    graphics.circle(px, py, focused ? 7.6 : 5.4).stroke({ width: focused ? 1.5 : 1.2, color, alpha: focused ? 0.34 : 0.22, alignment: 0.5 })
    if (focused) {
      drawCornerFrame(graphics, px - 8, py - 8, 16, 16, 5, color, 0.24, 1)
    }
    drawMarkerGlyph(graphics, px, py, marker.kind, 4.2, color)
  }

  for (const enemy of enemies) {
    if (!containsPoint(view, enemy.x, enemy.y)) {
      continue
    }

    const ex = mapX + (enemy.x - view.left) * scaleX
    const ey = mapY + (enemy.y - view.top) * scaleY
    graphics.poly([ex, ey - 2.4, ex + 2.4, ey, ex, ey + 2.4, ex - 2.4, ey]).fill({ color: palette.minimapEnemy, alpha: 0.88 })
  }

  if (containsPoint(view, player.x, player.y)) {
    const px = mapX + (player.x - view.left) * scaleX
    const py = mapY + (player.y - view.top) * scaleY
    graphics.circle(px, py, 6.4).fill({ color: palette.minimapPlayer, alpha: 0.18 })
    graphics.poly([px, py - 4.4, px + 4.4, py, px, py + 4.4, px - 4.4, py]).fill({ color: palette.minimapPlayer, alpha: 0.98 })
    graphics.circle(px, py, 1.5).fill({ color: palette.arenaCore, alpha: 0.94 })
  }
}

export function createFocusedViewBounds(
  bounds: ArenaBounds,
  center: { x: number; y: number },
  width: number,
  height: number,
): ArenaBounds {
  const halfWidth = width * 0.5
  const halfHeight = height * 0.5
  let left = center.x - halfWidth
  let top = center.y - halfHeight

  left = clamp(left, bounds.left, Math.max(bounds.left, bounds.right - width))
  top = clamp(top, bounds.top, Math.max(bounds.top, bounds.bottom - height))

  return {
    left,
    top,
    right: Math.min(bounds.right, left + width),
    bottom: Math.min(bounds.bottom, top + height),
  }
}

function getMarkerColor(kind: WorldMarker['kind']): number {
  if (kind === 'objective' || kind === 'locker') {
    return palette.warning
  }
  if (kind === 'extraction') {
    return palette.minimapMarker
  }
  if (kind === 'station') {
    return palette.frame
  }
  return palette.dash
}

function getMarkerRadius(kind: WorldMarker['kind']): number {
  if (kind === 'objective') {
    return 26
  }
  if (kind === 'extraction') {
    return 22
  }
  if (kind === 'station') {
    return 20
  }
  return 18
}

function drawMarkerGlyph(graphics: Graphics, x: number, y: number, kind: WorldMarker['kind'], size: number, color: number): void {
  if (kind === 'objective') {
    graphics.poly([x, y - size, x + size, y, x, y + size, x - size, y]).stroke({ width: 1.5, color, alpha: 0.94, alignment: 0.5 })
    graphics.moveTo(x, y - size * 0.44)
    graphics.lineTo(x, y + size * 0.44)
    graphics.stroke({ width: 1.2, color, alpha: 0.7, cap: 'round' })
    return
  }

  if (kind === 'extraction') {
    graphics.rect(x - size * 0.8, y - size * 0.8, size * 1.6, size * 1.6).stroke({ width: 1.4, color, alpha: 0.94, alignment: 0.5 })
    graphics.moveTo(x - size * 0.2, y - size * 0.3)
    graphics.lineTo(x + size * 0.4, y)
    graphics.lineTo(x - size * 0.2, y + size * 0.3)
    graphics.stroke({ width: 1.4, color, alpha: 0.74, cap: 'round', join: 'round' })
    return
  }

  if (kind === 'locker') {
    graphics.rect(x - size * 0.75, y - size * 0.7, size * 1.5, size * 1.4).stroke({ width: 1.3, color, alpha: 0.92, alignment: 0.5 })
    graphics.moveTo(x, y - size * 0.7)
    graphics.lineTo(x, y + size * 0.7)
    graphics.stroke({ width: 1.1, color, alpha: 0.64 })
    return
  }

  if (kind === 'station') {
    graphics.poly([
      x - size * 0.9,
      y - size * 0.25,
      x - size * 0.28,
      y - size * 0.9,
      x + size * 0.28,
      y - size * 0.9,
      x + size * 0.9,
      y - size * 0.25,
      x + size * 0.9,
      y + size * 0.25,
      x + size * 0.28,
      y + size * 0.9,
      x - size * 0.28,
      y + size * 0.9,
      x - size * 0.9,
      y + size * 0.25,
    ]).stroke({ width: 1.4, color, alpha: 0.94, alignment: 0.5, join: 'round' })
    return
  }

  graphics.poly([
    x,
    y - size,
    x + size * 0.85,
    y + size * 0.64,
    x,
    y + size * 0.18,
    x - size * 0.85,
    y + size * 0.64,
  ]).fill({ color, alpha: 0.92 })
}

function drawMarkerChevron(graphics: Graphics, x: number, y: number, offset: number, color: number): void {
  const size = 7
  graphics.moveTo(x - offset, y - size)
  graphics.lineTo(x - offset + size, y)
  graphics.lineTo(x - offset, y + size)
  graphics.moveTo(x + offset, y - size)
  graphics.lineTo(x + offset - size, y)
  graphics.lineTo(x + offset, y + size)
  graphics.stroke({ width: 1.4, color, alpha: 0.4, cap: 'round', join: 'round' })
}

export function drawCornerFrame(
  graphics: Graphics,
  x: number,
  y: number,
  width: number,
  height: number,
  cornerSize: number,
  color: number,
  alpha: number,
  strokeWidth: number,
): void {
  graphics.moveTo(x, y + cornerSize)
  graphics.lineTo(x, y)
  graphics.lineTo(x + cornerSize, y)
  graphics.moveTo(x + width - cornerSize, y)
  graphics.lineTo(x + width, y)
  graphics.lineTo(x + width, y + cornerSize)
  graphics.moveTo(x + width, y + height - cornerSize)
  graphics.lineTo(x + width, y + height)
  graphics.lineTo(x + width - cornerSize, y + height)
  graphics.moveTo(x + cornerSize, y + height)
  graphics.lineTo(x, y + height)
  graphics.lineTo(x, y + height - cornerSize)
  graphics.stroke({ width: strokeWidth, color, alpha, cap: 'round', join: 'round' })
}

function intersectRect(
  left: ArenaBounds,
  right: ArenaBounds,
): ArenaBounds | null {
  const clipped = {
    left: Math.max(left.left, right.left),
    top: Math.max(left.top, right.top),
    right: Math.min(left.right, right.right),
    bottom: Math.min(left.bottom, right.bottom),
  }

  return clipped.left < clipped.right && clipped.top < clipped.bottom ? clipped : null
}

function containsPoint(bounds: ArenaBounds, x: number, y: number): boolean {
  return x >= bounds.left && x <= bounds.right && y >= bounds.top && y <= bounds.bottom
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value))
}
