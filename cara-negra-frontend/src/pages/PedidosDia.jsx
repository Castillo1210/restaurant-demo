import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { ChevronLeft, Search, X, RefreshCw, ReceiptText, FileText, MessageSquareText, Ban, Receipt, ListOrdered, Download } from 'lucide-react';
import toast from 'react-hot-toast';
import { getPedidosJornada, getPedidosRango, exportarPedidosRango } from '../services/jornadaService';
import { getPedidoById } from '../services/ordersService';
import { getComprobanteDePedido } from '../services/comprobantesService';
import { connectPedidosHub, onHubEvent, onHubReconnected } from '../services/signalrService';
import { useAuth } from '../context/AuthContext';
import { useEmpresa } from '../context/EmpresaContext';
import { imprimirPrecuenta } from '../utils/precuenta';
import { agruparDetallesPorProducto } from '../utils/agruparDetalles';
import { etiquetaMesa } from '../utils/mesaLabel';
import { soles, horaLima, fechaOperativaTexto, hoyLimaISO } from '../utils/formato';
import ListaAnulaciones from '../components/ListaAnulaciones';
import ListaCambiosPrecio from '../components/ListaCambiosPrecio';
import ComprobanteElectronico from '../components/ComprobanteElectronico';
import EmitirComprobanteModal from '../components/EmitirComprobanteModal';

/** Estado del pedido → grupo del filtro y aspecto de la etiqueta. */
const GRUPO = {
  Pendiente: 'abiertos', EnPreparacion: 'abiertos', Listo: 'abiertos',
  Entregado: 'cobrados', Cancelado: 'anulados',
};
const ETIQUETA = {
  abiertos: { texto: 'Abierto', cls: 'text-accentYellow border-yellow-700/50 bg-yellow-500/10' },
  cobrados: { texto: 'Cobrado', cls: 'text-accentGreen border-emerald-700/50 bg-emerald-500/10' },
  anulados: { texto: 'Anulado', cls: 'text-red-400 border-red-800/50 bg-red-500/10' },
};
const FILTROS = [
  { id: 'todos', texto: 'Todos' },
  { id: 'abiertos', texto: 'Abiertos' },
  { id: 'cobrados', texto: 'Cobrados' },
  { id: 'anulados', texto: 'Anulados' },
];

// Primer día del mes actual en Lima ("yyyy-MM-01").
const inicioMesLima = () => `${hoyLimaISO().slice(0, 8)}01`;
// "12/11" a partir de una fecha operativa "2026-11-12T00:00:00".
const diaMes = (valor) => (valor ? `${String(valor).slice(8, 10)}/${String(valor).slice(5, 7)}` : '');

const CLASE_FECHA =
  'bg-card border border-gray-800/50 rounded-xl px-3 py-1.5 text-sm text-ink focus:outline-none focus:border-primary [color-scheme:dark]';
const claseAtajo = (activo) =>
  `px-3.5 py-1.5 rounded-full text-sm font-bold border transition-colors ${
    activo ? 'bg-primary text-white border-primary' : 'bg-card text-gray-400 border-gray-800 hover:text-ink'
  }`;

const sinTildes = (t) => String(t ?? '').normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();

/**
 * Pedidos del día: todos los pedidos de la jornada (abiertos, cobrados y anulados) en una lista
 * simple, con filtro, buscador, detalle y reimpresión. Para cajero y admin; el admin puede elegir
 * jornadas anteriores.
 */
