import { Graphics } from 'pixi.js'

import { itemById } from '../data/items'
import type { ItemCategory, ItemDefinition, ItemRarity } from '../data/types'
import type { InventoryItemRecord } from '../inventory/types'
import { palette } from '../theme/palette'
import { drawCornerFrame } from './surface'

const ITEM_RARITY_COLORS: Record<ItemRarity, number> = {
  common: 0x8aa5b3,
  uncommon: 0x6fb79f,
  rare: palette.frame,
  epic: palette.panelWarm,
  legendary: palette.danger,
}

const ITEM_CATEGORY_COLORS: Record<ItemCategory, number> = {
  resource: palette.panelWarm,
  intel: palette.frame,
  boss: palette.danger,
  consumable: palette.dash,
}

interface ItemVisualStyle {
  category: ItemCategory
  categoryColor: number
  rarityColor: number
  accentColor: number
  fillColor: number
  glowColor: number
  glyphColor: number
  softColor: number
}

interface DrawItemPlateOptions {
  item: InventoryItemRecord
  x: number
  y: number
  width: number
  height: number
  bodyAlpha: number
  strokeAlpha: number
  detailAlpha: number
  emphasis: number
  iconScale?: number
}

const GROUND_LOOT_CELL_SIZE = 16

export function drawInventoryItemVisual(
  graphics: Graphics,
  options: {
    item: InventoryItemRecord
    cellSize: number
    x: number
    y: number
    alpha?: number
    accentAlpha?: number
    strokeAlpha?: number
  },
): void {
  const width = options.item.width * options.cellSize
  const height = options.item.height * options.cellSize

  drawItemPlate(graphics, {
    item: options.item,
    x: options.x + 2,
    y: options.y + 2,
    width: Math.max(16, width - 4),
    height: Math.max(16, height - 4),
    bodyAlpha: options.alpha ?? 0.82,
    strokeAlpha: options.strokeAlpha ?? 0.64,
    detailAlpha: options.accentAlpha ?? 0.34,
    emphasis: 0.42,
    iconScale: 1,
  })
}

export function drawGroundLootVisual(
  graphics: Graphics,
  options: {
    item: InventoryItemRecord
    centerX: number
    centerY: number
    elapsedSeconds: number
    highlighted: boolean
  },
): void {
  const definition = itemById[options.item.itemId]
  const style = resolveItemVisualStyle(definition)
  const pulse = (Math.sin(options.elapsedSeconds * 4.8 + options.centerX * 0.012) + 1) * 0.5
  const floatOffset = Math.sin(options.elapsedSeconds * 2.6 + options.centerX * 0.013 + options.centerY * 0.009) * 1.8
  const sway = Math.sin(options.elapsedSeconds * 3.4 + options.centerX * 0.017 + options.centerY * 0.011) * (options.highlighted ? 1.3 : 0.7)
  const scale = options.highlighted ? 1.18 + pulse * 0.035 : 1.08 + pulse * 0.025
  const centerX = options.centerX + sway
  const centerY = options.centerY - 4 + floatOffset
  const width = Math.max(14, options.item.width * GROUND_LOOT_CELL_SIZE) * scale
  const height = Math.max(14, options.item.height * GROUND_LOOT_CELL_SIZE) * scale
  const x = centerX - width * 0.5
  const y = centerY - height * 0.5

  graphics.ellipse(centerX, centerY + height * 0.42, width * 0.46, Math.max(2.5, height * 0.15))
    .fill({ color: style.glowColor, alpha: options.highlighted ? 0.18 + pulse * 0.06 : 0.1 + pulse * 0.03 })
  graphics.ellipse(centerX, centerY + height * 0.42, width * 0.3, Math.max(2, height * 0.1))
    .fill({ color: style.rarityColor, alpha: options.highlighted ? 0.1 + pulse * 0.04 : 0.05 + pulse * 0.02 })

  drawGroundLootPlate(
    graphics,
    style,
    options.item,
    x,
    y,
    width,
    height,
    options.highlighted ? 0.92 : 0.84,
    options.highlighted ? 0.66 : 0.42,
    options.highlighted ? 0.54 : 0.32,
  )

  if (options.highlighted) {
    graphics.poly(buildGroundLootPlatePoints(style.category, x - 2, y - 2, width + 4, height + 4))
      .stroke({ width: 1.2, color: style.rarityColor, alpha: 0.7 + pulse * 0.1, alignment: 0.5, join: 'round' })
  }
}

