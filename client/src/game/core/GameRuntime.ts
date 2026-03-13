import { Application, type Ticker } from 'pixi.js'

import { CombatSandboxScene } from '../scenes/CombatSandboxScene'
import type { GameScene, ViewportSize } from './contracts'
import { InputController } from '../input/InputController'

const MAX_PIXEL_RATIO = 2

export class GameRuntime {
  private readonly app = new Application()
  private scene: GameScene | null = null
  private input: InputController | null = null
  private viewport: ViewportSize = { width: 0, height: 0 }
  private elapsedSeconds = 0

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
    this.scene = new CombatSandboxScene()
    this.app.stage.addChild(this.scene.container)

    this.syncViewport()
    this.app.ticker.add(this.handleTick)
  }

  destroy(): void {
    this.app.ticker.remove(this.handleTick)
    this.input?.destroy()
    this.input = null

    if (this.scene) {
      this.app.stage.removeChild(this.scene.container)
      this.scene.destroy()
      this.scene = null
    }

    this.app.destroy({ removeView: true }, false)
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
  shootHeld: false,
  dashPressed: false,
  restartPressed: false,
  weaponSwitch: null,
}
