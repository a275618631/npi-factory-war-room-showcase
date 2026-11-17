<template>
  <div class="deep-view-container" data-testid="view-l0-overview">
    <DeepBreadcrumb :items="breadcrumbs" />
    <h2>全部專案 (L0 Overview)</h2>
    <p>顯示所有專案卡片與健康燈號總覽</p>
    <div class="project-links" style="margin-top: 16px;">
      <el-button id="btn-goto-l1-project" type="primary" plain @click="router.push('/p/NPI-2601')">
        進入專案：NPI-2601 範例產品 A
      </el-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import DeepBreadcrumb from '../../components/DeepBreadcrumb.vue';

const router = useRouter();
const breadcrumbs = ref([
  { level: 'L0', label: '全部專案', path: '/overview' }
]);
</script>


