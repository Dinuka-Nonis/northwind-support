import { createContext, useContext } from 'react';
import type { CurrentUser, LoginPayload } from '../types/api';

export interface AuthContextValue {
  /** The signed-in agent, or null when nobody is. */
  user: CurrentUser | null;
  isAuthenticated: boolean;
  /** Signs in, or throws the API's {@link ApiError} (a 401 for wrong credentials). */
  login: (payload: LoginPayload) => Promise<void>;
  /** Clears the session. Route guards then send the user to the login page. */
  logout: () => void;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext);

  if (!value) {
    throw new Error('useAuth must be used inside an <AuthProvider>.');
  }

  return value;
}
