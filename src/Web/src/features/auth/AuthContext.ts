import { createContext, useContext } from 'react';
import type { AuthUser } from '../../shared/types';

export interface AuthContextValue {
  user: AuthUser | null;
  isAuthenticated: boolean;
  signIn: (accessToken: string, refreshToken: string, user: AuthUser) => void;
  signOut: () => void;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be used within an AuthProvider.');
  return value;
}
