<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'

import { buildRunSettlementPreview, gameStore } from '../app/gameStore'
import type { ResourceBundle } from '../game/data/types'
import { GameRuntime } from '../game/core/GameRuntime'
import type { LootEntry, RunResolutionOutcome } from '../game/session/types'
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
const showSettlementModal = computed(() => state.value.mode === 'combat' && activeRun.value?.status === 'awaiting-settlement')
const showCombatDock = computed(
  () =>
    state.value.mode === 'combat' &&
    !showSettlementModal.value &&
    !state.value.runtime.mapOverlayOpen &&
    Boolean(activeRun.value && state.value.runtime.primaryActionReady),
)
const settlementPreview = computed(() => {
  if (!activeRun.value || activeRun.value.status !== 'awaiting-settlement') {
    return null
  }

  return buildRunSettlementPreview(state.value.save.inventory, activeRun.value, activeRun.value.pendingOutcome ?? 'down')
})
const settlementTitle = computed(() => {
  const outcome = settlementPreview.value?.outcome ?? 'down'
  return outcome === 'down' ? '行动中止 / 结算确认' : '行动结算 / 返回确认'
})
const settlementSummary = computed(() => settlementPreview.value?.summaryLabel ?? '')
const settlementRouteLine = computed(() => {
  if (!activeRun.value) {
    return ''
  }

  const route = getWorldRoute(activeRun.value.map.routeId)
  return currentZone.value ? `${route.label} / ${currentZone.value.label}` : route.label
})
const settlementRecoveredResources = computed(() => formatResourceBundle(settlementPreview.value?.resourcesRecovered ?? null, '无资源回收'))
const settlementLostResources = computed(() => formatResourceBundle(settlementPreview.value?.resourcesLost ?? null, '无资源损失'))
const settlementRecoveredLoot = computed(() => formatLootEntries(settlementPreview.value?.lootRecovered ?? [], '无回收物资'))
const settlementLostLoot = computed(() => formatLootEntries(settlementPreview.value?.lootLost ?? [], '无遗失物资'))

