import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { ChevronLeft, Receipt, X, Ban, ReceiptText, Tag, FileText } from 'lucide-react';
import toast from 'react-hot-toast';
import { getPedidos, getPedidoById, aplicarDescuento, quitarDescuento } from '../services/ordersService';
import { getMetodosPago } from '../services/metodosPagoService';
import { getDescuentos } from '../services/descuentosService';
import { createPago, anularPago } from '../services/pagosService';
import { getMetodoPagoIcon } from '../services/metodoPagoIcons';
import { connectPedidosHub, onHubEvent } from '../services/signalrService';
import ReciboPedido from '../components/ReciboPedido';
import EmitirComprobanteModal from '../components/EmitirComprobanteModal';
import ComprobanteElectronico from '../components/ComprobanteElectronico';
import { agruparDetallesPorProducto } from '../utils/agruparDetalles';

const containerVariants = {
  hidden: {},
  show: { transition: { staggerChildren: 0.05 } },
};

const cardVariants = {
  hidden: { opacity: 0, scale: 0.94, y: 10 },
  show: { opacity: 1, scale: 1, y: 0, transition: { type: 'spring', stiffness: 260, damping: 22 } },
};

// Caja no filtra por estado: muestra cualquier pedido activo (todo menos Cancelado/Entregado).
// El estado (que ahora mueve la pantalla de cocina en tiempo real) es solo una etiqueta
// informativa, no un requisito para poder cobrar.
const ESTADO_PEDIDO_STYLES = {
  Pendiente: { text: 'text-accentYellow', bg: 'bg-yellow-950/40', border: 'border-yellow-900/40', label: 'Pendiente' },
  EnPreparacion: { text: 'text-primary', bg: 'bg-orange-950/40', border: 'border-orange-900/40', label: 'En preparación' },
  Listo: { text: 'text-accentGreen', bg: 'bg-emerald-950/40', border: 'border-emerald-900/40', label: 'Listo' },
};

const saldoPendienteDe = (pedido) => {
  const pagado = (pedido.pagos ?? [])
    .filter((p) => !p.anulado)
    .reduce((acc, p) => acc + p.monto, 0);
  return Math.max(0, pedido.total - pagado);
};

