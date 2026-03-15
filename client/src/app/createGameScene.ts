import type { GameScene } from '../game/core/contracts'
import { BaseCampScene } from '../game/scenes/BaseCampScene'
import { CombatSandboxScene } from '../game/scenes/CombatSandboxScene'
import type { GameMode } from './gameModes'
import type { GameStore } from './gameStore'

export function createGameScene(mode: GameMode, store: GameStore): GameScene {
  return mode === 'combat' ? new CombatSandboxScene(store) : new BaseCampScene(store)
}
