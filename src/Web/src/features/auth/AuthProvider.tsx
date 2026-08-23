import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type PropsWithChildren,
} from 'react';
import { useNavigate } from 'react-router-dom';
import * as tokens from '../../shared/api/apiClient';
import { clearAuth, getUser, setAuth } from '../../shared/api/tokens';
import type { AuthUser } from '../../shared/types';
import { AuthContext } from './AuthContext';

export function AuthProvider({ children }: PropsWithChildren) {
  const navigate = useNavigate();
  const [user, setUser] = useState<AuthUser | null>(() => getUser());

  useEffect(
    () =>
      tokens.onSessionExpired(() => {
        setUser(null);
        navigate('/login', { replace: true });
      }),
    [navigate],
  );

  const signIn = useCallback(
    (accessToken: string, refreshToken: string, nextUser: AuthUser) => {
      setAuth(accessToken, refreshToken, nextUser);
      setUser(nextUser);
    },
    [],
  );

  const signOut = useCallback(() => {
    clearAuth();
    setUser(null);
    navigate('/login', { replace: true });
  }, [navigate]);

  const value = useMemo(
    () => ({ user, isAuthenticated: user !== null, signIn, signOut }),
    [user, signIn, signOut],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
