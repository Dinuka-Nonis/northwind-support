import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from './useAuth';

interface RequireAuthProps {
  children: ReactNode;
}

/**
 * The route guard. A visitor who is not signed in is sent to the login page, and the page they
 * asked for travels along in the navigation state so the login page can bring them back to it.
 * Because it reads the auth state, it also reacts when the session is cleared while the page is
 * open (a logout, or the API answering 401 for an expired token).
 */
export function RequireAuth({ children }: RequireAuthProps) {
  const { isAuthenticated } = useAuth();
  const location = useLocation();

  if (!isAuthenticated) {
    const from = `${location.pathname}${location.search}${location.hash}`;

    return <Navigate to="/login" replace state={{ from }} />;
  }

  return <>{children}</>;
}
