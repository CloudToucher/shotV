import { Graphics, TextStyle, type ColorSource } from 'pixi.js'

import type { ArenaBounds, WorldMarker, WorldObstacle } from '../core/contracts'
import { palette } from '../theme/palette'

export const UI_FONT_FAMILY = 'Consolas, Courier New, Menlo, Monaco, PingFang SC, Microsoft YaHei UI, monospace'

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
  const cut = 12
  graphics.clear()
  
  graphics.poly([
    x + cut, y,
    x + width, y,
    x + width, y + height - cut,
    x + width - cut, y + height,
    x, y + height,
    x, y + cut
  ]).fill({ color: palette.uiPanel, alpha: 0.85 + accentAlpha * 0.15 })
  
  graphics.poly([
    x + cut, y,
    x + width, y,
    x + width, y + height - cut,
    x + width - cut, y + height,
    x, y + height,
    x, y + cut
  ]).stroke({ width: 1, color: palette.frame, alpha: 0.4, alignment: 0.5 })
  
  graphics.rect(x + 16, y + 8, width - 32, 2).fill({ color: palette.arenaCore, alpha: 0.6 })
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

  const cut = 24

  graphics.clear()
  graphics.rect(0, 0, screenWidth, screenHeight).fill({ color: palette.bgOuter, alpha: 0.85 })
  
  // Scanlines effect (simulated with faint lines)
  for (let sy = 0; sy < screenHeight; sy += 4) {
    graphics.rect(0, sy, screenWidth, 1).fill({ color: palette.frame, alpha: 0.05 })
  }

  // Main Panel Background
  graphics.poly([
    x + cut, y,
    x + width, y,
    x + width, y + height - cut,
    x + width - cut, y + height,
    x, y + height,
    x, y + cut
  ]).fill({ color: palette.uiPanel, alpha: 0.95 })
  
  // Main Panel Border
  graphics.poly([
    x + cut, y,
    x + width, y,
    x + width, y + height - cut,
    x + width - cut, y + height,
    x, y + height,
    x, y + cut
  ]).stroke({ width: 1.5, color: palette.frame, alpha: 0.6, alignment: 0.5 })

  // Header
  graphics.poly([
    x + cut, y,
    x + width, y,
    x + width, y + headerHeight,
    x, y + headerHeight,
    x, y + cut
  ]).fill({ color: palette.uiActive, alpha: 0.9 })
  
  graphics.rect(x + 20, y + 18, 52, 3).fill({ color: palette.panelWarm, alpha: 0.92 })
  graphics.rect(x + 20, y + headerHeight - 1, width - 40, 1).fill({ color: palette.panelLine, alpha: 0.42 })
  
  // Footer
  graphics.poly([
    x, y + height - footerHeight,
    x + width, y + height - footerHeight,
    x + width, y + height - cut,
    x + width - cut, y + height,
    x, y + height
  ]).fill({ color: palette.uiActive, alpha: 0.8 })
  
  graphics.rect(x + 20, y + height - footerHeight, width - 40, 1).fill({ color: palette.frame, alpha: 0.3 })
  
  drawCornerFrame(graphics, x + 10, y + 10, width - 20, height - 20, 20, palette.frame, 0.5, 1.5)
}

