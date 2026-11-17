<template>
  <span class="animated-number" :data-value="value">{{ formattedValue }}</span>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';

const props = withDefaults(
  defineProps<{
    value: number;
    duration?: number;
  }>(),
  {
    duration: 600,
  }
);

function isMotionEnabled(): boolean {
  if (typeof document === 'undefined' || !document.documentElement) {
    return false;
  }
  return document.documentElement.getAttribute('data-motion') === 'on';
}

const displayValue = ref<number>(isMotionEnabled() ? 0 : props.value);

const formattedValue = computed(() => {
  return Math.round(displayValue.value);
});

function animateTo(target: number) {
  if (!isMotionEnabled()) {
    displayValue.value = target;
    return;
  }

  const start = displayValue.value;
  const startTime = performance.now();
  const duration = props.duration;

  function step(now: number) {
    const elapsed = now - startTime;
    const progress = Math.min(elapsed / duration, 1);
    // Ease-out quad
    const easeProgress = 1 - (1 - progress) * (1 - progress);
    displayValue.value = start + (target - start) * easeProgress;

    if (progress < 1) {
      requestAnimationFrame(step);
    } else {
      displayValue.value = target;
    }
  }

  requestAnimationFrame(step);
}

onMounted(() => {
  if (isMotionEnabled()) {
    animateTo(props.value);
  } else {
    displayValue.value = props.value;
  }
});

watch(
  () => props.value,
  (newVal) => {
    animateTo(newVal);
  }
);
</script>

<style scoped>
.animated-number {
  font-variant-numeric: tabular-nums;
  display: inline-block;
}
</style>


