import { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { useAuth } from './AuthContext';
import { getJornadaActual } from '../services/jornadaService';

/**
 * Estado de la jornada (día operativo) para toda la app: si está abierta y desde cuándo.
 * Se consulta al iniciar sesión, cada minuto y al volver a la pestaña, así el mozo ve
 * enseguida cuando caja abre o cierra el día.
 */
const JornadaContext = createContext({
  cargada: false,
  abierta: false,
  jornada: null,
  recargar: async () => {},
});

const INTERVALO_MS = 60_000;

export function JornadaProvider({ children }) {
  const { user } = useAuth();
  const [estado, setEstado] = useState({ cargada: false, abierta: false, jornada: null });

  const recargar = useCallback(async () => {
    try {
      const data = await getJornadaActual();
      setEstado({ cargada: true, abierta: !!data?.abierta, jornada: data?.jornada ?? null });
      return data;
    } catch {
      // Sin conexión: se mantiene el último estado conocido.
      setEstado((e) => ({ ...e, cargada: true }));
      return null;
    }
  }, []);

  useEffect(() => {
    if (!user) {
      setEstado({ cargada: false, abierta: false, jornada: null });
      return undefined;
    }
    recargar();
    const id = setInterval(recargar, INTERVALO_MS);
    const alVolver = () => document.visibilityState === 'visible' && recargar();
    document.addEventListener('visibilitychange', alVolver);
    return () => {
      clearInterval(id);
      document.removeEventListener('visibilitychange', alVolver);
    };
  }, [user?.usuarioId, recargar]); // eslint-disable-line react-hooks/exhaustive-deps

  return <JornadaContext.Provider value={{ ...estado, recargar }}>{children}</JornadaContext.Provider>;
}

export const useJornada = () => useContext(JornadaContext);
