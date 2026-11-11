import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import {
  ChevronLeft, CalendarClock, Lock, ArrowDownCircle, ArrowUpCircle, Trash2, AlertTriangle, History, ChevronRight, Loader2, Ban, Pencil,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { useAuth } from '../context/AuthContext';
import { useJornada } from '../context/JornadaContext';
import AbrirJornadaForm from '../components/AbrirJornadaForm';
import CierreJornadaModal from '../components/CierreJornadaModal';
import ReporteCierre from '../components/ReporteCierre';
import ListaAnulaciones from '../components/ListaAnulaciones';
import ListaCambiosPrecio from '../components/ListaCambiosPrecio';
import {
  getResumenActual, registrarMovimiento, anularMovimiento, getHistorialJornadas, getResumenJornada,
} from '../services/jornadaService';
import { soles, fechaOperativaTexto, horaLima } from '../utils/formato';

const inputCls =
  'w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3 text-ink placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm';

function Tarjeta({ titulo, icon: Icon, children, accion }) {
  return (
    <section className="bg-card border border-gray-800/50 rounded-3xl p-4 space-y-3">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-extrabold text-ink flex items-center gap-2">
          {Icon && <Icon size={16} className="text-primary" />} {titulo}
        </h2>
        {accion}
      </div>
      {children}
    </section>
  );
}

function Dato({ label, valor }) {
  return (
    <div className="bg-background/60 border border-gray-800/60 rounded-2xl p-3">
      <p className="text-[10px] text-gray-500 uppercase tracking-wider font-bold">{label}</p>
      <p className="text-lg font-extrabold text-ink mt-0.5">{valor}</p>
    </div>
  );
}

/**
 * Jornada (día operativo): apertura, resumen en vivo, movimientos de caja, cierre (con lo registrado)
 * ciego y, para el ADMIN, historial de cierres con su reporte.
 */
export default function Jornada() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const { abierta, jornada, cargada, recargar } = useJornada();
  const esAdmin = user?.rol === 'ADMIN';

  const [resumen, setResumen] = useState(null);
  const [historial, setHistorial] = useState([]);
  const [reporte, setReporte] = useState(null);
  const [cerrando, setCerrando] = useState(false);
  const [mov, setMov] = useState({ tipo: 'Egreso', monto: '', motivo: '' });
  const [guardandoMov, setGuardandoMov] = useState(false);
  const [confirmarAnular, setConfirmarAnular] = useState(null);

  const cargarResumen = useCallback(async () => {
    if (!abierta) {
      setResumen(null);
      return;
    }
    try {
      setResumen(await getResumenActual());
    } catch (err) {
      toast.error(err.message);
    }
  }, [abierta]);

  const cargarHistorial = useCallback(async () => {
    if (!esAdmin) return;
    try {
      setHistorial(await getHistorialJornadas());
    } catch (err) {
      toast.error(err.message);
    }
  }, [esAdmin]);

  useEffect(() => {
    cargarResumen();
  }, [cargarResumen, jornada?.jornadaId]);

  useEffect(() => {
    cargarHistorial();
  }, [cargarHistorial, jornada?.jornadaId, abierta]);

  const guardarMovimiento = async (e) => {
    e.preventDefault();
    const monto = Number(mov.monto);
    if (!monto || monto <= 0) return toast.error('Ingresa un monto mayor a 0');
    if (!mov.motivo.trim()) return toast.error('Indica el motivo');
    setGuardandoMov(true);
    try {
      await registrarMovimiento({ tipo: mov.tipo, monto, motivo: mov.motivo.trim() });
      toast.success(mov.tipo === 'Egreso' ? 'Salida de efectivo registrada' : 'Entrada de efectivo registrada');
      setMov({ tipo: mov.tipo, monto: '', motivo: '' });
      cargarResumen();
    } catch (err) {
      toast.error(err.message);
    } finally {
      setGuardandoMov(false);
    }
  };

  const anular = async (m) => {
    if (confirmarAnular !== m.movimientoCajaId) {
      setConfirmarAnular(m.movimientoCajaId);
      return;
    }
    setConfirmarAnular(null);
    try {
      await anularMovimiento(m.movimientoCajaId);
      toast.success('Movimiento anulado');
      cargarResumen();
    } catch (err) {
      toast.error(err.message);
    }
  };

  const verReporte = async (id) => {
    try {
      setReporte(await getResumenJornada(id));
    } catch (err) {
      toast.error(err.message);
    }
  };

  const efectivo = resumen?.arqueo?.find((a) => a.esEfectivo);

  return (
    <div className="min-h-screen bg-background pb-10">
      <div className="px-5 pt-6 pb-4 flex items-center gap-3">
        <motion.button
          whileTap={{ scale: 0.9 }}
          onClick={() => navigate(-1)}
          className="p-2 bg-card rounded-full hover:bg-cardHighlight border border-gray-800/50"
        >
          <ChevronLeft size={22} />
        </motion.button>
        <div>
          <h1 className="text-2xl font-extrabold tracking-tight">Jornada y caja</h1>
          <p className="text-xs text-gray-500 mt-0.5">Apertura del día, movimientos de caja y cierre</p>
        </div>
      </div>

      <div className="px-5 space-y-4 max-w-2xl">
        {!cargada ? (
          <p className="text-sm text-gray-500 flex items-center gap-2"><Loader2 size={15} className="animate-spin" /> Cargando…</p>
        ) : !abierta ? (
          <Tarjeta titulo="No hay jornada abierta" icon={CalendarClock}>
            <p className="text-xs text-gray-400">Abre el día para que los mozos puedan tomar pedidos y caja pueda cobrar.</p>
            <AbrirJornadaForm onAbierta={() => recargar()} />
          </Tarjeta>
        ) : (
          <>
            {jornada?.esDeDiaAnterior && (
              <div className="bg-red-500/10 border border-red-500/30 rounded-2xl p-3 flex gap-2.5 text-sm text-red-200">
                <AlertTriangle size={18} className="text-red-400 flex-shrink-0 mt-0.5" />
                Esta jornada es de un día anterior. Ciérrala y luego abre la de hoy.
              </div>
            )}

            <Tarjeta
              titulo={`Jornada del ${fechaOperativaTexto(jornada.fechaOperativa)}`}
              icon={CalendarClock}
              accion={<span className="text-[10px] font-bold uppercase tracking-wider text-accentGreen bg-emerald-950/40 border border-emerald-800/60 px-2 py-0.5 rounded-full">Abierta</span>}
            >
              <p className="text-xs text-gray-400">
                Abierta a las {horaLima(jornada.abiertaEn)} por {jornada.abiertaPor} · fondo de caja {soles(jornada.fondoInicial)}
                {jornada.observacionApertura ? ` · ${jornada.observacionApertura}` : ''}
              </p>

              {resumen && (
                resumen.incluyeMontos ? (
                  <>
                    <div className="grid grid-cols-2 sm:grid-cols-3 gap-2">
                      <Dato label="Cobrado" valor={soles(resumen.totalVentas)} />
                      <Dato label="Pedidos cobrados" valor={resumen.cantidadPedidos} />
                      <Dato label="Ticket promedio" valor={soles(resumen.ticketPromedio)} />
                    </div>
                    {resumen.arqueo.length > 0 && (
                      <div className="bg-background/60 border border-gray-800/60 rounded-2xl p-3 space-y-1">
                        <p className="text-[10px] text-gray-500 uppercase tracking-wider font-bold mb-1">Debería haber (solo lo ve el admin)</p>
                        {resumen.arqueo.map((a) => (
                          <div key={`${a.metodoPagoId}-${a.metodoPago}`} className="flex justify-between text-sm">
                            <span className="text-gray-300">{a.metodoPago}</span>
                            <span className="font-bold text-ink">{soles(a.esperado)}</span>
                          </div>
                        ))}
                        {efectivo && (
                          <p className="text-[10px] text-gray-500 pt-1">
                            Efectivo = fondo + cobros en efectivo + ingresos − egresos.
                          </p>
                        )}
                      </div>
                    )}
                  </>
                ) : (
                  <>
                    <div className="grid grid-cols-2 gap-2">
                      <Dato label="Pedidos cobrados" valor={resumen.cantidadPedidos} />
                      <Dato label="Comprobantes" valor={resumen.comprobantes.boletas + resumen.comprobantes.facturas} />
                    </div>
                    <p className="text-[11px] text-gray-500 flex items-center gap-1.5">
                      <Lock size={12} /> Los montos se ven en el reporte, al cerrar la jornada.
                    </p>
                  </>
                )
              )}

              <motion.button
                whileTap={{ scale: 0.97 }}
                onClick={() => setCerrando(true)}
                className="w-full py-3.5 rounded-2xl bg-red-600/90 hover:bg-red-500 text-white font-bold text-sm flex items-center justify-center gap-2"
              >
                <Lock size={16} /> Cerrar jornada
              </motion.button>
            </Tarjeta>

            <Tarjeta titulo="Movimientos de caja" icon={ArrowDownCircle}>
              <p className="text-[11px] text-gray-500">
                Efectivo que sale o entra de la caja y no es una venta: compra de gas, pago a un proveedor, sencillo adicional…
              </p>
              <form onSubmit={guardarMovimiento} className="space-y-2">
                <div className="grid grid-cols-2 gap-2">
                  {[
                    { v: 'Egreso', t: 'Salida', I: ArrowUpCircle, cls: 'text-red-400 border-red-500/50 bg-red-500/10' },
                    { v: 'Ingreso', t: 'Entrada', I: ArrowDownCircle, cls: 'text-accentGreen border-emerald-500/50 bg-emerald-500/10' },
                  ].map(({ v, t, I, cls }) => (
                    <button
                      key={v}
                      type="button"
                      onClick={() => setMov({ ...mov, tipo: v })}
                      className={`py-2.5 rounded-xl border text-sm font-bold flex items-center justify-center gap-1.5 ${
                        mov.tipo === v ? cls : 'border-gray-800 text-gray-500'
                      }`}
                    >
                      <I size={16} /> {t}
                    </button>
                  ))}
                </div>
                <div className="grid grid-cols-[7rem_1fr] gap-2">
                  <input
                    type="number"
                    inputMode="decimal"
                    step="0.10"
                    min="0"
                    value={mov.monto}
                    onChange={(e) => setMov({ ...mov, monto: e.target.value })}
                    placeholder="S/ 0.00"
                    className={inputCls}
                  />
                  <input
                    value={mov.motivo}
                    onChange={(e) => setMov({ ...mov, motivo: e.target.value })}
                    maxLength={200}
                    placeholder={mov.tipo === 'Egreso' ? 'Motivo (ej: compra de gas)' : 'Motivo (ej: sencillo adicional)'}
                    className={inputCls}
                  />
                </div>
                <button
                  type="submit"
                  disabled={guardandoMov}
                  className="w-full py-3 rounded-2xl border border-dashed border-gray-700 text-gray-300 hover:text-ink hover:border-primary/50 font-bold text-sm disabled:opacity-50"
                >
                  {guardandoMov ? 'Registrando…' : `Registrar ${mov.tipo === 'Egreso' ? 'salida' : 'entrada'}`}
                </button>
              </form>

              {resumen?.movimientos?.length > 0 && (
                <div className="space-y-1.5 pt-1">
                  {resumen.movimientos.map((m) => (
                    <div key={m.movimientoCajaId} className={`flex items-center gap-3 bg-background/60 border border-gray-800/60 rounded-xl px-3 py-2 ${m.estaAnulado ? 'opacity-50' : ''}`}>
                      {m.tipo === 'Egreso'
                        ? <ArrowUpCircle size={16} className="text-red-400 flex-shrink-0" />
                        : <ArrowDownCircle size={16} className="text-accentGreen flex-shrink-0" />}
                      <div className="flex-1 min-w-0">
                        <p className={`text-sm text-ink truncate ${m.estaAnulado ? 'line-through' : ''}`}>{m.motivo}</p>
                        <p className="text-[11px] text-gray-500">
                          {horaLima(m.creadoEn)} · {m.registradoPor}{m.estaAnulado ? ` · anulado por ${m.anuladoPor ?? '—'}` : ''}
                        </p>
                      </div>
                      <span className={`font-bold text-sm ${m.tipo === 'Egreso' ? 'text-red-400' : 'text-accentGreen'}`}>
                        {m.tipo === 'Egreso' ? '−' : '+'}{Number(m.monto).toFixed(2)}
                      </span>
                      {!m.estaAnulado && (
                        <button
                          onClick={() => anular(m)}
                          onBlur={() => setConfirmarAnular(null)}
                          className={`rounded-full text-xs font-bold ${confirmarAnular === m.movimientoCajaId ? 'px-2.5 py-1 bg-red-500/20 text-red-400' : 'p-1.5 text-gray-500 hover:text-red-400'}`}
                          title="Anular"
                        >
                          {confirmarAnular === m.movimientoCajaId ? '¿Anular?' : <Trash2 size={14} />}
                        </button>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </Tarjeta>

            {/* Productos quitados de pedidos en el día: quién, cuándo y por qué */}
            {resumen?.anulaciones?.length > 0 && (
              <Tarjeta titulo={`Productos quitados (${resumen.productosAnulados})`} icon={Ban}>
                <ListaAnulaciones anulaciones={resumen.anulaciones} mostrarMesa mostrarMontos={resumen.incluyeMontos} />
              </Tarjeta>
            )}

            {/* Precios distintos a la carta (cajero / admin) */}
            {resumen?.cambiosPrecio?.length > 0 && (
              <Tarjeta titulo={`Precios cambiados (${resumen.cambiosPrecio.length})`} icon={Pencil}>
                <ListaCambiosPrecio cambios={resumen.cambiosPrecio} mostrarMesa />
              </Tarjeta>
            )}
          </>
        )}

        {esAdmin && historial.length > 0 && (
          <Tarjeta titulo="Historial de jornadas" icon={History}>
            <div className="space-y-1.5">
              {historial.map((h) => {
                const cerrada = h.estado === 'Cerrada';
                const dif = Number(h.diferenciaTotal ?? 0);
                return (
                  <button
                    key={h.jornadaId}
                    disabled={!cerrada}
                    onClick={() => verReporte(h.jornadaId)}
                    className="w-full flex items-center gap-3 bg-background/60 border border-gray-800/60 rounded-xl px-3 py-2.5 text-left hover:border-primary/40 disabled:hover:border-gray-800/60"
                  >
                    <div className="flex-1 min-w-0">
                      <p className="text-sm font-bold text-ink">{fechaOperativaTexto(h.fechaOperativa, true)}</p>
                      <p className="text-[11px] text-gray-500 truncate">
                        {cerrada ? `${h.cantidadPedidos} pedidos · cerró ${h.cerradaPor}` : `Abierta · ${h.abiertaPor}`}
                      </p>
                    </div>
                    {cerrada && (
                      <div className="text-right">
                        <p className="text-sm font-bold text-ink">{soles(h.totalVentas)}</p>
                        {/* Faltante / sobrante solo en cierres antiguos, cuando había arqueo. */}
                        {Math.abs(dif) >= 0.005 && (
                          <p className={`text-[11px] font-bold ${dif < 0 ? 'text-red-400' : 'text-accentYellow'}`}>
                            {dif < 0 ? `Faltante ${soles(-dif)}` : `Sobrante ${soles(dif)}`}
                          </p>
                        )}
                      </div>
                    )}
                    {cerrada && <ChevronRight size={16} className="text-gray-600" />}
                  </button>
                );
              })}
            </div>
          </Tarjeta>
        )}
      </div>

      <AnimatePresence>
        {cerrando && (
          <CierreJornadaModal
            onClose={() => setCerrando(false)}
            onCerrada={(r) => {
              setCerrando(false);
              setReporte(r);
              recargar();
            }}
          />
        )}
      </AnimatePresence>

      <AnimatePresence>
        {reporte && <ReporteCierre resumen={reporte} onClose={() => setReporte(null)} />}
      </AnimatePresence>
    </div>
  );
}
