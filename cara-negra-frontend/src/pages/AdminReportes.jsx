import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { ChevronLeft, TrendingUp, Receipt, Ban, Wallet, Trophy, Download, Table2, Percent } from 'lucide-react';
import toast from 'react-hot-toast';
import {
  getResumenVentas,
  getProductosMasVendidos,
  exportarResumenVentas,
  exportarPedidosPorMesa,
} from '../services/reportesService';
import { getMetodoPagoIcon } from '../services/metodoPagoIcons';
import { getMesasAdmin } from '../services/mesasAdminService';
import { getPedidos } from '../services/ordersService';
import { agruparDetallesPorProducto } from '../utils/agruparDetalles';

const startOfDay = (d) => new Date(d.getFullYear(), d.getMonth(), d.getDate());
const toInputValue = (d) => {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
};

const PRESETS = [
  { key: 'hoy', label: 'Hoy', rango: () => { const hoy = startOfDay(new Date()); return [hoy, hoy]; } },
  {
    key: '7d',
    label: 'Últimos 7 días',
    rango: () => { const hoy = startOfDay(new Date()); const desde = new Date(hoy); desde.setDate(desde.getDate() - 6); return [desde, hoy]; },
  },
  {
    key: 'mes',
    label: 'Este mes',
    rango: () => { const hoy = startOfDay(new Date()); const desde = new Date(hoy.getFullYear(), hoy.getMonth(), 1); return [desde, hoy]; },
  },
];

