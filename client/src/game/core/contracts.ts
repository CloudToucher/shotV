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

export interface InputSnapshot {
  moveX: number
  moveY: number
  pointerX: number
  pointerY: number
  hasPointer: boolean
  shootHeld: boolean
  dashPressed: boolean
  restartPressed: boolean
  weaponSwitch: 1 | 2 | 3 | null
}

export interface GameScene {
  readonly container: Container
  resize(viewport: ViewportSize): void
  update(deltaSeconds: number, elapsedSeconds: number, input: InputSnapshot): void
  destroy(): void
}
