import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { request } from '../api/client';
import { AuthProvider } from './AuthProvider';
import { RequireAuth } from './RequireAuth';
import { clearSession, getSession, setSession } from './session';

/** Shows where the visitor was sent from, the way the login page reads it. */
function LoginProbe() {
  const location = useLocation();
  const from = (location.state as { from?: string } | null)?.from ?? 'nowhere';

  return <p>Login page, wanted: {from}</p>;
}

/** A protected page that makes one API call, so a test can make the API answer 401. */
function TicketPage() {
  return (
    <div>
      <p>Ticket page</p>
      <button type="button" onClick={() => void request('/tickets/5').catch(() => undefined)}>
        Load
      </button>
    </div>
  );
}

function renderAt(path: string) {
  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/login" element={<LoginProbe />} />
          <Route
            path="/tickets/:id"
            element={
              <RequireAuth>
                <TicketPage />
              </RequireAuth>
            }
          />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  );
}

const signIn = () =>
  setSession({ token: 'token-1', user: { id: 4, fullName: 'Sara Lindqvist', email: 'sara@example.test' } });

describe('RequireAuth', () => {
  beforeEach(() => {
    clearSession();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    clearSession();
  });

  it('sends a signed-out visitor to the login page', () => {
    renderAt('/tickets/5');

    expect(screen.getByText(/Login page/)).toBeInTheDocument();
    expect(screen.queryByText('Ticket page')).not.toBeInTheDocument();
  });

  it('remembers the page the visitor wanted, including its query string', () => {
    renderAt('/tickets/5?tab=history');

    expect(screen.getByText('Login page, wanted: /tickets/5?tab=history')).toBeInTheDocument();
  });

  it('shows the page to a signed-in agent', () => {
    signIn();

    renderAt('/tickets/5');

    expect(screen.getByText('Ticket page')).toBeInTheDocument();
    expect(screen.queryByText(/Login page/)).not.toBeInTheDocument();
  });

  it('clears the session and goes to login when the API answers 401 (an expired token)', async () => {
    signIn();
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: false,
        status: 401,
        statusText: 'Unauthorized',
        json: () => Promise.resolve({}),
      }),
    );

    renderAt('/tickets/5');
    await userEvent.setup().click(screen.getByRole('button', { name: 'Load' }));

    expect(await screen.findByText('Login page, wanted: /tickets/5')).toBeInTheDocument();
    expect(getSession()).toBeNull();
  });
});
