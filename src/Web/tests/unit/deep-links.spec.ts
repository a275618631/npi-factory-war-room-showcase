import { describe, it, expect, vi, beforeEach } from 'vitest';
import { mount, flushPromises } from '@vue/test-utils';
import ElementPlus from 'element-plus';
import DeepBreadcrumb from '../../src/components/DeepBreadcrumb.vue';
import L1Project from '../../src/views/deep/L1Project.vue';
import L3Unit from '../../src/views/deep/L3Unit.vue';
import L4Item from '../../src/views/deep/L4Item.vue';

// Mock vue-router
const pushMock = vi.fn();
const backMock = vi.fn();
let mockRoute: any = {
  path: '/p/NPI-2601/g/DVT/u/SQE',
  params: { project: 'NPI-2601', stage: 'DVT', unit: 'SQE', type: 'deliverable', code: 'DVT-PPAP' },
  query: { filter: 'overdue' },
};

vi.mock('vue-router', () => ({
  useRouter: () => ({
    push: pushMock,
    back: backMock,
  }),
  useRoute: () => mockRoute,
}));

describe('Deep Link Routing & Breadcrumb Component', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders all 5 breadcrumb levels and supports clicking back', async () => {
    const items = [
      { level: 'L0', label: '全部專案', path: '/overview' },
      { level: 'L1', label: 'NPI-2601 範例產品 A', path: '/p/NPI-2601' },
      { level: 'L2', label: '設計驗證', path: '/p/NPI-2601/g/DVT' },
      { level: 'L3', label: 'SQE 供應商品質', path: '/p/NPI-2601/g/DVT/u/SQE' },
      { level: 'L4', label: 'DVT-PPAP PPAP 文件', path: '/p/NPI-2601/item/deliverable/DVT-PPAP' },
    ];

    const wrapper = mount(DeepBreadcrumb, {
      props: {
        items,
        preserveQuery: true,
      },
      global: {
        plugins: [ElementPlus],
      },
    });

    // Check all levels exist
    expect(wrapper.find('[data-testid="breadcrumb-l0"]').text()).toContain('全部專案');
    expect(wrapper.find('[data-testid="breadcrumb-l1"]').text()).toContain('NPI-2601 範例產品 A');
    expect(wrapper.find('[data-testid="breadcrumb-l2"]').text()).toContain('設計驗證');
    expect(wrapper.find('[data-testid="breadcrumb-l3"]').text()).toContain('SQE 供應商品質');
    expect(wrapper.find('[data-testid="breadcrumb-label-l4"]').text()).toContain('DVT-PPAP PPAP 文件');

    // Click L2 link -> should navigate to L2 path preserving query
    const l2Link = wrapper.find('[data-testid="breadcrumb-link-l2"]');
    await l2Link.trigger('click');
    expect(pushMock).toHaveBeenCalledWith({
      path: '/p/NPI-2601/g/DVT',
      query: { filter: 'overdue' },
    });
  });

  it('preserves filter query when navigating from L3 to L4 and back', async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        hasPermission: true,
        breadcrumb: [
          { level: 'L0', name: '全部專案', path: '/overview' },
          { level: 'L1', name: 'NPI-2601 範例產品 A', path: '/p/NPI-2601' },
          { level: 'L2', name: '設計驗證', path: '/p/NPI-2601/g/DVT' },
          { level: 'L3', name: 'SQE 供應商品質', path: '/p/NPI-2601/g/DVT/u/SQE' },
        ],
      }),
    });

    const wrapper = mount(L3Unit, {
      global: {
        plugins: [ElementPlus],
      },
    });

    await flushPromises();

    expect(wrapper.find('#active-filter-indicator').text()).toContain('當前篩選：overdue');

    // Click into L4 deliverable
    const gotoBtn = wrapper.find('#btn-goto-l4-deliverable');
    await gotoBtn.trigger('click');

    expect(pushMock).toHaveBeenCalledWith({
      path: '/p/NPI-2601/item/deliverable/DVT-PPAP',
      query: { filter: 'overdue' },
    });

    // Mount L4 and click back
    const l4Wrapper = mount(L4Item, {
      global: {
        plugins: [ElementPlus],
      },
    });

    const backBtn = l4Wrapper.find('#btn-back-to-l3');
    await backBtn.trigger('click');
    expect(backMock).toHaveBeenCalled();
  });

  it('displays permission denied box and hides project details when forbidden', async () => {
    global.fetch = vi.fn().mockResolvedValue({
      status: 403,
      ok: false,
      json: async () => ({
        hasPermission: false,
        errorMessage: '沒有權限',
        breadcrumb: [],
      }),
    });

    const wrapper = mount(L1Project, {
      global: {
        plugins: [ElementPlus],
      },
    });

    await flushPromises();

    expect(wrapper.find('#permission-denied').exists()).toBe(true);
    expect(wrapper.find('#permission-denied').text()).toContain('沒有權限');
    expect(wrapper.text()).not.toContain('範例產品 A');
  });
});


