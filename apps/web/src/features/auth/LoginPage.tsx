import { useState } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { toErrorMessage } from '../../api/client';
import { useAuth } from '../../auth/useAuth';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Field } from '../../components/Field';

/** The same limits as the API's login validator. */
const EMAIL_MAX_LENGTH = 256;
const PASSWORD_MAX_LENGTH = 128;

const DEFAULT_DESTINATION = '/tickets';

interface LoginErrors {
  email?: string;
  password?: string;
}

/** Checks the form against the API's limits. An empty result means it can be sent. */
function validate(email: string, password: string): LoginErrors {
  const errors: LoginErrors = {};

  if (email.length === 0) {
    errors.email = 'Email is required.';
  } else if (email.length > EMAIL_MAX_LENGTH) {
    errors.email = `Email must be at most ${EMAIL_MAX_LENGTH} characters.`;
  }

  if (password.length === 0) {
    errors.password = 'Password is required.';
  } else if (password.length > PASSWORD_MAX_LENGTH) {
    errors.password = `Password must be at most ${PASSWORD_MAX_LENGTH} characters.`;
  }

  return errors;
}

/**
 * Where to go once signed in: the page the visitor was sent away from, or the ticket list.
 * Only a path inside this app is accepted, so the navigation state can never send someone to
 * another site.
 */
function destinationAfterLogin(state: unknown): string {
  const from = (state as { from?: unknown } | null)?.from;

  const isAppPath =
    typeof from === 'string' &&
    from.startsWith('/') &&
    !from.startsWith('//') &&
    !from.startsWith('/login');

  return isAppPath ? from : DEFAULT_DESTINATION;
}

/**
 * The sign-in form. Checks the input before calling the API, shows a pending state while the
 * request runs, and shows the API's own message when the credentials are rejected. A visitor
 * who is already signed in is passed straight on.
 */
export function LoginPage() {
  const { isAuthenticated, login } = useAuth();
  const location = useLocation();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<LoginErrors>({});
  const [isSaving, setIsSaving] = useState(false);
  const [submitError, setSubmitError] = useState<string>();

  // Also what moves the visitor on after a successful sign-in: the session changes, this page
  // re-renders, and the redirect below runs.
  if (isAuthenticated) {
    return <Navigate to={destinationAfterLogin(location.state)} replace />;
  }

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedEmail = email.trim();
    const validationErrors = validate(trimmedEmail, password);
    setErrors(validationErrors);
    setSubmitError(undefined);

    // Invalid input never reaches the API.
    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSaving(true);

    try {
      // The password is sent as typed: trimming it would change what the agent chose.
      await login({ email: trimmedEmail, password });
    } catch (caught) {
      setSubmitError(toErrorMessage(caught, 'Could not sign in. Please try again.'));
      setIsSaving(false);
    }
  };

  return (
    <section className="login">
      <div className="card login__card">
        <h1>Sign in</h1>
        <p className="field__hint">Use your Northwind Support agent account.</p>

        <form className="form" onSubmit={submit} noValidate>
          <Field id="login-email" label="Email" error={errors.email}>
            {(fieldProps) => (
              <input
                {...fieldProps}
                type="email"
                autoComplete="username"
                value={email}
                disabled={isSaving}
                onChange={(event) => setEmail(event.target.value)}
              />
            )}
          </Field>

          <Field id="login-password" label="Password" error={errors.password}>
            {(fieldProps) => (
              <input
                {...fieldProps}
                type="password"
                autoComplete="current-password"
                value={password}
                disabled={isSaving}
                onChange={(event) => setPassword(event.target.value)}
              />
            )}
          </Field>

          {submitError && <ErrorMessage message={submitError} />}

          <div className="button-row">
            <button type="submit" className="button button--primary" disabled={isSaving}>
              {isSaving ? 'Signing in...' : 'Sign in'}
            </button>
          </div>
        </form>
      </div>
    </section>
  );
}
