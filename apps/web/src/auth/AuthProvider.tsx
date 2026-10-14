import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { authApi } from '../api/auth';
import { setUnauthorizedHandler } from '../api/client';
import type { CurrentUser, LoginPayload } from '../types/api';
import { clearSession, getSession, setSession } from './session';
import { AuthContext, type AuthContextValue } from './useAuth';

interface AuthProviderProps {
  children: ReactNode;
}

/**
 * Holds who is signed in for the React tree, on top of the in-memory session, and gives it to
 * `useAuth`. It also registers what the API client does when the API answers 401: clear the
 * session, which makes `RequireAuth` send the user to the login page.
 */
export function AuthProvider({ children }: AuthProviderProps) {
  const [user, setUser] = useState<CurrentUser | null>(() => getSession()?.user ?? null);

  const logout = useCallback(() => {
    clearSession();
    setUser(null);
  }, []);

  const login = useCallback(async (payload: LoginPayload) => {
    const response = await authApi.login(payload);

    setSession({ token: response.token, user: response.user });
    setUser(response.user);
  }, []);

  useEffect(() => {
    setUnauthorizedHandler(logout);

    return () => setUnauthorizedHandler(undefined);
  }, [logout]);

  const value = useMemo<AuthContextValue>(
    () => ({ user, isAuthenticated: user !== null, login, logout }),
    [user, login, logout],
  );

  return <AuthContext value={value}>{children}</AuthContext>;
}
