import { getSession } from '../auth/session';
import type { ProblemDetails } from '../types/api';

/**
 * Where the API lives. In development this stays '/api' and the Vite dev server proxies it
 * to the backend; set VITE_API_BASE_URL to point a build somewhere else. See .env.example.
 */
const baseUrl = import.meta.env.VITE_API_BASE_URL ?? '/api';

/** A failed HTTP call, carrying the problem document the API returned. */
export class ApiError extends Error {
  readonly status: number;

  readonly problem?: ProblemDetails;

  constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }

  /** Field name to messages, when the failure was a validation failure. */
  get fieldErrors(): Record<string, string[]> {
    return this.problem?.errors ?? {};
  }
}

export interface RequestOptions extends RequestInit {
  /**
   * Send the request without the session's token, and do not treat a 401 as an expired
   * session. For calls made while signed out, which is only the login itself: a 401 there means
   * wrong credentials, and the login page shows it.
   */
  anonymous?: boolean;
}

type UnauthorizedHandler = () => void;

let onUnauthorized: UnauthorizedHandler | undefined;

/**
 * Registers what happens when the API rejects the session's own token (it expired, or was
 * never valid). The auth provider uses it to clear the session, which sends the user to the
 * login page. Pass undefined to remove it.
 */
export function setUnauthorizedHandler(handler: UnauthorizedHandler | undefined): void {
  onUnauthorized = handler;
}

/**
 * The single place that talks to the API. Attaches the session's token to every call, returns
 * parsed JSON, or throws an {@link ApiError} that callers can show to the user.
 */
export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { anonymous = false, headers, ...init } = options;
  const token = anonymous ? undefined : getSession()?.token;

  const response = await fetch(`${baseUrl}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(headers ?? {}),
    },
  });

  if (!response.ok) {
    // Only when the token this call carried is still the session's token. A slow reply to a
    // call made before a fresh sign-in must not sign the new session out.
    if (response.status === 401 && token && getSession()?.token === token) {
      onUnauthorized?.();
    }

    const problem = await readProblem(response);

    throw new ApiError(
      response.status,
      problem?.detail ?? problem?.title ?? response.statusText,
      problem,
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/** Turns anything thrown by a fetch into a message worth showing a user. */
export function toErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    return error.message;
  }

  if (error instanceof Error) {
    return `${fallback} (${error.message})`;
  }

  return fallback;
}

async function readProblem(response: Response): Promise<ProblemDetails | undefined> {
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    // Not every failure has a JSON body - a dead server certainly will not.
    return undefined;
  }
}
