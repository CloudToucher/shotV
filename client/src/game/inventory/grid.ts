import { itemById } from '../data/items'
import type { ResourceBundle } from '../data/types'
import { RUN_QUICK_SLOT_COUNT, type GridInventoryState, type InventoryItemRecord } from './types'

export interface InventoryPlacementResult {
  placed: boolean
  items: InventoryItemRecord[]
}

export interface InventoryExtractionResult {
  item: InventoryItemRecord | null
  items: InventoryItemRecord[]
}

export interface InventoryArrangementResult {
  arranged: boolean
  items: InventoryItemRecord[]
}

export interface InventoryConsumeResult {
  consumed: boolean
  item: InventoryItemRecord | null
  items: InventoryItemRecord[]
}

export function createInventoryItemRecord(itemId: string, quantity: number, id = buildInventoryItemId(itemId)): InventoryItemRecord | null {
  const definition = itemById[itemId]

  if (!definition) {
    return null
  }

  return {
    id,
    itemId,
    quantity: Math.max(1, Math.min(definition.maxStack, Math.round(quantity))),
    x: 0,
    y: 0,
    width: definition.width,
    height: definition.height,
    rotated: false,
  }
}

export function placeItemInGrid(columns: number, rows: number, items: readonly InventoryItemRecord[], incoming: InventoryItemRecord): InventoryPlacementResult {
  const placement = findFirstPlacement(columns, rows, items, incoming)

  if (!placement) {
    return {
      placed: false,
      items: cloneInventoryItems(items),
    }
  }

  return {
    placed: true,
    items: [
      ...cloneInventoryItems(items),
      {
        ...incoming,
        x: placement.x,
        y: placement.y,
      },
    ],
  }
}

export function placeItemAtPosition(columns: number, rows: number, items: readonly InventoryItemRecord[], incoming: InventoryItemRecord, x: number, y: number): InventoryPlacementResult {
  if (!canPlaceItemAtPosition(columns, rows, items, incoming, x, y)) {
    return {
      placed: false,
      items: cloneInventoryItems(items),
    }
  }

  return {
    placed: true,
    items: [
      ...cloneInventoryItems(items),
      {
        ...incoming,
        x,
        y,
      },
    ],
  }
}

export function placeItemsInGrid(columns: number, rows: number, existingItems: readonly InventoryItemRecord[], incomingItems: readonly InventoryItemRecord[]) {
  let items = cloneInventoryItems(existingItems)
  const placedIds: string[] = []
  const rejected: InventoryItemRecord[] = []

  for (const incoming of [...incomingItems].sort(sortInventoryItemsForPacking)) {
    const result = placeItemInGrid(columns, rows, items, incoming)

    if (!result.placed) {
      rejected.push({ ...incoming })
      continue
    }

    items = result.items
    placedIds.push(incoming.id)
  }

  return {
    items,
    placedIds,
    rejected,
  }
}

export function getInventoryUsedCells(items: readonly InventoryItemRecord[]): number {
  return items.reduce((total, item) => total + item.width * item.height, 0)
}

export function getInventoryCapacity(columns: number, rows: number): number {
  return columns * rows
}

export function pickItemFromGridAtCell(items: readonly InventoryItemRecord[], x: number, y: number): InventoryExtractionResult {
  const target = findItemAtCell(items, x, y)

  if (!target) {
    return {
      item: null,
      items: cloneInventoryItems(items),
    }
  }

  return {
    item: { ...target },
    items: items.filter((item) => item.id !== target.id).map((item) => ({ ...item })),
  }
}

export function findItemAtCell(items: readonly InventoryItemRecord[], x: number, y: number): InventoryItemRecord | null {
  return items.find((item) => x >= item.x && x < item.x + item.width && y >= item.y && y < item.y + item.height) ?? null
}

export function rotateInventoryItem(item: InventoryItemRecord): InventoryItemRecord {
  return {
    ...item,
    width: item.height,
    height: item.width,
    rotated: !item.rotated,
  }
}

