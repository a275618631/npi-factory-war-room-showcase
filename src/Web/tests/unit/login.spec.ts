import { describe, it, expect, beforeEach, vi } from 'vitest';
import { setActivePinia, createPinia } from 'pinia';
import { useAuthStore, i18n, messages } from '../../src/main';

describe('Auth Store and i18n', () => {
  beforeEach(() => {
    vi.stubEnv('VITE_DEMO_PASSWORD', 'unit-test-only');
    setActivePinia(createPinia());
    sessionStorage.clear();
  });

  it('should initialize with unauthenticated state', () => {
    const authStore = useAuthStore();
    expect(authStore.isAuthenticated).toBe(false);
    expect(authStore.user).toBeNull();
  });

  it('should default authMode to oidc', () => {
    const authStore = useAuthStore();
    expect(authStore.authMode).toBe('oidc');
  });

  it('should successfully log in with demo credentials', () => {
    const authStore = useAuthStore();
    const result = authStore.loginDev('demo', 'unit-test-only');
    expect(result).toBe(true);
    expect(authStore.isAuthenticated).toBe(true);
    expect(authStore.user).toBe('demo');
    expect(sessionStorage.getItem('auth_user')).toBe('demo');
  });

  it('should fail to log in with invalid credentials', () => {
    const authStore = useAuthStore();
    const result = authStore.loginDev('demo', 'WrongPassword');
    expect(result).toBe(false);
    expect(authStore.isAuthenticated).toBe(false);
    expect(authStore.user).toBeNull();
  });

  it('should support language switching between zh-TW and en-US', () => {
    expect(messages['zh-TW'].app.title).toBe('NPI 專案管理平台');
    expect(messages['en-US'].app.title).toBe('NPI Project Management Platform');

    i18n.global.locale.value = 'en-US';
    expect(i18n.global.locale.value).toBe('en-US');

    i18n.global.locale.value = 'zh-TW';
    expect(i18n.global.locale.value).toBe('zh-TW');
  });
});