export function drawMapOverlayPanel(
  graphics: Graphics,
  x: number,
  y: number,
  width: number,
  height: number,
  accentColor: number = palette.frame,
): void {
  const cut = 16
  graphics.poly([
    x + cut, y,
    x + width, y,
    x + width, y + height - cut,
    x + width - cut, y + height,
    x, y + height,
    x, y + cut
  ]).fill({ color: palette.uiPanel, alpha: 0.95 })
  
  graphics.poly([
    x + cut, y,
    x + width, y,
    x + width, y + height - cut,
    x + width - cut, y + height,
    x, y + height,
    x, y + cut
  ]).stroke({ width: 1.5, color: accentColor, alpha: 0.4, alignment: 0.5 })
  
  graphics.rect(x + 16, y + 12, 34, 2).fill({ color: accentColor, alpha: 0.9 })
  drawCornerFrame(graphics, x + 8, y + 8, width - 16, height - 16, 10, accentColor, 0.3, 1.2)
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
  
  // Tactical Grid
  for (let x = bounds.left + majorStep; x < bounds.right; x += majorStep) {
    graphics.moveTo(x, bounds.top)
    graphics.lineTo(x, bounds.bottom)
  }
  for (let y = bounds.top + majorStep; y < bounds.bottom; y += majorStep) {
    graphics.moveTo(bounds.left, y)
    graphics.lineTo(bounds.right, y)
  }
  graphics.stroke({ width: 1, color: palette.grid, alpha: mode === 'combat' ? 0.3 : 0.15 })

  // Crosshairs at grid intersections
  for (let x = bounds.left + majorStep; x < bounds.right; x += majorStep) {
    for (let y = bounds.top + majorStep; y < bounds.bottom; y += majorStep) {
      graphics.moveTo(x - 4, y)
      graphics.lineTo(x + 4, y)
      graphics.moveTo(x, y - 4)
      graphics.lineTo(x, y + 4)
    }
  }
  graphics.stroke({ width: 1, color: palette.grid, alpha: 0.4 })

  // Boundary Warning Line
  const bInset = 12
  graphics.rect(bounds.left + bInset, bounds.top + bInset, width - bInset * 2, height - bInset * 2).stroke({ 
    width: 2, 
    color: palette.warning, 
    alpha: 0.3, 
    alignment: 0.5 
  })
  
  // Laser fence effect
  for (let i = 0; i < width; i += 60) {
    graphics.rect(bounds.left + bInset + i, bounds.top + bInset - 4, 20, 8).fill({ color: palette.warning, alpha: 0.4 })
    graphics.rect(bounds.left + bInset + i, bounds.bottom - bInset - 4, 20, 8).fill({ color: palette.warning, alpha: 0.4 })
  }
  for (let i = 0; i < height; i += 60) {
    graphics.rect(bounds.left + bInset - 4, bounds.top + bInset + i, 8, 20).fill({ color: palette.warning, alpha: 0.4 })
    graphics.rect(bounds.right - bInset - 4, bounds.top + bInset + i, 8, 20).fill({ color: palette.warning, alpha: 0.4 })
  }
}