function drawGroundLootPlate(
  graphics: Graphics,
  style: ItemVisualStyle,
  item: InventoryItemRecord,
  x: number,
  y: number,
  width: number,
  height: number,
  fillAlpha: number,
  strokeAlpha: number,
  detailAlpha: number,
): void {
  const points = buildGroundLootPlatePoints(style.category, x, y, width, height)
  const innerBounds = getGroundLootInnerBounds(x, y, width, height)

  graphics.poly(points).fill({ color: style.fillColor, alpha: fillAlpha })
  graphics.poly(points).stroke({ width: 1.1, color: style.categoryColor, alpha: strokeAlpha, alignment: 0.5, join: 'round' })
  graphics.rect(x + 2, y + 2, Math.max(4, width * 0.34), 1.2).fill({ color: style.rarityColor, alpha: 0.22 + detailAlpha * 0.34 })

  drawItemGlyph(graphics, itemById[item.itemId], style, innerBounds.x, innerBounds.y, innerBounds.width, innerBounds.height, detailAlpha, 0.56, {
    minGlyphSize: 4.5,
    minStroke: 0.8,
  })

  if (item.quantity > 1) {
    drawGroundLootStackHint(graphics, style, x, y, width, height, detailAlpha)
  }
}

function drawItemPlate(graphics: Graphics, options: DrawItemPlateOptions): void {
  const definition = itemById[options.item.itemId]
  const style = resolveItemVisualStyle(definition)
  const x = options.x
  const y = options.y
  const width = options.width
  const height = options.height
  const topRailInset = clamp(Math.min(width, height) * 0.18, 6, 12)
  const innerBounds = getInnerBounds(x, y, width, height, options.iconScale ?? 1)

  drawPlateShell(graphics, style, x, y, width, height, options.bodyAlpha, options.strokeAlpha, 1.5)

  graphics.rect(x + topRailInset, y + 5, Math.max(12, width - topRailInset * 2), 2)
    .fill({ color: style.rarityColor, alpha: 0.58 + options.detailAlpha * 0.45 + options.emphasis * 0.12 })

  graphics.rect(x + 7, y + height - 7, Math.max(10, width * 0.34), 2)
    .fill({ color: style.categoryColor, alpha: 0.14 + options.detailAlpha * 0.36 })

  drawCategoryMotif(graphics, style, innerBounds.x, innerBounds.y, innerBounds.width, innerBounds.height, options.detailAlpha, options.emphasis)
  drawItemGlyph(graphics, definition, style, innerBounds.x, innerBounds.y, innerBounds.width, innerBounds.height, options.detailAlpha, options.emphasis)
  drawCornerFrame(
    graphics,
    x + 5,
    y + 5,
    Math.max(10, width - 10),
    Math.max(10, height - 10),
    clamp(Math.min(width, height) * 0.14, 4, 6),
    style.rarityColor,
    0.18 + options.strokeAlpha * 0.28 + options.emphasis * 0.1,
    1,
  )

  if (options.item.quantity > 1) {
    drawStackMarker(graphics, style, x, y, width, height, options.item.quantity, options.detailAlpha, options.emphasis)
  }
}

function drawPlateShell(
  graphics: Graphics,
  style: ItemVisualStyle,
  x: number,
  y: number,
  width: number,
  height: number,
  fillAlpha: number,
  strokeAlpha: number,
  strokeWidth: number,
): void {
  const points = buildCategoryPlatePoints(style.category, x, y, width, height)
  const echoPoints = offsetPoints(points, 2, 2)

  graphics.poly(echoPoints).fill({ color: style.glowColor, alpha: fillAlpha * 0.16 })
  graphics.poly(points).fill({ color: style.fillColor, alpha: fillAlpha })
  graphics.poly(points).stroke({ width: strokeWidth, color: style.categoryColor, alpha: strokeAlpha, alignment: 0.5, join: 'round' })
}