export function canPlaceItemAtPosition(columns: number, rows: number, items: readonly InventoryItemRecord[], incoming: InventoryItemRecord, x: number, y: number): boolean {
  if (x < 0 || y < 0) {
    return false
  }

  if (x + incoming.width > columns || y + incoming.height > rows) {
    return false
  }

  return !intersectsAny(items, x, y, incoming.width, incoming.height)
}

export function buildResourceLedgerFromItems(items: readonly InventoryItemRecord[]): ResourceBundle {
  return items.reduce<ResourceBundle>(
    (ledger, item) => {
      const definition = itemById[item.itemId]

      if (!definition) {
        return ledger
      }

      return {
        salvage: ledger.salvage + definition.recoveredResources.salvage * item.quantity,
        alloy: ledger.alloy + definition.recoveredResources.alloy * item.quantity,
        research: ledger.research + definition.recoveredResources.research * item.quantity,
      }
    },
    {
      salvage: 0,
      alloy: 0,
      research: 0,
    },
  )
}

export function autoArrangeInventory(columns: number, rows: number, items: readonly InventoryItemRecord[]): InventoryArrangementResult {
  const orderedItems = cloneInventoryItems(items)
    .map((item) => ({ ...item, x: 0, y: 0 }))
    .sort(sortInventoryItemsForPacking)
  const occupied = new Array(columns * rows).fill(false)
  const arrangedItems = solveInventoryArrangement(columns, rows, occupied, orderedItems, [])

  if (!arrangedItems) {
    return {
      arranged: false,
      items: cloneInventoryItems(items),
    }
  }

  return {
    arranged: true,
    items: arrangedItems,
  }
}

export function cloneGridInventoryState(state: GridInventoryState): GridInventoryState {
  return {
    columns: state.columns,
    rows: state.rows,
    items: cloneInventoryItems(state.items),
    quickSlots: sanitizeQuickSlotBindings(state.quickSlots, state.items.map((item) => item.id)),
  }
}

export function cloneInventoryItems(items: readonly InventoryItemRecord[]): InventoryItemRecord[] {
  return items.map((item) => ({ ...item }))
}

export function consumeInventoryItemById(items: readonly InventoryItemRecord[], itemId: string, amount = 1): InventoryConsumeResult {
  const nextItems = cloneInventoryItems(items)
  const target = nextItems.find((item) => item.id === itemId)

  if (!target || amount <= 0) {
    return {
      consumed: false,
      item: null,
      items: nextItems,
    }
  }

  target.quantity = Math.max(0, target.quantity - amount)

  return {
    consumed: true,
    item: { ...target, quantity: Math.max(1, Math.min(amount, target.quantity + amount)) },
    items: nextItems.filter((item) => item.quantity > 0),
  }
}

export function sanitizeQuickSlotBindings(quickSlots: readonly (string | null)[] | undefined, validItemIds: Iterable<string>): (string | null)[] {
  const validIds = new Set(validItemIds)
  const seen = new Set<string>()
  const normalized = Array.from({ length: RUN_QUICK_SLOT_COUNT }, (_, index) => quickSlots?.[index] ?? null)

  return normalized.map((itemId) => {
    if (!itemId || !validIds.has(itemId) || seen.has(itemId)) {
      return null
    }

    seen.add(itemId)
    return itemId
  })
}

export function assignQuickSlotBinding(
  quickSlots: readonly (string | null)[],
  slotIndex: number,
  itemId: string | null,
): (string | null)[] {
  const normalized = Array.from({ length: RUN_QUICK_SLOT_COUNT }, (_, index) => quickSlots[index] ?? null)

  if (slotIndex < 0 || slotIndex >= normalized.length) {
    return normalized
  }

  const wasSameBinding = itemId !== null && normalized[slotIndex] === itemId

  for (let index = 0; index < normalized.length; index += 1) {
    if (normalized[index] === itemId) {
      normalized[index] = null
    }
  }

  normalized[slotIndex] = wasSameBinding ? null : itemId
  return normalized
}

