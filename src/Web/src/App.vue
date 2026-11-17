<template>
  <div class="app-container">
    <!-- Login View -->
    <div v-if="!authStore.isAuthenticated || route.path === '/login'" class="login-wrapper">
      <el-card class="login-card">
        <template #header>
          <div class="card-header">
            <h2>{{ t('app.title') }}</h2>
            <div class="lang-toggle">
              <el-button-group size="small">
                <el-button
                  id="login-lang-zh"
                  :type="currentLocale === 'zh-TW' ? 'primary' : 'default'"
                  @click="changeLocale('zh-TW')"
                >
                  繁中
                </el-button>
                <el-button
                  id="login-lang-en"
                  :type="currentLocale === 'en-US' ? 'primary' : 'default'"
                  @click="changeLocale('en-US')"
                >
                  EN
                </el-button>
              </el-button-group>
            </div>
          </div>
        </template>

        <el-form @submit.prevent="handleLogin">
          <el-form-item :label="t('auth.username')">
            <el-input id="username-input" v-model="loginForm.username" autocomplete="username" />
          </el-form-item>
          <el-form-item :label="t('auth.password')">
            <el-input id="password-input" v-model="loginForm.password" type="password" autocomplete="current-password" />
          </el-form-item>
          <p v-if="errorMessage" class="error-msg">{{ errorMessage }}</p>
          <p class="demo-hint">{{ t('auth.demoHint') }}</p>

          <el-button id="login-button" type="primary" native-type="submit" style="width: 100%">
            {{ t('auth.login') }}
          </el-button>

          <el-button style="width: 100%; margin-top: 10px; margin-left: 0" @click="handleOidcLogin">
            {{ t('auth.loginOidc') }}
          </el-button>
        </el-form>
      </el-card>
    </div>

    <!-- Main Shell Layout -->
    <el-container v-else class="layout-container">
      <el-header class="app-header">
        <div class="header-left">
          <span class="logo-text">{{ t('app.title') }}</span>
        </div>
        <div class="header-right">
          <!-- View Mode & Motion Toggles () -->
          <div class="view-controls" style="margin-right: 16px; display: flex; align-items: center; gap: 8px;">
            <el-button
              id="btn-view-mode-toggle"
              size="small"
              :type="viewModes.currentView.value === 'overview' ? 'warning' : 'default'"
              @click="viewModes.toggleViewMode"
            >
              {{ viewModes.currentView.value === 'overview' ? '總覽模式 (V)' : '輕鬆模式 (V)' }}
            </el-button>

            <el-button
              v-if="viewModes.companyAllowMotion.value"
              id="btn-motion-toggle"
              size="small"
              :type="viewModes.effectiveMotion.value === 'on' ? 'success' : 'info'"
              @click="viewModes.toggleMotion"
            >
              {{ viewModes.effectiveMotion.value === 'on' ? '動畫: 開' : '動畫: 關' }}
            </el-button>

            <el-tag
              v-if="viewModes.showSystemReducedMotionNotice.value"
              id="system-reduced-notice"
              size="small"
              type="danger"
              effect="plain"
            >
              系統設定減少動態
            </el-tag>
          </div>

          <el-button-group size="small" style="margin-right: 16px">
            <el-button
              id="header-lang-zh"
              :type="currentLocale === 'zh-TW' ? 'primary' : 'default'"
              @click="changeLocale('zh-TW')"
            >
              繁中
            </el-button>
            <el-button
              id="header-lang-en"
              :type="currentLocale === 'en-US' ? 'primary' : 'default'"
              @click="changeLocale('en-US')"
            >
              EN
            </el-button>
          </el-button-group>
          <span class="user-greeting">{{ authStore.user }}</span>
          <el-button id="logout-button" size="small" @click="handleLogout">{{ t('auth.logout') }}</el-button>
        </div>
      </el-header>

      <el-container class="body-container">
        <el-aside width="200px" class="app-aside">
          <el-menu :default-active="route.path === '/demo' ? '3' : (route.path === '/settings/stages' ? '4' : (route.path === '/import/history' ? '5' : '1'))" class="aside-menu">
            <el-menu-item index="1" @click="router.push('/')">
              <span>{{ t('nav.home') }}</span>
            </el-menu-item>
            <el-menu-item index="2">
              <span>{{ t('nav.projects') }}</span>
            </el-menu-item>
            <el-menu-item id="nav-demo" index="3" @click="router.push('/demo')">
              <span>{{ t('nav.demo') }}</span>
            </el-menu-item>
            <el-menu-item id="nav-stage-settings" index="4" @click="router.push('/settings/stages')">
              <span>{{ t('nav.stageSettings', '階段與關卡設定') }}</span>
            </el-menu-item>
            <el-menu-item id="nav-import-history" index="5" @click="router.push('/import/history')">
              <span>{{ t('nav.importHistory', '匯入紀錄') }}</span>
            </el-menu-item>
            <el-menu-item id="nav-overview" index="6" @click="router.push('/overview')">
              <span>{{ t('nav.overview', '全部專案') }}</span>
            </el-menu-item>
          </el-menu>
        </el-aside>

        <el-main class="app-main">
          <el-breadcrumb v-if="!route.path.startsWith('/p/') && route.path !== '/overview'" separator="/" class="app-breadcrumb">
            <el-breadcrumb-item :to="{ path: '/' }">{{ t('breadcrumb.home') }}</el-breadcrumb-item>
            <el-breadcrumb-item v-if="route.path === '/demo'">{{ t('breadcrumb.demo') }}</el-breadcrumb-item>
            <el-breadcrumb-item v-if="route.path === '/settings/stages'">{{ t('nav.stageSettings', '階段與關卡設定') }}</el-breadcrumb-item>
            <el-breadcrumb-item v-if="route.path === '/import/history'">{{ t('nav.importHistory', '匯入紀錄') }}</el-breadcrumb-item>
          </el-breadcrumb>

          <div v-if="route.path === '/import/history'" class="content-box">
            <ImportHistory />
          </div>

          <div v-else-if="route.path === '/settings/stages'" class="content-box">
            <StageSettings />
          </div>

          <div v-else-if="route.path === '/demo'" class="content-box">
            <h1 id="demo-title" class="home-title">{{ t('demo.title') }}</h1>
            <p>{{ t('demo.description') }}</p>

            <el-button id="ping-button" type="primary" :loading="pingLoading" @click="handlePing">
              {{ t('demo.pingButton') }}
            </el-button>

            <div v-if="pingData" id="ping-result" class="ping-result" style="margin-top: 16px;">
              <p><strong>{{ t('demo.messageLabel') }}:</strong> <span id="ping-msg">{{ pingData.message }}</span></p>
              <p><strong>{{ t('demo.timestampLabel') }}:</strong> <span id="ping-time">{{ pingData.timestamp }}</span></p>
            </div>
            <p v-if="pingError" id="ping-error" class="error-msg" style="margin-top: 16px;">{{ pingError }}</p>
          </div>

          <div v-else-if="route.path === '/overview' || route.path.startsWith('/p/')" class="content-box">
            <router-view />
          </div>

          <div v-else class="content-box">
            <h1 id="home-title" class="home-title">{{ t('app.title') }}</h1>
            <p>{{ t('auth.welcome') }}, {{ authStore.user }}!</p>

            <!-- Stage Timeline Component -->
            <div id="project-timeline-section" style="margin-top: 20px;">
              <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
                <h3 id="timeline-project-title" style="margin: 0; font-size: 16px; color: #303133;">
                  {{ activeProject ? `${activeProject.projectCode} ${activeProject.projectName}` : '專案關卡時間軸' }}
                </h3>
                <span v-if="activeProject" style="font-size: 13px; color: #909399;">
                  PM: {{ activeProject.pmPersonCode }} ｜ 總延誤：
                  <b :style="{ color: activeProject.totalDelayDays > 0 ? '#f56c6c' : '#67c23a' }">
                    {{ activeProject.totalDelayDays }} 天
                  </b>
                </span>
              </div>
              <StageTimeline
                v-if="activeProject && activeProject.stages"
                :stages="activeProject.stages"
                :current-stage-code="activeProject.currentStageCode"
              />
            </div>

            <!-- Animated Number Demo Component -->
            <div id="demo-animated-number-section" style="margin-top: 24px; padding: 16px; background: #fafafa; border-radius: 4px; border: 1px solid #ebeef5;">
              <span style="font-weight: 500; margin-right: 8px;">示範動畫數字元件：</span>
              <AnimatedNumber id="demo-animated-number" :value="100" style="font-size: 22px; font-weight: bold; color: #409eff;" />
            </div>

            <div v-if="demoProjects.length" id="demo-projects-section" style="margin-top: 24px;">
              <h3 id="projects-title">{{ t('demo.projectsTitle') }}</h3>
              <el-card shadow="never" style="margin-top: 10px;">
                <ul id="demo-projects-list" style="margin: 0; padding-left: 20px;">
                  <li v-for="name in demoProjects" :key="name" class="project-name-item">{{ name }}</li>
                </ul>
              </el-card>
            </div>
          </div>
        </el-main>
      </el-container>
    </el-container>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref, watch, onMounted, onUnmounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import { useAuthStore } from './auth';
