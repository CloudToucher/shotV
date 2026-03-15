export interface BaseResources {
  salvage: number
  alloy: number
  research: number
}

export interface BaseState {
  facilityLevel: number
  deploymentCount: number
  resources: BaseResources
  unlockedStations: string[]
}

export function createInitialBaseState(): BaseState {
  return {
    facilityLevel: 1,
    deploymentCount: 0,
    resources: {
      salvage: 120,
      alloy: 24,
      research: 0,
    },
    unlockedStations: ['command', 'workshop'],
  }
}
