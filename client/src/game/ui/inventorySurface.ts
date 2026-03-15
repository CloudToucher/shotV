import { Graphics } from 'pixi.js'

import type { InventoryItemRecord } from '../inventory/types'
import { palette } from '../theme/palette'
import { drawInventoryItemVisual } from './itemVisuals'

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

  graphics.rect(x, y, width, height).fill({ color: palette.uiPanel, alpha: 0.8 })
  graphics.rect(x, y, width, height).stroke({ width: 1.5, color: palette.frame, alpha: 0.3, alignment: 0.5 })
  
  // Tactical corners
  const cut = 8
  graphics.moveTo(x, y + cut).lineTo(x, y).lineTo(x + cut, y)
  graphics.moveTo(x + width - cut, y).lineTo(x + width, y).lineTo(x + width, y + cut)
  graphics.moveTo(x + width, y + height - cut).lineTo(x + width, y + height).lineTo(x + width - cut, y + height)
  graphics.moveTo(x + cut, y + height).lineTo(x, y + height).lineTo(x, y + height - cut)
  graphics.stroke({ width: 2, color: palette.frame, alpha: 0.8 })

  for (let row = 0; row < rows; row += 1) {
    for (let column = 0; column < columns; column += 1) {
      const cellX = x + column * cellSize
      const cellY = y + row * cellSize
      graphics.rect(cellX + 1, cellY + 1, cellSize - 2, cellSize - 2).fill({ color: palette.uiActive, alpha: 0.15 })
      graphics.rect(cellX + 1, cellY + 1, cellSize - 2, cellSize - 2).stroke({ width: 1, color: palette.frameSoft, alpha: 0.1, alignment: 0.5 })
      
      // Center dot for grid
      graphics.rect(cellX + cellSize / 2 - 1, cellY + cellSize / 2 - 1, 2, 2).fill({ color: palette.frameSoft, alpha: 0.2 })
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
  drawInventoryItemVisual(graphics, {
    item: options.item,
    cellSize: options.cellSize,
    x: options.x,
    y: options.y,
    alpha: options.alpha,
    accentAlpha: options.accentAlpha,
    strokeAlpha: options.strokeAlpha,
  })
}
