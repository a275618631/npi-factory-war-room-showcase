import { defineStore } from 'pinia';
import { UserManager, WebStorageStateStore } from 'oidc-client-ts';

// OIDC UserManager configuration
export const oidcUserManager = new UserManager({
  authority: import.meta.env.VITE_OIDC_AUTHORITY || 'http://localhost:5000',
  client_id: import.meta.env.VITE_OIDC_CLIENT_ID || 'npi-web',
  redirect_uri: typeof window !== 'undefined' ? `${window.location.origin}/callback` : 'http://localhost:5173/callback',
  response_type: 'code',
  scope: 'openid profile email roles npi',
  userStore: typeof window !== 'undefined' ? new WebStorageStateStore({ store: window.sessionStorage }) : undefined,
});

// Pinia Auth Store
export const useAuthStore = defineStore('auth', {
  state: () => ({
    user: (typeof window !== 'undefined' ? sessionStorage.getItem('auth_user') : null) as string | null,
    authMode: import.meta.env.VITE_AUTH_MODE || 'oidc',
  }),
  getters: {
    isAuthenticated: (state) => Boolean(state.user),
  },
  actions: {
    loginDev(username: string, password: string): boolean {
      const expectedUser = import.meta.env.VITE_DEMO_USER || 'demo';
      const expectedPass = import.meta.env.VITE_DEMO_PASSWORD;
      if (!expectedPass) return false;
      if (username === expectedUser && password === expectedPass) {
        this.user = username;
        if (typeof window !== 'undefined') {
          sessionStorage.setItem('auth_user', username);
        }
        return true;
      }
      return false;
    },
    async loginOidc(): Promise<void> {
      if (
        typeof window === 'undefined' ||
        import.meta.env.MODE === 'test' ||
        (typeof process !== 'undefined' && process.env?.NODE_ENV === 'test')
      ) {
        return;
      }
      try {
        await oidcUserManager.signinRedirect();
      } catch (err) {
        console.warn('OIDC redirect attempt:', err);
      }
    },
    logout(): void {
      this.user = null;
      if (typeof window !== 'undefined') {
        sessionStorage.removeItem('auth_user');
      }
    },
  },
});