export function drawWorldObstacles(graphics: Graphics, obstacles: readonly WorldObstacle[]): void {
  graphics.clear()

  for (const obstacle of obstacles) {
    const style = resolveObstacleStyle(obstacle.kind)
    const offset = 6 // 扁平错位回影的距离

    // 1. 错位回影 (Offset Echo) - 营造轻盈的数字空间感，而不是沉重的物理阴影
    graphics.rect(obstacle.x + offset, obstacle.y + offset, obstacle.width, obstacle.height)
      .fill({ color: style.color, alpha: 0.12 })

    // 2. 纯白底板 (Base Plate)
    graphics.rect(obstacle.x, obstacle.y, obstacle.width, obstacle.height)
      .fill({ color: 0xffffff, alpha: 0.95 })

    // 3. 工业斜线网点 (Diagonal Hatching) - 用来表达“实体质量”和“质感”，取代笨重的3D厚度
    const step = obstacle.kind === 'cover' ? 8 : 12
    for (let i = 0; i < obstacle.width + obstacle.height; i += step) {
      const startX = Math.max(0, i - obstacle.height)
      const startY = Math.min(obstacle.height, i)
      const endX = Math.min(obstacle.width, i)
      const endY = Math.max(0, i - obstacle.width)

      graphics.moveTo(obstacle.x + startX, obstacle.y + startY)
      graphics.lineTo(obstacle.x + endX, obstacle.y + endY)
    }
    graphics.stroke({ width: 1, color: style.color, alpha: 0.15 })

    // 4. 锐利的主边框 (Crisp Border)
    graphics.rect(obstacle.x, obstacle.y, obstacle.width, obstacle.height)
      .stroke({ width: 1.5, color: style.color, alpha: 0.85, alignment: 0 })

    // 5. 内部玻璃高光 (Inner Glass Highlight) - 增加一点晶莹剔透的质感
    graphics.moveTo(obstacle.x + 2, obstacle.y + obstacle.height - 2)
      .lineTo(obstacle.x + 2, obstacle.y + 2)
      .lineTo(obstacle.x + obstacle.width - 2, obstacle.y + 2)
      .stroke({ width: 2, color: 0xffffff, alpha: 0.9 })

    // 6. 内部功能性切割细节 (Functional Details)
    if (obstacle.kind === 'locker') {
      // 储物柜的门板切割
      graphics.rect(obstacle.x + 6, obstacle.y + 6, obstacle.width * 0.35, obstacle.height - 12)
        .fill({ color: style.color, alpha: 0.08 })
        .stroke({ width: 1, color: style.color, alpha: 0.4 })
      // 电子锁指示灯
      graphics.rect(obstacle.x + obstacle.width * 0.35 + 12, obstacle.y + obstacle.height / 2 - 6, 4, 12)
        .fill({ color: style.color, alpha: 0.8 })
    }

    if (obstacle.kind === 'station') {
      // 站点的数据核心
      const cx = obstacle.x + obstacle.width / 2
      const cy = obstacle.y + obstacle.height / 2
      graphics.circle(cx, cy, 10)
        .fill({ color: 0xffffff })
        .stroke({ width: 1.5, color: style.color, alpha: 0.8 })
      graphics.circle(cx, cy, 4)
        .fill({ color: style.color, alpha: 0.9 })
      // 数据连接线
      graphics.moveTo(obstacle.x + 6, cy).lineTo(cx - 14, cy)
      graphics.moveTo(cx + 14, cy).lineTo(obstacle.x + obstacle.width - 6, cy)
      graphics.stroke({ width: 1, color: style.color, alpha: 0.4 })
    }

    // 7. 战术折角修饰 (Tactical Corner Brackets) - 完美呼应玩家/怪物的几何语言
    const bracketLen = 8
    graphics.moveTo(obstacle.x, obstacle.y + bracketLen).lineTo(obstacle.x, obstacle.y).lineTo(obstacle.x + bracketLen, obstacle.y)
    graphics.moveTo(obstacle.x + obstacle.width - bracketLen, obstacle.y).lineTo(obstacle.x + obstacle.width, obstacle.y).lineTo(obstacle.x + obstacle.width, obstacle.y + bracketLen)
    graphics.moveTo(obstacle.x + obstacle.width, obstacle.y + obstacle.height - bracketLen).lineTo(obstacle.x + obstacle.width, obstacle.y + obstacle.height).lineTo(obstacle.x + obstacle.width - bracketLen, obstacle.y + obstacle.height)
    graphics.moveTo(obstacle.x + bracketLen, obstacle.y + obstacle.height).lineTo(obstacle.x, obstacle.y + obstacle.height).lineTo(obstacle.x, obstacle.y + obstacle.height - bracketLen)
    graphics.stroke({ width: 2.5, color: style.color, alpha: 1 })
  }
}

function resolveObstacleStyle(kind: WorldObstacle['kind']) {
  if (kind === 'cover') {
    return { color: palette.frame } // 战术蓝
  }
  if (kind === 'station') {
    return { color: palette.minimapMarker } // 安全绿
  }
  if (kind === 'locker') {
    return { color: palette.warning } // 价值橙
  }
  return { color: palette.frame }
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
    const pulse = (Math.sin(elapsedSeconds * 4 + index) + 1) * 0.5
    const focused = marker.id === highlightedMarkerId
    
    // Sharp geometric marker
    const size = baseRadius + (focused ? 4 : 0)
    
    graphics.poly([
      marker.x, marker.y - size - pulse * 4,
      marker.x + size + pulse * 4, marker.y,
      marker.x, marker.y + size + pulse * 4,
      marker.x - size - pulse * 4, marker.y
    ]).stroke({ width: focused ? 2 : 1.5, color, alpha: 0.8 + pulse * 0.2, alignment: 0.5 })
    
    graphics.poly([
      marker.x, marker.y - size * 0.6,
      marker.x + size * 0.6, marker.y,
      marker.x, marker.y + size * 0.6,
      marker.x - size * 0.6, marker.y
    ]).fill({ color, alpha: 0.2 + pulse * 0.1 })
    
    drawMarkerGlyph(graphics, marker.x, marker.y, marker.kind, size * 0.4, color)

    if (focused) {
      const frameSize = size * 2.5 + pulse * 4
      drawCornerFrame(graphics, marker.x - frameSize * 0.5, marker.y - frameSize * 0.5, frameSize, frameSize, 8, color, 0.6, 2)
      
      // Target crosshairs
      graphics.moveTo(marker.x - frameSize * 0.8, marker.y).lineTo(marker.x - frameSize * 0.6, marker.y)
      graphics.moveTo(marker.x + frameSize * 0.8, marker.y).lineTo(marker.x + frameSize * 0.6, marker.y)
      graphics.moveTo(marker.x, marker.y - frameSize * 0.8).lineTo(marker.x, marker.y - frameSize * 0.6)
      graphics.moveTo(marker.x, marker.y + frameSize * 0.8).lineTo(marker.x, marker.y + frameSize * 0.6)
      graphics.stroke({ width: 1.5, color, alpha: 0.8 })
    }
  })
}