import StageSettings from './views/settings/StageSettings.vue';
import ImportHistory from './views/import/ImportHistory.vue';
import AnimatedNumber from './components/AnimatedNumber.vue';
import StageTimeline from './components/StageTimeline.vue';
import { useViewModes } from './composables/useViewModes';
import type { paths, components } from './api/schema';

// Strongly-typed model definitions generated from backend OpenAPI
export type PingResponse = components['schemas']['PingResponse'];
export type DemoProjectsResponse = components['schemas']['DemoProjectsResponse'];
export type PingEndpoint = keyof paths;

const route = useRoute();
const router = useRouter();
const { t, locale } = useI18n();
const authStore = useAuthStore();
const viewModes = useViewModes();

const currentLocale = ref(locale.value);
const errorMessage = ref('');
const loginForm = reactive({
  username: '',
  password: '',
});

const demoProjects = ref<string[]>([]);
const activeProject = ref<any>(null);
const pingLoading = ref(false);
const pingError = ref('');
const pingData = ref<PingResponse | null>(null);

async function fetchProjects() {
  try {
    const res = await fetch('/api/demo/projects');
    if (res.ok) {
      const data: DemoProjectsResponse = await res.json();
      demoProjects.value = data.projects ?? [];
    }
  } catch (err) {
    console.error('Failed to load demo projects', err);
  }

  try {
    const resProj = await fetch('/api/projects');
    if (resProj.ok) {
      const projs = await resProj.json();
      if (Array.isArray(projs) && projs.length > 0) {
        activeProject.value = projs[0];
      }
    }
  } catch (err) {
    console.error('Failed to load project details', err);
  }
}

