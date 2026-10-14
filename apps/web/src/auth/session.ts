import type { CurrentUser } from '../types/api';

/** Who is signed in and the token that proves it. */
export interface Session {
  token: string;
  user: CurrentUser;
}

/**
 * The session lives in this module variable, in memory only. Nothing is written to
 * localStorage, sessionStorage or a cookie, so a script that runs on the page cannot read a
 * saved token from storage and a copy cannot outlive the tab. The price is that a refresh or
 * a new tab means signing in again; the README states this trade-off.
 */
let current: Session | null = null;

export function getSession(): Session | null {
  return current;
}

export function setSession(session: Session): void {
  current = session;
}

export function clearSession(): void {
  current = null;
}
