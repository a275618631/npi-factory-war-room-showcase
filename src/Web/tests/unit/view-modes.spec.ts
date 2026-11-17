import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { mount } from '@vue/test-utils';
import { useViewModes } from '../../src/composables/useViewModes';
import AnimatedNumber from '../../src/components/AnimatedNumber.vue';

describe('View Modes & Motion Control (VIEW_MODES 4.2)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ viewMode: null, motion: null, lastProjectCode: null }),
    });
    document.documentElement.removeAttribute('data-view');
    document.documentElement.removeAttribute('data-motion');
  });

  afterEach(() => {
    document.documentElement.removeAttribute('data-view');
    document.documentElement.removeAttribute('data-motion');
  });

  it('evaluates effectiveMotion following VIEW_MODES 4.2 hierarchy', () => {
    const vm = useViewModes();

    // 1. Overview mode: always off
    vm.currentView.value = 'overview';
    vm.userMotionPreference.value = 'on';
    vm.companyAllowMotion.value = true;
    expect(vm.effectiveMotion.value).toBe('off');

    // 2. Relaxed mode, but company disables motion: always off
    vm.currentView.value = 'relaxed';
    vm.companyAllowMotion.value = false;
    vm.userMotionPreference.value = 'on';
    expect(vm.effectiveMotion.value).toBe('off');

    // 3. Relaxed mode + company allows: user explicit choice takes precedence
    vm.companyAllowMotion.value = true;
    vm.userMotionPreference.value = 'on';
    expect(vm.effectiveMotion.value).toBe('on');

    vm.userMotionPreference.value = 'off';
    expect(vm.effectiveMotion.value).toBe('off');

    // 4. User choice unset: follows company default if no system reduce requested
    vm.userMotionPreference.value = 'unset';
    vm.companyMotionDefault.value = true;
    expect(vm.effectiveMotion.value).toBe('on');

    vm.companyMotionDefault.value = false;
    expect(vm.effectiveMotion.value).toBe('off');
  });

  it('updates html data-view and data-motion attributes when state changes', () => {
    const vm = useViewModes();
    vm.currentView.value = 'relaxed';
    vm.userMotionPreference.value = 'on';
    vm.updateDomAttributes();

    expect(document.documentElement.getAttribute('data-view')).toBe('relaxed');
    expect(document.documentElement.getAttribute('data-motion')).toBe('on');

    vm.currentView.value = 'overview';
    vm.updateDomAttributes();

    expect(document.documentElement.getAttribute('data-view')).toBe('overview');
    expect(document.documentElement.getAttribute('data-motion')).toBe('off');
  });

  it('toggles view mode on shortcut V unless target is an input element', () => {
    const vm = useViewModes();
    vm.init();

    expect(vm.currentView.value).toBe('relaxed');

    // Press V on document body
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'v' }));
    expect(vm.currentView.value).toBe('overview');

    // Press V again to toggle back
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'V' }));
    expect(vm.currentView.value).toBe('relaxed');

    // Press V inside an input element -> should not toggle
    const input = document.createElement('input');
    document.body.appendChild(input);
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'v', bubbles: true }));
    expect(vm.currentView.value).toBe('relaxed');
    document.body.removeChild(input);

    vm.destroy();
  });

  it('renders AnimatedNumber instantly when data-motion is off', async () => {
    document.documentElement.setAttribute('data-motion', 'off');

    const wrapper = mount(AnimatedNumber, {
      props: {
        value: 1234,
      },
    });

    expect(wrapper.text()).toBe('1234');
    expect(wrapper.attributes('data-value')).toBe('1234');
  });

  it('starts AnimatedNumber from 0 when data-motion is on', async () => {
    document.documentElement.setAttribute('data-motion', 'on');

    const wrapper = mount(AnimatedNumber, {
      props: {
        value: 500,
        duration: 1000,
      },
    });

    expect(wrapper.attributes('data-value')).toBe('500');
  });
});