export interface WorldInteractionHighlightOptions {
  centerX: number
  centerY: number
  radius: number
  color: number
  elapsedSeconds: number
  active?: boolean
  emphasis?: number
}

export function drawWorldInteractionHighlight(graphics: Graphics, options: WorldInteractionHighlightOptions): void {
  const active = options.active ?? false
  const emphasis = options.emphasis ?? 1
  const pulse = (Math.sin(options.elapsedSeconds * 4.2 + options.centerX * 0.012 + options.centerY * 0.008) + 1) * 0.5
  const outerRadiusX = options.radius + 10 * emphasis + pulse * 5 * emphasis
  const outerRadiusY = outerRadiusX * 0.62
  const innerRadiusX = Math.max(10, outerRadiusX * 0.58)
  const innerRadiusY = innerRadiusX * 0.62
  const bracketSize = Math.max(20, options.radius * 1.28)
  const bracketX = options.centerX - bracketSize * 0.5
  const bracketY = options.centerY - bracketSize * 0.5
  const alphaBoost = active ? 0.16 : 0

  graphics.ellipse(options.centerX, options.centerY + 4, outerRadiusX, outerRadiusY)
    .stroke({ width: active ? 2 : 1.4, color: options.color, alpha: 0.2 + pulse * 0.1 + alphaBoost, alignment: 0.5 })
  graphics.ellipse(options.centerX, options.centerY + 4, innerRadiusX, innerRadiusY)
    .stroke({ width: 1, color: options.color, alpha: 0.18 + pulse * 0.06 + alphaBoost * 0.7, alignment: 0.5 })
  graphics.ellipse(options.centerX, options.centerY + 4, innerRadiusX * 0.64, innerRadiusY * 0.64)
    .fill({ color: options.color, alpha: 0.04 + pulse * 0.02 + alphaBoost * 0.5 })

  drawCornerFrame(
    graphics,
    bracketX,
    bracketY,
    bracketSize,
    bracketSize,
    8,
    options.color,
    0.28 + pulse * 0.1 + alphaBoost,
    active ? 1.8 : 1.4,
  )

  const chevronOffsetX = outerRadiusX + 8
  const chevronOffsetY = outerRadiusY * 0.5 + 2
  const chevronSize = 6 + pulse * 1.4 + emphasis
  for (const direction of [-1, 1] as const) {
    const centerX = options.centerX + chevronOffsetX * direction
    graphics.moveTo(centerX - chevronSize * direction, options.centerY + 4 - chevronSize)
    graphics.lineTo(centerX, options.centerY + 4)
    graphics.lineTo(centerX - chevronSize * direction, options.centerY + 4 + chevronSize)
  }
  graphics.moveTo(options.centerX, options.centerY + 4 - chevronOffsetY - chevronSize)
  graphics.lineTo(options.centerX, options.centerY + 4 - chevronOffsetY + chevronSize * 0.2)
  graphics.moveTo(options.centerX, options.centerY + 4 + chevronOffsetY - chevronSize * 0.2)
  graphics.lineTo(options.centerX, options.centerY + 4 + chevronOffsetY + chevronSize)
  graphics.stroke({ width: 1.2, color: options.color, alpha: 0.34 + pulse * 0.08 + alphaBoost, cap: 'round', join: 'round' })

  graphics.circle(options.centerX, options.centerY + 4, active ? 2.4 : 2)
    .fill({ color: options.color, alpha: 0.62 + pulse * 0.18 + alphaBoost * 0.8 })
}

export interface WorldActionProgressOptions {
  centerX: number
  centerY: number
  width: number
  progress: number
  color: number
  elapsedSeconds: number
}

