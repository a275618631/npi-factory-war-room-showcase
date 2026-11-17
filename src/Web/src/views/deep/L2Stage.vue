<template>
  <div class="deep-view-container" data-testid="view-l2-stage">
    <div v-if="!hasPermission" id="permission-denied" class="permission-denied-box">
      <h3>沒有權限</h3>
    </div>
    <div v-else>
      <DeepBreadcrumb :items="breadcrumbItems" />
      <h2>關卡詳情 (L2 Stage): {{ route.params.stage }}</h2>
      <p>關卡審查狀態、離開條件與單位清單</p>
      <div style="margin-top: 16px;">
        <el-button id="btn-goto-l3-unit" type="primary" plain @click="router.push(`/p/${route.params.project}/g/${route.params.stage}/u/SQE`)">
          進入單位：SQE 供應商品質
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
  fetchContext(route.params.project as string, route.params.stage as string);
});
</script>


