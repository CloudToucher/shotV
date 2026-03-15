<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'

import { gameStore } from '../app/gameStore'
import { GameRuntime } from '../game/core/GameRuntime'
import { canExtractFromRunMap, getCurrentRunZone, getNextRunZone, getWorldRoute, isCurrentRunZoneCleared } from '../game/world/routes'

const host = ref<HTMLElement | null>(null)
const errorMessage = ref('')
const state = ref(gameStore.getState())

let runtime: GameRuntime | null = null
let unsubscribe: (() => void) | null = null

onMounted(async () => {
  if (!host.value) {
    return
  }

  unsubscribe = gameStore.subscribe((nextState) => {
    state.value = nextState
  })

  runtime = new GameRuntime(gameStore)

  try {
    await runtime.mount(host.value)
  } catch (error) {
    console.error(error)
    errorMessage.value = '渲染初始化失败，请查看控制台。'
  }
})

onBeforeUnmount(() => {
  unsubscribe?.()
  unsubscribe = null
  runtime?.destroy()
  runtime = null
})

const activeRun = computed(() => state.value.save.session.activeRun)
const selectedRoute = computed(() => getWorldRoute(state.value.save.world.selectedRouteId))
const currentZone = computed(() => (activeRun.value ? getCurrentRunZone(activeRun.value.map) : null))
const nextZone = computed(() => (activeRun.value ? getNextRunZone(activeRun.value.map) : null))
const canAdvance = computed(() => Boolean(activeRun.value && activeRun.value.status === 'active' && isCurrentRunZoneCleared(activeRun.value.map) && nextZone.value))
const canExtract = computed(() => Boolean(activeRun.value && activeRun.value.status === 'active' && canExtractFromRunMap(activeRun.value.map)))
const sceneHint = computed(() => state.value.runtime.primaryActionHint || (state.value.mode === 'base' ? '前往出击闸门后再部署。' : '前往出口后执行推进或撤离。'))
const showCombatDock = computed(
  () =>
    state.value.mode === 'combat' &&
    !state.value.runtime.mapOverlayOpen &&
    Boolean(activeRun.value && (activeRun.value.status === 'awaiting-settlement' || state.value.runtime.primaryActionReady)),
)

const headline = computed(() => {
  if (state.value.mode === 'base') {
    return '基地待命'
  }
  if (activeRun.value?.status === 'awaiting-settlement') {
    return '等待结算'
  }
  return currentZone.value ? `${getWorldRoute(activeRun.value!.map.routeId).label} / ${currentZone.value.label}` : '战区行动'
})

const subline = computed(() => {
  if (state.value.mode === 'base') {
    return `${selectedRoute.value.label} · ${selectedRoute.value.summary}`
  }
  if (!activeRun.value) {
    return '正在接入行动数据'
  }
  if (activeRun.value.status === 'awaiting-settlement') {
    return `结果：${formatOutcome(activeRun.value.pendingOutcome ?? 'down')}`
  }
  return nextZone.value ? `下一块区域：${nextZone.value.label}` : '当前区域已是路线终点'
})

const primaryActionLabel = computed(() => {
  if (state.value.mode === 'base') {
    return '部署行动'
  }
  if (activeRun.value?.status === 'awaiting-settlement') {
    return '结算返回'
  }
  if (canAdvance.value) {
    return '推进下一区域'
  }
  return '执行撤离'
})

const primaryActionDisabled = computed(() => {
  if (state.value.mode === 'base') {
    return !state.value.runtime.primaryActionReady
  }
  if (!activeRun.value) {
    return true
  }
  if (activeRun.value.status === 'awaiting-settlement') {
    return false
  }
  return (!canAdvance.value && !canExtract.value) || !state.value.runtime.primaryActionReady
})

function runPrimaryAction(): void {
  if (state.value.mode === 'base') {
    gameStore.deployCombat()
    return
  }
  if (!activeRun.value) {
    return
  }
  if (activeRun.value.status === 'awaiting-settlement') {
    gameStore.resolveActiveRunToBase()
    return
  }
  if (canAdvance.value) {
    gameStore.advanceActiveRunZone()
    return
  }
  if (canExtract.value) {
    gameStore.resolveActiveRunToBase('extracted')
  }
}

function extractNow(): void {
  if (activeRun.value && activeRun.value.status === 'active' && canExtract.value) {
    gameStore.resolveActiveRunToBase('extracted')
  }
}

function formatOutcome(outcome: 'extracted' | 'boss-clear' | 'down'): string {
  if (outcome === 'boss-clear') return '路线肃清'
  if (outcome === 'down') return '行动失败'
  return '成功撤离'
}
</script>

