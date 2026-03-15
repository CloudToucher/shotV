import type { InputSnapshot } from '../core/contracts'

const MOVEMENT_KEYS = new Set(['KeyW', 'KeyA', 'KeyS', 'KeyD', 'ArrowUp', 'ArrowLeft', 'ArrowDown', 'ArrowRight', 'Space'])
const PANEL_KEYS = new Set(['Tab', 'KeyI'])
const MAP_KEYS = new Set(['KeyM'])
const INTERACT_KEYS = new Set(['KeyE'])
const DIGIT_KEYS = new Map<string, 1 | 2 | 3 | 4>([
  ['Digit1', 1],
  ['Digit2', 2],
  ['Digit3', 3],
  ['Digit4', 4],
])
const QUICK_SLOT_USE_KEYS = new Map<string, 1 | 2 | 3 | 4>([
  ['KeyZ', 1],
  ['KeyX', 2],
  ['KeyC', 3],
  ['KeyV', 4],
])

export class InputController {
  private readonly host: HTMLElement
  private readonly pressedKeys = new Set<string>()
  private readonly pointer = {
    x: 0,
    y: 0,
    hasPointer: false,
    pressed: false,
    released: false,
    shootHeld: false,
  }

  private dashPressed = false
  private interactPressed = false
  private rotatePressed = false
  private sortPressed = false
  private panelTogglePressed = false
  private mapTogglePressed = false
  private weaponSwitch: 1 | 2 | 3 | null = null
  private quickSlotBind: 1 | 2 | 3 | 4 | null = null
  private quickSlotUse: 1 | 2 | 3 | 4 | null = null

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
      pointerPressed: this.pointer.pressed,
      pointerReleased: this.pointer.released,
      shootHeld: this.pointer.shootHeld,
      dashPressed: this.dashPressed,
      interactPressed: this.interactPressed,
      rotatePressed: this.rotatePressed,
      sortPressed: this.sortPressed,
      panelTogglePressed: this.panelTogglePressed,
      mapTogglePressed: this.mapTogglePressed,
      weaponSwitch: this.weaponSwitch,
      quickSlotBind: this.quickSlotBind,
      quickSlotUse: this.quickSlotUse,
    }

    this.pointer.pressed = false
    this.pointer.released = false
    this.dashPressed = false
    this.interactPressed = false
    this.rotatePressed = false
    this.sortPressed = false
    this.panelTogglePressed = false
    this.mapTogglePressed = false
    this.weaponSwitch = null
    this.quickSlotBind = null
    this.quickSlotUse = null

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

    if (PANEL_KEYS.has(event.code)) {
      event.preventDefault()
    }

    if (MAP_KEYS.has(event.code)) {
      event.preventDefault()
    }

    if (INTERACT_KEYS.has(event.code)) {
      event.preventDefault()
    }

    if (DIGIT_KEYS.has(event.code)) {
      event.preventDefault()
    }

    if (QUICK_SLOT_USE_KEYS.has(event.code)) {
      event.preventDefault()
    }

    if (event.code === 'Space' && !event.repeat && !this.pressedKeys.has('Space')) {
      this.dashPressed = true
    }

    if (!event.repeat) {
      const digit = DIGIT_KEYS.get(event.code)
      const quickUse = QUICK_SLOT_USE_KEYS.get(event.code)

      if (digit) {
        this.quickSlotBind = digit
        if (digit === 1 || digit === 2 || digit === 3) {
          this.weaponSwitch = digit
        }
      } else if (quickUse) {
        this.quickSlotUse = quickUse
      } else if (event.code === 'KeyR') {
        this.rotatePressed = true
      } else if (event.code === 'KeyF') {
        this.sortPressed = true
      } else if (INTERACT_KEYS.has(event.code)) {
        this.interactPressed = true
      } else if (PANEL_KEYS.has(event.code)) {
        this.panelTogglePressed = true
      } else if (MAP_KEYS.has(event.code)) {
        this.mapTogglePressed = true
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
    this.pointer.pressed = true
    this.pointer.released = false
    this.pointer.shootHeld = true
  }

  private readonly handlePointerUp = (event: PointerEvent): void => {
    if (event.button === 0) {
      const rect = this.host.getBoundingClientRect()
      this.pointer.x = event.clientX - rect.left
      this.pointer.y = event.clientY - rect.top
      this.pointer.hasPointer = this.pointer.x >= 0 && this.pointer.x <= rect.width && this.pointer.y >= 0 && this.pointer.y <= rect.height
      this.pointer.released = true
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
