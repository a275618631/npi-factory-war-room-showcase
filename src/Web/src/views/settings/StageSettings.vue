<template>
  <div class="stage-settings-container">
    <div class="page-header">
      <h2 id="stage-settings-title">{{ t('settings.stageTitle', '階段與關卡設定') }}</h2>
      <p class="page-desc">{{ t('settings.stageDesc', '管理 NPI 專案的關卡生命週期、階段名稱、順序與代表色。停用的階段不能被新專案使用，但舊資料完整保留。') }}</p>
    </div>

    <el-card shadow="never" class="table-card">
      <template #header>
        <div class="card-toolbar">
          <span class="toolbar-title">{{ t('settings.stageList', '關卡清單') }}</span>
          <div class="toolbar-actions">
            <el-button id="btn-add-stage" type="primary" plain @click="handleAddStage">
              {{ t('common.add', '新增階段') }}
            </el-button>
            <el-button id="btn-reload-stages" :loading="loading" @click="fetchStages">
              {{ t('common.refresh', '重新整理') }}
            </el-button>
            <el-button id="btn-save-stages" type="primary" :loading="saving" @click="saveStages">
              {{ t('common.save', '儲存設定') }}
            </el-button>
          </div>
        </div>
      </template>

      <el-alert
        v-if="saveMessage"
        id="save-success-alert"
        :title="saveMessage"
        type="success"
        show-icon
        closable
        style="margin-bottom: 16px;"
      />

      <el-table id="stage-table" :data="stages" style="width: 100%" row-key="code">
        <el-table-column label="順序" width="100" align="center">
          <template #default="{ $index }">
            <span class="stage-order">{{ $index + 1 }}</span>
          </template>
        </el-table-column>

        <el-table-column prop="code" label="階段代號" width="140">
          <template #default="{ row }">
            <el-tag :color="row.color" effect="dark" style="color: #fff; font-weight: bold;">
              {{ row.code }}
            </el-tag>
          </template>
        </el-table-column>

        <el-table-column prop="name" label="階段名稱" min-width="180">
          <template #default="{ row }">
            <el-input
              :id="`stage-name-input-${row.code}`"
              v-model="row.name"
              placeholder="請輸入顯示名稱"
              size="small"
            />
          </template>
        </el-table-column>

        <el-table-column prop="color" label="代表色" width="120" align="center">
          <template #default="{ row }">
            <div class="color-picker-cell">
              <el-color-picker v-model="row.color" size="small" :show-alpha="false" />
            </div>
          </template>
        </el-table-column>

        <el-table-column prop="isActive" label="啟用狀態" width="120" align="center">
          <template #default="{ row }">
            <el-switch
              :id="`stage-active-switch-${row.code}`"
              v-model="row.isActive"
              active-text="啟用"
              inactive-text="停用"
              inline-prompt
            />
          </template>
        </el-table-column>

        <el-table-column label="排序操作" width="140" align="center">
          <template #default="{ $index }">
            <el-button-group size="small">
              <el-button
                :disabled="$index === 0"
                @click="moveStage($index, -1)"
              >
                ↑
              </el-button>
              <el-button
                :disabled="$index === stages.length - 1"
                @click="moveStage($index, 1)"
              >
                ↓
              </el-button>
            </el-button-group>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 新增階段對話框 -->
    <el-dialog
      v-model="addDialogVisible"
      title="新增專案階段"
      width="400px"
      append-to-body
    >
      <el-form label-position="top">
        <el-form-item label="階段代號 (Code)">
          <el-input id="input-new-code" v-model="newStage.code" placeholder="例：Pilot" />
        </el-form-item>
        <el-form-item label="階段名稱 (Name)">
          <el-input id="input-new-name" v-model="newStage.name" placeholder="例：小批量試產" />
        </el-form-item>
        <el-form-item label="代表顏色">
          <el-color-picker v-model="newStage.color" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="addDialogVisible = false">取消</el-button>
        <el-button id="btn-confirm-add" type="primary" @click="confirmAddStage">確定新增</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';

export interface StageConfigItem {
  code: string;
  name: string;
  order: number;
  color: string;
  isActive: boolean;
  description?: string;
}

const { t } = useI18n();

const stages = ref<StageConfigItem[]>([]);
const loading = ref(false);
const saving = ref(false);
const saveMessage = ref('');
const addDialogVisible = ref(false);

const newStage = ref<StageConfigItem>({
  code: '',
  name: '',
  order: 0,
  color: '#409EFF',
  isActive: true,
});

async function fetchStages() {
  loading.value = true;
  saveMessage.value = '';
  try {
    const res = await fetch('/api/settings/stages');
    if (res.ok) {
      stages.value = await res.json();
    } else {
      loadDefaults();
    }
  } catch (err) {
    loadDefaults();
  } finally {
    loading.value = false;
  }
}

function loadDefaults() {
  stages.value = [
    { code: 'Concept', name: '概念', order: 1, color: '#909399', isActive: true },
    { code: 'Feasibility', name: '可行性', order: 2, color: '#E6A23C', isActive: true },
    { code: 'EVT', name: '工程驗證', order: 3, color: '#409EFF', isActive: true },
    { code: 'DVT', name: '設計驗證', order: 4, color: '#67C23A', isActive: true },
    { code: 'PVT', name: '生產驗證', order: 5, color: '#E6A23C', isActive: true },
    { code: 'MP', name: '量產', order: 6, color: '#909399', isActive: true },
  ];
}

async function saveStages() {
  saving.value = true;
  saveMessage.value = '';
  try {
    // 重新編定 Order
    stages.value.forEach((s, idx) => {
      s.order = idx + 1;
    });

    const res = await fetch('/api/settings/stages', {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(stages.value),
    });

    if (res.ok) {
      stages.value = await res.json();
      saveMessage.value = '階段與關卡設定已成功儲存！';
    } else {
      saveMessage.value = '儲存失敗，請檢查網路連線或伺服器回應。';
    }
  } catch (err) {
    saveMessage.value = '儲存發生錯誤。';
  } finally {
    saving.value = false;
  }
}

function moveStage(index: number, direction: number) {
  const targetIndex = index + direction;
  if (targetIndex < 0 || targetIndex >= stages.value.length) return;
  const temp = stages.value[index];
  stages.value[index] = stages.value[targetIndex];
  stages.value[targetIndex] = temp;
}

function handleAddStage() {
  newStage.value = {
    code: '',
    name: '',
    order: stages.value.length + 1,
    color: '#409EFF',
    isActive: true,
  };
  addDialogVisible.value = true;
}

function confirmAddStage() {
  if (!newStage.value.code || !newStage.value.name) return;
  stages.value.push({ ...newStage.value, order: stages.value.length + 1 });
  addDialogVisible.value = false;
}

onMounted(() => {
  fetchStages();
});
</script>

<style scoped>
.stage-settings-container {
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
  gap: 8px;
}
.stage-order {
  font-weight: 600;
  color: #909399;
}
.color-picker-cell {
  display: flex;
  justify-content: center;
  align-items: center;
}
</style>


