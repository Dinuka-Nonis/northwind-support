import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { clearSession, setSession } from '../auth/session';
import { ApiError, request, setUnauthorizedHandler } from './client';

const fetchMock = vi.fn<typeof fetch>();

/** The smallest Response the client reads: a status and a JSON body. */
function reply(status: number, body: unknown = {}): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    statusText: '',
    json: () => Promise.resolve(body),
  } as unknown as Response;
}

/** The headers of the request the client last sent. */
function sentHeaders(): Record<string, string> {
  return fetchMock.mock.calls[0][1]?.headers as Record<string, string>;
}

const signIn = (token = 'token-1') =>
  setSession({ token, user: { id: 4, fullName: 'Sara Lindqvist', email: 'sara@example.test' } });

describe('request authentication', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
    fetchMock.mockReset();
    clearSession();
    setUnauthorizedHandler(undefined);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    clearSession();
    setUnauthorizedHandler(undefined);
  });

  it('attaches the token as a bearer header when someone is signed in', async () => {
    fetchMock.mockResolvedValue(reply(200, []));
    signIn('token-1');

    await request('/tickets');

    expect(sentHeaders()).toMatchObject({ Authorization: 'Bearer token-1' });
  });

  it('sends no Authorization header when nobody is signed in', async () => {
    fetchMock.mockResolvedValue(reply(200, []));

    await request('/tickets');

    expect(sentHeaders()).not.toHaveProperty('Authorization');
  });

  it('sends an anonymous request without the token, even when someone is signed in', async () => {
    fetchMock.mockResolvedValue(reply(200, {}));
    signIn();

    await request('/auth/login', { method: 'POST', anonymous: true });

    expect(sentHeaders()).not.toHaveProperty('Authorization');
  });

  it('tells the handler when the API rejects the session token with a 401', async () => {
    const onUnauthorized = vi.fn();
    setUnauthorizedHandler(onUnauthorized);
    fetchMock.mockResolvedValue(reply(401, { title: 'Unauthorized' }));
    signIn();

    await expect(request('/tickets')).rejects.toMatchObject({ status: 401 });

    expect(onUnauthorized).toHaveBeenCalledTimes(1);
  });

  it('does not treat a 401 from the login itself as an expired session', async () => {
    const onUnauthorized = vi.fn();
    setUnauthorizedHandler(onUnauthorized);
    fetchMock.mockResolvedValue(reply(401, { detail: 'Invalid email or password.' }));
    signIn();

    const failure = await request('/auth/login', { method: 'POST', anonymous: true }).catch(
      (caught: unknown) => caught,
    );

    expect(failure).toBeInstanceOf(ApiError);
    expect((failure as ApiError).message).toBe('Invalid email or password.');
    expect(onUnauthorized).not.toHaveBeenCalled();
  });

  it('does not call the handler for a 401 when there was no token to reject', async () => {
    const onUnauthorized = vi.fn();
    setUnauthorizedHandler(onUnauthorized);
    fetchMock.mockResolvedValue(reply(401));

    await expect(request('/tickets')).rejects.toBeInstanceOf(ApiError);

    expect(onUnauthorized).not.toHaveBeenCalled();
  });

  it('ignores a late 401 for a token that is no longer the session token', async () => {
    const onUnauthorized = vi.fn();
    setUnauthorizedHandler(onUnauthorized);
    signIn('old-token');

    fetchMock.mockImplementation(() => {
      // The agent signs in again while the old call is still in flight.
      signIn('new-token');
      return Promise.resolve(reply(401));
    });

    await expect(request('/tickets')).rejects.toBeInstanceOf(ApiError);

    expect(onUnauthorized).not.toHaveBeenCalled();
  });

  it('does not call the handler for other failures', async () => {
    const onUnauthorized = vi.fn();
    setUnauthorizedHandler(onUnauthorized);
    fetchMock.mockResolvedValue(reply(409, { detail: 'Already Critical.' }));
    signIn();

    await expect(request('/tickets/1/escalate')).rejects.toMatchObject({ status: 409 });

    expect(onUnauthorized).not.toHaveBeenCalled();
  });
});