function findFirstPlacement(columns: number, rows: number, existingItems: readonly InventoryItemRecord[], incoming: InventoryItemRecord) {
  for (let y = 0; y <= rows - incoming.height; y += 1) {
    for (let x = 0; x <= columns - incoming.width; x += 1) {
      if (!intersectsAny(existingItems, x, y, incoming.width, incoming.height)) {
        return { x, y }
      }
    }
  }

  return null
}

function solveInventoryArrangement(
  columns: number,
  rows: number,
  occupied: boolean[],
  remainingItems: InventoryItemRecord[],
  placedItems: InventoryItemRecord[],
): InventoryItemRecord[] | null {
  if (remainingItems.length === 0) {
    return placedItems.map((item) => ({ ...item }))
  }

  const anchorIndex = occupied.findIndex((cell) => !cell)

  if (anchorIndex === -1) {
    return null
  }

  const anchorX = anchorIndex % columns
  const anchorY = Math.floor(anchorIndex / columns)
  const attemptedVariants = new Set<string>()

  for (let index = 0; index < remainingItems.length; index += 1) {
    const item = remainingItems[index]

    for (const variant of getArrangementVariants(item)) {
      const signature = `${variant.itemId}:${variant.quantity}:${variant.width}x${variant.height}:${variant.rotated}`

      if (attemptedVariants.has(signature)) {
        continue
      }

      attemptedVariants.add(signature)

      if (!canPlaceOnOccupiedGrid(columns, rows, occupied, variant, anchorX, anchorY)) {
        continue
      }

      markOccupiedCells(columns, occupied, variant, anchorX, anchorY, true)
      const nextRemaining = remainingItems.slice(0, index).concat(remainingItems.slice(index + 1))
      const nextPlaced = [...placedItems, { ...variant, x: anchorX, y: anchorY }]
      const arranged = solveInventoryArrangement(columns, rows, occupied, nextRemaining, nextPlaced)

      markOccupiedCells(columns, occupied, variant, anchorX, anchorY, false)

      if (arranged) {
        return arranged
      }
    }
  }

  return null
}

function getArrangementVariants(item: InventoryItemRecord): InventoryItemRecord[] {
  const variants = [{ ...item }]

  if (item.width !== item.height) {
    variants.push(rotateInventoryItem(item))
  }

  return variants
}

function canPlaceOnOccupiedGrid(
  columns: number,
  rows: number,
  occupied: readonly boolean[],
  item: InventoryItemRecord,
  x: number,
  y: number,
): boolean {
  if (x + item.width > columns || y + item.height > rows) {
    return false
  }

  for (let row = y; row < y + item.height; row += 1) {
    for (let column = x; column < x + item.width; column += 1) {
      if (occupied[row * columns + column]) {
        return false
      }
    }
  }

  return true
}

function markOccupiedCells(
  columns: number,
  occupied: boolean[],
  item: InventoryItemRecord,
  x: number,
  y: number,
  value: boolean,
): void {
  for (let row = y; row < y + item.height; row += 1) {
    for (let column = x; column < x + item.width; column += 1) {
      occupied[row * columns + column] = value
    }
  }
}

function intersectsAny(items: readonly InventoryItemRecord[], x: number, y: number, width: number, height: number): boolean {
  return items.some((item) => intersectsGridRect(item, x, y, width, height))
}

function intersectsGridRect(item: InventoryItemRecord, x: number, y: number, width: number, height: number): boolean {
  return item.x < x + width && item.x + item.width > x && item.y < y + height && item.y + item.height > y
}

function sortInventoryItemsForPacking(left: InventoryItemRecord, right: InventoryItemRecord): number {
  const areaDelta = right.width * right.height - left.width * left.height

  if (areaDelta !== 0) {
    return areaDelta
  }

  return right.quantity - left.quantity
}

function buildInventoryItemId(itemId: string): string {
  return `item-${itemId}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`
}
