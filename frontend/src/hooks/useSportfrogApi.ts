import { useEffect, useState } from 'react';
import { apiCheckHealth, clearApiSession, loginToApi, type ApiStatus } from '../services/api';

export function useSportfrogApi() {
  const [status, setStatus] = useState<ApiStatus>('checking');
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(false);

  useEffect(() => {
    let cancelled = false;

    async function check() {
      const token = localStorage.getItem('sportfrog_access_token');
      if (!token) {
        if (!cancelled) {
          setIsAuthenticated(false);
          setStatus('offline');
        }
        return;
      }

      try {
        const online = await apiCheckHealth();
        if (!cancelled) {
          setStatus(online ? 'online' : 'offline');
          setIsAuthenticated(online);
        }
      } catch {
        if (!cancelled) {
          setStatus('offline');
          setIsAuthenticated(false);
        }
      }
    }

    void check();
    return () => {
      cancelled = true;
    };
  }, []);

  return {
    status,
    isAuthenticated,
    login: async (email: string, password: string) => {
      const session = await loginToApi(email, password);
      setIsAuthenticated(true);
      setStatus('online');
      return session;
    },
    logout: () => {
      clearApiSession();
      setIsAuthenticated(false);
      setStatus('offline');
    },
  };
}
