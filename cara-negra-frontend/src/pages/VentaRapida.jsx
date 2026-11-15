import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { Minus, Plus, Trash2, ListOrdered, CalendarClock, LogOut, ShoppingBag, Search, X, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { useAuth } from '../context/AuthContext';
import { useJornada } from '../context/JornadaContext';
import { getMenuItems } from '../services/menuService';
import { getMetodosPago } from '../services/metodosPagoService';
import { getMetodoPagoIcon } from '../services/metodoPagoIcons';
import { ventaDirecta, nuevoCodigoOperacion } from '../services/ventasService';
import useAccionUnica from '../hooks/useAccionUnica';
import FotoProducto from '../components/FotoProducto';
import EmitirComprobanteModal from '../components/EmitirComprobanteModal';
import ComprobanteElectronico from '../components/ComprobanteElectronico';
import { soles } from '../utils/formato';

const esEfectivo = (nombre) => (nombre ?? '').toLowerCase().includes('efectivo');
const redondear = (n) => Math.round(Number(n || 0) * 100) / 100;
const TODAS = 'Todos';

/**
 * Venta rápida (mostrador): entradas de la piscina, ropa de baño, flotadores… Se toca el producto,
 * se cobra y listo: sin mesa, sin cocina y sin dejar nada abierto. Al cobrar se ofrece la boleta.
 *
 * La TAQUILLA vende en la caja de la piscina (su propia jornada y arqueo); el CAJERO y el ADMIN
 * venden en la caja del restaurante. Solo aparecen los productos de categorías "de mostrador".
 */
export default function VentaRapida() {
  const navigate = useNavigate();
  const { user, logout } = useAuth();
  const { abierta, cargada } = useJornada();
  const esTaquilla = user?.rol === 'TAQUILLA';
  const unaVez = useAccionUnica();

  const [productos, setProductos] = useState([]);
  const [metodos, setMetodos] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [categoria, setCategoria] = useState(TODAS);
  const [busqueda, setBusqueda] = useState('');
  const [carrito, setCarrito] = useState([]); // [{ id, nombre, precio, cantidad }]
  const [metodoId, setMetodoId] = useState(null);
  const [recibido, setRecibido] = useState('');
  const [referencia, setReferencia] = useState('');
  const [cobrando, setCobrando] = useState(false);
  const [operacionId, setOperacionId] = useState(nuevoCodigoOperacion);
  const [venta, setVenta] = useState(null); // última venta: { pedidoId, total, vuelto }
  const [emitirPara, setEmitirPara] = useState(null);
  const [comprobante, setComprobante] = useState(null);

  useEffect(() => {
    let vivo = true;
    Promise.all([getMenuItems(), getMetodosPago()])
      .then(([items, mets]) => {
        if (!vivo) return;
        setProductos(items.filter((p) => p.esMostrador));
        setMetodos(mets);
        setMetodoId((actual) => actual ?? (mets.find((m) => esEfectivo(m.nombre)) ?? mets[0])?.id ?? null);
      })
      .catch((err) => toast.error(err.message ?? 'No se pudo cargar la información'))
      .finally(() => vivo && setCargando(false));
    return () => { vivo = false; };
  }, []);

  const categorias = useMemo(
    () => [TODAS, ...Array.from(new Set(productos.map((p) => p.categoryName))).sort()],
    [productos]
  );

  const visibles = useMemo(() => {
    const q = busqueda.trim().toLowerCase();
    return productos.filter((p) =>
      (categoria === TODAS || p.categoryName === categoria) && (!q || p.name.toLowerCase().includes(q)));
  }, [productos, categoria, busqueda]);

  const total = redondear(carrito.reduce((acc, l) => acc + l.precio * l.cantidad, 0));
  const unidades = carrito.reduce((acc, l) => acc + l.cantidad, 0);
  const metodo = metodos.find((m) => m.id === metodoId);
  const pagaEnEfectivo = esEfectivo(metodo?.nombre);
  const montoRecibido = recibido === '' ? total : redondear(String(recibido).replace(',', '.'));
  const vuelto = pagaEnEfectivo ? redondear(montoRecibido - total) : 0;
  const faltaDinero = pagaEnEfectivo && montoRecibido < total;

  const agregar = (p) => {
    setVenta(null);
    setCarrito((c) => {
      const existe = c.find((l) => l.id === p.id);
      if (existe) return c.map((l) => (l.id === p.id ? { ...l, cantidad: Math.min(100, l.cantidad + 1) } : l));
      return [...c, { id: p.id, nombre: p.name, precio: Number(p.price), cantidad: 1 }];
    });
  };
  const cambiar = (id, delta) =>
    setCarrito((c) => c.map((l) => (l.id === id ? { ...l, cantidad: l.cantidad + delta } : l)).filter((l) => l.cantidad > 0));
  const quitar = (id) => setCarrito((c) => c.filter((l) => l.id !== id));
  const vaciar = () => {
    setCarrito([]);
    setRecibido('');
    setReferencia('');
    setOperacionId(nuevoCodigoOperacion());
  };

  const cobrar = () => unaVez(async () => {
    if (carrito.length === 0) return toast.error('Agrega al menos un producto');
    if (!metodo) return toast.error('Elige cómo paga el cliente');
    if (faltaDinero) return toast.error('El monto recibido no alcanza para el total');
    setCobrando(true);
    try {
      const resultado = await ventaDirecta({
        items: carrito.map((l) => ({ productoId: l.id, cantidad: l.cantidad })),
        // En efectivo se registra solo el total: el vuelto no queda en caja.
        pagos: [{ metodoPagoId: metodo.id, monto: total, referencia: pagaEnEfectivo ? undefined : referencia.trim() || undefined }],
        operacionId,
      });
      setVenta({ pedidoId: resultado.pedidoId, total: resultado.total, vuelto: pagaEnEfectivo ? vuelto : 0 });
      vaciar();
      toast.success('Venta registrada');
      // Como en caja: al terminar el cobro se ofrece la boleta (se puede cerrar sin emitirla).
      setEmitirPara({ id: resultado.pedidoId, total: resultado.total, salonNombre: '', mesaNumero: 'MOSTRADOR' });
    } catch (err) {
      toast.error(err.message ?? 'No se pudo registrar la venta');
    } finally {
      setCobrando(false);
    }
  });

  const salir = () => {
    logout();
    navigate('/login');
  };

  const botonHeader = 'flex items-center gap-1.5 px-3.5 py-2 bg-card rounded-full hover:bg-cardHighlight border border-gray-800/50 text-xs font-bold text-gray-300';

  return (
    <div className="min-h-screen bg-background pb-10">
      <div className="max-w-6xl mx-auto">
        {/* ── Encabezado ─────────────────────────────────── */}
        <div className="px-5 pt-6 pb-4 flex flex-wrap items-center gap-3">
          <div className="flex-1 min-w-[10rem]">
            <h1 className="text-2xl font-extrabold tracking-tight">Venta rápida</h1>
            <p className="text-xs text-gray-500 mt-0.5">
              {esTaquilla ? 'Caja de la piscina' : 'Caja del restaurante'} · {user?.nombreCompleto}
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            <button onClick={() => navigate('/pedidos-dia')} className={botonHeader}>
              <ListOrdered size={15} /> Ventas de hoy
            </button>
            <button onClick={() => navigate('/jornada')} className={botonHeader}>
              <CalendarClock size={15} /> Caja
            </button>
            {esTaquilla ? (
              <button onClick={salir} className={botonHeader}>
                <LogOut size={15} /> Salir
              </button>
            ) : (
              <button onClick={() => navigate(-1)} className={botonHeader}>
                Volver
              </button>
            )}
          </div>
        </div>

        {cargada && !abierta && (
          <div className="mx-5 mb-4 bg-yellow-950/40 border border-yellow-800/50 rounded-2xl px-4 py-3 text-sm text-yellow-100/90 flex flex-wrap items-center gap-3">
            <span className="flex-1">La caja no está abierta: ábrela con el fondo de caja para empezar a vender.</span>
            <button onClick={() => navigate('/jornada')} className="text-xs font-bold bg-accentYellow/20 text-accentYellow border border-yellow-700/60 px-3 py-1.5 rounded-full">
              Abrir caja
            </button>
          </div>
        )}

        <div className="px-5 grid gap-4 lg:grid-cols-[minmax(0,1fr)_22rem]">
          {/* ── Productos ─────────────────────────────────── */}
          <section className="space-y-3 min-w-0">
            <div className="relative">
              <Search size={16} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-500" />
              <input
                value={busqueda}
                onChange={(e) => setBusqueda(e.target.value)}
                placeholder="Buscar producto…"
                className="w-full bg-card border border-gray-800/50 rounded-2xl pl-10 pr-10 py-2.5 text-sm text-ink placeholder-gray-600 focus:outline-none focus:border-primary"
              />
              {busqueda && (
                <button onClick={() => setBusqueda('')} className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-500" aria-label="Limpiar">
                  <X size={16} />
                </button>
              )}
            </div>

            {categorias.length > 2 && (
              <div className="flex flex-wrap gap-2">
                {categorias.map((c) => (
                  <button
                    key={c}
                    onClick={() => setCategoria(c)}
                    className={`px-3.5 py-1.5 rounded-full text-sm font-bold border transition-colors ${
                      categoria === c ? 'bg-primary text-white border-primary' : 'bg-card text-gray-400 border-gray-800 hover:text-ink'
                    }`}
                  >
                    {c}
                  </button>
                ))}
              </div>
            )}

            {cargando ? (
              <p className="text-sm text-gray-500 flex items-center gap-2"><Loader2 size={15} className="animate-spin" /> Cargando productos…</p>
            ) : productos.length === 0 ? (
              <div className="bg-card border border-gray-800/50 rounded-2xl p-6 text-center text-sm text-gray-400">
                No hay productos de mostrador. En <b>Administrar → Carta y menú</b>, marca la categoría (por ejemplo
                "Piscina") como <b>Venta de mostrador</b> y agrégale sus productos.
              </div>
            ) : (
              <div className="grid grid-cols-2 sm:grid-cols-3 xl:grid-cols-4 gap-2.5">
                {visibles.map((p) => {
                  const enCarrito = carrito.find((l) => l.id === p.id)?.cantidad ?? 0;
                  return (
                    <motion.button
                      key={p.id}
                      whileTap={{ scale: 0.96 }}
                      onClick={() => agregar(p)}
                      className={`relative text-left bg-card border rounded-2xl overflow-hidden transition-colors ${
                        enCarrito ? 'border-primary/70' : 'border-gray-800/50 hover:border-primary/40'
                      }`}
                    >
                      <FotoProducto src={p.image} alt={p.name} className="w-full h-24" iconSize={22} />
                      <div className="p-2.5">
                        <p className="text-sm font-bold text-ink leading-tight line-clamp-2">{p.name}</p>
                        <p className="text-sm font-extrabold text-primary mt-1">{soles(p.price)}</p>
                      </div>
                      {enCarrito > 0 && (
                        <span className="absolute top-2 right-2 min-w-[1.75rem] h-7 px-1.5 rounded-full bg-primary text-white text-sm font-extrabold flex items-center justify-center">
                          {enCarrito}
                        </span>
                      )}
                    </motion.button>
                  );
                })}
              </div>
            )}
          </section>

          {/* ── Venta en curso y cobro ────────────────────── */}
          <aside className="bg-card border border-gray-800/50 rounded-3xl p-4 space-y-3 lg:sticky lg:top-4 self-start">
            <div className="flex items-center justify-between">
              <h2 className="text-sm font-extrabold text-ink flex items-center gap-2">
                <ShoppingBag size={16} className="text-primary" /> Venta {unidades > 0 && <span className="text-gray-500 font-bold">· {unidades} und.</span>}
              </h2>
              {carrito.length > 0 && (
                <button onClick={vaciar} className="text-xs font-bold text-gray-500 hover:text-red-400">Vaciar</button>
              )}
            </div>

            {venta && carrito.length === 0 && (
              <div className="bg-emerald-500/10 border border-emerald-700/50 rounded-2xl px-3 py-2.5 text-sm">
                <p className="font-bold text-accentGreen">Venta #{venta.pedidoId} registrada · {soles(venta.total)}</p>
                {venta.vuelto > 0 && <p className="text-ink mt-0.5">Vuelto: <b>{soles(venta.vuelto)}</b></p>}
              </div>
            )}

            {carrito.length === 0 ? (
              <p className="text-sm text-gray-500 py-4 text-center">Toca un producto para agregarlo.</p>
            ) : (
              <div className="space-y-2 max-h-[40vh] overflow-y-auto pr-1">
                {carrito.map((l) => (
                  <div key={l.id} className="flex items-center gap-2">
                    <div className="flex-1 min-w-0">
                      <p className="text-sm text-gray-200 truncate">{l.nombre}</p>
                      <p className="text-xs text-gray-500">{soles(l.precio)} c/u</p>
                    </div>
                    <div className="flex items-center gap-1.5">
                      <button onClick={() => cambiar(l.id, -1)} className="w-7 h-7 rounded-full border border-gray-700/70 flex items-center justify-center text-gray-300" aria-label="Quitar uno">
                        <Minus size={14} />
                      </button>
                      <span className="w-6 text-center text-sm font-bold tabular-nums">{l.cantidad}</span>
                      <button onClick={() => cambiar(l.id, 1)} className="w-7 h-7 rounded-full border border-gray-700/70 flex items-center justify-center text-gray-300" aria-label="Agregar uno">
                        <Plus size={14} />
                      </button>
                    </div>
                    <span className="w-16 text-right text-sm font-bold tabular-nums">{soles(l.precio * l.cantidad)}</span>
                    <button onClick={() => quitar(l.id)} className="text-gray-600 hover:text-red-400" aria-label="Quitar">
                      <Trash2 size={15} />
                    </button>
                  </div>
                ))}
              </div>
            )}

            <div className="border-t border-dashed border-gray-700 pt-3 flex justify-between items-baseline">
              <span className="text-sm font-bold text-gray-400">Total</span>
              <span className="text-2xl font-extrabold text-ink tabular-nums">{soles(total)}</span>
            </div>

            {/* Medio de pago */}
            <div className="flex flex-wrap gap-2">
              {metodos.map((m) => (
                <button
                  key={m.id}
                  onClick={() => { setMetodoId(m.id); setRecibido(''); }}
                  className={`flex items-center gap-1.5 px-3 py-2 rounded-xl text-sm font-bold border transition-colors ${
                    metodoId === m.id ? 'bg-primary/20 border-primary text-primary' : 'bg-background border-gray-800 text-gray-400 hover:border-gray-600'
                  }`}
                >
                  <span className="text-base leading-none">{getMetodoPagoIcon(m.nombre)}</span> {m.nombre}
                </button>
              ))}
            </div>

            {pagaEnEfectivo ? (
              <div className="space-y-2">
                <label className="block text-xs font-bold text-gray-400">Recibe (opcional, para calcular el vuelto)</label>
                <input
                  value={recibido}
                  onChange={(e) => setRecibido(e.target.value)}
                  inputMode="decimal"
                  placeholder={total ? total.toFixed(2) : '0.00'}
                  className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-2.5 text-ink text-lg font-bold focus:outline-none focus:border-primary"
                />
                <div className="flex flex-wrap gap-1.5">
                  {[10, 20, 50, 100, 200].filter((b) => b >= total).slice(0, 4).map((b) => (
                    <button key={b} onClick={() => setRecibido(String(b))} className="px-3 py-1.5 rounded-full text-xs font-bold border border-gray-700 text-gray-300 hover:border-primary/60">
                      S/ {b}
                    </button>
                  ))}
                </div>
                {recibido !== '' && (
                  <p className={`text-sm font-bold ${faltaDinero ? 'text-red-400' : 'text-accentGreen'}`}>
                    {faltaDinero ? `Faltan ${soles(total - montoRecibido)}` : `Vuelto: ${soles(vuelto)}`}
                  </p>
                )}
              </div>
            ) : metodo ? (
              <input
                value={referencia}
                onChange={(e) => setReferencia(e.target.value)}
                maxLength={100}
                placeholder="N.º de operación (opcional)"
                className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-2.5 text-sm text-ink placeholder-gray-600 focus:outline-none focus:border-primary"
              />
            ) : null}

            <motion.button
              whileTap={{ scale: 0.98 }}
              onClick={cobrar}
              disabled={cobrando || carrito.length === 0 || !metodo || faltaDinero || (cargada && !abierta)}
              className="w-full py-4 rounded-2xl bg-primary text-white font-extrabold text-lg disabled:opacity-40 flex items-center justify-center gap-2"
            >
              {cobrando ? <Loader2 size={18} className="animate-spin" /> : null}
              {cobrando ? 'Cobrando…' : `Cobrar ${soles(total)}`}
            </motion.button>
          </aside>
        </div>
      </div>

      <AnimatePresence>
        {emitirPara && (
          <EmitirComprobanteModal
            pedido={emitirPara}
            onEmitido={(c) => {
              setEmitirPara(null);
              setComprobante(c);
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
