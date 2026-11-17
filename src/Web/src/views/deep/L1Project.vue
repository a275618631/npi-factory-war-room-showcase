<template>
  <div class="deep-view-container" data-testid="view-l1-project">
    <div v-if="!hasPermission" id="permission-denied" class="permission-denied-box">
      <h3>沒有權限</h3>
    </div>
    <div v-else>
      <DeepBreadcrumb :items="breadcrumbItems" />
      <h2>專案詳情 (L1 Project): {{ route.params.project }}</h2>
      <p>專案基本資料、關卡時間軸與狀態矩陣</p>
      <div style="margin-top: 16px;">
        <el-button id="btn-goto-l2-stage" type="primary" plain @click="router.push(`/p/${route.params.project}/g/DVT`)">
          進入關卡：DVT
        </el-button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import DeepBreadcrumb from '../../components/DeepBreadcrumb.vue';
import { useDeepLinkContext } from '../../composables/useDeepLinkContext';

const route = useRoute();
const router = useRouter();
const { hasPermission, breadcrumbItems, fetchContext } = useDeepLinkContext();

onMounted(() => {
  fetchContext(route.params.project as string);
});
</script>


