import { useMemo, useEffect, useCallback, useState } from 'react';
import { AuthContext } from '../auth-context';
import { JWT_STORAGE_KEY } from './constant';
import { setSession, isValidToken } from './utils';
import { renewSession } from './action';

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);

  const checkSession = useCallback(async () => {
    const token = sessionStorage.getItem(JWT_STORAGE_KEY);

    if (token && isValidToken(token)) {
      setUser({ organizations: readOrgs(), accessToken: token });
      setLoading(false);
      return;
    }

    setSession(null);
    const renewed = await renewSession();
    setUser(renewed ? { organizations: renewed.organizations || [], accessToken: renewed.accessToken } : null);
    setLoading(false);
  }, []);

  useEffect(() => { checkSession(); }, [checkSession]);

  const value = useMemo(() => ({
    user,
    loading,
    authenticated: !!user,
    checkSession,
  }), [user, loading, checkSession]);

  return <AuthContext value={value}>{children}</AuthContext>;
}

function readOrgs() {
  try { return JSON.parse(localStorage.getItem('organizations') || '[]'); }
  catch { return []; }
}