<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'

import { GameRuntime } from '../game/core/GameRuntime'

const host = ref<HTMLElement | null>(null)
const errorMessage = ref('')

let runtime: GameRuntime | null = null

onMounted(async () => {
  if (!host.value) {
    return
  }

  runtime = new GameRuntime()

  try {
    await runtime.mount(host.value)
  } catch (error) {
    console.error(error)
    errorMessage.value = 'Renderer bootstrap failed. Check the browser console.'
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
    <div v-if="errorMessage" class="viewport-error">{{ errorMessage }}</div>
  </div>
</template>

<style scoped>
.viewport-shell {
  position: relative;
  width: 100%;
  min-height: 100vh;
  overflow: hidden;
  background:
    radial-gradient(circle at top, rgba(255, 255, 255, 0.08), transparent 38%),
    linear-gradient(180deg, rgba(6, 20, 28, 0.94), rgba(4, 14, 20, 0.98));
}

.viewport-host {
  width: 100%;
  min-height: 100vh;
}

.viewport-host :deep(canvas) {
  display: block;
  width: 100%;
  height: 100%;
}

.viewport-error {
  position: absolute;
  inset: 24px;
  display: grid;
  place-items: center;
  font-size: 14px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: rgba(255, 194, 174, 0.92);
}
</style>
