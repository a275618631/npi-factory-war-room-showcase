import { createApp } from 'vue';
import { createPinia } from 'pinia';
import { createRouter, createWebHistory } from 'vue-router';
import { createI18n } from 'vue-i18n';
import ElementPlus from 'element-plus';
import 'element-plus/dist/index.css';
import { useAuthStore, oidcUserManager } from './auth';
import App from './App.vue';
import L0Overview from './views/deep/L0Overview.vue';
import L1Project from './views/deep/L1Project.vue';
import L2Stage from './views/deep/L2Stage.vue';
import L3Unit from './views/deep/L3Unit.vue';
import L4Item from './views/deep/L4Item.vue';

export { useAuthStore, oidcUserManager };

// i18n configuration
export const messages = {
  'zh-TW': {
    app: { title: 'NPI 專案管理平台' },
    nav: { home: '首頁', projects: '專案管理', demo: '示範頁', stageSettings: '階段與關卡設定', importHistory: '匯入紀錄' },
    auth: {
      login: '登入',
      logout: '登出',
      username: '帳號',
      password: '密碼',
      loginOidc: '以 SSO / OIDC 登入',
      demoHint: '請使用本機展示環境設定的帳號密碼登入',
      failed: '帳號或密碼錯誤',
      welcome: '歡迎使用',
    },
    breadcrumb: { home: '首頁', demo: '示範頁' },
    demo: {
      title: 'OpenAPI 示範 API',
      description: '透過後端 OpenAPI 產生的型別呼叫示範 API。',
      pingButton: '呼叫 Ping API',
      messageLabel: '訊息',
      timestampLabel: '時間戳記',
      loading: '載入中...',
      projectsTitle: '示範專案清單',
    },
  },
  'en-US': {
    app: { title: 'NPI Project Management Platform' },
    nav: { home: 'Home', projects: 'Projects', demo: 'Demo', stageSettings: 'Stage Settings', importHistory: 'Import History' },
    auth: {
      login: 'Login',
      logout: 'Logout',
      username: 'Username',
      password: 'Password',
      loginOidc: 'Login with SSO / OIDC',
      demoHint: 'Use credentials configured for the local showcase environment',
      failed: 'Invalid username or password',
      welcome: 'Welcome',
    },
    breadcrumb: { home: 'Home', demo: 'Demo' },
    demo: {
      title: 'OpenAPI Demo API',
      description: 'Call the demo API using types generated from backend OpenAPI.',
      pingButton: 'Call Ping API',
      messageLabel: 'Message',
      timestampLabel: 'Timestamp',
      loading: 'Loading...',
      projectsTitle: 'Demo Projects',
    },
  },
};

export const i18n = createI18n({
  legacy: false,
  locale: 'zh-TW',
  fallbackLocale: 'en-US',
  messages,
});

if (typeof window !== 'undefined' && typeof (window as unknown as { stop?: () => void }).stop !== 'function') {
  (window as unknown as { stop: () => void }).stop = () => {};
}

// Router configuration
export const routes = [
  { path: '/', name: 'home', meta: { requiresAuth: true } },
  { path: '/demo', name: 'demo', meta: { requiresAuth: true } },
  { path: '/settings/stages', name: 'stageSettings', meta: { requiresAuth: true } },
  { path: '/import/history', name: 'importHistory', meta: { requiresAuth: true } },
  { path: '/overview', name: 'overview', component: L0Overview, meta: { requiresAuth: true } },
  { path: '/p/:project', name: 'projectDetail', component: L1Project, meta: { requiresAuth: true } },
  { path: '/p/:project/g/:stage', name: 'stageDetail', component: L2Stage, meta: { requiresAuth: true } },
  { path: '/p/:project/g/:stage/u/:unit', name: 'unitDetail', component: L3Unit, meta: { requiresAuth: true } },
  { path: '/p/:project/item/:type/:code', name: 'itemDetail', component: L4Item, meta: { requiresAuth: true } },
  { path: '/login', name: 'login', meta: { requiresAuth: false } },
  { path: '/callback', name: 'callback', meta: { requiresAuth: false } },
];

export const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: { template: '<div></div>' } },
    { path: '/demo', component: { template: '<div></div>' } },
    { path: '/settings/stages', component: { template: '<div></div>' } },
    { path: '/import/history', component: { template: '<div></div>' } },
    { path: '/overview', component: L0Overview },
    { path: '/p/:project', component: L1Project },
    { path: '/p/:project/g/:stage', component: L2Stage },
    { path: '/p/:project/g/:stage/u/:unit', component: L3Unit },
    { path: '/p/:project/item/:type/:code', component: L4Item },
    { path: '/login', component: { template: '<div></div>' } },
    { path: '/callback', component: { template: '<div></div>' } },
  ],
});

export const pinia = createPinia();

router.beforeEach(async (to, _from) => {
  const authStore = useAuthStore(pinia);

  if (to.path === '/callback') {
    if (authStore.authMode === 'oidc') {
      try {
        const user = await oidcUserManager.signinCallback();
        authStore.user = user?.profile?.preferred_username || user?.profile?.name || user?.profile?.sub || 'demo';
        if (typeof window !== 'undefined') {
          sessionStorage.setItem('auth_user', authStore.user);
        }
        return '/';
      } catch (err) {
        console.error('OIDC signin callback error', err);
        return '/login';
      }
    }
    return '/';
  }

  const isAuth = authStore.isAuthenticated;

  if (to.path !== '/login' && !isAuth) {
    if (authStore.authMode === 'oidc') {
      await authStore.loginOidc();
      return false;
    }
    return '/login';
  } else if (to.path === '/login') {
    if (isAuth) {
      return '/';
    }
    if (authStore.authMode === 'oidc') {
      await authStore.loginOidc();
      return false;
    }
  }
  return true;
});

const app = createApp(App);
app.use(pinia);
app.use(router);
app.use(i18n);
app.use(ElementPlus);

if (typeof document !== 'undefined' && document.getElementById('app')) {
  app.mount('#app');
}