function drawCategoryMotif(
  graphics: Graphics,
  style: ItemVisualStyle,
  x: number,
  y: number,
  width: number,
  height: number,
  detailAlpha: number,
  emphasis: number,
): void {
  const alpha = 0.1 + detailAlpha * 0.22 + emphasis * 0.05

  if (style.category === 'resource') {
    const step = clamp(Math.min(width, height) * 0.22, 5, 8)
    for (let index = 0; index < 3; index += 1) {
      const baseX = x + width * 0.14 + index * step
      graphics.moveTo(baseX, y + height - 6)
      graphics.lineTo(baseX + step, y + height * 0.38)
    }
    graphics.stroke({ width: 1, color: style.softColor, alpha, cap: 'round' })
    return
  }

  if (style.category === 'intel') {
    const railX = x + width * 0.5
    graphics.moveTo(railX, y + 4)
    graphics.lineTo(railX, y + height - 4)
    graphics.moveTo(x + 5, y + height * 0.28)
    graphics.lineTo(x + width - 5, y + height * 0.28)
    graphics.moveTo(x + 5, y + height * 0.72)
    graphics.lineTo(x + width - 5, y + height * 0.72)
    graphics.stroke({ width: 1, color: style.softColor, alpha, cap: 'round' })

    graphics.rect(railX - 2, y + height * 0.34, 4, Math.max(6, height * 0.18))
      .fill({ color: style.rarityColor, alpha: 0.12 + detailAlpha * 0.24 })
    return
  }

  if (style.category === 'boss') {
    const diamondSize = Math.min(width, height) * 0.22
    const cx = x + width * 0.5
    const cy = y + height * 0.5
    graphics.poly([
      cx,
      cy - diamondSize,
      cx + diamondSize,
      cy,
      cx,
      cy + diamondSize,
      cx - diamondSize,
      cy,
    ]).stroke({ width: 1, color: style.softColor, alpha, alignment: 0.5, join: 'round' })

    graphics.moveTo(x + 6, cy)
    graphics.lineTo(cx - diamondSize - 4, cy)
    graphics.moveTo(cx + diamondSize + 4, cy)
    graphics.lineTo(x + width - 6, cy)
    graphics.stroke({ width: 1, color: style.softColor, alpha: alpha * 0.9, cap: 'round' })
    return
  }

  graphics.moveTo(x + 6, y + height * 0.5)
  graphics.lineTo(x + width - 6, y + height * 0.5)
  graphics.stroke({ width: 1, color: style.softColor, alpha, cap: 'round' })

  const capsuleWidth = Math.max(8, width * 0.18)
  graphics.rect(x + 5, y + height * 0.5 - 3, capsuleWidth, 6)
    .stroke({ width: 1, color: style.softColor, alpha: alpha * 0.95, alignment: 0.5 })
  graphics.rect(x + width - 5 - capsuleWidth, y + height * 0.5 - 3, capsuleWidth, 6)
    .stroke({ width: 1, color: style.softColor, alpha: alpha * 0.95, alignment: 0.5 })
}

