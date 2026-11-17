import { ref } from 'vue';
import type { BreadcrumbNode } from '../components/DeepBreadcrumb.vue';

export function useDeepLinkContext() {
  const loading = ref(false);
  const hasPermission = ref(true);
  const breadcrumbItems = ref<BreadcrumbNode[]>([]);
  const errorMessage = ref('');

  async function fetchContext(
    projectCode: string,
    stageCode?: string,
    unitCode?: string,
    itemType?: string,
    itemCode?: string
  ) {
    loading.value = true;
    errorMessage.value = '';
    try {
      const params = new URLSearchParams({ projectCode });
      if (stageCode) params.append('stageCode', stageCode);
      if (unitCode) params.append('unitCode', unitCode);
      if (itemType) params.append('itemType', itemType);
      if (itemCode) params.append('itemCode', itemCode);

      const res = await fetch(`/api/deep-links/context?${params.toString()}`);
      if (res.status === 403 || res.status === 404) {
        hasPermission.value = false;
        errorMessage.value = '沒有權限';
        breadcrumbItems.value = [];
        return;
      }

      if (res.ok) {
        const data = await res.json();
        if (data.hasPermission === false) {
          hasPermission.value = false;
          errorMessage.value = data.errorMessage || '沒有權限';
          breadcrumbItems.value = [];
        } else {
          hasPermission.value = true;
          breadcrumbItems.value = (data.breadcrumb || []).map((b: any) => ({
            level: b.level,
            label: b.name,
            path: b.path,
          }));
        }
      } else {
        hasPermission.value = false;
        errorMessage.value = '沒有權限';
        breadcrumbItems.value = [];
      }
    } catch {
      hasPermission.value = false;
      errorMessage.value = '沒有權限';
      breadcrumbItems.value = [];
    } finally {
      loading.value = false;
    }
  }

  return {
    loading,
    hasPermission,
    breadcrumbItems,
    errorMessage,
    fetchContext,
  };
}


