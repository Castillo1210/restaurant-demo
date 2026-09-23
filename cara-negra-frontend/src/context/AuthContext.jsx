import { createContext, useContext, useState, useCallback, useEffect } from 'react';
import { getCurrentUser, logout as serviceLogout, login as serviceLogin } from '../services/authService';
import { setUnauthorizedHandler } from '../services/api';
import { disconnectPedidosHub } from '../services/signalrService';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(() => getCurrentUser());

  const logout = useCallback(() => {
    serviceLogout();
    setUser(null);
    disconnectPedidosHub();
  }, []);

  // Si el backend responde 401 (token vencido/inválido) en cualquier request,
  // limpiamos la sesión local para forzar un nuevo login.
  useEffect(() => {
    setUnauthorizedHandler(() => {
      setUser(null);
      disconnectPedidosHub();
    });
    return () => setUnauthorizedHandler(null);
  }, []);

  const login = useCallback(async (nombreUsuario, password) => {
    const auth = await serviceLogin(nombreUsuario, password);
    setUser(auth);
    return auth;
  }, []);

  return (
    <AuthContext.Provider value={{ user, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
};
