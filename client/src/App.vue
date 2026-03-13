<script setup lang="ts">
import GameViewport from './components/GameViewport.vue'
</script>

<template>
  <main class="app-shell">
    <GameViewport />
  </main>
</template>

<style scoped>
.app-shell {
  width: 100vw;
  min-height: 100vh;
  padding: 0;
}
</style>