export function drawWorldActionProgress(graphics: Graphics, options: WorldActionProgressOptions): void {
  const progress = clamp(options.progress, 0, 1)
  const width = Math.max(84, options.width)
  const height = 16
  const x = options.centerX - width * 0.5
  const y = options.centerY + 40
  const cut = 6
  const inset = 8
  const trackWidth = width - inset * 2
  const fillWidth = trackWidth * progress
  const shimmerWidth = 16
  const shimmerX = x + inset + ((trackWidth + shimmerWidth) * ((Math.sin(options.elapsedSeconds * 5.2) + 1) * 0.5) - shimmerWidth)

  graphics.poly([
    x + cut, y,
    x + width, y,
    x + width, y + height - cut,
    x + width - cut, y + height,
    x, y + height,
    x, y + cut,
  ]).fill({ color: palette.uiPanel, alpha: 0.94 })

  graphics.poly([
    x + cut, y,
    x + width, y,
    x + width, y + height - cut,
    x + width - cut, y + height,
    x, y + height,
    x, y + cut,
  ]).stroke({ width: 1.2, color: options.color, alpha: 0.34, alignment: 0.5 })

  graphics.rect(x + inset, y + 6, trackWidth, 4).fill({ color: palette.frameSoft, alpha: 0.18 })
  graphics.rect(x + inset, y + 6, fillWidth, 4).fill({ color: options.color, alpha: 0.88 })

  if (fillWidth > 8) {
    const shimmerStart = Math.max(x + inset, shimmerX)
    const shimmerEnd = Math.min(x + inset + fillWidth, shimmerStart + shimmerWidth)
    const shimmerDrawWidth = shimmerEnd - shimmerStart

    if (shimmerDrawWidth > 0) {
      graphics.rect(shimmerStart, y + 6, shimmerDrawWidth, 4).fill({ color: 0xffffff, alpha: 0.28 })
    }
  }

  drawCornerFrame(graphics, x + 3, y + 3, width - 6, height - 6, 4, options.color, 0.22, 1)
}

export interface WorldGateVisualOptions {
  centerX: number
  centerY: number
  width: number
  height: number
  color: number
  elapsedSeconds: number
  openness: number
  active?: boolean
}

export function drawWorldGateVisual(graphics: Graphics, options: WorldGateVisualOptions): void {
  const openness = clamp(options.openness, 0, 1)
  const active = options.active ?? false
  const x = options.centerX - options.width * 0.5
  const y = options.centerY - options.height * 0.5
  const pulse = (Math.sin(options.elapsedSeconds * 5.1 + options.centerX * 0.008) + 1) * 0.5
  const glow = 0.14 + pulse * 0.08 + (active ? 0.08 : 0)
  const doorTravel = options.width * 0.18 * openness
  const doorPadding = 8
  const frameInset = 6
  const doorHalfWidth = options.width * 0.5 - doorPadding - 4
  const doorHeight = options.height - 18
  const doorY = y + 9
  const leftDoorX = x + doorPadding - doorTravel
  const rightDoorX = options.centerX + 4 + doorTravel
  const centerGap = 8 + options.width * 0.18 * openness

  drawCornerFrame(graphics, x, y, options.width, options.height, 12, options.color, 0.34 + glow, 1.6)
  graphics.rect(x + frameInset, y + frameInset, options.width - frameInset * 2, options.height - frameInset * 2)
    .stroke({ width: 1.1, color: options.color, alpha: 0.16 + glow, alignment: 0.5 })

  graphics.rect(x + 12, y + 4, options.width - 24, 2).fill({ color: options.color, alpha: 0.34 + glow })
  graphics.rect(x + 12, y + options.height - 6, options.width - 24, 2).fill({ color: options.color, alpha: 0.24 + glow * 0.9 })
  graphics.rect(x + 10, y + options.height + 6, options.width - 20, 2).fill({ color: options.color, alpha: 0.18 + glow * 0.8 })
  graphics.rect(options.centerX - centerGap * 0.5, y + options.height + 4, centerGap, 6).fill({ color: options.color, alpha: 0.08 + openness * 0.18 })

  drawGateLeaf(graphics, leftDoorX, doorY, doorHalfWidth, doorHeight, options.color, glow, true)
  drawGateLeaf(graphics, rightDoorX, doorY, doorHalfWidth, doorHeight, options.color, glow, false)

  if (centerGap > 8) {
    graphics.rect(options.centerX - centerGap * 0.5, y + 10, centerGap, options.height - 20)
      .fill({ color: options.color, alpha: 0.04 + openness * 0.09 })
    graphics.rect(options.centerX - 1, y + 14, 2, options.height - 28)
      .fill({ color: options.color, alpha: 0.12 + openness * 0.18 + (active ? 0.08 : 0) })
  } else {
    graphics.rect(options.centerX - 1, y + 14, 2, options.height - 28)
      .fill({ color: options.color, alpha: 0.22 + glow })
  }
}

