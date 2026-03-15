import type { Container } from 'pixi.js'

export interface ViewportSize {
  width: number
  height: number
}

export interface ArenaBounds {
  left: number
  top: number
  right: number
  bottom: number
}

export interface WorldObstacle {
  id: string
  x: number
  y: number
  width: number
  height: number
  label?: string
  kind?: 'wall' | 'cover' | 'locker' | 'station'
}

export interface WorldMarker {
  id: string
  x: number
  y: number
  label: string
  kind: 'entry' | 'objective' | 'extraction' | 'locker' | 'station'
}

export interface InputSnapshot {
  moveX: number
  moveY: number
  pointerX: number
  pointerY: number
  hasPointer: boolean
  pointerPressed: boolean
  pointerReleased: boolean
  shootHeld: boolean
  dashPressed: boolean
  interactPressed: boolean
  rotatePressed: boolean
  sortPressed: boolean
  panelTogglePressed: boolean
  mapTogglePressed: boolean
  weaponSwitch: 1 | 2 | 3 | null
  quickSlotBind: 1 | 2 | 3 | 4 | null
  quickSlotUse: 1 | 2 | 3 | 4 | null
}

export interface GameScene {
  readonly container: Container
  resize(viewport: ViewportSize): void
  update(deltaSeconds: number, elapsedSeconds: number, input: InputSnapshot): void
  destroy(): void
}