export default function Caja() {
  const navigate = useNavigate();

  const [pedidosActivos, setPedidosActivos] = useState([]);
  const [isLoadingLista, setIsLoadingLista] = useState(true);
  const [metodos, setMetodos] = useState([]);
  const [descuentosVigentes, setDescuentosVigentes] = useState([]);

  const [pedidoSeleccionado, setPedidoSeleccionado] = useState(null);
  const [isLoadingDetalle, setIsLoadingDetalle] = useState(false);
  const [metodoPagoId, setMetodoPagoId] = useState(null);
  const [monto, setMonto] = useState('');
  const [referencia, setReferencia] = useState('');
  const [isCobrando, setIsCobrando] = useState(false);
  const [anulandoId, setAnulandoId] = useState(null);
  const [descuentoSeleccionadoId, setDescuentoSeleccionadoId] = useState(null);
  const [isAplicandoDescuento, setIsAplicandoDescuento] = useState(false);

  const [recibo, setRecibo] = useState(null); // { pedido, pagos }
  const [emitirPara, setEmitirPara] = useState(null); // pedido pagado pendiente de comprobante
  const [comprobante, setComprobante] = useState(null); // comprobante electrónico emitido

  const cargarListos = useCallback(() => {
    setIsLoadingLista(true);
    // Sin filtro de "estado": se traen todos los pedidos recientes y se descartan acá los
    // que ya no se pueden cobrar (Cancelado) o que ya están totalmente pagados (Entregado).
    // Se ordenan del más antiguo al más nuevo para que caja atienda en el orden en que
    // llegaron los pedidos.
    getPedidos({ pageSize: 100 })
      .then((res) => {
        const activos = res.items
          .filter((p) => p.estado !== 'Cancelado' && p.estado !== 'Entregado')
          .sort((a, b) => new Date(a.creadoEn) - new Date(b.creadoEn));
        setPedidosActivos(activos);
      })
      .catch(() => toast.error('No se pudieron cargar los pedidos activos'))
      .finally(() => setIsLoadingLista(false));
  }, []);

  useEffect(() => {
    cargarListos();
    getMetodosPago().then(setMetodos);
    getDescuentos({ soloVigentes: true }).then(setDescuentosVigentes);
  }, [cargarListos]);

  // Tiempo real: refrescar la lista cuando cambian pedidos/pagos/mesas.
  useEffect(() => {
    let unsubscribers = [];
    connectPedidosHub().then((connection) => {
      if (!connection) return;
      unsubscribers = [
        onHubEvent('NuevoPedido', cargarListos),
        onHubEvent('PedidoEstadoCambiado', cargarListos),
        onHubEvent('PedidoActualizado', cargarListos),
        onHubEvent('PagoRecibido', cargarListos),
        onHubEvent('PagoAnulado', cargarListos),
      ];
    });
    return () => unsubscribers.forEach((unsub) => unsub());
  }, [cargarListos]);

  const abrirCobro = async (pedidoResumen) => {
    setIsLoadingDetalle(true);
    setPedidoSeleccionado({ id: pedidoResumen.id }); // abre el panel de inmediato con un placeholder
    try {
      const detalle = await getPedidoById(pedidoResumen.id);
      setPedidoSeleccionado(detalle);
      const saldo = saldoPendienteDe(detalle);
      setMonto(saldo > 0 ? saldo.toFixed(2) : '');
      setMetodoPagoId(metodos[0]?.id ?? null);
      setReferencia('');
      setDescuentoSeleccionadoId(null);
    } catch (err) {
      toast.error('No se pudo cargar el detalle del pedido');
      setPedidoSeleccionado(null);
    } finally {
      setIsLoadingDetalle(false);
    }
  };

  const cerrarCobro = () => {
    setPedidoSeleccionado(null);
    setMonto('');
    setReferencia('');
  };

  const refrescarDetalle = async (pedidoId) => {
    const detalle = await getPedidoById(pedidoId);
    setPedidoSeleccionado(detalle);
    return detalle;
  };

  const handleCobrar = async () => {
    if (!pedidoSeleccionado?.total) return;
    const montoNum = parseFloat(monto);

    if (!metodoPagoId) {
      toast.error('Selecciona un método de pago');
      return;
    }
    if (!montoNum || montoNum <= 0) {
      toast.error('Ingresa un monto válido');
      return;
    }

    setIsCobrando(true);
    try {
      await createPago({
        pedidoId: pedidoSeleccionado.id,
        monto: montoNum,
        metodoPagoId,
        referencia: referencia.trim() || undefined,
      });

      const detalleActualizado = await refrescarDetalle(pedidoSeleccionado.id);
      cargarListos();

      if (detalleActualizado.estado === 'Entregado') {
        // Pedido totalmente pagado: elegir boleta/factura electrónica (o recibo interno).
        setEmitirPara(detalleActualizado);
        cerrarCobro();
      } else {
        const saldo = saldoPendienteDe(detalleActualizado);
        toast.success(`Pago parcial registrado. Saldo pendiente: S/ ${saldo.toFixed(2)}`);
        setMonto(saldo.toFixed(2));
        setReferencia('');
      }
    } catch (err) {
      toast.error(err.message ?? 'No se pudo registrar el cobro');
    } finally {
      setIsCobrando(false);
    }
  };

  const handleAnular = async (pago) => {
    const motivo = window.prompt(`Motivo para anular el pago de S/ ${pago.monto.toFixed(2)} (${pago.metodoPagoNombre}):`);
    if (motivo === null) return;
    if (!motivo.trim()) {
      toast.error('Debes indicar un motivo para anular el pago');
      return;
    }

    setAnulandoId(pago.id);
    try {
      await anularPago(pago.id, motivo.trim());
      toast.success('Pago anulado correctamente');
      await refrescarDetalle(pedidoSeleccionado.id);
      cargarListos();
    } catch (err) {
      toast.error(err.message ?? 'No se pudo anular el pago');
    } finally {
      setAnulandoId(null);
    }
  };

  const handleAplicarDescuento = async () => {
    if (!descuentoSeleccionadoId) {
      toast.error('Selecciona un descuento');
      return;
    }
    setIsAplicandoDescuento(true);
    try {
      const detalleActualizado = await aplicarDescuento(pedidoSeleccionado.id, descuentoSeleccionadoId);
      setPedidoSeleccionado(detalleActualizado);
      const saldo = saldoPendienteDe(detalleActualizado);
      setMonto(saldo > 0 ? saldo.toFixed(2) : '');
      cargarListos();
      toast.success('Descuento aplicado');
    } catch (err) {
      toast.error(err.message ?? 'No se pudo aplicar el descuento');
    } finally {
      setIsAplicandoDescuento(false);
    }
  };

  const handleQuitarDescuento = async () => {
    setIsAplicandoDescuento(true);
    try {
      const detalleActualizado = await quitarDescuento(pedidoSeleccionado.id);
      setPedidoSeleccionado(detalleActualizado);
      const saldo = saldoPendienteDe(detalleActualizado);
      setMonto(saldo > 0 ? saldo.toFixed(2) : '');
      cargarListos();
      toast.success('Descuento quitado');
    } catch (err) {
      toast.error(err.message ?? 'No se pudo quitar el descuento');
    } finally {
      setIsAplicandoDescuento(false);
    }
  };

  const saldoPendiente = pedidoSeleccionado?.total != null ? saldoPendienteDe(pedidoSeleccionado) : 0;
  const pagosActivos = (pedidoSeleccionado?.pagos ?? []).filter((p) => !p.anulado);
  const pagosAnulados = (pedidoSeleccionado?.pagos ?? []).filter((p) => p.anulado);

  return (
    <div className="min-h-screen bg-background pb-10">
      {/* ── Header ──────────────────────────────────────── */}
      <div className="px-5 pt-6 pb-4 flex items-center gap-3">
        <motion.button
          whileTap={{ scale: 0.9 }}
          onClick={() => navigate('/dashboard')}
          className="p-2 bg-card rounded-full hover:bg-cardHighlight border border-gray-800/50"
        >
          <ChevronLeft size={22} />
        </motion.button>
        <div className="flex-1">
          <h1 className="text-2xl font-extrabold tracking-tight">Caja</h1>
          <p className="text-xs text-gray-500 mt-0.5">Pedidos activos para cobrar</p>
        </div>
        <motion.button
          whileTap={{ scale: 0.95 }}
          onClick={() => navigate('/comprobantes')}
          className="flex items-center gap-1.5 px-3.5 py-2 bg-card rounded-full hover:bg-cardHighlight border border-gray-800/50 text-xs font-bold text-gray-300"
        >
          <FileText size={15} />
          Comprobantes
        </motion.button>
      </div>

      {/* ── Lista de pedidos activos ────────────────────── */}
      {isLoadingLista && (
        <p className="px-5 text-sm text-gray-600">Cargando…</p>
      )}

      {!isLoadingLista && pedidosActivos.length === 0 && (
        <div className="px-5 mt-10 flex flex-col items-center text-center">
          <div className="w-16 h-16 bg-card rounded-3xl flex items-center justify-center mb-4 border border-gray-800">
            <Receipt size={28} className="text-gray-600" />
          </div>
          <p className="text-gray-400 text-sm">No hay pedidos activos para cobrar por ahora.</p>
        </div>
      )}

      <motion.div
        variants={containerVariants}
        initial="hidden"
        animate="show"
        className="px-5 grid grid-cols-1 gap-3"
      >
        {pedidosActivos.map((pedido) => {
          const saldo = saldoPendienteDe(pedido);
          const estiloEstado = ESTADO_PEDIDO_STYLES[pedido.estado] ?? ESTADO_PEDIDO_STYLES.Pendiente;
          return (
            <motion.button
              key={pedido.id}
              variants={cardVariants}
              whileTap={{ scale: 0.98 }}
              onClick={() => abrirCobro(pedido)}
              className="bg-card border border-gray-800/50 rounded-2xl p-4 flex justify-between items-center text-left hover:border-primary/40 transition-colors"
            >
              <div>
                <div className="flex items-center gap-2">
                  <p className="font-extrabold text-white text-lg">Mesa {pedido.mesaNumero}</p>
                  <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full border ${estiloEstado.bg} ${estiloEstado.border} ${estiloEstado.text}`}>
                    {estiloEstado.label}
                  </span>
                </div>
                <p className="text-xs text-gray-500 mt-0.5">Pedido #{pedido.id} · {pedido.detalles.length} ítems</p>
              </div>
              <div className="text-right">
                <p className="text-primary font-extrabold text-lg">S/ {saldo.toFixed(2)}</p>
                {saldo < pedido.total && (
                  <p className="text-[10px] text-gray-500">de S/ {pedido.total.toFixed(2)}</p>
                )}
              </div>
            </motion.button>
          );
        })}
      </motion.div>

      {/* ── Panel de cobro ──────────────────────────────── */}
      <AnimatePresence>
        {pedidoSeleccionado && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-50 flex flex-col justify-end"
          >
            <motion.div className="absolute inset-0 bg-black/70 backdrop-blur-sm" onClick={cerrarCobro} />

            <motion.div
              initial={{ y: '100%' }}
              animate={{ y: 0 }}
              exit={{ y: '100%' }}
              transition={{ type: 'spring', stiffness: 300, damping: 30 }}
              className="relative z-10 bg-card w-full rounded-t-3xl flex flex-col shadow-2xl border-t border-gray-700/40"
              style={{ maxHeight: '92dvh' }}
            >
              <div className="flex justify-center pt-3 pb-1">
                <div className="w-10 h-1 rounded-full bg-gray-700" />
              </div>

              <div className="px-5 pb-3 pt-2 flex justify-between items-start border-b border-gray-800/60">
                <div>
                  <p className="text-[11px] text-gray-500 font-medium uppercase tracking-wider">Cobrando</p>
                  <div className="flex items-center gap-2">
                    <h2 className="text-xl font-extrabold tracking-tight">
                      Mesa {pedidoSeleccionado.mesaNumero ?? '—'}
                    </h2>
                    {pedidoSeleccionado.estado && (
                      <span className={`text-[10px] font-bold px-2 py-0.5 rounded-full border ${
                        (ESTADO_PEDIDO_STYLES[pedidoSeleccionado.estado] ?? ESTADO_PEDIDO_STYLES.Pendiente).bg
                      } ${(ESTADO_PEDIDO_STYLES[pedidoSeleccionado.estado] ?? ESTADO_PEDIDO_STYLES.Pendiente).border} ${
                        (ESTADO_PEDIDO_STYLES[pedidoSeleccionado.estado] ?? ESTADO_PEDIDO_STYLES.Pendiente).text
                      }`}>
                        {(ESTADO_PEDIDO_STYLES[pedidoSeleccionado.estado] ?? ESTADO_PEDIDO_STYLES.Pendiente).label}
                      </span>
                    )}
                  </div>
                </div>
                <motion.button
                  whileTap={{ scale: 0.9 }}
                  onClick={cerrarCobro}
                  className="p-2 bg-background/60 rounded-full hover:bg-gray-700 transition-colors mt-1"
                >
                  <X size={18} className="text-gray-400" />
                </motion.button>
              </div>

              {isLoadingDetalle ? (
                <p className="px-5 py-8 text-center text-sm text-gray-600">Cargando pedido…</p>
              ) : (
                <div className="flex-1 overflow-y-auto px-5 py-4 space-y-5 pb-8">
                  {/* Ítems del pedido (consolidados: si el mismo producto se pidió más de una
                      vez en distintos momentos, se muestra como una sola línea sumada) */}
                  <div className="space-y-1.5">
                    {agruparDetallesPorProducto(pedidoSeleccionado.detalles).map((d) => (
                      <div key={d.id} className="flex justify-between text-sm">
                        <span className="text-gray-300">{d.cantidad}x {d.productoNombre}</span>
                        <span className="text-gray-400">S/ {(d.monto * d.cantidad).toFixed(2)}</span>
                      </div>
                    ))}
                  </div>

                  <div className="h-px bg-gray-800/60" />

                  {pedidoSeleccionado.descuento && (
                    <div className="flex justify-between items-center text-sm">
                      <span className="text-gray-400">Sub total</span>
                      <span className="text-gray-400">S/ {pedidoSeleccionado.subTotal.toFixed(2)}</span>
                    </div>
                  )}

                  {pedidoSeleccionado.descuento && (
                    <div className="flex justify-between items-center text-sm">
                      <span className="text-accentGreen flex items-center gap-1">
                        <Tag size={13} /> {pedidoSeleccionado.descuento.nombre}
                        {pedidoSeleccionado.descuento.esPorcentaje ? ` (${pedidoSeleccionado.descuento.valor}%)` : ''}
                      </span>
                      <span className="text-accentGreen font-semibold">
                        - S/ {pedidoSeleccionado.descuento.montoDescuento.toFixed(2)}
                      </span>
                    </div>
                  )}

                  <div className="flex justify-between items-center">
                    <span className="text-gray-400 text-sm">Total del pedido</span>
                    <span className="text-white font-bold">S/ {pedidoSeleccionado.total.toFixed(2)}</span>
                  </div>

                  {/* Descuento: aplicar (solo si aún no tiene pagos ni descuento) o quitar */}
                  {pagosActivos.length === 0 && (
                    pedidoSeleccionado.descuento ? (
                      <button
                        onClick={handleQuitarDescuento}
                        disabled={isAplicandoDescuento}
                        className="w-full flex items-center justify-center gap-2 py-2.5 rounded-2xl border border-red-900/40 text-red-500 hover:bg-red-500/10 transition-colors disabled:opacity-50 text-xs font-bold"
                      >
                        <X size={13} />
                        Quitar descuento
                      </button>
                    ) : descuentosVigentes.length > 0 ? (
                      <div className="bg-background rounded-2xl p-3.5 border border-gray-800/40 space-y-2.5">
                        <p className="text-[11px] font-bold text-gray-500 uppercase tracking-wider">Aplicar descuento</p>
                        <select
                          value={descuentoSeleccionadoId ?? ''}
                          onChange={(e) => setDescuentoSeleccionadoId(Number(e.target.value) || null)}
                          className="w-full bg-card border border-gray-700/50 rounded-xl px-3 py-2.5 text-white text-sm focus:outline-none focus:border-primary transition-colors"
                        >
                          <option value="">Selecciona un descuento…</option>
                          {descuentosVigentes.map((d) => (
                            <option key={d.id} value={d.id}>
                              {d.nombre} ({d.esPorcentaje ? `${d.valor}%` : `S/ ${d.valor.toFixed(2)}`})
                            </option>
                          ))}
                        </select>
                        <button
                          onClick={handleAplicarDescuento}
                          disabled={isAplicandoDescuento || !descuentoSeleccionadoId}
                          className="w-full flex items-center justify-center gap-2 py-2.5 rounded-xl bg-primary/15 border border-primary/40 text-primary hover:bg-primary/25 transition-colors disabled:opacity-50 text-xs font-bold"
                        >
                          <Tag size={13} />
                          Aplicar descuento
                        </button>
                      </div>
                    ) : null
                  )}

                  {/* Historial de pagos, si hay pagos parciales */}
                  {pagosActivos.length > 0 && (
                    <div className="bg-background rounded-2xl p-3.5 border border-gray-800/40 space-y-2">
                      <p className="text-[11px] font-bold text-gray-500 uppercase tracking-wider">Pagos registrados</p>
                      {pagosActivos.map((p) => (
                        <div key={p.id} className="flex justify-between items-center text-sm">
                          <span className="text-gray-300">
                            {getMetodoPagoIcon(p.metodoPagoNombre)} {p.metodoPagoNombre}
                            {p.referencia ? ` · ${p.referencia}` : ''}
                          </span>
                          <div className="flex items-center gap-2">
                            <span className="text-white font-semibold">S/ {p.monto.toFixed(2)}</span>
                            <button
                              onClick={() => handleAnular(p)}
                              disabled={anulandoId === p.id}
                              className="p-1.5 hover:bg-red-500/20 rounded-full text-gray-600 hover:text-red-500 transition-colors disabled:opacity-50"
                              title="Anular pago"
                            >
                              <Ban size={14} />
                            </button>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}

                  {pagosAnulados.length > 0 && (
                    <div className="space-y-1">
                      {pagosAnulados.map((p) => (
                        <p key={p.id} className="text-xs text-gray-600 line-through">
                          {p.metodoPagoNombre} · S/ {p.monto.toFixed(2)} (anulado)
                        </p>
                      ))}
                    </div>
                  )}

                  <div className="flex justify-between items-center bg-orange-950/30 border border-orange-900/30 rounded-2xl px-4 py-3">
                    <span className="text-primary text-sm font-bold">Saldo pendiente</span>
                    <span className="text-primary font-extrabold text-xl">S/ {saldoPendiente.toFixed(2)}</span>
                  </div>

                  {saldoPendiente > 0 && (
                    <>
                      {/* Selector de método de pago */}
                      <div>
                        <p className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Método de pago</p>
                        <div className="grid grid-cols-3 gap-2">
                          {metodos.map((m) => (
                            <button
                              key={m.id}
                              onClick={() => setMetodoPagoId(m.id)}
                              className={`flex flex-col items-center justify-center gap-1 py-3 rounded-2xl border text-xs font-semibold transition-colors ${
                                metodoPagoId === m.id
                                  ? 'bg-primary/20 border-primary text-primary'
                                  : 'bg-background border-gray-800/50 text-gray-400 hover:border-gray-600'
                              }`}
                            >
                              <span className="text-lg">{getMetodoPagoIcon(m.nombre)}</span>
                              {m.nombre}
                            </button>
                          ))}
                        </div>
                        {metodos.length === 0 && (
                          <p className="text-xs text-gray-600 mt-2">
                            No hay métodos de pago configurados. Pídele al administrador que los active.
                          </p>
                        )}
                      </div>

                      {/* Monto */}
                      <div>
                        <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">
                          Monto a cobrar
                        </label>
                        <div className="relative">
                          <span className="absolute left-4 top-1/2 -translate-y-1/2 text-gray-500 text-sm">S/</span>
                          <input
                            type="number"
                            step="0.01"
                            min="0"
                            value={monto}
                            onChange={(e) => setMonto(e.target.value)}
                            className="w-full bg-background border border-gray-700/50 rounded-2xl pl-10 pr-4 py-3.5 text-white text-lg font-bold focus:outline-none focus:border-primary transition-colors"
                          />
                        </div>
                        {parseFloat(monto) > 0 && parseFloat(monto) < saldoPendiente - 0.009 && (
                          <p className="text-xs text-accentYellow mt-1.5">
                            Pago parcial: quedará un saldo de S/ {(saldoPendiente - parseFloat(monto)).toFixed(2)}.
                          </p>
                        )}
                      </div>

                      {/* Referencia (opcional) */}
                      <div>
                        <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">
                          Referencia (opcional)
                        </label>
                        <input
                          type="text"
                          value={referencia}
                          onChange={(e) => setReferencia(e.target.value)}
                          placeholder="Ej: código de operación Yape"
                          className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm"
                        />
                      </div>

                      <motion.button
                        whileTap={{ scale: 0.97 }}
                        onClick={handleCobrar}
                        disabled={isCobrando}
                        className="w-full bg-primary hover:bg-primaryHover disabled:opacity-60 text-white font-extrabold py-4 rounded-2xl flex justify-center items-center gap-2 transition-colors shadow-glow-orange text-base"
                      >
                        {isCobrando ? (
                          <span className="animate-pulse">Procesando…</span>
                        ) : (
                          <>
                            <ReceiptText size={18} />
                            Registrar cobro
                          </>
                        )}
                      </motion.button>
                    </>
                  )}

                  {saldoPendiente <= 0 && (
                    <p className="text-center text-sm text-accentGreen font-semibold py-2">
                      Este pedido ya está pagado en su totalidad.
                    </p>
                  )}
                </div>
              )}
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* ── Emisión de boleta / factura electrónica ─────── */}
      <AnimatePresence>
        {emitirPara && (
          <EmitirComprobanteModal
            pedido={emitirPara}
            onEmitido={(c) => {
              setEmitirPara(null);
              setComprobante(c);
            }}
            onReciboInterno={() => {
              setRecibo({ pedido: emitirPara, pagos: emitirPara.pagos });
              setEmitirPara(null);
            }}
            onClose={() => setEmitirPara(null)}
          />
        )}
      </AnimatePresence>

      <AnimatePresence>
        {comprobante && (
          <ComprobanteElectronico comprobante={comprobante} onClose={() => setComprobante(null)} />
        )}
      </AnimatePresence>

      {/* ── Recibo interno ──────────────────────────────── */}
      <AnimatePresence>
        {recibo && (
          <ReciboPedido
            pedido={recibo.pedido}
            pagos={recibo.pagos}
            onClose={() => setRecibo(null)}
          />
        )}
      </AnimatePresence>
    </div>
  );
}
