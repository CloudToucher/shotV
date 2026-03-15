import { Application, type Ticker } from 'pixi.js'

import { createGameScene } from '../../app/createGameScene'
import type { GameStore } from '../../app/gameStore'
import type { GameScene, ViewportSize } from './contracts'
import { InputController } from '../input/InputController'

const MAX_PIXEL_RATIO = 2

export class GameRuntime {
  private readonly app = new Application()
  private readonly store: GameStore
  private scene: GameScene | null = null
  private input: InputController | null = null
  private viewport: ViewportSize = { width: 0, height: 0 }
  private elapsedSeconds = 0
  private unsubscribe: (() => void) | null = null

  constructor(store: GameStore) {
    this.store = store
  }

  async mount(host: HTMLElement): Promise<void> {
    await this.app.init({
      resizeTo: host,
      antialias: true,
      autoDensity: true,
      backgroundAlpha: 0,
      resolution: Math.min(window.devicePixelRatio || 1, MAX_PIXEL_RATIO),
      preference: 'webgl',
      powerPreference: 'high-performance',
    })

    host.appendChild(this.app.canvas)
    this.app.canvas.classList.add('game-canvas')

    this.input = new InputController(host)
    this.swapScene(this.store.getState().mode)
    this.unsubscribe = this.store.subscribe((state, previousState) => {
      if (state.mode !== previousState.mode) {
        this.swapScene(state.mode)
      }
    })

    this.syncViewport()
    this.app.ticker.add(this.handleTick)
  }

  destroy(): void {
    this.app.ticker.remove(this.handleTick)
    this.unsubscribe?.()
    this.unsubscribe = null
    this.input?.destroy()
    this.input = null

    if (this.scene) {
      this.app.stage.removeChild(this.scene.container)
      this.scene.destroy()
      this.scene = null
    }

    this.app.destroy({ removeView: true }, false)
  }

  private swapScene(mode: 'base' | 'combat'): void {
    if (this.scene) {
      this.app.stage.removeChild(this.scene.container)
      this.scene.destroy()
      this.scene = null
    }

    this.scene = createGameScene(mode, this.store)
    this.app.stage.addChild(this.scene.container)
    this.scene.resize(this.viewport)
  }

  private readonly handleTick = (ticker: Ticker): void => {
    const deltaSeconds = ticker.deltaMS / 1000

    this.elapsedSeconds += deltaSeconds
    this.syncViewport()
    this.scene?.update(deltaSeconds, this.elapsedSeconds, this.input?.getSnapshot() ?? emptyInput)
  }

  private syncViewport(): void {
    const nextViewport = {
      width: this.app.screen.width,
      height: this.app.screen.height,
    }

    if (nextViewport.width === this.viewport.width && nextViewport.height === this.viewport.height) {
      return
    }

    this.viewport = nextViewport
    this.scene?.resize(nextViewport)
  }
}

const emptyInput = {
  moveX: 0,
  moveY: 0,
  pointerX: 0,
  pointerY: 0,
  hasPointer: false,
  pointerPressed: false,
  pointerReleased: false,
  shootHeld: false,
  dashPressed: false,
  interactPressed: false,
  rotatePressed: false,
  sortPressed: false,
  panelTogglePressed: false,
  mapTogglePressed: false,
  weaponSwitch: null,
  quickSlotBind: null,
  quickSlotUse: null,
}
