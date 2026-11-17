<template>
  <div class="stage-timeline-container" data-testid="stage-timeline">
    <div class="timeline-track">
      <div
        v-for="(stage, idx) in sortedStages"
        :key="stage.stageCode"
        class="timeline-node"
        :class="{
          'is-current': isCurrentStage(stage),
          'is-delayed': stage.delayDays > 0,
          'is-completed': isCompletedStage(stage),
        }"
        :data-stage-code="stage.stageCode"
      >
        <!-- Connector Line -->
        <div v-if="idx > 0" class="connector-line" :class="{ 'connector-active': isCompletedStage(stage) || isCurrentStage(stage) }" />

        <!-- Node Header / Badge -->
        <div class="node-header">
          <div class="node-badge" :class="{ 'current-badge': isCurrentStage(stage) }">
            <span class="step-num">{{ idx + 1 }}</span>
          </div>
          <span class="stage-title" :data-testid="'stage-name-' + stage.stageCode">
            {{ stage.stageName || stage.stageCode }}
          </span>
          <el-tag
            v-if="isCurrentStage(stage)"
            size="small"
            type="primary"
            effect="dark"
            class="current-tag"
            data-testid="current-stage-tag"
          >
            目前關卡
          </el-tag>
        </div>

        <!-- Node Body / Details -->
        <div class="node-body">
          <div class="date-row">
            <span class="label">計畫：</span>
            <span class="val">{{ formatDate(stage.plannedStart) }} ~ {{ formatDate(stage.plannedEnd) }}</span>
          </div>

          <div v-if="stage.actualStart || stage.actualEnd" class="date-row">
            <span class="label">實際：</span>
            <span class="val">{{ formatDate(stage.actualStart) }} ~ {{ formatDate(stage.actualEnd) || '進行中' }}</span>
          </div>

          <div v-if="stage.gateDate" class="gate-row">
            <span class="label">審查日：</span>
            <span class="val">{{ formatDate(stage.gateDate) }}</span>
          </div>

          <div v-if="stage.gateOwnerPersonCode" class="owner-row">
            <span class="label">負責人：</span>
            <span class="val">{{ stage.gateOwnerPersonCode }}</span>
          </div>

          <!-- Delay Warning -->
          <div v-if="stage.delayDays > 0" class="delay-row" data-testid="delay-indicator">
            <span class="delay-text">延誤 {{ stage.delayDays }} 天</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';

export interface StageTimelineItem {
  stageCode: string;
  stageName?: string;
  plannedStart: string;
  plannedEnd: string;
  actualStart?: string | null;
  actualEnd?: string | null;
  gateDate?: string | null;
  gateOwnerPersonCode?: string | null;
  delayDays: number;
  isDelayed?: boolean;
  isCurrent?: boolean;
}

const props = withDefaults(
  defineProps<{
    stages: StageTimelineItem[];
    currentStageCode?: string;
  }>(),
  {
    stages: () => [],
    currentStageCode: '',
  }
);

const sortedStages = computed(() => {
  return [...props.stages].sort((a, b) => {
    return new Date(a.plannedStart).getTime() - new Date(b.plannedStart).getTime();
  });
});

function isCurrentStage(stage: StageTimelineItem): boolean {
  if (stage.isCurrent) return true;
  if (!props.currentStageCode) return false;
  return stage.stageCode.toLowerCase() === props.currentStageCode.toLowerCase();
}

function isCompletedStage(stage: StageTimelineItem): boolean {
  return !!stage.actualEnd;
}

function formatDate(val?: string | null): string {
  if (!val) return '-';
  return val.split('T')[0];
}
</script>

<style scoped>
.stage-timeline-container {
  padding: 16px;
  background: #ffffff;
  border-radius: 6px;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.06);
  overflow-x: auto;
}

.timeline-track {
  display: flex;
  align-items: flex-start;
  min-width: 720px;
  gap: 16px;
}

.timeline-node {
  flex: 1;
  position: relative;
  background: #fcfdfe;
  border: 1px solid #e4e7ed;
  border-radius: 6px;
  padding: 12px;
  transition: border-color 0.2s, box-shadow 0.2s;
}

.timeline-node.is-current {
  border-color: #409eff;
  background: #ecf5ff;
  box-shadow: 0 2px 8px rgba(64, 158, 255, 0.15);
}

.timeline-node.is-delayed {
  border-color: #fde2e2;
  background: #fef0f0;
}

.timeline-node.is-current.is-delayed {
  border-color: #f56c6c;
  background: #fef0f0;
}

.node-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
  border-bottom: 1px solid #ebeef5;
  padding-bottom: 6px;
}

.node-badge {
  width: 22px;
  height: 22px;
  border-radius: 50%;
  background: #909399;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  font-weight: bold;
}

.node-badge.current-badge {
  background: #409eff;
}

.stage-title {
  font-weight: 600;
  font-size: 14px;
  color: #303133;
}

.current-tag {
  margin-left: auto;
}

.node-body {
  font-size: 12px;
  color: #606266;
  line-height: 1.6;
}

.date-row,
.gate-row,
.owner-row {
  display: flex;
  justify-content: space-between;
}

.label {
  color: #909399;
}

.delay-row {
  margin-top: 6px;
  padding-top: 4px;
  border-top: 1px dashed #fbc4c4;
  text-align: right;
}

.delay-text {
  color: #f56c6c;
  font-weight: bold;
  font-size: 13px;
}

/* Motion breathing for current stage in relaxed mode */
html[data-motion='on'] .timeline-node.is-current {
  animation: pulse-current 2.5s infinite ease-in-out;
}

@keyframes pulse-current {
  0%, 100% {
    box-shadow: 0 0 0 0 rgba(64, 158, 255, 0.4);
  }
  50% {
    box-shadow: 0 0 0 6px rgba(64, 158, 255, 0);
  }
}
</style>