export default function AdminReportes() {
  const navigate = useNavigate();
  const [preset, setPreset] = useState('hoy');
  const [fechaDesde, setFechaDesde] = useState(() => PRESETS[0].rango()[0]);
  const [fechaHasta, setFechaHasta] = useState(() => PRESETS[0].rango()[1]);

  const [resumen, setResumen] = useState(null);
  const [topProductos, setTopProductos] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [exportandoResumen, setExportandoResumen] = useState(false);

  // ── Pedidos por mesa ──────────────────────────────────
  const [mesas, setMesas] = useState([]);
  const [mesaSeleccionadaId, setMesaSeleccionadaId] = useState('');
  const [pedidosMesa, setPedidosMesa] = useState([]);
  const [isLoadingPedidosMesa, setIsLoadingPedidosMesa] = useState(false);
  const [exportandoMesa, setExportandoMesa] = useState(false);

  const cargar = useCallback((desde, hasta) => {
    setIsLoading(true);
    Promise.all([getResumenVentas(desde, hasta), getProductosMasVendidos(desde, hasta, 10)])
      .then(([r, top]) => {
        setResumen(r);
        setTopProductos(top);
      })
      .catch((err) => toast.error(err.message ?? 'No se pudo cargar el reporte'))
      .finally(() => setIsLoading(false));
  }, []);

  useEffect(() => {
    cargar(fechaDesde, fechaHasta);
    // Solo se dispara cuando cambia el rango efectivo, no en cada render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fechaDesde.getTime(), fechaHasta.getTime()]);

  useEffect(() => {
    getMesasAdmin()
      .then(setMesas)
      .catch(() => toast.error('No se pudieron cargar las mesas'));
  }, []);

  useEffect(() => {
    if (!mesaSeleccionadaId) {
      setPedidosMesa([]);
      return;
    }
    setIsLoadingPedidosMesa(true);
    getPedidos({
      mesaId: mesaSeleccionadaId,
      fechaDesde: toInputValue(fechaDesde),
      fechaHasta: toInputValue(fechaHasta),
      pageSize: 200,
    })
      .then((res) => setPedidosMesa(res.items))
      .catch((err) => toast.error(err.message ?? 'No se pudieron cargar los pedidos de la mesa'))
      .finally(() => setIsLoadingPedidosMesa(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mesaSeleccionadaId, fechaDesde.getTime(), fechaHasta.getTime()]);

  const handleExportarResumen = async () => {
    setExportandoResumen(true);
    try {
      await exportarResumenVentas(fechaDesde, fechaHasta);
    } catch (err) {
      toast.error(err.message ?? 'No se pudo exportar el reporte');
    } finally {
      setExportandoResumen(false);
    }
  };

  const handleExportarPedidosMesa = async () => {
    const mesa = mesas.find((m) => String(m.id) === String(mesaSeleccionadaId));
    if (!mesa) return;
    setExportandoMesa(true);
    try {
      await exportarPedidosPorMesa(mesa.id, mesa.numeroMesa, fechaDesde, fechaHasta);
    } catch (err) {
      toast.error(err.message ?? 'No se pudo exportar los pedidos de la mesa');
    } finally {
      setExportandoMesa(false);
    }
  };

  const aplicarPreset = (p) => {
    const [desde, hasta] = p.rango();
    setPreset(p.key);
    setFechaDesde(desde);
    setFechaHasta(hasta);
  };

  const cambiarFechaManual = (campo, valor) => {
    if (!valor) return;
    const [y, m, d] = valor.split('-').map(Number);
    const fecha = new Date(y, m - 1, d);
    setPreset('personalizado');
    if (campo === 'desde') setFechaDesde(fecha);
    else setFechaHasta(fecha);
  };

  return (
    <div className="min-h-screen bg-background pb-10">
      <div className="px-5 pt-6 pb-4 flex items-center gap-3">
        <motion.button
          whileTap={{ scale: 0.9 }}
          onClick={() => navigate('/admin')}
          className="p-2 bg-card rounded-full hover:bg-cardHighlight border border-gray-800/50"
        >
          <ChevronLeft size={22} />
        </motion.button>
        <div>
          <h1 className="text-2xl font-extrabold tracking-tight">Reportes de ventas</h1>
          <p className="text-xs text-gray-500 mt-0.5">Resumen de cobros y productos más vendidos</p>
        </div>
      </div>

      {/* ── Selector de rango ───────────────────────────────── */}
      <div className="px-5 flex gap-2 mb-3 flex-wrap">
        {PRESETS.map((p) => (
          <button
            key={p.key}
            onClick={() => aplicarPreset(p)}
            className={`px-4 py-2 rounded-full text-sm font-semibold transition-colors ${
              preset === p.key
                ? 'bg-primary text-white shadow-glow-orange'
                : 'bg-card text-gray-400 border border-gray-800/50 hover:text-white'
            }`}
          >
            {p.label}
          </button>
        ))}
      </div>

      <div className="px-5 flex gap-2 mb-6">
        <input
          type="date"
          value={toInputValue(fechaDesde)}
          onChange={(e) => cambiarFechaManual('desde', e.target.value)}
          className="flex-1 bg-card border border-gray-800/50 rounded-2xl px-3 py-2.5 text-white text-sm focus:outline-none focus:border-primary transition-colors"
        />
        <input
          type="date"
          value={toInputValue(fechaHasta)}
          onChange={(e) => cambiarFechaManual('hasta', e.target.value)}
          className="flex-1 bg-card border border-gray-800/50 rounded-2xl px-3 py-2.5 text-white text-sm focus:outline-none focus:border-primary transition-colors"
        />
      </div>

      <div className="px-5 mb-6">
        <button
          onClick={handleExportarResumen}
          disabled={exportandoResumen || isLoading}
          className="w-full flex items-center justify-center gap-2 py-3 rounded-2xl border border-dashed border-gray-700 text-gray-400 hover:text-white hover:border-primary/50 transition-colors font-semibold text-sm disabled:opacity-50"
        >
          <Download size={15} />
          {exportandoResumen ? 'Exportando…' : 'Exportar resumen a Excel'}
        </button>
      </div>

      {isLoading || !resumen ? (
        <p className="px-5 text-sm text-gray-600">Cargando…</p>
      ) : (
        <>
          {/* ── Tarjetas resumen ────────────────────────────── */}
          <div className="px-5 grid grid-cols-2 gap-3 mb-6">
            <div className="bg-orange-950/30 border border-orange-900/30 rounded-2xl p-4">
              <div className="flex items-center gap-2 text-primary mb-1.5">
                <Wallet size={15} />
                <span className="text-[10px] font-bold uppercase tracking-wider">Total ventas</span>
              </div>
              <p className="text-2xl font-extrabold text-white">S/ {resumen.totalVentas.toFixed(2)}</p>
            </div>
            <div className="bg-card border border-gray-800/50 rounded-2xl p-4">
              <div className="flex items-center gap-2 text-gray-400 mb-1.5">
                <TrendingUp size={15} />
                <span className="text-[10px] font-bold uppercase tracking-wider">Ticket promedio</span>
              </div>
              <p className="text-2xl font-extrabold text-white">S/ {resumen.ticketPromedio.toFixed(2)}</p>
            </div>
            <div className="bg-card border border-gray-800/50 rounded-2xl p-4">
              <div className="flex items-center gap-2 text-gray-400 mb-1.5">
                <Receipt size={15} />
                <span className="text-[10px] font-bold uppercase tracking-wider">Pedidos</span>
              </div>
              <p className="text-2xl font-extrabold text-white">{resumen.cantidadPedidos}</p>
              <p className="text-[11px] text-gray-500 mt-0.5">{resumen.cantidadPedidosPagados} pagados</p>
            </div>
            <div className="bg-card border border-gray-800/50 rounded-2xl p-4">
              <div className="flex items-center gap-2 text-gray-400 mb-1.5">
                <Ban size={15} />
                <span className="text-[10px] font-bold uppercase tracking-wider">Cancelados</span>
              </div>
              <p className="text-2xl font-extrabold text-white">{resumen.cantidadPedidosCancelados}</p>
            </div>
            <div className="bg-card border border-gray-800/50 rounded-2xl p-4 col-span-2">
              <div className="flex items-center gap-2 text-gray-400 mb-1.5">
                <Percent size={15} />
                <span className="text-[10px] font-bold uppercase tracking-wider">Total descuentos otorgados</span>
              </div>
              <p className="text-2xl font-extrabold text-white">S/ {resumen.totalDescuentos.toFixed(2)}</p>
            </div>
          </div>

          {/* ── Ventas por método de pago ───────────────────── */}
          <div className="px-5 mb-6">
            <h2 className="text-sm font-bold text-gray-400 uppercase tracking-wider mb-3">Ventas por método de pago</h2>
            {resumen.ventasPorMetodoPago.length === 0 ? (
              <p className="text-sm text-gray-600">No hay ventas registradas en este rango.</p>
            ) : (
              <div className="bg-card border border-gray-800/50 rounded-2xl divide-y divide-gray-800/60">
                {resumen.ventasPorMetodoPago.map((v) => (
                  <div key={v.metodoPagoNombre} className="flex justify-between items-center px-4 py-3">
                    <span className="text-sm text-gray-300">
                      {getMetodoPagoIcon(v.metodoPagoNombre)} {v.metodoPagoNombre}
                      <span className="text-gray-600 text-xs ml-1.5">({v.cantidadPagos})</span>
                    </span>
                    <span className="font-bold text-white text-sm">S/ {v.total.toFixed(2)}</span>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* ── Productos más vendidos ──────────────────────── */}
          <div className="px-5">
            <h2 className="text-sm font-bold text-gray-400 uppercase tracking-wider mb-3 flex items-center gap-1.5">
              <Trophy size={14} />
              Productos más vendidos
            </h2>
            {topProductos.length === 0 ? (
              <p className="text-sm text-gray-600">No hay ventas de productos en este rango.</p>
            ) : (
              <div className="space-y-2">
                {topProductos.map((p, idx) => (
                  <div key={p.productoId} className="bg-card border border-gray-800/50 rounded-2xl p-3.5 flex items-center gap-3">
                    <span className="w-7 h-7 rounded-full bg-background flex items-center justify-center text-xs font-extrabold text-gray-400 flex-shrink-0">
                      {idx + 1}
                    </span>
                    <div className="flex-1 min-w-0">
                      <p className="font-bold text-white text-sm truncate">{p.productoNombre}</p>
                      <p className="text-xs text-gray-500">{p.categoriaNombre} · {p.cantidadVendida} vendidos</p>
                    </div>
                    <p className="text-primary font-extrabold text-sm flex-shrink-0">S/ {p.totalVendido.toFixed(2)}</p>
                  </div>
                ))}
              </div>
            )}
          </div>
        </>
      )}

      {/* ── Pedidos por mesa ─────────────────────────────── */}
      <div className="px-5 mt-8">
        <h2 className="text-sm font-bold text-gray-400 uppercase tracking-wider mb-3 flex items-center gap-1.5">
          <Table2 size={14} />
          Pedidos por mesa
        </h2>

        <div className="flex gap-2 mb-3">
          <select
            value={mesaSeleccionadaId}
            onChange={(e) => setMesaSeleccionadaId(e.target.value)}
            className="flex-1 bg-card border border-gray-800/50 rounded-2xl px-3 py-2.5 text-white text-sm focus:outline-none focus:border-primary transition-colors"
          >
            <option value="">Selecciona una mesa…</option>
            {mesas.map((m) => (
              <option key={m.id} value={m.id}>Mesa {m.numeroMesa}</option>
            ))}
          </select>
          <button
            onClick={handleExportarPedidosMesa}
            disabled={!mesaSeleccionadaId || exportandoMesa || pedidosMesa.length === 0}
            className="px-4 rounded-2xl border border-dashed border-gray-700 text-gray-400 hover:text-white hover:border-primary/50 transition-colors disabled:opacity-40 flex items-center gap-1.5 text-xs font-semibold"
            title="Exportar a Excel"
          >
            <Download size={14} />
            Excel
          </button>
        </div>

        {!mesaSeleccionadaId && (
          <p className="text-sm text-gray-600">Elige una mesa para ver sus pedidos en el rango de fechas seleccionado arriba.</p>
        )}

        {mesaSeleccionadaId && isLoadingPedidosMesa && (
          <p className="text-sm text-gray-600">Cargando pedidos…</p>
        )}

        {mesaSeleccionadaId && !isLoadingPedidosMesa && pedidosMesa.length === 0 && (
          <p className="text-sm text-gray-600">Esta mesa no tiene pedidos en el rango de fechas seleccionado.</p>
        )}

        {mesaSeleccionadaId && !isLoadingPedidosMesa && pedidosMesa.length > 0 && (
          <div className="space-y-2">
            {pedidosMesa.map((pedido) => (
              <div key={pedido.id} className="bg-card border border-gray-800/50 rounded-2xl p-3.5">
                <div className="flex justify-between items-center mb-1.5">
                  <span className="text-sm font-bold text-white">Pedido #{pedido.id}</span>
                  <span className="text-xs text-gray-500">
                    {new Date(pedido.creadoEn).toLocaleString('es-PE', { dateStyle: 'short', timeStyle: 'short' })}
                  </span>
                </div>
                <div className="space-y-1 mb-2">
                  {agruparDetallesPorProducto(pedido.detalles).map((d) => (
                    <p key={d.id} className="text-xs text-gray-400">
                      {d.cantidad}x {d.productoNombre}
                    </p>
                  ))}
                </div>
                <div className="flex justify-between items-center text-xs">
                  <span className="text-gray-500">{pedido.estado}</span>
                  <span className="font-bold text-primary">S/ {pedido.total.toFixed(2)}</span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
