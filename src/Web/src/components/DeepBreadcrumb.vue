<template>
  <el-breadcrumb separator="/" class="deep-breadcrumb" data-testid="deep-breadcrumb">
    <el-breadcrumb-item
      v-for="(item, idx) in items"
      :key="item.path"
      :data-testid="'breadcrumb-' + item.level.toLowerCase()"
    >
      <span
        v-if="idx === items.length - 1"
        class="breadcrumb-current"
        :data-testid="'breadcrumb-label-' + item.level.toLowerCase()"
      >
        {{ item.label }}
      </span>
      <a
        v-else
        href="javascript:void(0)"
        class="breadcrumb-link"
        :data-testid="'breadcrumb-link-' + item.level.toLowerCase()"
        @click="handleNavigate(item.path)"
      >
        {{ item.label }}
      </a>
    </el-breadcrumb-item>
  </el-breadcrumb>
</template>

<script setup lang="ts">
import { useRouter, useRoute } from 'vue-router';

export interface BreadcrumbNode {
  level: string; // "L0", "L1", "L2", "L3", "L4"
  label: string;
  path: string;
}

const props = withDefaults(
  defineProps<{
    items: BreadcrumbNode[];
    preserveQuery?: boolean;
  }>(),
  {
    items: () => [],
    preserveQuery: true,
  }
);

const router = useRouter();
const route = useRoute();

function handleNavigate(path: string) {
  if (props.preserveQuery && Object.keys(route.query).length > 0) {
    router.push({ path, query: { ...route.query } });
  } else {
    router.push(path);
  }
}
</script>

<style scoped>
.deep-breadcrumb {
  margin-bottom: 20px;
  padding: 8px 12px;
  background: #f8fafc;
  border-radius: 4px;
}

.breadcrumb-link {
  color: #409eff;
  cursor: pointer;
  text-decoration: none;
}

.breadcrumb-link:hover {
  text-decoration: underline;
}

.breadcrumb-current {
  color: #303133;
  font-weight: 600;
}
</style>


