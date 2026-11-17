<template>
  <div class="import-history-container">
    <div class="page-header">
      <h2 id="import-history-title">{{ t('import.historyTitle', '資料匯入紀錄與錯誤報表') }}</h2>
      <p class="page-desc">{{ t('import.historyDesc', '檢視每一批資料的匯入方式、筆數、成功與失敗狀態，並可下載錯誤報表 Excel。') }}</p>
    </div>

    <el-card shadow="never" class="table-card">
      <template #header>
        <div class="card-toolbar">
          <span class="toolbar-title">{{ t('import.batchList', '匯入批次紀錄') }}</span>
          <div class="toolbar-actions">
            <el-upload
              action="/api/import/upload"
              :show-file-list="false"
              :auto-upload="false"
              :on-change="handleFileSelected"
            >
              <el-button id="btn-upload-file" type="primary" plain>
                {{ t('import.uploadButton', '上傳 CSV / Excel') }}
              </el-button>
            </el-upload>
            <el-button id="btn-refresh-history" :loading="loading" @click="fetchBatches">
              {{ t('common.refresh', '重新整理') }}
            </el-button>
          </div>
        </div>
      </template>

      <el-table id="batch-table" :data="batches" style="width: 100%" row-key="id">
        <el-table-column prop="createdAt" label="匯入時間" min-width="160">
          <template #default="{ row }">
            <span>{{ formatDate(row.createdAt) }}</span>
          </template>
        </el-table-column>

        <el-table-column prop="importMethod" label="匯入方式" width="140">
          <template #default="{ row }">
            <el-tag effect="plain">{{ row.importMethod }}</el-tag>
          </template>
        </el-table-column>

        <el-table-column prop="fileName" label="檔案名稱" min-width="150">
          <template #default="{ row }">
            <span>{{ row.fileName || '-' }}</span>
          </template>
        </el-table-column>

        <el-table-column prop="tableName" label="資料表" width="140">
          <template #default="{ row }">
            <code>{{ row.tableName }}</code>
          </template>
        </el-table-column>

        <el-table-column prop="totalRows" label="總筆數" width="90" align="center" />

        <el-table-column prop="successRows" label="成功" width="90" align="center">
          <template #default="{ row }">
            <span style="color: #67c23a; font-weight: bold;">{{ row.successRows }}</span>
          </template>
        </el-table-column>

        <el-table-column prop="failedRows" label="失敗" width="90" align="center">
          <template #default="{ row }">
            <span :style="{ color: row.failedRows > 0 ? '#f56c6c' : '#909399', fontWeight: 'bold' }">
              {{ row.failedRows }}
            </span>
          </template>
        </el-table-column>

        <el-table-column prop="status" label="狀態" width="100" align="center">
          <template #default="{ row }">
            <el-tag :type="row.status === '成功' ? 'success' : 'danger'">
              {{ row.status }}
            </el-tag>
          </template>
        </el-table-column>

        <el-table-column label="操作" width="220" align="center">
          <template #default="{ row }">
            <el-button
              :id="`btn-view-errors-${row.id}`"
              size="small"
              :type="row.failedRows > 0 ? 'warning' : 'info'"
              plain
              @click="viewErrors(row)"
            >
              明細
            </el-button>
            <el-button
              v-if="row.failedRows > 0"
              :id="`btn-download-excel-${row.id}`"
              size="small"
              type="primary"
              plain
              @click="downloadErrorReport(row.id)"
            >
              下載 Excel
            </el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 錯誤明細對話框 -->
    <el-dialog
      v-model="detailVisible"
      :title="`匯入錯誤明細（批次編號: ${selectedBatch?.id ?? ''}）`"
      width="750px"
    >
      <div v-if="detailLoading" style="text-align: center; padding: 20px;">
        載入中...
      </div>
      <div v-else>
        <div style="margin-bottom: 12px; display: flex; justify-content: space-between; align-items: center;">
          <span>共 {{ currentErrors.length }} 筆錯誤</span>
          <el-button
            v-if="currentErrors.length > 0 && selectedBatch"
            type="primary"
            size="small"
            @click="downloadErrorReport(selectedBatch.id)"
          >
            下載錯誤報表 Excel
          </el-button>
        </div>

        <el-table id="error-table" :data="currentErrors" max-height="350" style="width: 100%">
          <el-table-column prop="rowNumber" label="列號" width="70" align="center" />
          <el-table-column prop="fieldName" label="欄位" width="120" />
          <el-table-column prop="originalValue" label="原值" width="120">
            <template #default="{ row }">
              <span>{{ row.originalValue || '(空白)' }}</span>
            </template>
          </el-table-column>
          <el-table-column prop="reason" label="錯誤原因" min-width="180" />
          <el-table-column prop="suggestion" label="建議修法" min-width="160" />
        </el-table>
      </div>
      <template #footer>
        <el-button @click="detailVisible = false">關閉</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import type { UploadFile } from 'element-plus';

export interface ImportBatchItem {
  id: string;
  tableName: string;
  fileName: string;
  status: string;
  totalRows: number;
  successRows: number;
  failedRows: number;
  importMethod: string;
  createdAt: string;
}

export interface ImportErrorItem {
  id: string;
  importBatchId: string;
  rowNumber: number;
  fieldName: string;
  originalValue?: string;
  reason: string;
  suggestion?: string;
  createdAt: string;
}

const { t } = useI18n();

const batches = ref<ImportBatchItem[]>([]);
const loading = ref(false);
const detailVisible = ref(false);
const detailLoading = ref(false);
const selectedBatch = ref<ImportBatchItem | null>(null);
const currentErrors = ref<ImportErrorItem[]>([]);

async function fetchBatches() {
  loading.value = true;
  try {
    const res = await fetch('/api/import/batches');
    if (res.ok) {
      batches.value = await res.json();
    }
  } catch (err) {
    console.error('Failed to load import batches', err);
  } finally {
    loading.value = false;
  }
}

async function viewErrors(batch: ImportBatchItem) {
  selectedBatch.value = batch;
  detailVisible.value = true;
  detailLoading.value = true;
  currentErrors.value = [];

  try {
    const res = await fetch(`/api/import/batches/${batch.id}`);
    if (res.ok) {
      const data = await res.json();
      currentErrors.value = data.errors ?? [];
    }
  } catch (err) {
    console.error('Failed to load batch errors', err);
  } finally {
    detailLoading.value = false;
  }
}

function downloadErrorReport(batchId: string) {
  window.open(`/api/import/batches/${batchId}/error-report`, '_blank');
}

function handleFileSelected(uploadFile: UploadFile) {
  console.log('Selected file for import:', uploadFile.name);
}

function formatDate(dateStr: string) {
  if (!dateStr) return '';
  const d = new Date(dateStr);
  return d.toLocaleString('zh-TW', { hour12: false });
}

onMounted(() => {
  fetchBatches();
});
</script>

<style scoped>
.import-history-container {
  padding: 8px 0;
}
.page-header {
  margin-bottom: 20px;
}
.page-header h2 {
  font-size: 20px;
  font-weight: 600;
  margin: 0 0 8px 0;
}
.page-desc {
  font-size: 14px;
  color: #606266;
  margin: 0;
}
.card-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.toolbar-title {
  font-weight: 600;
  font-size: 15px;
}
.toolbar-actions {
  display: flex;
  gap: 10px;
  align-items: center;
}
</style>


