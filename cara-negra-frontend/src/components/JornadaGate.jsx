import { useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { CalendarClock, AlertTriangle, Sun } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import { useJornada } from '../context/JornadaContext';
import { useEmpresa } from '../context/EmpresaContext';
import AbrirJornadaForm from './AbrirJornadaForm';
import { fechaOperativaTexto } from '../utils/formato';

const OMITIDO_KEY = 'caraNegra_jornadaOmitida';
const RUTAS_SIN_AVISO = ['/login', '/cocina'];

const leerOmitido = (usuarioId) => {
  try {
    return sessionStorage.getItem(OMITIDO_KEY) === String(usuarioId);
  } catch {
    return false;
  }
};

/**
 * Apertura obligatoria del día y avisos de jornada:
 *  - ADMIN / CAJERO sin jornada abierta → pantalla "Abrir jornada" al entrar (se puede dejar
 *    para después con "Ahora no"; queda un aviso arriba).
 *  - MOZO sin jornada → aviso "el día aún no se ha abierto" (no puede tomar pedidos).
 *  - Jornada abierta de un día anterior → aviso para cerrarla.
 */
export default function JornadaGate() {
  const { user } = useAuth();
  const { cargada, abierta, jornada, recargar } = useJornada();
  const { nombreComercial } = useEmpresa();
  const location = useLocation();
  const navigate = useNavigate();
  const [omitido, setOmitido] = useState(() => (user ? leerOmitido(user.usuarioId) : false));

  if (!user || !cargada || RUTAS_SIN_AVISO.includes(location.pathname) || user.rol === 'COCINA') return null;

  const puedeAbrir = user.rol === 'ADMIN' || user.rol === 'CAJERO';
  const enPaginaJornada = location.pathname === '/jornada';

  const omitir = () => {
    try {
      sessionStorage.setItem(OMITIDO_KEY, String(user.usuarioId));
    } catch {
      // sin almacenamiento: solo se oculta mientras dure esta pantalla
    }
    setOmitido(true);
  };

  // ── Pantalla obligatoria de apertura ──
  const mostrarApertura = puedeAbrir && !abierta && !omitido && !enPaginaJornada && !leerOmitido(user.usuarioId);

  return (
    <>
      <AnimatePresence>
        {mostrarApertura && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-[70] bg-background/95 backdrop-blur-sm overflow-y-auto"
          >
            <div className="min-h-full flex flex-col items-center justify-center p-6">
              <div className="w-full max-w-sm">
                <div className="flex flex-col items-center text-center mb-6">
                  <div className="w-16 h-16 bg-primary/15 rounded-3xl flex items-center justify-center mb-3">
                    <Sun size={30} className="text-primary" />
                  </div>
                  <h1 className="text-2xl font-extrabold tracking-tight">Abrir el día</h1>
                  <p className="text-sm text-gray-400 mt-1">
                    {nombreComercial ? `${nombreComercial}: ` : ''}para tomar pedidos y cobrar, primero abre la jornada con el fondo de caja.
                  </p>
                </div>
                <AbrirJornadaForm
                  onAbierta={() => recargar()}
                  onOmitir={omitir}
                  textoOmitir={user.rol === 'ADMIN' ? 'Ahora no (solo administrar)' : 'Ahora no'}
                />
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* ── Avisos (no bloquean) ── */}
      {!abierta && !mostrarApertura && !enPaginaJornada && (
        <div className="sticky top-0 z-40 bg-yellow-950/90 border-b border-yellow-800/50 backdrop-blur px-4 py-2.5 flex items-center gap-3 text-sm">
          <CalendarClock size={18} className="text-accentYellow flex-shrink-0" />
          <p className="flex-1 text-yellow-100/90 text-xs sm:text-sm">
            {puedeAbrir
              ? 'El día aún no se ha abierto: no se pueden tomar pedidos ni cobrar.'
              : 'El día aún no se ha abierto. Avisa al administrador o a caja para empezar a tomar pedidos.'}
          </p>
          {puedeAbrir && (
            <button
              onClick={() => navigate('/jornada')}
              className="flex-shrink-0 text-xs font-bold bg-accentYellow/20 text-accentYellow border border-yellow-700/60 px-3 py-1.5 rounded-full"
            >
              Abrir jornada
            </button>
          )}
        </div>
      )}

      {abierta && jornada?.esDeDiaAnterior && puedeAbrir && !enPaginaJornada && (
        <div className="sticky top-0 z-40 bg-red-950/90 border-b border-red-800/50 backdrop-blur px-4 py-2.5 flex items-center gap-3 text-sm">
          <AlertTriangle size={18} className="text-red-400 flex-shrink-0" />
          <p className="flex-1 text-red-100/90 text-xs sm:text-sm">
            La jornada del <b>{fechaOperativaTexto(jornada.fechaOperativa)}</b> sigue abierta. Ciérrala antes de empezar el día.
          </p>
          <button
            onClick={() => navigate('/jornada')}
            className="flex-shrink-0 text-xs font-bold bg-red-500/20 text-red-300 border border-red-700/60 px-3 py-1.5 rounded-full"
          >
            Cerrar jornada
          </button>
        </div>
      )}
    </>
  );
}
