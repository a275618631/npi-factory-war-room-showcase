<template>
  <div class="deep-view-container" data-testid="view-l4-item">
    <div v-if="!hasPermission" id="permission-denied" class="permission-denied-box">
      <h3>沒有權限</h3>
    </div>
    <div v-else>
      <DeepBreadcrumb :items="breadcrumbItems" />
      <h2>細項工作 (L4 Item): {{ route.params.code }}</h2>
      <p>類型: {{ route.params.type }} ｜ 代號: {{ route.params.code }}</p>

      <div style="margin-top: 16px;">
        <el-button id="btn-back-to-l3" @click="router.back()">
          返回上一層
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
  fetchContext(
    route.params.project as string,
    undefined,
    undefined,
    route.params.type as string,
    route.params.code as string
  );
});
</script>


