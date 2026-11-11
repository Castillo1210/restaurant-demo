import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { useAuth } from '../context/AuthContext';

const EVENTOS_ACTIVIDAD = ['pointerdown', 'keydown', 'touchstart', 'wheel'];

/**
 * Cierra la sesión de un MOZO que entró con PIN en un dispositivo compartido (tablet) tras
 * X minutos sin tocar la pantalla (se configura por dispositivo en Admin → Dispositivos).
 * Así el siguiente mozo no queda usando la sesión del anterior.
 * No aplica a COCINA (la pantalla de cocina se mira sin tocarla) ni a ingresos con contraseña.
 */
export default function CierreInactividad() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const minutos =
    user?.metodo === 'pin' && user?.rol === 'MOZO' ? Number(user.cierreInactividadMinutos) || 0 : 0;

  useEffect(() => {
    if (!minutos) return undefined;

    let ultimaActividad = Date.now();
    const marcar = () => {
      ultimaActividad = Date.now();
    };
    EVENTOS_ACTIVIDAD.forEach((ev) => window.addEventListener(ev, marcar, { passive: true }));

    // Se compara contra la hora (no un setTimeout largo) para que funcione aunque la tablet
    // haya estado con la pantalla apagada.
    const id = setInterval(() => {
      if (Date.now() - ultimaActividad >= minutos * 60_000) {
        clearInterval(id);
        logout();
        navigate('/login', { replace: true });
        toast('Sesión cerrada por inactividad', { id: 'cierre-inactividad' });
      }
    }, 15_000);

    return () => {
      clearInterval(id);
      EVENTOS_ACTIVIDAD.forEach((ev) => window.removeEventListener(ev, marcar));
    };
  }, [minutos, user?.usuarioId, logout, navigate]);

  return null;
}
