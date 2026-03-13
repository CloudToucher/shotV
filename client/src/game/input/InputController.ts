import type { InputSnapshot } from '../core/contracts'

const MOVEMENT_KEYS = new Set(['KeyW', 'KeyA', 'KeyS', 'KeyD', 'ArrowUp', 'ArrowLeft', 'ArrowDown', 'ArrowRight', 'Space'])

export class InputController {
  private readonly host: HTMLElement
  private readonly pressedKeys = new Set<string>()
  private readonly pointer = {
    x: 0,
    y: 0,
    hasPointer: false,
    shootHeld: false,
  }

  private dashPressed = false
  private restartPressed = false
  private weaponSwitch: 1 | 2 | 3 | null = null

  constructor(host: HTMLElement) {
    this.host = host
    window.addEventListener('keydown', this.handleKeyDown)
    window.addEventListener('keyup', this.handleKeyUp)
    this.host.addEventListener('pointermove', this.handlePointerMove)
    this.host.addEventListener('pointerdown', this.handlePointerDown)
    window.addEventListener('pointerup', this.handlePointerUp)
    this.host.addEventListener('pointerleave', this.handlePointerLeave)
    this.host.addEventListener('contextmenu', this.preventContextMenu)
  }

  getSnapshot(): InputSnapshot {
    const moveX = axis(this.isPressed('KeyD', 'ArrowRight'), this.isPressed('KeyA', 'ArrowLeft'))
    const moveY = axis(this.isPressed('KeyS', 'ArrowDown'), this.isPressed('KeyW', 'ArrowUp'))
    const snapshot: InputSnapshot = {
      moveX,
      moveY,
      pointerX: this.pointer.x,
      pointerY: this.pointer.y,
      hasPointer: this.pointer.hasPointer,
      shootHeld: this.pointer.shootHeld,
      dashPressed: this.dashPressed,
      restartPressed: this.restartPressed,
      weaponSwitch: this.weaponSwitch,
    }

    this.dashPressed = false
    this.restartPressed = false
    this.weaponSwitch = null

    return snapshot
  }

  destroy(): void {
    window.removeEventListener('keydown', this.handleKeyDown)
    window.removeEventListener('keyup', this.handleKeyUp)
    this.host.removeEventListener('pointermove', this.handlePointerMove)
    this.host.removeEventListener('pointerdown', this.handlePointerDown)
    window.removeEventListener('pointerup', this.handlePointerUp)
    this.host.removeEventListener('pointerleave', this.handlePointerLeave)
    this.host.removeEventListener('contextmenu', this.preventContextMenu)
  }

  private readonly handleKeyDown = (event: KeyboardEvent): void => {
    if (MOVEMENT_KEYS.has(event.code)) {
      event.preventDefault()
    }

    if (event.code === 'Space' && !event.repeat && !this.pressedKeys.has('Space')) {
      this.dashPressed = true
    }

    if (!event.repeat) {
      if (event.code === 'Digit1') {
        this.weaponSwitch = 1
      } else if (event.code === 'Digit2') {
        this.weaponSwitch = 2
      } else if (event.code === 'Digit3') {
        this.weaponSwitch = 3
      } else if (event.code === 'KeyR') {
        this.restartPressed = true
      }
    }

    this.pressedKeys.add(event.code)
  }

  private readonly handleKeyUp = (event: KeyboardEvent): void => {
    this.pressedKeys.delete(event.code)
  }

  private readonly handlePointerMove = (event: PointerEvent): void => {
    const rect = this.host.getBoundingClientRect()

    this.pointer.x = event.clientX - rect.left
    this.pointer.y = event.clientY - rect.top
    this.pointer.hasPointer = this.pointer.x >= 0 && this.pointer.x <= rect.width && this.pointer.y >= 0 && this.pointer.y <= rect.height
  }

  private readonly handlePointerDown = (event: PointerEvent): void => {
    if (event.button !== 0) {
      return
    }

    this.handlePointerMove(event)
    this.pointer.shootHeld = true
  }

  private readonly handlePointerUp = (event: PointerEvent): void => {
    if (event.button === 0) {
      this.pointer.shootHeld = false
    }
  }

  private readonly handlePointerLeave = (): void => {
    this.pointer.hasPointer = false
    this.pointer.shootHeld = false
  }

  private readonly preventContextMenu = (event: MouseEvent): void => {
    event.preventDefault()
  }

  private isPressed(primary: string, secondary: string): boolean {
    return this.pressedKeys.has(primary) || this.pressedKeys.has(secondary)
  }
}

function axis(positive: boolean, negative: boolean): number {
  if (positive === negative) {
    return 0
  }

  return positive ? 1 : -1
}
