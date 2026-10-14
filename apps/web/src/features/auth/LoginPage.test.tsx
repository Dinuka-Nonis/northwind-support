import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { authApi } from '../../api/auth';
import { ApiError } from '../../api/client';
import { AuthProvider } from '../../auth/AuthProvider';
import { clearSession } from '../../auth/session';
import type { LoginResponse } from '../../types/api';
import { LoginPage } from './LoginPage';

vi.mock('../../api/auth', () => ({
  authApi: { login: vi.fn() },
}));

const login = vi.mocked(authApi.login);

const signedIn: LoginResponse = {
  token: 'token-1',
  expiresAtUtc: '2026-10-14T10:00:00Z',
  user: { id: 4, fullName: 'Sara Lindqvist', email: 'sara@example.test' },
};

/** The login page at /login, with a stand-in for the page the visitor was sent from. */
function renderLogin(state?: { from: string }) {
  return render(
    <AuthProvider>
      <MemoryRouter initialEntries={[{ pathname: '/login', state }]}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/tickets" element={<p>Ticket list</p>} />
          <Route path="/tickets/5" element={<p>Ticket five</p>} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  );
}

async function fillAndSubmit(email: string, password: string) {
  const user = userEvent.setup();

  if (email) {
    await user.type(screen.getByLabelText('Email'), email);
  }

  if (password) {
    await user.type(screen.getByLabelText('Password'), password);
  }

  await user.click(screen.getByRole('button', { name: 'Sign in' }));
}

describe('LoginPage', () => {
  beforeEach(() => {
    login.mockReset();
    clearSession();
  });

  afterEach(() => {
    clearSession();
  });

  it('labels both fields, so a screen reader announces them', () => {
    renderLogin();

    expect(screen.getByLabelText('Email')).toHaveAttribute('type', 'email');
    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'password');
  });

  it('does not call the API when both fields are empty, and says what is missing', async () => {
    renderLogin();

    await fillAndSubmit('', '');

    expect(login).not.toHaveBeenCalled();
    expect(screen.getByText('Email is required.')).toBeInTheDocument();
    expect(screen.getByText('Password is required.')).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true');
  });

  it('does not call the API for an email of only spaces', async () => {
    renderLogin();

    await fillAndSubmit('   ', 'Local_Dev_Agent1');

    expect(login).not.toHaveBeenCalled();
    expect(screen.getByText('Email is required.')).toBeInTheDocument();
  });

  it('does not call the API for a password over the limit', async () => {
    const user = userEvent.setup();
    renderLogin();

    await user.type(screen.getByLabelText('Email'), 'sara@example.test');
    await user.click(screen.getByLabelText('Password'));
    await user.paste('p'.repeat(129));
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(login).not.toHaveBeenCalled();
    expect(screen.getByText('Password must be at most 128 characters.')).toBeInTheDocument();
  });

  it('shows a clear error when the credentials are rejected, and lets the agent try again', async () => {
    login.mockRejectedValue(new ApiError(401, 'Invalid email or password.'));
    renderLogin();

    await fillAndSubmit('sara@example.test', 'wrong-password');

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.');
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeEnabled();
    expect(screen.queryByText('Ticket list')).not.toBeInTheDocument();
  });

  it('is busy while the request is in flight', async () => {
    login.mockReturnValue(new Promise<LoginResponse>(() => {}));
    renderLogin();

    await fillAndSubmit('sara@example.test', 'Local_Dev_Agent1');

    expect(await screen.findByRole('button', { name: 'Signing in...' })).toBeDisabled();
  });

  it('sends the trimmed email and the password as typed, then goes to the ticket list', async () => {
    login.mockResolvedValue(signedIn);
    renderLogin();

    await fillAndSubmit('  sara@example.test  ', ' pass word ');

    expect(login).toHaveBeenCalledWith({ email: 'sara@example.test', password: ' pass word ' });
    expect(await screen.findByText('Ticket list')).toBeInTheDocument();
  });

  it('returns to the page the visitor wanted before being sent to login', async () => {
    login.mockResolvedValue(signedIn);
    renderLogin({ from: '/tickets/5' });

    await fillAndSubmit('sara@example.test', 'Local_Dev_Agent1');

    expect(await screen.findByText('Ticket five')).toBeInTheDocument();
  });

  it('ignores a remembered address that is not a page of this app', async () => {
    login.mockResolvedValue(signedIn);
    renderLogin({ from: '//evil.example/phish' });

    await fillAndSubmit('sara@example.test', 'Local_Dev_Agent1');

    expect(await screen.findByText('Ticket list')).toBeInTheDocument();
  });
});