function drawItemGlyph(
  graphics: Graphics,
  definition: ItemDefinition | undefined,
  style: ItemVisualStyle,
  x: number,
  y: number,
  width: number,
  height: number,
  detailAlpha: number,
  emphasis: number,
  metrics?: {
    minGlyphSize?: number
    minStroke?: number
  },
): void {
  const cx = x + width * 0.5
  const cy = y + height * 0.5
  const minGlyphSize = metrics?.minGlyphSize ?? 8
  const minStroke = metrics?.minStroke ?? 1
  const glyphWidth = Math.max(minGlyphSize, width * 0.42)
  const glyphHeight = Math.max(minGlyphSize, height * 0.42)
  const stroke = clamp(Math.min(glyphWidth, glyphHeight) * 0.1, minStroke, 1.6)
  const accentAlpha = 0.14 + detailAlpha * 0.28 + emphasis * 0.08
  const strokeAlpha = 0.72 + detailAlpha * 0.18 + emphasis * 0.08
  const accentColor = style.accentColor

  switch (definition?.id) {
    case 'salvage-scrap':
      graphics.moveTo(cx - glyphWidth * 0.28, cy + glyphHeight * 0.16)
      graphics.lineTo(cx - glyphWidth * 0.02, cy - glyphHeight * 0.12)
      graphics.moveTo(cx + glyphWidth * 0.04, cy + glyphHeight * 0.22)
      graphics.lineTo(cx + glyphWidth * 0.28, cy - glyphHeight * 0.04)
      graphics.stroke({ width: stroke, color: style.glyphColor, alpha: strokeAlpha, cap: 'round', join: 'round' })
      graphics.rect(cx - 1.5, cy - 1.5, 3, 3).fill({ color: style.rarityColor, alpha: accentAlpha })
      return

    case 'telemetry-cache':
      graphics.rect(cx - glyphWidth * 0.12, cy - glyphHeight * 0.34, glyphWidth * 0.24, glyphHeight * 0.68)
        .stroke({ width: stroke, color: style.glyphColor, alpha: strokeAlpha, alignment: 0.5 })
      graphics.rect(cx - glyphWidth * 0.05, cy - glyphHeight * 0.08, glyphWidth * 0.1, glyphHeight * 0.16)
        .fill({ color: accentColor, alpha: accentAlpha })
      graphics.moveTo(cx - glyphWidth * 0.28, cy - glyphHeight * 0.14)
      graphics.lineTo(cx - glyphWidth * 0.18, cy - glyphHeight * 0.14)
      graphics.moveTo(cx + glyphWidth * 0.18, cy + glyphHeight * 0.14)
      graphics.lineTo(cx + glyphWidth * 0.28, cy + glyphHeight * 0.14)
      graphics.stroke({ width: stroke * 0.9, color: style.rarityColor, alpha: strokeAlpha * 0.7, cap: 'round' })
      return

    case 'alloy-plate':
      graphics.poly([
        cx - glyphWidth * 0.32,
        cy - glyphHeight * 0.08,
        cx - glyphWidth * 0.12,
        cy - glyphHeight * 0.28,
        cx + glyphWidth * 0.32,
        cy - glyphHeight * 0.28,
        cx + glyphWidth * 0.12,
        cy + glyphHeight * 0.08,
      ]).stroke({ width: stroke, color: style.glyphColor, alpha: strokeAlpha, alignment: 0.5, join: 'round' })
      graphics.rect(cx - glyphWidth * 0.08, cy - 1, glyphWidth * 0.16, 2).fill({ color: accentColor, alpha: accentAlpha })
      return

    case 'aegis-core':
      graphics.poly([
        cx,
        cy - glyphHeight * 0.32,
        cx + glyphWidth * 0.28,
        cy,
        cx,
        cy + glyphHeight * 0.32,
        cx - glyphWidth * 0.28,
        cy,
      ]).stroke({ width: stroke, color: style.glyphColor, alpha: strokeAlpha, alignment: 0.5, join: 'round' })
      graphics.poly([
        cx,
        cy - glyphHeight * 0.14,
        cx + glyphWidth * 0.12,
        cy,
        cx,
        cy + glyphHeight * 0.14,
        cx - glyphWidth * 0.12,
        cy,
      ]).fill({ color: accentColor, alpha: accentAlpha })
      return

    case 'med-injector':
      graphics.rect(cx - glyphWidth * 0.08, cy - glyphHeight * 0.3, glyphWidth * 0.16, glyphHeight * 0.48)
        .stroke({ width: stroke, color: style.glyphColor, alpha: strokeAlpha, alignment: 0.5 })
      graphics.moveTo(cx, cy - glyphHeight * 0.42)
      graphics.lineTo(cx, cy - glyphHeight * 0.3)
      graphics.moveTo(cx - glyphWidth * 0.14, cy + glyphHeight * 0.18)
      graphics.lineTo(cx + glyphWidth * 0.14, cy + glyphHeight * 0.18)
      graphics.stroke({ width: stroke * 0.9, color: style.rarityColor, alpha: strokeAlpha * 0.9, cap: 'round' })
      return

    case 'field-kit':
      graphics.rect(cx - glyphWidth * 0.3, cy - glyphHeight * 0.18, glyphWidth * 0.6, glyphHeight * 0.36)
        .stroke({ width: stroke, color: style.glyphColor, alpha: strokeAlpha, alignment: 0.5 })
      graphics.moveTo(cx - glyphWidth * 0.12, cy)
      graphics.lineTo(cx + glyphWidth * 0.12, cy)
      graphics.moveTo(cx, cy - glyphHeight * 0.12)
      graphics.lineTo(cx, cy + glyphHeight * 0.12)
      graphics.stroke({ width: stroke * 1.05, color: style.rarityColor, alpha: strokeAlpha * 0.95, cap: 'round' })
      graphics.moveTo(cx - glyphWidth * 0.1, cy - glyphHeight * 0.18)
      graphics.lineTo(cx - glyphWidth * 0.02, cy - glyphHeight * 0.28)
      graphics.lineTo(cx + glyphWidth * 0.1, cy - glyphHeight * 0.28)
      graphics.lineTo(cx + glyphWidth * 0.18, cy - glyphHeight * 0.18)
      graphics.stroke({ width: stroke * 0.85, color: style.glyphColor, alpha: strokeAlpha * 0.8, join: 'round' })
      return

    case 'shock-charge':
      graphics.circle(cx, cy, Math.min(glyphWidth, glyphHeight) * 0.26)
        .stroke({ width: stroke, color: style.glyphColor, alpha: strokeAlpha, alignment: 0.5 })
      graphics.moveTo(cx - glyphWidth * 0.06, cy - glyphHeight * 0.2)
      graphics.lineTo(cx + glyphWidth * 0.02, cy - glyphHeight * 0.02)
      graphics.lineTo(cx - glyphWidth * 0.02, cy - glyphHeight * 0.02)
      graphics.lineTo(cx + glyphWidth * 0.08, cy + glyphHeight * 0.2)
      graphics.stroke({ width: stroke, color: accentColor, alpha: strokeAlpha, cap: 'round', join: 'round' })
      return

    case 'dash-cell':
      graphics.rect(cx - glyphWidth * 0.2, cy - glyphHeight * 0.28, glyphWidth * 0.4, glyphHeight * 0.56)
        .stroke({ width: stroke, color: style.glyphColor, alpha: strokeAlpha, alignment: 0.5 })
      graphics.rect(cx + glyphWidth * 0.2, cy - glyphHeight * 0.08, glyphWidth * 0.06, glyphHeight * 0.16)
        .fill({ color: style.glyphColor, alpha: strokeAlpha })
      graphics.moveTo(cx - glyphWidth * 0.08, cy - glyphHeight * 0.12)
      graphics.lineTo(cx + glyphWidth * 0.04, cy)
      graphics.lineTo(cx - glyphWidth * 0.08, cy + glyphHeight * 0.12)
      graphics.stroke({ width: stroke * 0.95, color: accentColor, alpha: strokeAlpha, cap: 'round', join: 'round' })
      return

    default:
      graphics.poly([
        cx,
        cy - glyphHeight * 0.28,
        cx + glyphWidth * 0.24,
        cy,
        cx,
        cy + glyphHeight * 0.28,
        cx - glyphWidth * 0.24,
        cy,
      ]).stroke({ width: stroke, color: style.glyphColor, alpha: strokeAlpha, alignment: 0.5, join: 'round' })
  }
}