<template>
  <div class="viewport-shell">
    <div ref="host" class="viewport-host"></div>
    <aside v-if="showCombatDock" class="combat-dock">
      <div class="dock-kicker">TACTICAL LINK</div>
      <div class="dock-title">{{ headline }}</div>
      <div class="dock-copy">{{ subline }}</div>
      <div class="dock-copy dock-copy--accent">{{ sceneHint }}</div>
      <div class="dock-actions dock-actions--compact">
        <button class="dock-button dock-button--primary" :disabled="primaryActionDisabled" @click="runPrimaryAction">{{ primaryActionLabel }}</button>
        <button
          v-if="activeRun?.status === 'active' && canAdvance && canExtract && state.runtime.primaryActionReady"
          class="dock-button"
          @click="extractNow"
        >
          立即撤离
        </button>
      </div>
    </aside>
    <div v-if="errorMessage" class="viewport-error">{{ errorMessage }}</div>
  </div>
</template>

<style scoped>
.viewport-shell {
  position: relative;
  width: 100%;
  height: 100vh;
  min-height: 100vh;
  overflow: hidden;
  font-family: 'Bahnschrift', 'Microsoft YaHei UI', 'PingFang SC', 'Noto Sans SC', sans-serif;
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.36), rgba(234, 244, 249, 0.88)),
    radial-gradient(circle at top right, rgba(77, 185, 230, 0.18), transparent 28%),
    radial-gradient(circle at left bottom, rgba(255, 157, 77, 0.12), transparent 24%);
}

.viewport-host {
  width: 100%;
  height: 100vh;
  min-height: 100vh;
}

.viewport-host :deep(canvas) {
  display: block;
  width: 100%;
  height: 100%;
}

.combat-dock {
  --cut: 18px;
  position: absolute;
  top: 128px;
  right: 18px;
  width: min(312px, calc(100vw - 36px));
  display: grid;
  gap: 12px;
  padding: 18px 18px 16px;
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.97), rgba(239, 248, 252, 0.94)),
    rgba(247, 252, 255, 0.88);
  border: 1px solid rgba(77, 185, 230, 0.2);
  box-shadow:
    0 20px 44px rgba(120, 154, 169, 0.18),
    inset 0 1px 0 rgba(255, 255, 255, 0.8);
  backdrop-filter: blur(18px);
  clip-path: polygon(var(--cut) 0, 100% 0, 100% calc(100% - var(--cut)), calc(100% - var(--cut)) 100%, 0 100%, 0 var(--cut));
  animation: dock-enter 220ms cubic-bezier(0.18, 0.82, 0.3, 1) both;
}

.combat-dock::before {
  content: '';
  position: absolute;
  inset: 0;
  pointer-events: none;
  background:
    linear-gradient(135deg, rgba(77, 185, 230, 0.1), transparent 26%),
    linear-gradient(180deg, rgba(255, 255, 255, 0.34), transparent 58%);
}

.combat-dock::after {
  content: '';
  position: absolute;
  top: 16px;
  left: 18px;
  width: 40px;
  height: 3px;
  background: rgba(255, 157, 77, 0.88);
}

.dock-kicker {
  position: relative;
  font-size: 11px;
  letter-spacing: 0.18em;
  text-transform: uppercase;
  color: rgba(170, 103, 43, 0.9);
  padding-top: 4px;
}

.dock-title {
  position: relative;
  font-size: 22px;
  font-weight: 700;
  letter-spacing: 0.02em;
  color: rgba(20, 50, 69, 0.98);
}

.dock-copy {
  position: relative;
  font-size: 13px;
  line-height: 1.58;
  color: rgba(73, 100, 117, 0.92);
}

.dock-copy--accent {
  color: rgba(40, 132, 95, 0.96);
}

.dock-actions {
  position: relative;
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 4px;
}

.dock-actions--compact {
  margin-top: 0;
}

.dock-button {
  --cut: 12px;
  border: 1px solid rgba(77, 185, 230, 0.2);
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.96), rgba(238, 246, 250, 0.92)),
    rgba(247, 252, 255, 0.95);
  color: rgba(20, 50, 69, 0.96);
  padding: 10px 16px;
  font-size: 13px;
  font-weight: 700;
  letter-spacing: 0.02em;
  cursor: pointer;
  clip-path: polygon(var(--cut) 0, 100% 0, 100% calc(100% - var(--cut)), calc(100% - var(--cut)) 100%, 0 100%, 0 var(--cut));
  transition:
    transform 140ms ease,
    border-color 140ms ease,
    background 140ms ease;
}

.dock-button:hover:not(:disabled) {
  transform: translateY(-1px);
  border-color: rgba(255, 157, 77, 0.28);
}

.dock-button:disabled {
  opacity: 0.45;
  cursor: default;
}

.dock-button--primary {
  background:
    linear-gradient(180deg, rgba(77, 185, 230, 0.24), rgba(77, 185, 230, 0.1)),
    rgba(226, 244, 251, 0.98);
}

.viewport-error {
  position: absolute;
  inset: 24px;
  display: grid;
  place-items: center;
  color: rgba(154, 71, 45, 0.96);
  font-size: 14px;
}

@keyframes dock-enter {
  from {
    opacity: 0;
    transform: translate3d(14px, -8px, 0);
  }

  to {
    opacity: 1;
    transform: translate3d(0, 0, 0);
  }
}

@media (max-width: 720px) {
  .combat-dock {
    left: 16px;
    right: 16px;
    width: auto;
  }
}
</style>
