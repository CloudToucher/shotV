import type { RunMapState, RunZoneState } from '../session/types'
import { buildLayoutSeedFromText } from './layout'
import type { WorldRouteDefinition } from './types'

const emptyBossState: RunMapState['boss'] = {
  spawned: false,
  defeated: false,
  label: null,
  phase: null,
  health: null,
  maxHealth: null,
}

export const worldRoutes: WorldRouteDefinition[] = [
  {
    id: 'combat-sandbox-route',
    label: '前线穿廊',
    summary: '由外环码头切入，穿过中继巢区，最后从货运升降机撤离。',
    zones: [
      {
        id: 'perimeter-dock',
        label: '外围码头',
        kind: 'perimeter',
        description: '入口压力较低，适合确认手感和路线。',
        threatLevel: 1,
        rewardMultiplier: 1,
        allowsExtraction: true,
      },
      {
        id: 'relay-nest',
        label: '中继巢区',
        kind: 'high-risk',
        description: '敌群密度明显抬升，但回收效率也更高。',
        threatLevel: 2,
        rewardMultiplier: 1.2,
        allowsExtraction: false,
      },
      {
        id: 'vault-approach',
        label: '金库前厅',
        kind: 'high-value',
        description: '高价值区入口，敌人更耐打，掉落更偏向合金与研究样本。',
        threatLevel: 3,
        rewardMultiplier: 1.45,
        allowsExtraction: false,
      },
      {
        id: 'freight-lift',
        label: '货运升降机',
        kind: 'extraction',
        description: '终端撤离出口，完成压制后即可带走整局战利品。',
        threatLevel: 2,
        rewardMultiplier: 1.15,
        allowsExtraction: true,
      },
    ],
  },
  {
    id: 'foundry-loop-route',
    label: '熔炉环线',
    summary: '线路更短、节奏更快，适合高频刷取基础资源。',
    zones: [
      {
        id: 'slag-yard',
        label: '废渣场',
        kind: 'perimeter',
        description: '短线入口区，适合试火后快速撤离。',
        threatLevel: 1,
        rewardMultiplier: 1.05,
        allowsExtraction: true,
      },
      {
        id: 'smelter-core',
        label: '熔炉核心',
        kind: 'high-risk',
        description: '高压中段区域，合金产出更稳定。',
        threatLevel: 3,
        rewardMultiplier: 1.35,
        allowsExtraction: false,
      },
      {
        id: 'rail-elevator',
        label: '轨道电梯',
        kind: 'extraction',
        description: '路线终点，清空后可直接结束本轮行动。',
        threatLevel: 2,
        rewardMultiplier: 1.1,
        allowsExtraction: true,
      },
    ],
  },
  {
    id: 'frost-wharf-route',
    label: '霜港折返',
    summary: '寒区港口副本，占位内容为主，后续会接入低能见度和环境危害。',
    zones: [
      {
        id: 'ice-dock',
        label: '冰封泊位',
        kind: 'perimeter',
        description: '风压低、敌情轻，适合作为副本壳子占位。',
        threatLevel: 1,
        rewardMultiplier: 1,
        allowsExtraction: true,
      },
      {
        id: 'cold-storage',
        label: '冷库连廊',
        kind: 'high-risk',
        description: '占位区域，预留给环境交互和冻结机制。',
        threatLevel: 2,
        rewardMultiplier: 1.18,
        allowsExtraction: false,
      },
      {
        id: 'breaker-gate',
        label: '破冰闸门',
        kind: 'extraction',
        description: '临时出口，后续会替换为完整副本终点事件。',
        threatLevel: 2,
        rewardMultiplier: 1.08,
        allowsExtraction: true,
      },
    ],
  },
  {
    id: 'archive-drop-route',
    label: '资料库坠层',
    summary: '档案设施副本，目前是结构空壳，后续用于高价值情报线。',
    zones: [
      {
        id: 'surface-stack',
        label: '表层书库',
        kind: 'perimeter',
        description: '安静但视野复杂，适合作为探索模板。',
        threatLevel: 1,
        rewardMultiplier: 1.04,
        allowsExtraction: true,
      },
      {
        id: 'index-shaft',
        label: '索引井道',
        kind: 'high-value',
        description: '占位区，后续会塞入密码门和资料采集交互。',
        threatLevel: 2,
        rewardMultiplier: 1.22,
        allowsExtraction: false,
      },
      {
        id: 'sealed-vault',
        label: '封存库厅',
        kind: 'extraction',
        description: '终点出口，占位版本仅保留基础推进流程。',
        threatLevel: 3,
        rewardMultiplier: 1.18,
        allowsExtraction: true,
      },
    ],
  },
  {
    id: 'blackwell-route',
    label: '黑井穿梭',
    summary: '竖井运输副本，现阶段提供路线选择壳子，后续接入垂直区域和平台交互。',
    zones: [
      {
        id: 'shaft-mouth',
        label: '井口平台',
        kind: 'perimeter',
        description: '进场平台，保留快速撤离口。',
        threatLevel: 1,
        rewardMultiplier: 1.02,
        allowsExtraction: true,
      },
      {
        id: 'maintenance-ring',
        label: '维护环廊',
        kind: 'high-risk',
        description: '占位中段，用于后续平台切换和环形战区。',
        threatLevel: 2,
        rewardMultiplier: 1.2,
        allowsExtraction: false,
      },
      {
        id: 'deep-anchor',
        label: '深层锚点',
        kind: 'extraction',
        description: '深层出口，当前只保留地图与结算骨架。',
        threatLevel: 3,
        rewardMultiplier: 1.16,
        allowsExtraction: true,
      },
    ],
  },
]

