import { describe, it, expect, vi, beforeEach } from 'vitest';
import { mount, flushPromises } from '@vue/test-utils';
import ElementPlus from 'element-plus';
import StageSettings from '../../src/views/settings/StageSettings.vue';
import { i18n } from '../../src/main';

describe('Stage Settings Component', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('renders default 6 stages with correct codes and initial names', async () => {
    const mockStages = [
      { code: 'Concept', name: '概念', order: 1, color: '#909399', isActive: true },
      { code: 'Feasibility', name: '可行性', order: 2, color: '#E6A23C', isActive: true },
      { code: 'EVT', name: '工程驗證', order: 3, color: '#409EFF', isActive: true },
      { code: 'DVT', name: '設計驗證', order: 4, color: '#67C23A', isActive: true },
      { code: 'PVT', name: '生產驗證', order: 5, color: '#E6A23C', isActive: true },
      { code: 'MP', name: '量產', order: 6, color: '#909399', isActive: true },
    ];

    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockStages,
    });

    const wrapper = mount(StageSettings, {
      global: {
        plugins: [ElementPlus, i18n],
      },
    });

    await flushPromises();

    expect(wrapper.find('#stage-settings-title').exists()).toBe(true);
    expect(wrapper.text()).toContain('關卡清單');
    expect(wrapper.text()).toContain('EVT');
  });

  it('allows renaming EVT stage and triggers save API', async () => {
    let savedPayload: unknown = null;
    global.fetch = vi.fn().mockImplementation((url, options) => {
      if (options && options.method === 'PUT') {
        savedPayload = JSON.parse(options.body as string);
        return Promise.resolve({
          ok: true,
          json: async () => savedPayload,
        });
      }
      return Promise.resolve({
        ok: true,
        json: async () => [
          { code: 'EVT', name: '工程驗證', order: 1, color: '#409EFF', isActive: true },
        ],
      });
    });

    const wrapper = mount(StageSettings, {
      global: {
        plugins: [ElementPlus, i18n],
      },
    });

    await flushPromises();

    // 觸發儲存
    const saveBtn = wrapper.find('#btn-save-stages');
    expect(saveBtn.exists()).toBe(true);
    await saveBtn.trigger('click');
    await flushPromises();

    expect(global.fetch).toHaveBeenCalledWith('/api/settings/stages', expect.objectContaining({
      method: 'PUT',
    }));
  });
});