export default function PedidosDia() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const empresa = useEmpresa();
  const esAdmin = user?.rol === 'ADMIN';

  // null = la jornada actual (se actualiza en vivo); { desde, hasta } = historial de esas fechas (solo ADMIN).
  const [rango, setRango] = useState(null);
  const [exportando, setExportando] = useState(false);
  const [datos, setDatos] = useState(null);
  const [cargando, setCargando] = useState(true);
  const [filtro, setFiltro] = useState('todos');
  const [busqueda, setBusqueda] = useState('');
  const [detalle, setDetalle] = useState(null); // pedido completo abierto
  const [cargandoDetalle, setCargandoDetalle] = useState(false);
  const [comprobante, setComprobante] = useState(null);
  // Pedido cobrado al que se le emite la boleta/factura desde aquí (p. ej. se cerró la ventana
  // de comprobante en Caja sin emitirla).
  const [emitirPara, setEmitirPara] = useState(null);

  const cargar = useCallback(async () => {
    setCargando(true);
    try {
      setDatos(rango ? await getPedidosRango(rango.desde, rango.hasta) : await getPedidosJornada(null));
    } catch (err) {
      toast.error(err.message);
    } finally {
      setCargando(false);
    }
  }, [rango]);

  useEffect(() => { cargar(); }, [cargar]);

  // En el día abierto la lista se actualiza sola cuando hay pedidos, cobros o anulaciones.
  useEffect(() => {
    if (rango !== null) return undefined;
    connectPedidosHub().catch(() => {});
    const quitar = [
      onHubEvent('NuevoPedido', cargar),
      onHubEvent('PedidoEstadoCambiado', cargar),
      onHubEvent('PedidoActualizado', cargar),
      onHubEvent('PagoRecibido', cargar),
      onHubEvent('PagoAnulado', cargar),
      onHubReconnected(cargar),
    ];
    return () => quitar.forEach((q) => q?.());
  }, [rango, cargar]);

  const pedidos = datos?.pedidos ?? [];
  const conteo = useMemo(() => {
    const c = { todos: pedidos.length, abiertos: 0, cobrados: 0, anulados: 0 };
    pedidos.forEach((p) => { c[GRUPO[p.estado] ?? 'abiertos'] += 1; });
    return c;
  }, [pedidos]);
  const totalCobrado = pedidos.reduce((a, p) => a + (p.estado === 'Cancelado' ? 0 : Number(p.pagado || 0)), 0);

  const visibles = useMemo(() => {
    const q = sinTildes(busqueda.trim()).replace(/^#/, '');
    return pedidos.filter((p) => {
      if (filtro !== 'todos' && (GRUPO[p.estado] ?? 'abiertos') !== filtro) return false;
      if (!q) return true;
      return String(p.pedidoId) === q
        || sinTildes(etiquetaMesa(p.salonNombre, p.mesaNumero)).includes(q)
        || sinTildes(`${p.salonNombre} ${p.mesaNumero}`).includes(q)
        || sinTildes(p.mozo).includes(q);
    });
  }, [pedidos, filtro, busqueda]);

  const abrirDetalle = async (p) => {
    setCargandoDetalle(true);
    setDetalle({ id: p.pedidoId, resumen: p });
    try {
      const completo = await getPedidoById(p.pedidoId);
      setDetalle({ ...completo, resumen: p });
    } catch {
      toast.error('No se pudo abrir el pedido');
      setDetalle(null);
    } finally {
      setCargandoDetalle(false);
    }
  };

  const verComprobante = async (pedidoId) => {
    try {
      const c = await getComprobanteDePedido(pedidoId);
      if (c) setComprobante(c);
      else toast('Este pedido no tiene boleta ni factura');
    } catch {
      toast.error('No se pudo abrir el comprobante');
    }
  };

  const j = datos?.jornada;
  // Fechas que muestran los selectores: las del historial elegido o el día de la jornada actual.
  const fechaJornada = j?.fechaOperativa ? String(j.fechaOperativa).slice(0, 10) : hoyLimaISO();
  const desde = rango?.desde ?? fechaJornada;
  const hasta = rango?.hasta ?? fechaJornada;
  const variosDias = rango !== null && rango.desde !== rango.hasta;
  const esEsteMes = rango?.desde === inicioMesLima() && rango?.hasta === hoyLimaISO();

  // Si "Desde" queda después de "Hasta" (o al revés), se mueve la otra fecha para que el rango siga siendo válido.
  const cambiarDesde = (valor) => {
    if (valor) setRango({ desde: valor, hasta: valor > hasta ? valor : hasta });
  };
  const cambiarHasta = (valor) => {
    if (valor) setRango({ desde: valor < desde ? valor : desde, hasta: valor });
  };

  const exportarExcel = async () => {
    setExportando(true);
    try {
      await exportarPedidosRango(desde, hasta);
    } catch (err) {
      toast.error(err.message);
    } finally {
      setExportando(false);
    }
  };

  const subtitulo = rango
    ? `${rango.desde === rango.hasta ? fechaOperativaTexto(rango.desde, true) : `Del ${fechaOperativaTexto(rango.desde, true)} al ${fechaOperativaTexto(rango.hasta, true)}`} · ${datos?.cantidadJornadas ?? 0} ${datos?.cantidadJornadas === 1 ? 'jornada' : 'jornadas'}`
    : j ? `Jornada del ${fechaOperativaTexto(j.fechaOperativa)} · ${j.estado === 'Abierta' ? 'abierta' : 'cerrada'}` : 'Sin jornadas todavía';

  return (
    <div className="min-h-screen bg-background pb-10">
      <div className="max-w-3xl mx-auto">
        {/* ── Encabezado ─────────────────────────────────── */}
        <div className="px-5 pt-6 pb-3 flex flex-wrap items-center gap-3">
          <motion.button
            whileTap={{ scale: 0.9 }}
            onClick={() => navigate(-1)}
            className="p-2 bg-card rounded-full hover:bg-cardHighlight border border-gray-800/50"
            aria-label="Volver"
          >
            <ChevronLeft size={22} />
          </motion.button>
          <div className="flex-1 min-w-[10rem]">
            <h1 className="text-2xl font-extrabold tracking-tight">{rango ? 'Historial de pedidos' : 'Pedidos del día'}</h1>
            <p className="text-xs text-gray-500 mt-0.5">{subtitulo}</p>
          </div>
          <motion.button
            whileTap={{ scale: 0.9 }}
            onClick={cargar}
            className="p-2 bg-card rounded-full hover:bg-cardHighlight border border-gray-800/50"
            aria-label="Actualizar"
            title="Actualizar"
          >
            <RefreshCw size={18} className={cargando ? 'animate-spin text-gray-500' : 'text-gray-300'} />
          </motion.button>
        </div>

        {/* ── Resumen y filtros ──────────────────────────── */}
        <div className="px-5 space-y-3">
          {/* Historial por fechas (solo el administrador): se filtra por el día de cada jornada. */}
          {esAdmin && (
            <div className="flex flex-wrap items-center gap-2">
              <label className="flex items-center gap-1.5 text-xs text-gray-500">
                Desde
                <input type="date" value={desde} max={hoyLimaISO()} onChange={(e) => cambiarDesde(e.target.value)} className={CLASE_FECHA} />
              </label>
              <label className="flex items-center gap-1.5 text-xs text-gray-500">
                Hasta
                <input type="date" value={hasta} max={hoyLimaISO()} onChange={(e) => cambiarHasta(e.target.value)} className={CLASE_FECHA} />
              </label>
              <button onClick={() => setRango(null)} className={claseAtajo(rango === null)}>Jornada actual</button>
              <button onClick={() => setRango({ desde: inicioMesLima(), hasta: hoyLimaISO() })} className={claseAtajo(esEsteMes)}>
                Este mes
              </button>
              <button
                onClick={exportarExcel}
                disabled={exportando || cargando || pedidos.length === 0}
                title="Descargar en Excel los pedidos de las fechas elegidas"
                className="ml-auto flex items-center gap-1.5 px-3.5 py-1.5 rounded-full text-sm font-bold border bg-card border-gray-800 text-gray-300 hover:border-primary/60 hover:text-primary transition-colors disabled:opacity-40 disabled:pointer-events-none"
              >
                <Download size={14} />
                {exportando ? 'Exportando…' : 'Exportar a Excel'}
              </button>
            </div>
          )}

          <div className="grid grid-cols-2 gap-2">
            <div className="bg-card border border-gray-800/50 rounded-2xl px-4 py-3">
              <p className="text-[10px] text-gray-500 uppercase tracking-wider font-bold">Pedidos</p>
              <p className="text-xl font-extrabold text-ink">{conteo.todos}</p>
            </div>
            <div className="bg-card border border-gray-800/50 rounded-2xl px-4 py-3">
              <p className="text-[10px] text-gray-500 uppercase tracking-wider font-bold">Cobrado</p>
              <p className="text-xl font-extrabold text-primary">{soles(totalCobrado)}</p>
            </div>
          </div>

          <div className="flex flex-wrap gap-2" role="tablist" aria-label="Filtrar pedidos">
            {FILTROS.map((f) => (
              <button
                key={f.id}
                role="tab"
                aria-selected={filtro === f.id}
                onClick={() => setFiltro(f.id)}
                className={`px-3.5 py-1.5 rounded-full text-sm font-bold border transition-colors ${
                  filtro === f.id ? 'bg-primary text-white border-primary' : 'bg-card text-gray-400 border-gray-800 hover:text-ink'
                }`}
              >
                {f.texto} <span className={filtro === f.id ? 'text-white/80' : 'text-gray-500'}>{conteo[f.id]}</span>
              </button>
            ))}
          </div>

          <div className="relative">
            <Search size={16} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-500" />
            <input
              value={busqueda}
              onChange={(e) => setBusqueda(e.target.value)}
              placeholder="Buscar mesa, número de pedido o mozo…"
              aria-label="Buscar pedido"
              className="w-full bg-card border border-gray-800/50 rounded-2xl pl-10 pr-10 py-2.5 text-sm text-ink placeholder-gray-600 focus:outline-none focus:border-primary"
            />
            {busqueda && (
              <button onClick={() => setBusqueda('')} className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-500" aria-label="Limpiar búsqueda">
                <X size={16} />
              </button>
            )}
          </div>
        </div>

        {/* ── Lista ──────────────────────────────────────── */}
        <div className="px-5 mt-4 space-y-2">
          {cargando && !datos && <p className="text-sm text-gray-600">Cargando…</p>}
          {!cargando && visibles.length === 0 && (
            <div className="text-center py-10 text-gray-600">
              <ListOrdered size={28} className="mx-auto mb-2" />
              <p className="text-sm">
                {pedidos.length > 0 ? 'Ningún pedido coincide.' : rango ? 'No hay pedidos en esas fechas.' : 'Todavía no hay pedidos en esta jornada.'}
              </p>
            </div>
          )}
          {visibles.map((p) => {
            const grupo = GRUPO[p.estado] ?? 'abiertos';
            const et = ETIQUETA[grupo];
            return (
              <motion.button
                key={p.pedidoId}
                whileTap={{ scale: 0.99 }}
                onClick={() => abrirDetalle(p)}
                className={`w-full text-left bg-card border border-gray-800/50 rounded-2xl px-4 py-3 flex items-center gap-3 hover:border-primary/40 transition-colors ${grupo === 'anulados' ? 'opacity-70' : ''}`}
              >
                <span className="w-16 flex-shrink-0 text-sm font-bold text-gray-400 tabular-nums">
                  {variosDias && <span className="block text-xs text-gray-500">{diaMes(p.fechaOperativa)}</span>}
                  {horaLima(p.creadoEn)}
                </span>
                <div className="flex-1 min-w-0">
                  <p className="font-extrabold text-ink truncate">
                    {etiquetaMesa(p.salonNombre, p.mesaNumero)} <span className="text-gray-500 font-bold text-sm">· #{p.pedidoId}</span>
                  </p>
                  <p className="text-xs text-gray-500 truncate">
                    {p.mozo} · {p.cantidadPlatos} {p.cantidadPlatos === 1 ? 'plato' : 'platos'}
                    {p.metodosPago.length > 0 && ` · ${p.metodosPago.join(' + ')}`}
                    {p.comprobante && ` · ${p.comprobante.numero}`}
                  </p>
                  {p.comentario && (
                    <p className="text-[11px] text-amber-400 font-bold truncate flex items-center gap-1">
                      <MessageSquareText size={11} className="flex-shrink-0" /> {p.comentario}
                    </p>
                  )}
                </div>
                <div className="text-right flex-shrink-0">
                  <p className={`font-extrabold ${grupo === 'anulados' ? 'text-gray-500 line-through' : 'text-ink'}`}>{soles(p.total)}</p>
                  <span className={`inline-block mt-0.5 text-[10px] font-bold uppercase tracking-wide border rounded-full px-2 py-0.5 ${et.cls}`}>{et.texto}</span>
                </div>
              </motion.button>
            );
          })}
        </div>
      </div>

      {/* ── Detalle de un pedido ─────────────────────────── */}
      <AnimatePresence>
        {detalle && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-50 bg-black/70 flex items-end sm:items-center justify-center sm:p-4"
            onClick={() => setDetalle(null)}
          >
            <motion.div
              initial={{ y: 40 }}
              animate={{ y: 0 }}
              onClick={(e) => e.stopPropagation()}
              role="dialog"
              aria-label="Detalle del pedido"
              className="w-full sm:max-w-lg max-h-[90dvh] flex flex-col bg-card border border-gray-700/50 rounded-t-3xl sm:rounded-3xl"
            >
              <div className="px-5 pt-5 pb-3 flex items-start gap-3 border-b border-gray-800/60">
                <div className="flex-1 min-w-0">
                  <p className="text-[11px] text-gray-500 font-bold uppercase tracking-wider">
                    Pedido #{detalle.id} · {horaLima(detalle.resumen.creadoEn)}
                  </p>
                  <h2 className="text-xl font-extrabold text-ink leading-tight">
                    {etiquetaMesa(detalle.resumen.salonNombre, detalle.resumen.mesaNumero)}
                  </h2>
                  <p className="text-xs text-gray-500">Atendió {detalle.resumen.mozo}</p>
                </div>
                <span className={`mt-1 text-[10px] font-bold uppercase tracking-wide border rounded-full px-2 py-0.5 ${ETIQUETA[GRUPO[detalle.resumen.estado] ?? 'abiertos'].cls}`}>
                  {ETIQUETA[GRUPO[detalle.resumen.estado] ?? 'abiertos'].texto}
                </span>
                <button onClick={() => setDetalle(null)} className="p-1.5 rounded-full hover:bg-cardHighlight text-gray-400" aria-label="Cerrar">
                  <X size={18} />
                </button>
              </div>

              <div className="flex-1 overflow-y-auto px-5 py-4 space-y-4">
                {cargandoDetalle || !detalle.detalles ? (
                  <p className="text-sm text-gray-600">Cargando…</p>
                ) : (
                  <>
                    {detalle.comentario && (
                      <p className="text-sm font-bold text-amber-400 flex items-start gap-1.5">
                        <MessageSquareText size={15} className="mt-0.5 flex-shrink-0" /> {detalle.comentario}
                      </p>
                    )}
                    <div className="space-y-1.5">
                      {agruparDetallesPorProducto(detalle.detalles.filter((d) => detalle.estado === 'Cancelado' || d.estado !== 'Cancelado')).map((d) => (
                        <div key={d.id} className="flex justify-between gap-3 text-sm">
                          <span className={`min-w-0 break-words ${detalle.estado === 'Cancelado' ? 'text-gray-500 line-through' : 'text-gray-200'}`}>
                            {d.cantidad}x {d.productoNombre}
                            {d.notas && <span className="block text-xs font-bold text-amber-400 no-underline">» {d.notas}</span>}
                          </span>
                          <span className="text-gray-400 flex-shrink-0">{soles(d.monto * d.cantidad)}</span>
                        </div>
                      ))}
                    </div>
                    <div className="border-t border-dashed border-gray-700 pt-2 space-y-1 text-sm">
                      {detalle.descuento && (
                        <div className="flex justify-between text-gray-400">
                          <span>Descuento {detalle.descuento.nombre}</span>
                          <span>- {soles(detalle.descuento.montoDescuento)}</span>
                        </div>
                      )}
                      <div className="flex justify-between font-extrabold text-ink">
                        <span>Total</span><span>{soles(detalle.total)}</span>
                      </div>
                    </div>

                    {detalle.pagos.length > 0 && (
                      <div>
                        <p className="text-[11px] text-gray-500 font-bold uppercase tracking-wider mb-1">Pagos</p>
                        {detalle.pagos.map((g) => (
                          <div key={g.id} className={`flex justify-between text-sm ${g.anulado ? 'text-gray-500 line-through' : 'text-gray-300'}`}>
                            <span>{horaLima(g.creadoEn)} · {g.metodoPagoNombre}{g.referencia ? ` (${g.referencia})` : ''}</span>
                            <span>{soles(g.monto)}</span>
                          </div>
                        ))}
                      </div>
                    )}

                    {detalle.anulados?.length > 0 && (
                      <div>
                        <p className="text-[11px] text-red-400/90 font-bold uppercase tracking-wider mb-1 flex items-center gap-1">
                          <Ban size={12} /> Quitados / anulados
                        </p>
                        <ListaAnulaciones anulaciones={detalle.anulados} compacta />
                      </div>
                    )}
                    {detalle.cambiosPrecio?.length > 0 && (
                      <div>
                        <p className="text-[11px] text-gray-500 font-bold uppercase tracking-wider mb-1">Precios cambiados</p>
                        <ListaCambiosPrecio cambios={detalle.cambiosPrecio} />
                      </div>
                    )}
                  </>
                )}
              </div>

              {detalle.detalles && (
                <div className="px-5 py-4 border-t border-gray-800/60 flex flex-wrap gap-2">
                  {detalle.estado !== 'Cancelado' && (
                    <button
                      onClick={() => imprimirPrecuenta(detalle, empresa)}
                      className="flex-1 min-w-[9rem] flex items-center justify-center gap-1.5 py-3 rounded-2xl border border-primary/40 bg-primary/10 text-primary font-bold text-sm"
                    >
                      <ReceiptText size={16} /> Nota de pedido
                    </button>
                  )}
                  {detalle.resumen.comprobante && (
                    <button
                      onClick={() => verComprobante(detalle.id)}
                      className="flex-1 min-w-[9rem] flex items-center justify-center gap-1.5 py-3 rounded-2xl border border-gray-700 text-gray-200 font-bold text-sm"
                    >
                      <FileText size={16} /> {detalle.resumen.comprobante.tipo === 'Factura' ? 'Factura' : 'Boleta'} {detalle.resumen.comprobante.numero}
                    </button>
                  )}
                  {/* Sin comprobante, o el que tiene no vale (SUNAT lo rechazó o no se pudo enviar): se puede emitir otro. */}
                  {detalle.estado === 'Entregado' &&
                    (!detalle.resumen.comprobante || ['Rechazado', 'ErrorEnvio'].includes(detalle.resumen.comprobante.estado)) && (
                    <button
                      onClick={() => setEmitirPara(detalle)}
                      className="flex-1 min-w-[9rem] flex items-center justify-center gap-1.5 py-3 rounded-2xl bg-primary text-white font-bold text-sm"
                    >
                      <FileText size={16} /> Emitir boleta o factura
                    </button>
                  )}
                  {GRUPO[detalle.estado] === 'abiertos' && (
                    <button
                      onClick={() => navigate('/caja')}
                      className="flex-1 min-w-[9rem] flex items-center justify-center gap-1.5 py-3 rounded-2xl bg-primary text-white font-bold text-sm"
                    >
                      <Receipt size={16} /> Ir a cobrar
                    </button>
                  )}
                </div>
              )}
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      <AnimatePresence>
        {emitirPara && (
          <EmitirComprobanteModal
            pedido={emitirPara}
            onEmitido={(c) => {
              setEmitirPara(null);
              setDetalle(null);
              setComprobante(c);
              cargar();
            }}
            onClose={() => setEmitirPara(null)}
          />
        )}
      </AnimatePresence>

      <AnimatePresence>
        {comprobante && (
          <ComprobanteElectronico
            comprobante={comprobante}
            onClose={() => setComprobante(null)}
            onActualizado={setComprobante}
          />
        )}
      </AnimatePresence>
    </div>
  );
}