const headline = computed(() => {
  if (state.value.mode === 'base') {
    return '基地待命'
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
    gameStore.markRunOutcome('extracted')
  }
}

function extractNow(): void {
  if (activeRun.value && activeRun.value.status === 'active' && canExtract.value) {
    gameStore.markRunOutcome('extracted')
  }
}

function formatOutcome(outcome: RunResolutionOutcome): string {
  if (outcome === 'boss-clear') return '路线肃清'
  if (outcome === 'down') return '行动失败'
  return '成功撤离'
}

function formatDuration(totalSeconds: number): string {
  const seconds = Math.max(0, Math.round(totalSeconds))
  const minutes = Math.floor(seconds / 60)
  const restSeconds = seconds % 60

  if (minutes <= 0) {
    return `${restSeconds} 秒`
  }

  return `${minutes} 分 ${String(restSeconds).padStart(2, '0')} 秒`
}

function formatResourceBundle(bundle: ResourceBundle | null, emptyLabel: string): string {
  if (!bundle) {
    return emptyLabel
  }

  const total = bundle.salvage + bundle.alloy + bundle.research
  if (total <= 0) {
    return emptyLabel
  }

  return `废料 ${bundle.salvage} / 合金 ${bundle.alloy} / 研究 ${bundle.research}`
}

function formatLootEntries(entries: LootEntry[], emptyLabel: string): string {
  if (entries.length === 0) {
    return emptyLabel
  }

  const labels = entries.slice(0, 4).map((entry) => entry.label)
  return entries.length > 4 ? `${labels.join(' / ')} 等 ${entries.length} 项` : labels.join(' / ')
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
    <div v-if="showSettlementModal && settlementPreview" class="settlement-layer">
      <section class="settlement-modal">
        <div class="settlement-kicker">SETTLEMENT LINK</div>
        <div class="settlement-title">{{ settlementTitle }}</div>
        <div class="settlement-copy">{{ settlementRouteLine }}</div>
        <div class="settlement-copy settlement-copy--accent">{{ settlementSummary }}</div>
        <div class="settlement-grid">
          <article class="settlement-card">
            <div class="settlement-label">行动结果</div>
            <div class="settlement-value">{{ formatOutcome(settlementPreview.outcome) }}</div>
            <div class="settlement-meta">用时 {{ formatDuration(settlementPreview.durationSeconds) }}</div>
          </article>
          <article class="settlement-card">
            <div class="settlement-label">战斗数据</div>
            <div class="settlement-value">击杀 {{ settlementPreview.kills }}</div>
            <div class="settlement-meta">最高波次 {{ settlementPreview.highestWave }}</div>
          </article>
          <article class="settlement-card">
            <div class="settlement-label">资源回收</div>
            <div class="settlement-value settlement-value--compact">{{ settlementRecoveredResources }}</div>
            <div class="settlement-meta">损失：{{ settlementLostResources }}</div>
          </article>
          <article class="settlement-card">
            <div class="settlement-label">物资结算</div>
            <div class="settlement-value settlement-value--compact">{{ settlementRecoveredLoot }}</div>
            <div class="settlement-meta">遗失：{{ settlementLostLoot }}</div>
          </article>
        </div>
        <div class="settlement-footnote">确认后返回基地，仓储无法容纳的携行物资会计入遗失。</div>
        <div class="settlement-actions">
          <button class="dock-button dock-button--primary settlement-button" @click="runPrimaryAction">确认结算并返回基地</button>
        </div>
      </section>
    </div>
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
  font-family: 'Consolas', 'Courier New', 'PingFang SC', 'Microsoft YaHei UI', monospace;
  background: linear-gradient(180deg, #f4f9fc 0%, #e8f1f5 100%);
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
  --cut: 12px;
  position: absolute;
  top: 128px;
  right: 18px;
  width: min(312px, calc(100vw - 36px));
  display: grid;
  gap: 12px;
  padding: 18px 18px 16px;
  background: rgba(255, 255, 255, 0.95);
  border: 1px solid rgba(77, 185, 230, 0.4);
  box-shadow: 0 8px 24px rgba(77, 185, 230, 0.15);
  backdrop-filter: blur(12px);
  clip-path: polygon(var(--cut) 0, 100% 0, 100% calc(100% - var(--cut)), calc(100% - var(--cut)) 100%, 0 100%, 0 var(--cut));
  animation: dock-enter 220ms cubic-bezier(0.18, 0.82, 0.3, 1) both;
}

.combat-dock::before {
  content: '';
  position: absolute;
  inset: 0;
  pointer-events: none;
  background: linear-gradient(135deg, rgba(77, 185, 230, 0.1), transparent 40%);
}

.combat-dock::after {
  content: '';
  position: absolute;
  top: 16px;
  left: 18px;
  width: 40px;
  height: 2px;
  background: rgba(77, 185, 230, 0.88);
  box-shadow: 0 0 8px rgba(77, 185, 230, 0.4);
}

.dock-kicker {
  position: relative;
  font-size: 11px;
  letter-spacing: 0.2em;
  text-transform: uppercase;
  color: rgba(77, 185, 230, 0.9);
  padding-top: 4px;
}

.dock-title {
  position: relative;
  font-size: 20px;
  font-weight: 700;
  letter-spacing: 0.05em;
  color: #143245;
}

.dock-copy {
  position: relative;
  font-size: 12px;
  line-height: 1.6;
  color: #5f8194;
}

.dock-copy--accent {
  color: #ff9d4d;
}

.dock-actions {
  position: relative;
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 8px;
}

.dock-actions--compact {
  margin-top: 4px;
}

.dock-button {
  --cut: 8px;
  border: 1px solid rgba(77, 185, 230, 0.4);
  background: rgba(255, 255, 255, 0.9);
  color: #143245;
  padding: 8px 16px;
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.05em;
  cursor: pointer;
  clip-path: polygon(var(--cut) 0, 100% 0, 100% calc(100% - var(--cut)), calc(100% - var(--cut)) 100%, 0 100%, 0 var(--cut));
  transition: all 140ms ease;
  font-family: inherit;
  text-transform: uppercase;
}

.dock-button:hover:not(:disabled) {
  transform: translateY(-1px);
  border-color: rgba(77, 185, 230, 0.8);
  background: rgba(77, 185, 230, 0.1);
  box-shadow: 0 4px 12px rgba(77, 185, 230, 0.15);
}

.dock-button:disabled {
  opacity: 0.4;
  cursor: default;
  border-color: rgba(77, 185, 230, 0.2);
}

.dock-button--primary {
  background: rgba(77, 185, 230, 0.15);
  border-color: rgba(77, 185, 230, 0.6);
  color: #143245;
}

.dock-button--primary:hover:not(:disabled) {
  background: rgba(77, 185, 230, 0.25);
}

.settlement-layer {
  position: absolute;
  inset: 0;
  display: grid;
  place-items: center;
  padding: 24px;
  background: linear-gradient(180deg, rgba(232, 241, 245, 0.18), rgba(20, 50, 69, 0.22));
  backdrop-filter: blur(10px);
}

.settlement-modal {
  --cut: 16px;
  position: relative;
  width: min(720px, calc(100vw - 32px));
  display: grid;
  gap: 14px;
  padding: 24px 24px 22px;
  background: rgba(255, 255, 255, 0.94);
  border: 1px solid rgba(77, 185, 230, 0.46);
  box-shadow: 0 18px 44px rgba(20, 50, 69, 0.18);
  clip-path: polygon(var(--cut) 0, 100% 0, 100% calc(100% - var(--cut)), calc(100% - var(--cut)) 100%, 0 100%, 0 var(--cut));
  animation: dock-enter 220ms cubic-bezier(0.18, 0.82, 0.3, 1) both;
}

.settlement-modal::before {
  content: '';
  position: absolute;
  inset: 0;
  pointer-events: none;
  background: linear-gradient(135deg, rgba(77, 185, 230, 0.12), transparent 44%);
}

.settlement-modal::after {
  content: '';
  position: absolute;
  top: 18px;
  left: 24px;
  width: 54px;
  height: 2px;
  background: rgba(77, 185, 230, 0.92);
  box-shadow: 0 0 8px rgba(77, 185, 230, 0.4);
}

.settlement-kicker,
.settlement-title,
.settlement-copy,
.settlement-grid,
.settlement-footnote,
.settlement-actions {
  position: relative;
}

.settlement-kicker {
  padding-top: 6px;
  font-size: 11px;
  letter-spacing: 0.22em;
  text-transform: uppercase;
  color: rgba(77, 185, 230, 0.92);
}

.settlement-title {
  font-size: 24px;
  font-weight: 700;
  letter-spacing: 0.05em;
  color: #143245;
}

.settlement-copy {
  font-size: 13px;
  line-height: 1.6;
  color: #5f8194;
}

.settlement-copy--accent {
  color: #ff9d4d;
}

.settlement-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}

.settlement-card {
  --cut: 10px;
  min-height: 118px;
  display: grid;
  gap: 8px;
  padding: 16px;
  background: rgba(232, 241, 245, 0.72);
  border: 1px solid rgba(77, 185, 230, 0.24);
  clip-path: polygon(var(--cut) 0, 100% 0, 100% calc(100% - var(--cut)), calc(100% - var(--cut)) 100%, 0 100%, 0 var(--cut));
}

.settlement-label {
  font-size: 11px;
  letter-spacing: 0.18em;
  text-transform: uppercase;
  color: rgba(77, 185, 230, 0.86);
}

.settlement-value {
  font-size: 18px;
  font-weight: 700;
  line-height: 1.5;
  color: #143245;
}

.settlement-value--compact {
  font-size: 14px;
  font-weight: 600;
}

.settlement-meta {
  font-size: 12px;
  line-height: 1.7;
  color: #5f8194;
}

.settlement-footnote {
  font-size: 12px;
  line-height: 1.6;
  color: #5f8194;
}

.settlement-actions {
  display: flex;
  justify-content: flex-end;
}

.settlement-button {
  min-width: 220px;
  justify-content: center;
}

.viewport-error {
  position: absolute;
  inset: 24px;
  display: grid;
  place-items: center;
  color: rgba(228, 109, 97, 0.96);
  font-size: 14px;
  font-family: inherit;
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

  .settlement-layer {
    padding: 16px;
  }

  .settlement-modal {
    width: min(100%, 560px);
    padding: 22px 18px 18px;
  }

  .settlement-grid {
    grid-template-columns: 1fr;
  }

  .settlement-button {
    width: 100%;
    min-width: 0;
  }
}
</style>
