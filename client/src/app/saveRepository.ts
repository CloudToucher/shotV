import { createInitialSaveState, hydrateSaveState } from './saveState'
import type { SaveState } from './types'

const STORAGE_KEY = 'shotv.save'

export class LocalSaveRepository {
  load(): SaveState {
    if (typeof window === 'undefined') {
      return createInitialSaveState()
    }

    const raw = window.localStorage.getItem(STORAGE_KEY)

    if (!raw) {
      return createInitialSaveState()
    }

    try {
      return hydrateSaveState(JSON.parse(raw))
    } catch {
      return createInitialSaveState()
    }
  }

  save(saveState: SaveState): void {
    if (typeof window === 'undefined') {
      return
    }

    window.localStorage.setItem(STORAGE_KEY, JSON.stringify(saveState))
  }

  clear(): void {
    if (typeof window === 'undefined') {
      return
    }

    window.localStorage.removeItem(STORAGE_KEY)
  }
}
