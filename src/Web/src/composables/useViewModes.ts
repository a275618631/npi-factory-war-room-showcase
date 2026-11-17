import { ref, computed, watch, onMounted, onUnmounted } from 'vue';
import { usePreferredReducedMotion } from '@vueuse/core';

export function useViewModes() {
  const currentView = ref<'relaxed' | 'overview'>('relaxed');
  const userMotionPreference = ref<'on' | 'off' | 'unset'>('unset');
  const companyAllowMotion = ref<boolean>(true);
  const companyMotionDefault = ref<boolean>(true);
  const companyAllowSwitch = ref<boolean>(true);

  let preferredReducedMotion: any;
  try {
    preferredReducedMotion = usePreferredReducedMotion();
  } catch {
    preferredReducedMotion = ref<'no-preference' | 'reduce'>('no-preference');
  }

  // Priority (docs/VIEW_MODES.md 4.2):
  // 1. Overview mode: always off
  // 2. Company allow motion is false: always off
  // 3. User choice: on / off
  // 4. System prefers-reduced-motion: off if reduce
  // 5. Company motion default: on / off
  const effectiveMotion = computed<'on' | 'off'>(() => {
    if (currentView.value === 'overview') {
      return 'off';
    }
    if (!companyAllowMotion.value) {
      return 'off';
    }
    if (userMotionPreference.value === 'on') {
      return 'on';
    }
    if (userMotionPreference.value === 'off') {
      return 'off';
    }
    if (preferredReducedMotion?.value === 'reduce') {
      return 'off';
    }
    return companyMotionDefault.value ? 'on' : 'off';
  });

  const showSystemReducedMotionNotice = computed(() => {
    return preferredReducedMotion?.value === 'reduce' && userMotionPreference.value === 'on';
  });

  function updateDomAttributes() {
    if (typeof document !== 'undefined' && document.documentElement) {
      document.documentElement.setAttribute('data-view', currentView.value);
      document.documentElement.setAttribute('data-motion', effectiveMotion.value);
    }
  }

  async function loadUserPreference() {
    try {
      const res = await fetch('/api/user-preference');
      if (res.ok) {
        const data = await res.json();
        if (data.viewMode === 'overview' || data.viewMode === 'relaxed') {
          // If query param ?view= exists, it takes precedence but doesn't overwrite DB
          if (typeof window !== 'undefined') {
            const params = new URLSearchParams(window.location.search);
            const viewParam = params.get('view');
            if (viewParam === 'overview' || viewParam === 'relaxed') {
              currentView.value = viewParam;
            } else {
              currentView.value = data.viewMode;
            }
          } else {
            currentView.value = data.viewMode;
          }
        }
        if (data.motion === 'on' || data.motion === 'off' || data.motion === 'unset') {
          userMotionPreference.value = data.motion;
        }
      }
    } catch (err) {
      console.warn('Failed to fetch user preference', err);
    }
  }

  async function savePreference(partial: { viewMode?: string; motion?: string; lastProjectCode?: string }) {
    try {
      await fetch('/api/user-preference', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(partial),
      });
    } catch (err) {
      console.warn('Failed to save user preference', err);
    }
  }

  function setViewMode(mode: 'relaxed' | 'overview', persist = true) {
    currentView.value = mode;
    if (persist) {
      savePreference({ viewMode: mode });
    }
  }

  function toggleViewMode() {
    if (!companyAllowSwitch.value) return;
    const nextMode = currentView.value === 'relaxed' ? 'overview' : 'relaxed';
    setViewMode(nextMode, true);
  }

  function setMotion(motion: 'on' | 'off' | 'unset', persist = true) {
    userMotionPreference.value = motion;
    if (persist) {
      savePreference({ motion });
    }
  }

  function toggleMotion() {
    const nextMotion = effectiveMotion.value === 'on' ? 'off' : 'on';
    setMotion(nextMotion, true);
  }

  function handleKeydown(e: KeyboardEvent) {
    if (e.key === 'v' || e.key === 'V') {
      const target = e.target as HTMLElement | null;
      if (target) {
        const tagName = target.tagName ? target.tagName.toLowerCase() : '';
        if (tagName === 'input' || tagName === 'textarea' || tagName === 'select' || target.isContentEditable) {
          return;
        }
      }
      toggleViewMode();
    }
  }

  watch([currentView, effectiveMotion], () => {
    updateDomAttributes();
  }, { immediate: true });

  function init() {
    if (typeof window !== 'undefined') {
      const params = new URLSearchParams(window.location.search);
      const viewParam = params.get('view');
      if (viewParam === 'overview' || viewParam === 'relaxed') {
        currentView.value = viewParam;
      }
      window.addEventListener('keydown', handleKeydown);
    }
    updateDomAttributes();
    loadUserPreference();
  }

  function destroy() {
    if (typeof window !== 'undefined') {
      window.removeEventListener('keydown', handleKeydown);
    }
  }

  return {
    currentView,
    userMotionPreference,
    companyAllowMotion,
    companyMotionDefault,
    companyAllowSwitch,
    preferredReducedMotion,
    effectiveMotion,
    showSystemReducedMotionNotice,
    setViewMode,
    toggleViewMode,
    setMotion,
    toggleMotion,
    init,
    destroy,
    updateDomAttributes,
    loadUserPreference,
  };
}


