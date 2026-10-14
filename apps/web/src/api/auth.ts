import type { LoginPayload, LoginResponse } from '../types/api';
import { request } from './client';

export const authApi = {
  /** Anonymous on purpose: there is no token yet, and a 401 here means wrong credentials. */
  login: (payload: LoginPayload) =>
    request<LoginResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify(payload),
      anonymous: true,
    }),
};
