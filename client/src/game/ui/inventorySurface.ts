import { Graphics } from 'pixi.js'

import { itemById } from '../data/items'
import type { InventoryItemRecord } from '../inventory/types'
import { palette } from '../theme/palette'
import { drawCornerFrame } from './surface'

export interface InventoryGridPoint {
  x: number
  y: number
}

export interface InventoryGridDrawOptions {
  x: number
  y: number
  columns: number
  rows: number
  cellSize: number
  items: readonly InventoryItemRecord[]
}

export function drawInventoryGrid(graphics: Graphics, options: InventoryGridDrawOptions): void {
  const { x, y, columns, rows, cellSize, items } = options
  const width = columns * cellSize
  const height = rows * cellSize

  graphics.roundRect(x, y, width, height, 12).fill({ color: palette.arenaCore, alpha: 0.84 })
  graphics.roundRect(x, y, width, height, 12).stroke({ width: 1.1, color: palette.frame, alpha: 0.22, alignment: 0.5 })
  graphics.roundRect(x + 2, y + 2, width - 4, height - 4, 10).stroke({ width: 1, color: palette.frameSoft, alpha: 0.14, alignment: 0.5 })
  drawCornerFrame(graphics, x + 8, y + 8, width - 16, height - 16, 10, palette.frame, 0.14, 1)

  for (let row = 0; row < rows; row += 1) {
    for (let column = 0; column < columns; column += 1) {
      const cellX = x + column * cellSize
      const cellY = y + row * cellSize
      graphics.roundRect(cellX + 1.5, cellY + 1.5, cellSize - 3, cellSize - 3, 6).fill({ color: palette.uiActive, alpha: 0.32 })
      graphics.roundRect(cellX + 1.5, cellY + 1.5, cellSize - 3, cellSize - 3, 6).stroke({ width: 1, color: palette.frameSoft, alpha: 0.12, alignment: 0.5 })
    }
  }

  for (const item of items) {
    drawInventoryItem(graphics, { item, cellSize, x: x + item.x * cellSize, y: y + item.y * cellSize })
  }
}

export function resolveInventoryCellAtPoint(options: Omit<InventoryGridDrawOptions, 'items'>, pointX: number, pointY: number): InventoryGridPoint | null {
  const localX = pointX - options.x
  const localY = pointY - options.y

  if (localX < 0 || localY < 0) {
    return null
  }

  const x = Math.floor(localX / options.cellSize)
  const y = Math.floor(localY / options.cellSize)

  if (x < 0 || y < 0 || x >= options.columns || y >= options.rows) {
    return null
  }

  return { x, y }
}

export function drawFloatingInventoryItem(
  graphics: Graphics,
  options: {
    item: InventoryItemRecord
    cellSize: number
    x: number
    y: number
    valid: boolean
  },
): void {
  drawInventoryItem(graphics, {
    item: options.item,
    cellSize: options.cellSize,
    x: options.x,
    y: options.y,
    alpha: options.valid ? 0.72 : 0.4,
    accentAlpha: options.valid ? 0.3 : 0.18,
    strokeAlpha: options.valid ? 0.7 : 0.4,
  })
}

function drawInventoryItem(
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
  const definition = itemById[options.item.itemId]
  const itemWidth = options.item.width * options.cellSize
  const itemHeight = options.item.height * options.cellSize
  const tint = definition?.tint ?? palette.warning
  const accent = definition?.accent ?? palette.accent
  const alpha = options.alpha ?? 0.2
  const accentAlpha = options.accentAlpha ?? 0.3
  const strokeAlpha = options.strokeAlpha ?? 0.56

  graphics.roundRect(options.x + 2, options.y + 2, itemWidth - 4, itemHeight - 4, 8).fill({ color: tint, alpha })
  graphics.roundRect(options.x + 2, options.y + 2, itemWidth - 4, itemHeight - 4, 8).stroke({ width: 1.4, color: tint, alpha: strokeAlpha, alignment: 0.5 })
  graphics.roundRect(options.x + 6, options.y + 6, itemWidth - 12, 8, 999).fill({ color: accent, alpha: accentAlpha })
  drawCornerFrame(
    graphics,
    options.x + 6,
    options.y + 6,
    Math.max(10, itemWidth - 12),
    Math.max(10, itemHeight - 12),
    Math.max(6, Math.min(10, Math.min(itemWidth, itemHeight) * 0.18)),
    tint,
    strokeAlpha * 0.42,
    1,
  )

  if (options.item.quantity > 1) {
    graphics.roundRect(options.x + itemWidth - 20, options.y + itemHeight - 18, 14, 12, 6).fill({ color: accent, alpha: Math.min(0.88, accentAlpha + 0.3) })
  }
}
