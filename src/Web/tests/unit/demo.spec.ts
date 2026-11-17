import { describe, it, expect, beforeEach, vi } from 'vitest';
import { mount } from '@vue/test-utils';
import ElementPlus from 'element-plus';
import App from '../../src/App.vue';
import { pinia, router, i18n, useAuthStore } from '../../src/main';
import type { components, paths } from '../../src/api/schema';

describe('OpenAPI Demo API and Generated Types', () => {
  beforeEach(async () => {
    sessionStorage.clear();
    const authStore = useAuthStore(pinia);
    authStore.user = 'demo';
    sessionStorage.setItem('auth_user', 'demo');
    await router.push('/demo');
    await router.isReady();
    vi.restoreAllMocks();
  });

  it('should typecheck PingResponse structure matching schema.d.ts', () => {
    type ExpectedPing = components['schemas']['PingResponse'];
    const mockPing: ExpectedPing = {
      message: 'pong',
      timestamp: '2026-11-03T10:00:00.000Z',
    };

    expect(mockPing.message).toBe('pong');
    expect(typeof mockPing.timestamp).toBe('string');

    type PingPath = paths['/api/demo/ping']['get']['responses'][200]['content']['application/json'];
    const pathCheck: PingPath = mockPing;
    expect(pathCheck.message).toBe('pong');
  });

  it('should render demo page and call /api/demo/ping with typed response', async () => {
    const mockData: components['schemas']['PingResponse'] = {
      message: 'pong',
      timestamp: '2026-11-03T10:00:00.000Z',
    };

    const fetchSpy = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => mockData,
    });
    vi.stubGlobal('fetch', fetchSpy);

    const wrapper = mount(App, {
      global: {
        plugins: [pinia, router, i18n, ElementPlus],
      },
    });

    // Check demo title
    expect(wrapper.find('#demo-title').exists()).toBe(true);

    // Click ping button
    const pingButton = wrapper.find('#ping-button');
    expect(pingButton.exists()).toBe(true);
    await pingButton.trigger('click');

    // Wait for async call to settle
    await vi.waitFor(() => {
      expect(wrapper.find('#ping-result').exists()).toBe(true);
    });

    expect(fetchSpy).toHaveBeenCalledWith('/api/demo/ping');
    expect(wrapper.find('#ping-msg').text()).toBe('pong');
    expect(wrapper.find('#ping-time').text()).toBe('2026-11-03T10:00:00.000Z');
  });
});


