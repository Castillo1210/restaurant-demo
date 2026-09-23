import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

/**
 * Bloquea el acceso a rutas protegidas si no hay una sesión activa.
 * Antes de la Fase 1 cualquiera podía navegar directo a /dashboard sin login.
 *
 * Fase 2: acepta un prop opcional `roles` (p.ej. ['CAJERO', 'ADMIN']) para
 * restringir además por rol. Si el usuario está autenticado pero su rol no
 * está permitido, se le manda de vuelta al mapa de mesas en lugar de /login
 * (ya tiene sesión válida, solo no tiene permiso para esa sección).
 */
/**
 * @param {{ children: import('react').ReactNode, roles?: string[] }} props
 */
export default function RequireAuth({ children, roles }) {
  const { user } = useAuth();
  const location = useLocation();

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  if (roles && roles.length > 0 && !roles.includes(user.rol)) {
    // Cocina no usa el mapa de mesas: su pantalla "de inicio" es el tablero de cocina.
    return <Navigate to={user.rol === 'COCINA' ? '/cocina' : '/dashboard'} replace />;
  }

  return children;
}
