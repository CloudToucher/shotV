<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'

import { GameRuntime } from '../game/core/GameRuntime'

const host = ref<HTMLElement | null>(null)
const status = ref<'booting' | 'ready' | 'error'>('booting')
const statusMessage = ref('初始化 Pixi 渲染层...')

let runtime: GameRuntime | null = null

onMounted(async () => {
  if (!host.value) {
    return
  }

  runtime = new GameRuntime()

  try {
    await runtime.mount(host.value)
    status.value = 'ready'
    statusMessage.value = '阶段 3：1 / 2 / 3 切换机枪、榴弹、狙击'
  } catch (error) {
    console.error(error)
    status.value = 'error'
    statusMessage.value = 'Pixi 初始化失败，请查看浏览器控制台。'
  }
})

onBeforeUnmount(() => {
  runtime?.destroy()
  runtime = null
})
</script>

<template>
  <div class="viewport-shell">
    <div ref="host" class="viewport-host"></div>
    <div class="viewport-overlay">
      <span class="status-pill" :class="`status-pill--${status}`">{{ statusMessage }}</span>
    </div>
  </div>
</template>

<style scoped>
.viewport-shell {
  position: relative;
  width: 100%;
  height: 100%;
  min-height: 520px;
  overflow: hidden;
  border-radius: 28px;
  background:
    linear-gradient(180deg, rgba(255, 255, 255, 0.86), rgba(228, 244, 250, 0.94));
  box-shadow:
    inset 0 1px 0 rgba(255, 255, 255, 0.75),
    0 24px 80px rgba(69, 131, 161, 0.18);
}

.viewport-host {
  width: 100%;
  height: 100%;
}

.viewport-host :deep(canvas) {
  display: block;
  width: 100%;
  height: 100%;
}

.viewport-overlay {
  pointer-events: none;
  position: absolute;
  inset: 18px 18px auto;
}

.status-pill {
  display: inline-flex;
  align-items: center;
  min-height: 38px;
  padding: 0 14px;
  border: 1px solid rgba(77, 185, 230, 0.18);
  border-radius: 999px;
  background: rgba(248, 253, 255, 0.84);
  backdrop-filter: blur(10px);
  font-size: 12px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: #21455a;
}

.status-pill--ready {
  border-color: rgba(77, 185, 230, 0.36);
  color: #21455a;
}

.status-pill--error {
  border-color: rgba(255, 122, 92, 0.25);
  color: #9c4726;
}

@media (max-width: 900px) {
  .viewport-shell {
    min-height: 460px;
  }
}
</style>