export const worldRouteById = Object.fromEntries(worldRoutes.map((route) => [route.id, route])) as Record<string, WorldRouteDefinition>

export function getWorldRoute(routeId: string): WorldRouteDefinition {
  return worldRouteById[routeId] ?? worldRoutes[0]
}

export function getNextWorldRouteId(currentRouteId: string): string {
  const currentIndex = worldRoutes.findIndex((route) => route.id === currentRouteId)

  if (currentIndex === -1) {
    return worldRoutes[0].id
  }

  return worldRoutes[(currentIndex + 1) % worldRoutes.length].id
}

export function createRunMapStateForRoute(routeId: string): RunMapState {
  const route = getWorldRoute(routeId)

  return {
    sceneId: 'combat-sandbox',
    routeId: route.id,
    currentZoneId: route.zones[0].id,
    layoutSeed: createLayoutSeed(route.id, route.zones[0].id),
    zones: route.zones.map((zone, index) => ({
      id: zone.id,
      label: zone.label,
      kind: zone.kind,
      status: index === 0 ? 'active' : 'locked',
      threatLevel: zone.threatLevel,
      rewardMultiplier: zone.rewardMultiplier,
      allowsExtraction: zone.allowsExtraction,
      description: zone.description,
    })),
    currentWave: 0,
    highestWave: 0,
    hostilesRemaining: 0,
    boss: {
      ...emptyBossState,
    },
  }
}

export function getCurrentRunZone(map: Pick<RunMapState, 'currentZoneId' | 'zones'>): RunZoneState | null {
  return map.zones.find((zone) => zone.id === map.currentZoneId) ?? null
}

export function getNextRunZone(map: Pick<RunMapState, 'currentZoneId' | 'zones'>): RunZoneState | null {
  const currentIndex = map.zones.findIndex((zone) => zone.id === map.currentZoneId)

  if (currentIndex === -1 || currentIndex + 1 >= map.zones.length) {
    return null
  }

  return map.zones[currentIndex + 1]
}

export function isCurrentRunZoneCleared(map: Pick<RunMapState, 'currentZoneId' | 'zones'>): boolean {
  return getCurrentRunZone(map)?.status === 'cleared'
}

export function canExtractFromRunMap(map: Pick<RunMapState, 'currentZoneId' | 'zones'>): boolean {
  return Boolean(getCurrentRunZone(map)?.allowsExtraction)
}

export function isRunRouteComplete(map: Pick<RunMapState, 'currentZoneId' | 'zones'>): boolean {
  const current = getCurrentRunZone(map)

  return Boolean(current && current.status === 'cleared' && !getNextRunZone(map))
}

export function markCurrentRunZoneCleared(map: RunMapState): RunMapState {
  return {
    ...map,
    zones: map.zones.map((zone) => (zone.id === map.currentZoneId ? { ...zone, status: 'cleared' } : zone)),
    hostilesRemaining: 0,
    boss: {
      ...map.boss,
      defeated: true,
      health: 0,
    },
  }
}

export function advanceRunMapZone(map: RunMapState): RunMapState | null {
  const current = getCurrentRunZone(map)
  const next = getNextRunZone(map)

  if (!current || current.status !== 'cleared' || !next) {
    return null
  }

  return {
    ...map,
    currentZoneId: next.id,
    layoutSeed: createLayoutSeed(map.routeId, next.id),
    zones: map.zones.map((zone) => {
      if (zone.id === current.id) {
        return { ...zone, status: 'cleared' }
      }

      if (zone.id === next.id) {
        return { ...zone, status: 'active' }
      }

      return zone
    }),
    currentWave: 0,
    highestWave: 0,
    hostilesRemaining: 0,
    boss: {
      ...emptyBossState,
    },
  }
}

function createLayoutSeed(routeId: string, zoneId: string): number {
  return buildLayoutSeedFromText(`${routeId}:${zoneId}:${Date.now()}:${Math.random()}`)
}
