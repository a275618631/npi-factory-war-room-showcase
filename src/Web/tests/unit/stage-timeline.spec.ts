import { describe, it, expect } from 'vitest';
import { mount } from '@vue/test-utils';
import ElementPlus from 'element-plus';
import StageTimeline from '../../src/components/StageTimeline.vue';

describe('Stage Timeline Component', () => {
  const mockStages = [
    {
      stageCode: 'Concept',
      stageName: '概念',
      plannedStart: '2026-04-01',
      plannedEnd: '2026-05-15',
      actualStart: '2026-04-01',
      actualEnd: '2026-05-15',
      gateDate: '2026-05-18',
      gateOwnerPersonCode: 'E1001',
      delayDays: 0,
      isDelayed: false,
      isCurrent: false,
    },
    {
      stageCode: 'Feasibility',
      stageName: '可行性',
      plannedStart: '2026-05-16',
      plannedEnd: '2026-07-15',
      actualStart: '2026-05-16',
      actualEnd: '2026-07-20',
      gateDate: '2026-07-23',
      gateOwnerPersonCode: 'E1002',
      delayDays: 5,
      isDelayed: true,
      isCurrent: false,
    },
    {
      stageCode: 'EVT',
      stageName: '工程驗證',
      plannedStart: '2026-07-21',
      plannedEnd: '2026-09-30',
      actualStart: '2026-07-21',
      actualEnd: '2026-10-05',
      gateDate: '2026-10-08',
      gateOwnerPersonCode: 'E1003',
      delayDays: 5,
      isDelayed: true,
      isCurrent: false,
    },
    {
      stageCode: 'DVT',
      stageName: '設計驗證',
      plannedStart: '2026-10-06',
      plannedEnd: '2026-12-15',
      actualStart: '2026-10-06',
      actualEnd: null,
      gateDate: '2026-12-18',
      gateOwnerPersonCode: 'E1004',
      delayDays: 0,
      isDelayed: false,
      isCurrent: true,
    },
    {
      stageCode: 'PVT',
      stageName: '生產驗證',
      plannedStart: '2026-12-16',
      plannedEnd: '2027-02-15',
      actualStart: null,
      actualEnd: null,
      gateDate: '2027-02-18',
      gateOwnerPersonCode: 'E1005',
      delayDays: 0,
      isDelayed: false,
      isCurrent: false,
    },
    {
      stageCode: 'MP',
      stageName: '量產',
      plannedStart: '2027-02-16',
      plannedEnd: '2027-04-30',
      actualStart: null,
      actualEnd: null,
      gateDate: '2027-05-05',
      gateOwnerPersonCode: 'E1001',
      delayDays: 0,
      isDelayed: false,
      isCurrent: false,
    },
  ];

  it('renders all 6 stages and highlights current stage DVT', () => {
    const wrapper = mount(StageTimeline, {
      props: {
        stages: mockStages,
        currentStageCode: 'DVT',
      },
      global: {
        plugins: [ElementPlus],
      },
    });

    // Check all stage codes exist
    expect(wrapper.find('[data-stage-code="Concept"]').exists()).toBe(true);
    expect(wrapper.find('[data-stage-code="Feasibility"]').exists()).toBe(true);
    expect(wrapper.find('[data-stage-code="EVT"]').exists()).toBe(true);
    expect(wrapper.find('[data-stage-code="DVT"]').exists()).toBe(true);
    expect(wrapper.find('[data-stage-code="PVT"]').exists()).toBe(true);
    expect(wrapper.find('[data-stage-code="MP"]').exists()).toBe(true);

    // Current stage DVT highlighted
    const dvtNode = wrapper.find('[data-stage-code="DVT"]');
    expect(dvtNode.classes()).toContain('is-current');
    expect(dvtNode.text()).toContain('目前關卡');
  });

  it('displays red delay days for delayed stages Feasibility and EVT', () => {
    const wrapper = mount(StageTimeline, {
      props: {
        stages: mockStages,
        currentStageCode: 'DVT',
      },
      global: {
        plugins: [ElementPlus],
      },
    });

    const feasibilityNode = wrapper.find('[data-stage-code="Feasibility"]');
    expect(feasibilityNode.classes()).toContain('is-delayed');
    expect(feasibilityNode.text()).toContain('延誤 5 天');

    const evtNode = wrapper.find('[data-stage-code="EVT"]');
    expect(evtNode.classes()).toContain('is-delayed');
    expect(evtNode.text()).toContain('延誤 5 天');
  });

  it('reflects updated stage name after renaming EVT to custom name', async () => {
    const renamedStages = mockStages.map((s) => {
      if (s.stageCode === 'EVT') {
        return { ...s, stageName: '工程樣品驗證' };
      }
      return s;
    });

    const wrapper = mount(StageTimeline, {
      props: {
        stages: renamedStages,
        currentStageCode: 'DVT',
      },
      global: {
        plugins: [ElementPlus],
      },
    });

    expect(wrapper.find('[data-testid="stage-name-EVT"]').text()).toBe('工程樣品驗證');
  });
});