function drawStackMarker(
  graphics: Graphics,
  style: ItemVisualStyle,
  x: number,
  y: number,
  width: number,
  height: number,
  quantity: number,
  detailAlpha: number,
  emphasis: number,
): void {
  const markerWidth = Math.max(12, Math.min(18, width * 0.3))
  const markerHeight = 10
  const markerX = x + width - markerWidth - 6
  const markerY = y + height - markerHeight - 6
  const tickCount = Math.min(4, quantity)

  graphics.poly([
    markerX + 4,
    markerY,
    markerX + markerWidth,
    markerY,
    markerX + markerWidth,
    markerY + markerHeight - 3,
    markerX + markerWidth - 4,
    markerY + markerHeight,
    markerX,
    markerY + markerHeight,
    markerX,
    markerY + 4,
  ]).fill({ color: style.rarityColor, alpha: 0.78 + detailAlpha * 0.12 + emphasis * 0.08 })

  const gap = markerWidth / (tickCount + 1)
  for (let index = 1; index <= tickCount; index += 1) {
    const tickX = markerX + gap * index
    graphics.moveTo(tickX, markerY + 2)
    graphics.lineTo(tickX, markerY + markerHeight - 2)
  }
  graphics.stroke({ width: 1, color: palette.uiPanel, alpha: 0.84, cap: 'round' })
}

function resolveItemVisualStyle(definition: ItemDefinition | undefined): ItemVisualStyle {
  const category = definition?.category ?? 'resource'
  const rarityColor = ITEM_RARITY_COLORS[definition?.rarity ?? 'common']
  const categoryColor = ITEM_CATEGORY_COLORS[category]
  const accentColor = definition?.accent ?? rarityColor
  const tintColor = definition?.tint ?? categoryColor

  return {
    category,
    categoryColor,
    rarityColor,
    accentColor,
    fillColor: mixColors(palette.uiPanel, tintColor, 0.08),
    glowColor: mixColors(rarityColor, 0xffffff, 0.24),
    glyphColor: mixColors(mixColors(accentColor, tintColor, 0.5), rarityColor, 0.34),
    softColor: mixColors(categoryColor, 0xffffff, 0.55),
  }
}

