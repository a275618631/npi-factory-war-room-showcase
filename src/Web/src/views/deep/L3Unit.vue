<template>
  <div class="deep-view-container" data-testid="view-l3-unit">
    <div v-if="!hasPermission" id="permission-denied" class="permission-denied-box">
      <h3>沒有權限</h3>
    </div>
    <div v-else>
      <DeepBreadcrumb :items="breadcrumbItems" />
      <h2>單位在關卡 (L3 Unit): {{ route.params.unit }}</h2>
      <p>細項工作清單：交付物、問題、卡關</p>

      <!-- Filter Controls (URL Query State) -->
      <div class="filter-bar" style="margin: 16px 0; display: flex; align-items: center; gap: 12px;">
        <span style="font-size: 13px;">篩選狀態：</span>
        <el-select
          id="l3-filter-select"
          v-model="currentFilter"
          placeholder="選擇篩選條件"
          size="small"
          style="width: 140px;"
          @change="updateFilterQuery"
        >
          <el-option label="全部" value="all" />
          <el-option label="僅看逾期" value="overdue" />
          <el-option label="僅看我負責" value="my" />
        </el-select>
        <span v-if="route.query.filter" id="active-filter-indicator" style="font-size: 12px; color: #409eff;">
          當前篩選：{{ route.query.filter }}
        </span>
      </div>

      <div style="margin-top: 16px;">
        <el-button
          id="btn-goto-l4-deliverable"
          type="primary"
          plain
          @click="router.push({ path: `/p/${route.params.project}/item/deliverable/DVT-PPAP`, query: route.query })"
        >
          進入細項：DVT-PPAP PPAP 文件
        </el-button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import DeepBreadcrumb from '../../components/DeepBreadcrumb.vue';
import { useDeepLinkContext } from '../../composables/useDeepLinkContext';

const route = useRoute();
const router = useRouter();
const { hasPermission, breadcrumbItems, fetchContext } = useDeepLinkContext();

const currentFilter = ref((route.query.filter as string) || 'all');

function updateFilterQuery(val: string) {
  router.push({
    path: route.path,
    query: { ...route.query, filter: val },
  });
}

onMounted(() => {
  fetchContext(
    route.params.project as string,
    route.params.stage as string,
    route.params.unit as string
  );
});
</script>