export interface WorldExtractionSurgeOptions {
  centerX: number
  centerY: number
  progress: number
  elapsedSeconds: number
  radius: number
  color: number
}

export function drawWorldExtractionSurge(graphics: Graphics, options: WorldExtractionSurgeOptions): void {
  const progress = clamp(options.progress, 0, 1)

  if (progress <= 0) {
    return
  }

  const pulse = (Math.sin(options.elapsedSeconds * 13.2) + 1) * 0.5
  const radius = Math.max(120, options.radius)
  const coreRadius = 18 + progress * 20

  for (let index = 0; index < 3; index += 1) {
    const ringProgress = clamp(progress * 1.14 - index * 0.16, 0, 1)

    if (ringProgress <= 0) {
      continue
    }

    const ringRadius = lerp(radius * 1.06, 36 + index * 10, ringProgress)
    const alpha = (1 - ringProgress) * (0.22 - index * 0.04) + 0.04
    graphics.ellipse(options.centerX, options.centerY + 6, ringRadius, ringRadius * 0.62)
      .stroke({ width: 1.2 + (1 - ringProgress) * 1.4, color: options.color, alpha, alignment: 0.5 })
  }

  const spokeProgress = Math.pow(progress, 0.82)
  const outerRadius = lerp(radius * 1.12, radius * 0.7, spokeProgress)
  const innerRadius = lerp(12, 46, spokeProgress)
  const spokeCount = 10

  for (let index = 0; index < spokeCount; index += 1) {
    const angle = (Math.PI * 2 * index) / spokeCount + options.elapsedSeconds * 0.12
    const wave = Math.sin(options.elapsedSeconds * 5.1 + index * 0.8) * 10
    const startX = options.centerX + Math.cos(angle) * (outerRadius + wave)
    const startY = options.centerY + Math.sin(angle) * (outerRadius * 0.64 + wave * 0.36)
    const endX = options.centerX + Math.cos(angle) * innerRadius
    const endY = options.centerY + Math.sin(angle) * innerRadius * 0.72

    graphics.moveTo(startX, startY)
    graphics.lineTo(endX, endY)
  }
  graphics.stroke({ width: 1.4, color: options.color, alpha: 0.08 + spokeProgress * 0.26, cap: 'round', join: 'round' })

  graphics.circle(options.centerX, options.centerY, coreRadius * 0.78)
    .fill({ color: options.color, alpha: 0.06 + progress * 0.12 })
  graphics.circle(options.centerX, options.centerY, coreRadius * 0.44)
    .stroke({ width: 1.5, color: options.color, alpha: 0.26 + progress * 0.28, alignment: 0.5 })

  if (progress > 0.72) {
    const beamProgress = (progress - 0.72) / 0.28
    const beamHeight = lerp(radius * 0.3, radius * 0.96, beamProgress)
    const beamWidth = 10 + Math.sin(beamProgress * Math.PI) * 16 + pulse * 4
    const beamX = options.centerX - beamWidth * 0.5
    const beamY = options.centerY - beamHeight

    graphics.rect(beamX, beamY, beamWidth, beamHeight + 26)
      .fill({ color: options.color, alpha: 0.04 + beamProgress * 0.12 })
    graphics.rect(options.centerX - 2, beamY, 4, beamHeight + 30)
      .fill({ color: 0xffffff, alpha: 0.18 + beamProgress * 0.62 })
    graphics.ellipse(options.centerX, options.centerY + 4, beamWidth * 1.2, 10 + beamProgress * 8)
      .fill({ color: options.color, alpha: 0.1 + beamProgress * 0.16 })
  }
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

  drawGlassPanel(graphics, x, y, width, height, 0.2)

  graphics.rect(mapX, mapY, mapWidth, mapHeight).fill({ color: palette.minimapBg, alpha: 0.9 })
  graphics.rect(mapX, mapY, mapWidth, mapHeight).stroke({ width: 1, color: palette.frame, alpha: 0.3, alignment: 0.5 })

  for (let guide = 1; guide < 6; guide += 1) {
    const gx = mapX + (mapWidth / 6) * guide
    const gy = mapY + (mapHeight / 6) * guide
    graphics.moveTo(gx, mapY)
    graphics.lineTo(gx, mapY + mapHeight)
    graphics.moveTo(mapX, gy)
    graphics.lineTo(mapX + mapWidth, gy)
  }
  graphics.stroke({ width: 1, color: palette.grid, alpha: 0.15 })

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

    graphics.rect(ox, oy, ow, oh).fill({ color: getMinimapObstacleColor(obstacle.kind), alpha: 0.8 })
  }

  if (cameraBounds) {
    const clipped = intersectRect(view, cameraBounds)

    if (clipped) {
      const cx = mapX + (clipped.left - view.left) * scaleX
      const cy = mapY + (clipped.top - view.top) * scaleY
      const cw = Math.max(8, (clipped.right - clipped.left) * scaleX)
      const ch = Math.max(8, (clipped.bottom - clipped.top) * scaleY)

      drawCornerFrame(graphics, cx, cy, cw, ch, 10, palette.minimapBorder, 0.8, 1.5)
      graphics.rect(cx, cy, cw, ch).stroke({ width: 1, color: palette.minimapBorder, alpha: 0.3, alignment: 0.5 })
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

    graphics.poly([
      px, py - 6,
      px + 6, py,
      px, py + 6,
      px - 6, py
    ]).stroke({ width: focused ? 2 : 1.5, color, alpha: focused ? 0.8 : 0.4, alignment: 0.5 })
    
    if (focused) {
      drawCornerFrame(graphics, px - 8, py - 8, 16, 16, 5, color, 0.6, 1.5)
    }
  }

  for (const enemy of enemies) {
    if (!containsPoint(view, enemy.x, enemy.y)) {
      continue
    }

    const ex = mapX + (enemy.x - view.left) * scaleX
    const ey = mapY + (enemy.y - view.top) * scaleY
    graphics.poly([ex - 3, ey - 3, ex + 3, ey - 3, ex, ey + 3]).fill({ color: palette.minimapEnemy, alpha: 0.9 })
  }

  if (containsPoint(view, player.x, player.y)) {
    const px = mapX + (player.x - view.left) * scaleX
    const py = mapY + (player.y - view.top) * scaleY
    graphics.poly([px, py - 5, px + 5, py, px, py + 5, px - 5, py]).fill({ color: palette.minimapPlayer, alpha: 1 })
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

function drawGateLeaf(
  graphics: Graphics,
  x: number,
  y: number,
  width: number,
  height: number,
  color: number,
  glow: number,
  left: boolean,
): void {
  const cut = clamp(Math.min(width, height) * 0.14, 5, 9)
  const seamInset = left ? width - 6 : 6
  const seamDirection = left ? -1 : 1
  const points = left
    ? [
        x + cut,
        y,
        x + width,
        y,
        x + width,
        y + height - cut,
        x + width - cut,
        y + height,
        x,
        y + height,
        x,
        y + cut,
      ]
    : [
        x,
        y,
        x + width - cut,
        y,
        x + width,
        y + cut,
        x + width,
        y + height,
        x + cut,
        y + height,
        x,
        y + height - cut,
      ]

  graphics.poly(points).fill({ color: palette.uiPanel, alpha: 0.9 })
  graphics.poly(points).stroke({ width: 1.2, color, alpha: 0.26 + glow, alignment: 0.5, join: 'round' })

  for (let offset = 10; offset < height - 8; offset += 10) {
    graphics.moveTo(x + (left ? 8 : width - 8), y + offset)
    graphics.lineTo(x + seamInset + seamDirection * 18, y + offset - 6)
  }
  graphics.stroke({ width: 1, color, alpha: 0.12 + glow * 0.7, cap: 'round' })

  graphics.rect(x + 6, y + 6, width - 12, 2).fill({ color, alpha: 0.18 + glow })
  graphics.rect(x + 6, y + height - 8, width - 12, 2).fill({ color, alpha: 0.12 + glow * 0.8 })
  graphics.rect(x + seamInset, y + 8, 2, height - 16).fill({ color, alpha: 0.2 + glow })
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

function lerp(start: number, end: number, t: number): number {
  return start + (end - start) * t
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value))
}
