import { describe, it, expect, vi, beforeEach } from 'vitest';
import { mount, flushPromises } from '@vue/test-utils';
import ElementPlus from 'element-plus';
import ImportHistory from '../../src/views/import/ImportHistory.vue';
import { i18n } from '../../src/main';

describe('Import History Component', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('renders batch list table and shows upload button', async () => {
    const mockBatches = [
      {
        id: '11111111-1111-1111-1111-111111111111',
        tableName: 'stg_person',
        fileName: 'person_2026.csv',
        status: '成功',
        totalRows: 10,
        successRows: 10,
        failedRows: 0,
        importMethod: 'CSV 上傳',
        createdAt: '2026-11-04T08:00:00Z',
      },
      {
        id: '22222222-2222-2222-2222-222222222222',
        tableName: 'stg_org_unit',
        fileName: 'org_err.xlsx',
        status: '失敗',
        totalRows: 5,
        successRows: 3,
        failedRows: 2,
        importMethod: 'Excel 上傳',
        createdAt: '2026-11-04T09:00:00Z',
      },
    ];

    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => mockBatches,
    });

    const wrapper = mount(ImportHistory, {
      global: {
        plugins: [ElementPlus, i18n],
      },
    });

    await flushPromises();

    expect(wrapper.find('#import-history-title').exists()).toBe(true);
    expect(wrapper.find('#btn-upload-file').exists()).toBe(true);
    expect(wrapper.text()).toContain('person_2026.csv');
    expect(wrapper.text()).toContain('org_err.xlsx');
    expect(wrapper.text()).toContain('下載 Excel');
  });

  it('opens error dialog when clicking detail button', async () => {
    const mockBatches = [
      {
        id: '22222222-2222-2222-2222-222222222222',
        tableName: 'stg_org_unit',
        fileName: 'org_err.xlsx',
        status: '失敗',
        totalRows: 5,
        successRows: 3,
        failedRows: 2,
        importMethod: 'Excel 上傳',
        createdAt: '2026-11-04T09:00:00Z',
      },
    ];

    const mockDetail = {
      batch: mockBatches[0],
      errors: [
        {
          id: 'err-1',
          importBatchId: '22222222-2222-2222-2222-222222222222',
          rowNumber: 2,
          fieldName: 'unit_code',
          originalValue: '',
          reason: '部門代碼必填',
          suggestion: '請補填',
          createdAt: '2026-11-04T09:00:00Z',
        },
      ],
    };

    global.fetch = vi.fn().mockImplementation((url) => {
      if (url.includes('/api/import/batches/22222222-2222-2222-2222-222222222222')) {
        return Promise.resolve({
          ok: true,
          json: async () => mockDetail,
        });
      }
      return Promise.resolve({
        ok: true,
        json: async () => mockBatches,
      });
    });

    const wrapper = mount(ImportHistory, {
      global: {
        plugins: [ElementPlus, i18n],
      },
    });

    await flushPromises();

    const detailBtn = wrapper.find('#btn-view-errors-22222222-2222-2222-2222-222222222222');
    expect(detailBtn.exists()).toBe(true);
    await detailBtn.trigger('click');
    await flushPromises();

    expect(wrapper.text()).toContain('部門代碼必填');
  });
});


