<script setup lang="ts">
import GameViewport from './components/GameViewport.vue'
</script>

<template>
  <main class="app-shell">
    <section class="info-panel">
      <p class="eyebrow">ShotV MVP · Stage 3</p>
      <h1>统一亮色场地，三把武器各有自己的瞄准语义。</h1>
      <p class="description">
        现在可以用 `1/2/3` 切换机枪、榴弹、狙击。三把武器共享同一套冷白青蓝底色和信号橙强调，但会通过箭头轮廓、准星结构、范围提示和射击节奏来表达差异。
      </p>
      <div class="fact-grid">
        <article class="fact-card">
          <span class="fact-label">统一底色</span>
          <strong>冷白场地 + 青蓝边框</strong>
        </article>
        <article class="fact-card">
          <span class="fact-label">武器识别</span>
          <strong>箭头轮廓 + 准星语义</strong>
        </article>
        <article class="fact-card">
          <span class="fact-label">当前验证</span>
          <strong>切枪、节奏、范围、反馈</strong>
        </article>
      </div>
    </section>

    <section class="viewport-panel">
      <GameViewport />
      <p class="viewport-caption">
        `1` 机枪：连续压制。`2` 榴弹：带落点爆炸范围。`3` 狙击：长距离高反馈。`WASD` 移动，左键射击，`Space` 冲刺。
      </p>
    </section>
  </main>
</template>

<style scoped>
.app-shell {
  display: grid;
  grid-template-columns: minmax(280px, 420px) minmax(0, 1fr);
  gap: 28px;
  width: min(1440px, 100%);
  min-height: 100vh;
  margin: 0 auto;
  padding: 32px;
}

.info-panel,
.viewport-panel {
  position: relative;
  border: 1px solid rgba(77, 185, 230, 0.18);
  border-radius: 30px;
  background: rgba(248, 253, 255, 0.86);
  box-shadow:
    inset 0 1px 0 rgba(255, 255, 255, 0.7),
    0 24px 70px rgba(69, 131, 161, 0.18);
}

.info-panel {
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  padding: 32px;
}

.eyebrow {
  margin: 0 0 14px;
  font-size: 12px;
  letter-spacing: 0.24em;
  text-transform: uppercase;
  color: #2c8fb7;
}

h1 {
  margin: 0 0 18px;
  font-size: clamp(32px, 4.2vw, 56px);
  line-height: 0.98;
  letter-spacing: -0.04em;
}

.description {
  margin: 0;
  font-size: 17px;
  line-height: 1.7;
  color: rgba(20, 50, 69, 0.76);
}

.fact-grid {
  display: grid;
  gap: 14px;
  margin-top: 28px;
}

.fact-card {
  padding: 16px 18px;
  border: 1px solid rgba(77, 185, 230, 0.14);
  border-radius: 20px;
  background: rgba(223, 245, 255, 0.5);
}

.fact-card strong {
  display: block;
  font-size: 18px;
  line-height: 1.3;
}

.fact-label {
  display: block;
  margin-bottom: 6px;
  font-size: 12px;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  color: rgba(95, 129, 148, 0.92);
}

.viewport-panel {
  display: flex;
  flex-direction: column;
  min-height: 640px;
  padding: 18px;
}

.viewport-panel :deep(.viewport-shell) {
  flex: 1;
}

.viewport-caption {
  margin: 14px 6px 2px;
  font-size: 13px;
  letter-spacing: 0.03em;
  color: rgba(63, 98, 118, 0.78);
}

@media (max-width: 1100px) {
  .app-shell {
    grid-template-columns: 1fr;
  }

  .viewport-panel {
    min-height: 520px;
  }
}

@media (max-width: 640px) {
  .app-shell {
    padding: 18px;
  }

  .info-panel,
  .viewport-panel {
    border-radius: 24px;
  }

  .info-panel {
    padding: 24px;
  }

  .viewport-panel {
    padding: 14px;
  }
}
</style>