async function handlePing(): Promise<PingResponse | null> {
  const endpoint: PingEndpoint = '/api/demo/ping';
  pingLoading.value = true;
  pingError.value = '';
  try {
    const res = await fetch(endpoint);
    if (!res.ok) {
      throw new Error(`Request failed with status ${res.status}`);
    }
    const data: paths['/api/demo/ping']['get']['responses'][200]['content']['application/json'] = await res.json();
    pingData.value = data;
    return data;
  } catch (err: unknown) {
    pingError.value = err instanceof Error ? err.message : String(err);
    return null;
  } finally {
    pingLoading.value = false;
  }
}

function changeLocale(val: string) {
  locale.value = val as 'zh-TW' | 'en-US';
  if (typeof document !== 'undefined') {
    document.documentElement.lang = val;
  }
}

function handleLogin() {
  errorMessage.value = '';
  const success = authStore.loginDev(loginForm.username, loginForm.password);
  if (success) {
    router.push('/');
  } else {
    errorMessage.value = t('auth.failed');
  }
}

async function handleOidcLogin() {
  await authStore.loginOidc();
}

function handleLogout() {
  authStore.logout();
  router.push('/login');
}

onMounted(() => {
  viewModes.init();
});

onUnmounted(() => {
  viewModes.destroy();
});

watch(
  () => authStore.isAuthenticated,
  (isAuth) => {
    if (!isAuth && route.path !== '/login' && route.path !== '/callback') {
      if (authStore.authMode === 'oidc') {
        authStore.loginOidc();
      } else {
        router.push('/login');
      }
    } else if (isAuth) {
      fetchProjects();
    }
  },
  { immediate: true }
);
</script>

<style>
/* Global motion control:
   [data-motion="off"] disables all CSS animations and transitions instantly */
[data-motion="off"] *,
[data-motion="off"] *::before,
[data-motion="off"] *::after {
  transition-duration: 0s !important;
  transition-delay: 0s !important;
  animation-duration: 0s !important;
  animation-delay: 0s !important;
}

html:not([data-motion='on']) *,
html:not([data-motion='on']) *::before,
html:not([data-motion='on']) *::after {
  animation: none !important;
  transition: none !important;
}

body { margin: 0; background-color: #f5f7fa; font-family: sans-serif; }
.app-container { min-height: 100vh; display: flex; flex-direction: column; }
.login-wrapper { display: flex; justify-content: center; align-items: center; min-height: 100vh; background-color: #eceff4; }
.login-card { width: 400px; }
.card-header { display: flex; justify-content: space-between; align-items: center; }
.error-msg { color: #f56c6c; font-size: 13px; margin: 5px 0; }
.demo-hint { color: #909399; font-size: 12px; margin-bottom: 12px; }
.layout-container { min-height: 100vh; }
.app-header { background-color: #2b3a4a; color: #fff; display: flex; justify-content: space-between; align-items: center; padding: 0 20px; height: 60px; }
.header-right { display: flex; align-items: center; }
.user-greeting { margin-right: 12px; font-size: 14px; }
.body-container { min-height: calc(100vh - 60px); }
.app-aside { background-color: #ffffff; border-right: 1px solid #dcdfe6; }
.aside-menu { border-right: none; }
.app-main { padding: 20px; }
.app-breadcrumb { margin-bottom: 20px; }
.content-box { background-color: #ffffff; padding: 24px; border-radius: 4px; box-shadow: 0 2px 8px rgba(0, 0, 0, 0.05); }
.home-title { margin-top: 0; font-size: 24px; color: #303133; }
</style>