function buildCategoryPlatePoints(category: ItemCategory, x: number, y: number, width: number, height: number): number[] {
  const cut = getCardCut(width, height)

  if (category === 'resource') {
    return [
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
  }

  if (category === 'intel') {
    return [
      x + cut,
      y,
      x + width - cut,
      y,
      x + width,
      y + cut,
      x + width,
      y + height - cut,
      x + width - cut,
      y + height,
      x + cut,
      y + height,
      x,
      y + height - cut,
      x,
      y + cut,
    ]
  }

  if (category === 'boss') {
    const heavyCut = clamp(cut * 1.18, 6, Math.min(width, height) * 0.34)
    return [
      x + heavyCut,
      y,
      x + width - heavyCut,
      y,
      x + width,
      y + heavyCut,
      x + width,
      y + height - heavyCut,
      x + width - heavyCut,
      y + height,
      x + heavyCut,
      y + height,
      x,
      y + height - heavyCut,
      x,
      y + heavyCut,
    ]
  }

  return [
    x + cut,
    y,
    x + width - cut,
    y,
    x + width,
    y + height * 0.5,
    x + width - cut,
    y + height,
    x + cut,
    y + height,
    x,
    y + height * 0.5,
  ]
}

function getInnerBounds(x: number, y: number, width: number, height: number, iconScale: number) {
  const paddingScale = 1 / clamp(iconScale, 0.85, 1.2)
  const paddingX = clamp(width * 0.18 * paddingScale, 4, 11)
  const paddingY = clamp(height * 0.18 * paddingScale, 4, 11)

  return {
    x: x + paddingX,
    y: y + paddingY,
    width: Math.max(10, width - paddingX * 2),
    height: Math.max(10, height - paddingY * 2),
  }
}

function getGroundLootInnerBounds(x: number, y: number, width: number, height: number) {
  const paddingX = clamp(width * 0.14, 1.5, 4)
  const paddingY = clamp(height * 0.14, 1.5, 4)

  return {
    x: x + paddingX,
    y: y + paddingY,
    width: Math.max(4, width - paddingX * 2),
    height: Math.max(4, height - paddingY * 2),
  }
}

function buildGroundLootPlatePoints(category: ItemCategory, x: number, y: number, width: number, height: number): number[] {
  const cut = clamp(Math.min(width, height) * 0.18, 1.5, 4)

  if (category === 'boss') {
    const heavyCut = clamp(cut * 1.15, 2, Math.min(width, height) * 0.28)
    return [
      x + heavyCut,
      y,
      x + width - heavyCut,
      y,
      x + width,
      y + heavyCut,
      x + width,
      y + height - heavyCut,
      x + width - heavyCut,
      y + height,
      x + heavyCut,
      y + height,
      x,
      y + height - heavyCut,
      x,
      y + heavyCut,
    ]
  }

  if (category === 'consumable') {
    return [
      x + cut,
      y,
      x + width - cut,
      y,
      x + width,
      y + height * 0.5,
      x + width - cut,
      y + height,
      x + cut,
      y + height,
      x,
      y + height * 0.5,
    ]
  }

  return [
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
}

function drawGroundLootStackHint(
  graphics: Graphics,
  style: ItemVisualStyle,
  x: number,
  y: number,
  width: number,
  height: number,
  detailAlpha: number,
): void {
  const dotRadius = clamp(Math.min(width, height) * 0.1, 1, 1.8)
  const baseX = x + width - dotRadius * 2.4
  const baseY = y + height - dotRadius * 2.4

  for (let index = 0; index < 2; index += 1) {
    graphics.circle(baseX - index * (dotRadius * 2.6), baseY, dotRadius)
      .fill({ color: style.rarityColor, alpha: 0.5 + detailAlpha * 0.28 })
  }
}

function getCardCut(width: number, height: number): number {
  return clamp(Math.min(width, height) * 0.2, 4, 10)
}

function offsetPoints(points: number[], dx: number, dy: number): number[] {
  const shifted: number[] = []

  for (let index = 0; index < points.length; index += 2) {
    shifted.push(points[index] + dx, points[index + 1] + dy)
  }

  return shifted
}

function mixColors(colorA: number, colorB: number, ratio: number): number {
  const weight = clamp(ratio, 0, 1)
  const inverse = 1 - weight
  const red = ((colorA >> 16) & 0xff) * inverse + ((colorB >> 16) & 0xff) * weight
  const green = ((colorA >> 8) & 0xff) * inverse + ((colorB >> 8) & 0xff) * weight
  const blue = (colorA & 0xff) * inverse + (colorB & 0xff) * weight

  return (Math.round(red) << 16) | (Math.round(green) << 8) | Math.round(blue)
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value))
}
