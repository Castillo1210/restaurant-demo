import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { X, Loader2, AlertTriangle, CheckCircle2, Lock, RefreshCw, Receipt, Ban } from 'lucide-react';
import toast from 'react-hot-toast';
import { getPreCierre, cerrarJornada } from '../services/jornadaService';
import { etiquetaMesa } from '../utils/mesaLabel';
import { soles, fechaOperativaTexto } from '../utils/formato';
import { useAuth } from '../context/AuthContext';
import AnularPedidoDialog from './AnularPedidoDialog';

/**
 * Cierre de la jornada en 2 pasos:
 *  1. Revisión: no se puede cerrar con pedidos sin cobrar (se listan; se pueden anular aquí).
 *  2. Confirmar: se cierra con lo REGISTRADO en el sistema (sin arqueo / conteo de caja) y se
 *     devuelve el reporte de cierre (onCerrada).
 */
export default function CierreJornadaModal({ onClose, onCerrada }) {
  const navigate = useNavigate();
  const { user } = useAuth();
  const [pre, setPre] = useState(null);
  // Pedido abierto que se anula desde aquí (p. ej. el cliente se fue sin consumir).
  const [anulando, setAnulando] = useState(null);
  const [cargando, setCargando] = useState(true);
  const [paso, setPaso] = useState('revision'); // revision | confirmar
  const [observacion, setObservacion] = useState('');
  const [cerrando, setCerrando] = useState(false);
  const onCloseRef = useRef(onClose);
  onCloseRef.current = onClose;

  const cargar = useCallback(async () => {
    setCargando(true);
    try {
      setPre(await getPreCierre());
    } catch (err) {
      toast.error(err.message);
      onCloseRef.current?.();
    } finally {
      setCargando(false);
    }
  }, []);

  useEffect(() => {
    cargar();
  }, [cargar]);

  const confirmarCierre = async () => {
    setCerrando(true);
    try {
      // Sin arqueo: la jornada se cierra con lo que el sistema registró (cobros por medio de pago,
      // fondo, entradas y salidas). El reporte muestra lo registrado, sin faltante ni sobrante.
      const resumen = await cerrarJornada({ observacion: observacion.trim() || undefined });
      toast.success('Jornada cerrada');
      onCerrada(resumen);
    } catch (err) {
      toast.error(err.message);
      setPaso('revision');
      cargar();
    } finally {
      setCerrando(false);
    }
  };

  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      className="fixed inset-0 z-[60] flex flex-col justify-end sm:justify-center sm:items-center"
    >
      <div className="absolute inset-0 bg-black/70 backdrop-blur-sm" onClick={cerrando ? undefined : onClose} />
      <motion.div
        initial={{ y: '100%' }}
        animate={{ y: 0 }}
        exit={{ y: '100%' }}
        transition={{ type: 'spring', stiffness: 300, damping: 30 }}
        className="relative z-10 bg-card w-full sm:max-w-md rounded-t-3xl sm:rounded-3xl flex flex-col shadow-2xl border-t border-gray-700/40"
        style={{ maxHeight: '94dvh' }}
      >
        <div className="px-5 pt-5 pb-3 flex justify-between items-start border-b border-gray-800/60">
          <div>
            <h2 className="text-lg font-extrabold tracking-tight">Cerrar jornada</h2>
            {pre && <p className="text-xs text-gray-500">{fechaOperativaTexto(pre.jornada.fechaOperativa)} · abierta por {pre.jornada.abiertaPor}</p>}
          </div>
          <button onClick={onClose} disabled={cerrando} className="p-2 bg-background/60 rounded-full hover:bg-gray-700">
            <X size={18} className="text-gray-400" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto px-5 py-4 space-y-4">
          {cargando || !pre ? (
            <p className="text-sm text-gray-500 flex items-center gap-2"><Loader2 size={15} className="animate-spin" /> Revisando…</p>
          ) : paso === 'revision' ? (
            <>
              {pre.pedidosAbiertos.length > 0 ? (
                <div className="space-y-3">
                  <div className="bg-red-500/10 border border-red-500/30 rounded-2xl p-3 flex gap-2.5">
                    <AlertTriangle size={18} className="text-red-400 flex-shrink-0 mt-0.5" />
                    <p className="text-sm text-red-200">
                      Hay <b>{pre.pedidosAbiertos.length}</b> pedido(s) sin cobrar. Cóbralos o anúlalos antes de cerrar.
                    </p>
                  </div>
                  <div className="space-y-2">
                    {pre.pedidosAbiertos.map((p) => (
                      <div key={p.pedidoId} className="bg-background/60 border border-gray-800 rounded-xl px-3 py-2.5 flex justify-between items-center text-sm">
                        <div className="min-w-0">
                          <p className="font-bold text-ink truncate">{etiquetaMesa(p.salonNombre, p.mesaNumero)} · #{p.pedidoId}</p>
                          <p className="text-[11px] text-gray-500 truncate">{p.mozo} · {p.estado}</p>
                        </div>
                        <div className="flex items-center gap-2 flex-shrink-0">
                          <div className="text-right">
                            <p className="font-bold text-ink">{soles(p.total)}</p>
                            {p.pagado > 0 && <p className="text-[11px] text-gray-500">pagado {soles(p.pagado)}</p>}
                          </div>
                          {/* Sin pagos se puede anular aquí mismo; con pagos se resuelve en Caja. */}
                          {!(p.pagado > 0) && (
                            <button
                              onClick={() => setAnulando({ id: p.pedidoId, salonNombre: p.salonNombre, mesaNumero: p.mesaNumero })}
                              className="flex items-center gap-1 px-2.5 py-1.5 rounded-full border border-red-500/40 text-red-400 hover:bg-red-500/10 text-xs font-bold"
                              title="Anular el pedido completo"
                            >
                              <Ban size={13} /> Anular
                            </button>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                  <div className="flex gap-2">
                    <button onClick={() => navigate('/caja')} className="flex-1 py-3 rounded-2xl bg-primary text-white font-bold text-sm flex items-center justify-center gap-2">
                      <Receipt size={16} /> Ir a caja
                    </button>
                    <button onClick={cargar} className="flex-1 py-3 rounded-2xl border border-gray-700 text-gray-300 font-bold text-sm flex items-center justify-center gap-2">
                      <RefreshCw size={16} /> Revisar de nuevo
                    </button>
                  </div>
                </div>
              ) : (
                <div className="bg-emerald-500/10 border border-emerald-500/30 rounded-2xl p-3 flex gap-2.5">
                  <CheckCircle2 size={18} className="text-emerald-400 flex-shrink-0 mt-0.5" />
                  <p className="text-sm text-emerald-200">Todos los pedidos están cobrados.</p>
                </div>
              )}

              {pre.comprobantesPendientes > 0 && (
                <div className="bg-yellow-500/10 border border-yellow-500/30 rounded-2xl p-3 flex gap-2.5">
                  <AlertTriangle size={18} className="text-accentYellow flex-shrink-0 mt-0.5" />
                  <p className="text-xs text-yellow-100/90">
                    {pre.comprobantesPendientes} comprobante(s) aún por confirmar en SUNAT. No impide el cierre: se siguen
                    enviando solos.
                  </p>
                </div>
              )}

              {pre.puedeCerrar && (
                <motion.button
                  whileTap={{ scale: 0.97 }}
                  onClick={() => setPaso('confirmar')}
                  className="w-full bg-primary hover:bg-primaryHover text-white font-bold py-4 rounded-2xl shadow-glow-orange"
                >
                  Cerrar jornada
                </motion.button>
              )}
            </>
          ) : (
            <>
              <div className="bg-background/60 border border-gray-800 rounded-2xl p-3 text-sm text-gray-300 flex gap-2.5">
                <Lock size={16} className="text-gray-500 flex-shrink-0 mt-0.5" />
                <p>
                  La jornada se cierra con <b className="text-ink">lo registrado en el sistema</b>: cobros por medio de pago,
                  fondo inicial, entradas y salidas de caja. No hay que contar. Al cerrar sale el reporte para imprimir.
                </p>
              </div>

              <div>
                <label className="block text-xs font-bold text-gray-300 mb-1.5">Observación (opcional)</label>
                <textarea
                  value={observacion}
                  onChange={(e) => setObservacion(e.target.value)}
                  disabled={cerrando}
                  maxLength={500}
                  rows={2}
                  placeholder="Ej: se cerró temprano por lluvia"
                  className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3 text-ink text-sm placeholder-gray-600 focus:outline-none focus:border-primary"
                />
              </div>

              <div className="space-y-2">
                <p className="text-sm text-center text-accentYellow font-semibold">
                  ¿Confirmas? Después de cerrar, los pagos de esta jornada no se pueden modificar.
                </p>
                <div className="flex gap-2">
                  <button onClick={() => setPaso('revision')} disabled={cerrando} className="flex-1 py-3.5 rounded-2xl border border-gray-700 text-gray-300 font-bold text-sm">
                    Volver
                  </button>
                  <button
                    onClick={confirmarCierre}
                    disabled={cerrando}
                    className="flex-1 py-3.5 rounded-2xl bg-red-600 hover:bg-red-500 text-white font-bold text-sm flex items-center justify-center gap-2 disabled:opacity-60"
                  >
                    {cerrando && <Loader2 size={16} className="animate-spin" />}
                    Sí, cerrar
                  </button>
                </div>
              </div>
            </>
          )}
        </div>
      </motion.div>

      <AnularPedidoDialog
        pedido={anulando}
        quien={user?.nombreCompleto}
        onCerrar={() => setAnulando(null)}
        onAnulado={() => {
          setAnulando(null);
          cargar();
        }}
      />
    </motion.div>
  );
}
