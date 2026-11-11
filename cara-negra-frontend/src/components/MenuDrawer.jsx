import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { X, ArrowLeft, Plus, Minus, ShoppingBag, UtensilsCrossed, Search, SearchX, Maximize2, LayoutGrid, Ban } from 'lucide-react';
import { useCart } from '../CartContext';
import { getCategories, getMenuItems } from '../services/menuService';
import { prepararIndice, buscarEnIndice, resaltarCoincidencias } from '../utils/buscarProductos';
import FotoProducto from './FotoProducto';
import { getActivePedidoForMesa, eliminarDetalle, getAnulacionesPedido, cambiarPrecioDetalle } from '../services/ordersService';
import useMediaQuery from '../hooks/useMediaQuery';
import PanelPedido from './PanelPedido';
import ComentarioPedido from './ComentarioPedido';
import FilaEnviado from './FilaEnviado';
import useEnviarPedido, { IMPRIMEN_SU_COMANDA } from '../hooks/useEnviarPedido';
import { useEmpresa } from '../context/EmpresaContext';
import { imprimirComandaEnCaja, imprimirAnulacionEnCaja } from '../utils/comandaCaja';
import NotaPlatoDialog from './NotaPlatoDialog';
import { getLadoPanel, alCambiarLadoPanel } from '../utils/ladoPanel';
import { useAuth } from '../context/AuthContext';
import ListaAnulaciones from './ListaAnulaciones';
import toast from 'react-hot-toast';
import { etiquetaMesa } from '../utils/mesaLabel';

const TODAS = 'todas';
const porNombre = (a, b) => a.name.localeCompare(b.name, 'es', { sensitivity: 'base' });

// Motivos frecuentes para quitar un producto (un toque); también se puede escribir otro.
const MOTIVOS_RAPIDOS = ['El cliente ya no lo quiere', 'Error al tomar el pedido', 'Se acabó / no hay', 'Demora en cocina'];

export default function MenuDrawer({ isOpen, onClose }) {
  const navigate = useNavigate();
  const { activeTable, setActiveTable, addToCart, updateQuantity, quitarUnoDelProducto, removeFromCart, setItemPrice, setItemNote, cart, total } = useCart();
  // Envío de un clic desde el panel del monitor (misma lógica que la pantalla de revisión).
  const { enviar, enviando } = useEnviarPedido();
  const { nombreComercial } = useEmpresa();
  // Línea del carrito cuya nota se está escribiendo (vista de monitor).
  const [notaDe, setNotaDe] = useState(null);
  // Monitor (caja / PC): la carta ocupa la pantalla y el listado del pedido queda al costado.
  const esMonitor = useMediaQuery('(min-width: 1024px)');
  // Lado del listado del pedido en el monitor (Ajustes → "en este equipo").
  const [ladoPanel, setLadoPanelActual] = useState(getLadoPanel);
  useEffect(() => alCambiarLadoPanel(setLadoPanelActual), []);

  const [categories, setCategories] = useState([]);
  // TODAS = toda la carta en una sola cuadrícula, en orden alfabético (las categorías siguen
  // disponibles como filtro).
  const [activeCategory, setActiveCategory] = useState(TODAS);
  // Toda la carta disponible se carga UNA vez al abrir: cambiar de categoría y buscar es
  // instantáneo (sin una petición por categoría).
  const [todos, setTodos] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [consulta, setConsulta] = useState('');
  const [vistaPrevia, setVistaPrevia] = useState(null); // producto con la foto ampliada
  const inputBusqueda = useRef(null);
  const [addedIds, setAddedIds] = useState(new Set()); // para feedback visual

  // Pedido ya registrado de la mesa (si está Ocupada): se muestra aparte del
  // carrito de ítems nuevos, porque ya fue enviado a cocina y vive en el
  // backend, no en el CartContext local.
  const [pedidoActual, setPedidoActual] = useState(null);
  const [isLoadingPedido, setIsLoadingPedido] = useState(false);
  const [removingId, setRemovingId] = useState(null);
  const { user } = useAuth();
  // Productos ya quitados de este pedido (quién, cuándo, por qué) y el que se está quitando.
  const [anulados, setAnulados] = useState([]);
  const [quitando, setQuitando] = useState(null); // { detalle, cantidad, motivo }
  // Cambio de precio (solo cajero / admin): { tipo: 'carrito'|'enviado', id, nombre, precioCarta, valor, motivo }
  const [editPrecio, setEditPrecio] = useState(null);
  const [guardandoPrecio, setGuardandoPrecio] = useState(false);
  const puedeCambiarPrecio = ['CAJERO', 'ADMIN'].includes(user?.rol);
  // El mozo solo trabaja sobre SUS mesas (el servidor también lo valida).
  const esSuMesa = () => user?.rol !== 'MOZO' || pedidoActual?.usuarioId === user?.usuarioId;
  // Quitar platos: además, con el pedido ya "Listo" solo caja o admin (p. ej. el cliente rechazó
  // un plato que ya salió de cocina).
  const puedeQuitar = () => esSuMesa() && (user?.rol !== 'MOZO' || pedidoActual?.estado !== 'Listo');

  const cargarPedidoActual = useCallback(() => {
    if (!activeTable || activeTable.status !== 'occupied') {
      setPedidoActual(null);
      return;
    }
    setIsLoadingPedido(true);
    getActivePedidoForMesa(activeTable.id)
      .then(async (p) => {
        setPedidoActual(p);
        setAnulados(p ? await getAnulacionesPedido(p.id) : []);
      })
      .catch(() => setPedidoActual(null))
      .finally(() => setIsLoadingPedido(false));
  }, [activeTable]);

  // Cargar categorías, toda la carta y el pedido ya registrado (si la mesa está ocupada) al abrir
  useEffect(() => {
    if (!isOpen) return;
    setConsulta('');
    setVistaPrevia(null);
    setIsLoading(true);
    Promise.all([getCategories(), getMenuItems()])
      .then(([cats, productos]) => {
        setCategories(cats);
        setActiveCategory((actual) => (actual === TODAS || cats.some((c) => c.id === actual) ? actual : TODAS));
        // Solo productos de categorías activas (las que el mozo puede ver).
        const activas = new Set(cats.map((c) => c.id));
        setTodos(productos.filter((p) => activas.has(p.category)));
      })
      .finally(() => setIsLoading(false));
    cargarPedidoActual();
  }, [isOpen, cargarPedidoActual]);

  const indice = useMemo(() => prepararIndice(todos), [todos]);
  const buscando = consulta.trim().length > 0;
  const items = useMemo(() => {
    if (buscando) return buscarEnIndice(indice, consulta);
    const lista = activeCategory === TODAS ? todos : todos.filter((p) => p.category === activeCategory);
    return [...lista].sort(porNombre);
  }, [buscando, indice, consulta, todos, activeCategory]);

  const limpiarBusqueda = () => {
    setConsulta('');
    inputBusqueda.current?.focus();
  };

  // Quitar algo ya enviado a cocina pide motivo (y cuántas unidades): queda registrado con el
  // nombre de quien lo hizo y a cocina le sale un aviso "ANULADO".
  // `cantidad`: unidades a quitar ya elegidas con "−" en la fila (si no, todas).
  const abrirQuitar = (detalle, cantidad = detalle.cantidad) => setQuitando({ detalle, cantidad, motivo: '' });

  // Quitar TODO lo que queda del pedido = anular el pedido completo (el servidor lo hace y libera la mesa).
  const esUltimoPlato = Boolean(
    quitando && pedidoActual
      && (pedidoActual.detalles ?? []).filter((d) => d.estado !== 'Cancelado').length === 1
      && quitando.cantidad === quitando.detalle.cantidad
  );

  const confirmarQuitar = async () => {
    const { detalle, cantidad, motivo } = quitando;
    if (motivo.trim().length < 3) {
      toast.error('Indica el motivo (queda registrado)');
      return;
    }
    try {
      await quitarEnviado(detalle, cantidad, motivo.trim());
      setQuitando(null);
    } catch {
      // el aviso ya se mostró; la ventana queda abierta para reintentar
    }
  };

  // Quita `cantidad` unidades de un producto ya enviado (desde la ventana o con "−" + motivo en la fila).
  // Si falla muestra el aviso y relanza el error, para que quien llamó no dé la baja por hecha.
  const quitarEnviado = async (detalle, cantidad, motivo) => {
    setRemovingId(detalle.id);
    try {
      const actualizado = await eliminarDetalle(pedidoActual.id, detalle.id, { motivo, cantidad });
      // Administrador y cajero: el aviso "ANULADO" sale en la ticketera de este equipo (no en cocina).
      if (IMPRIMEN_SU_COMANDA.includes(user?.rol)) {
        imprimirAnulacionEnCaja({
          salonNombre: activeTable?.salonNombre,
          mesaNumero: activeTable?.numeroMesa,
          anulo: user?.nombreCompleto,
          motivo,
          pedidoCompleto: actualizado.estado === 'Cancelado',
          items: [{ cantidad, nombre: detalle.productoNombre }],
        }).catch((e) => console.error('No se pudo imprimir el aviso de anulación:', e));
      }
      if (actualizado.estado === 'Cancelado') {
        // Era el último plato: el servidor anuló el pedido completo y liberó la mesa.
        toast.success(`Pedido #${actualizado.id} anulado · la mesa quedó libre`);
        setPedidoActual(null);
        setAnulados([]);
        setActiveTable({ ...activeTable, status: 'free' });
        // Sin nada por enviar no hay nada más que hacer en esta mesa: de vuelta a las mesas.
        if (cart.length === 0) onClose?.();
        return;
      }
      setPedidoActual(actualizado);
      setAnulados(await getAnulacionesPedido(pedidoActual.id));
      toast.success(`${cantidad}x ${detalle.productoNombre} quitado · ${IMPRIMEN_SU_COMANDA.includes(user?.rol) ? 'aviso impreso aquí' : 'aviso enviado a cocina'}`);
    } catch (err) {
      toast.error(err.message ?? 'No se pudo quitar el ítem');
      throw err;
    } finally {
      setRemovingId(null);
    }
  };

  // "+" en algo ya enviado: una unidad más del mismo producto a "Por enviar" (sale como ADICIONAL).
  const masDeEnviado = (detalle) => {
    const producto = todos.find((p) => p.id === detalle.productoId);
    if (!producto) {
      toast.error(`${detalle.productoNombre} ya no está disponible en la carta`);
      return;
    }
    handleAdd(producto);
  };

  // Precio de algo ya enviado, cambiado en la misma fila (solo cajero / admin). Queda registrado.
  const guardarPrecioEnviado = async (detalle, precio) => {
    try {
      const actualizado = await cambiarPrecioDetalle(pedidoActual.id, detalle.id, { precio, motivo: '' });
      setPedidoActual(actualizado);
      toast.success(`Precio de ${detalle.productoNombre}: S/ ${precio.toFixed(2)} (queda registrado)`);
    } catch (err) {
      toast.error(err.message);
      throw err;
    }
  };

  const abrirPrecioCarrito = (item) =>
    setEditPrecio({ tipo: 'carrito', id: item.linea, nombre: item.name, precioCarta: item.precioCarta ?? item.price, valor: String(item.price), motivo: '' });

  const guardarPrecio = async () => {
    const precio = Math.round(Number(String(editPrecio.valor).replace(',', '.')) * 100) / 100;
    if (!Number.isFinite(precio) || precio < 0) {
      toast.error('Escribe un precio válido');
      return;
    }
    if (editPrecio.tipo === 'carrito') {
      setItemPrice(editPrecio.id, precio);
      setEditPrecio(null);
      return;
    }
    setGuardandoPrecio(true);
    try {
      const actualizado = await cambiarPrecioDetalle(pedidoActual.id, editPrecio.id, { precio, motivo: editPrecio.motivo.trim() });
      setPedidoActual(actualizado);
      setEditPrecio(null);
      toast.success(`Precio de ${editPrecio.nombre} cambiado a S/ ${precio.toFixed(2)} (queda registrado)`);
    } catch (err) {
      toast.error(err.message);
    } finally {
      setGuardandoPrecio(false);
    }
  };

  // Un clic en "Enviar a cocina" (monitor): envía, avisa y vuelve al mapa de mesas. Si quien envía es
  // el admin o el cajero, la comanda ya salió en la ticketera de este equipo; el aviso permite repetirla.
  const enviarDesdePanel = async () => {
    try {
      const enviado = await enviar();
      if (!enviado) return; // doble clic: ya se está enviando
      const { pedido, comandaCaja } = enviado;
      const mesa = etiquetaMesa(pedido.salonNombre, pedido.mesaNumero);
      const imprimeAqui = IMPRIMEN_SU_COMANDA.includes(user?.rol);
      toast.success(
        (t) => (
          <span className="flex items-center gap-3">
            <span>
              {comandaCaja.esAdicional ? 'Adicional' : 'Pedido'} de {mesa} {imprimeAqui ? 'enviado · comanda impresa aquí' : 'enviado a cocina'}
            </span>
            {imprimeAqui && (
              <button
                type="button"
                onClick={() => { imprimirComandaEnCaja({ ...comandaCaja, nombreComercial }); toast.dismiss(t.id); }}
                className="text-xs font-bold text-primary whitespace-nowrap"
              >
                Imprimir otra vez
              </button>
            )}
          </span>
        ),
        { duration: 5000 },
      );
      setActiveTable({ ...activeTable, status: 'occupied' });
      onClose?.();
    } catch (err) {
      toast.error(err.message ?? 'No se pudo enviar el pedido. Intenta de nuevo.');
    }
  };

  // Unidades del plato en el carrito (puede estar en varias líneas con notas distintas).
  const getCartQuantity = (itemId) =>
    cart.reduce((acc, i) => (i.id === itemId ? acc + i.quantity : acc), 0);

  const cartItemCount = cart.reduce((acc, i) => acc + i.quantity, 0);

  const handleAdd = (item) => {
    addToCart(item);
    // Feedback visual momentáneo
    setAddedIds((prev) => new Set(prev).add(item.id));
    setTimeout(
      () => setAddedIds((prev) => { const s = new Set(prev); s.delete(item.id); return s; }),
      600
    );
    toast.success(`${item.name} añadido`, {
      icon: '✅',
      duration: 1500,
    });
  };

  if (!activeTable) return null;

  return (
    <AnimatePresence>
      {isOpen && (
      <motion.div
        key="drawer-backdrop"
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        className={`fixed inset-0 z-50 flex flex-col ${esMonitor ? '' : 'justify-end'}`}
      >
        {/* Backdrop */}
        <motion.div
          className="absolute inset-0 bg-black/70 backdrop-blur-sm"
          onClick={onClose}
        />

        {/* Drawer */}
        <motion.div
          key="drawer-panel"
          initial={esMonitor ? { opacity: 0 } : { y: '100%' }}
          animate={esMonitor ? { opacity: 1 } : { y: 0 }}
          exit={esMonitor ? { opacity: 0 } : { y: '100%' }}
          transition={{ type: 'spring', stiffness: 300, damping: 30 }}
          className={esMonitor
            ? 'relative z-10 bg-card w-full h-full flex flex-col'
            : 'relative z-10 bg-card w-full rounded-t-3xl flex flex-col shadow-2xl border-t border-gray-700/40'}
          style={esMonitor ? undefined : { maxHeight: '90dvh' }}
        >
          {/* Handle bar */}
          {!esMonitor && (
            <div className="flex justify-center pt-3 pb-1">
              <div className="w-10 h-1 rounded-full bg-gray-700" />
            </div>
          )}

          {/* Header */}
          <div className="px-4 pb-2.5 pt-1 flex items-center gap-3 border-b border-gray-800/60">
            {/* Atrás (a la izquierda, como en el resto de pantallas): vuelve al mapa de mesas */}
            <motion.button
              whileTap={{ scale: 0.9 }}
              onClick={onClose}
              className="p-2 bg-background/60 rounded-full hover:bg-cardHighlight transition-colors border border-gray-800/50"
              aria-label="Volver a las mesas"
              title="Volver a las mesas"
            >
              <ArrowLeft size={20} className="text-ink" />
            </motion.button>
            <div>
              <p className="text-[11px] text-gray-500 font-medium uppercase tracking-wider">
                Tomando pedido
              </p>
              <h2 className="text-lg font-extrabold tracking-tight leading-tight">{etiquetaMesa(activeTable?.salonNombre, activeTable?.numeroMesa)}</h2>
            </div>
          </div>

          {/* En monitor: carta (izquierda) + listado del pedido (derecha). En celular: una columna. */}
          <div className={esMonitor ? `flex-1 flex min-h-0 ${ladoPanel === 'izquierda' ? 'flex-row-reverse' : ''}` : 'contents'}>
          <div className={esMonitor ? 'flex-1 flex flex-col min-w-0 min-h-0' : 'contents'}>

          {/* Pedido ya registrado de la mesa (Ocupada): lo que ya se envió a cocina */}
          {!esMonitor && isLoadingPedido && (
            <div className="px-5 py-3 border-b border-gray-800/40">
              <p className="text-xs text-gray-600">Cargando pedido actual…</p>
            </div>
          )}
          {!esMonitor && !isLoadingPedido && pedidoActual && (
            <div className="px-5 py-3 border-b border-gray-800/40 bg-background/40">
              <div className="flex items-center justify-between mb-2">
                <p className="text-[11px] text-gray-500 font-bold uppercase tracking-wider">
                  Ya en el pedido #{pedidoActual.id}
                </p>
                <p className="text-xs font-bold text-primary">S/ {pedidoActual.total.toFixed(2)}</p>
              </div>
              {/* Lo ya enviado: − cantidad +, precio (caja/admin) y tacho en cada plato */}
              <div className="space-y-1.5 max-h-[40dvh] overflow-y-auto">
                {pedidoActual.detalles.map((d) => (
                  <FilaEnviado
                    key={d.id}
                    d={d}
                    compacta
                    puedeQuitar={puedeQuitar(d)}
                    puedeAgregar={esSuMesa()}
                    puedeCambiarPrecio={puedeCambiarPrecio}
                    ocupado={removingId === d.id}
                    esUnicoPlato={pedidoActual.detalles.length === 1}
                    onQuitar={quitarEnviado}
                    onQuitarConVentana={abrirQuitar}
                    onMas={masDeEnviado}
                    onGuardarPrecio={guardarPrecioEnviado}
                  />
                ))}
              </div>
              {/* Comentario general: ya no se ofrece "Agregar comentario"; solo se muestra si el pedido trae uno. */}
              {pedidoActual.comentario && (
                <div className="mt-2">
                  <ComentarioPedido
                    pedido={pedidoActual}
                    editable={esSuMesa()}
                    compacto
                    onGuardado={(comentario) => setPedidoActual((p) => (p ? { ...p, comentario } : p))}
                  />
                </div>
              )}
              {anulados.length > 0 && (
                <details className="mt-2">
                  <summary className="text-[11px] text-red-400/90 font-bold cursor-pointer select-none">
                    Quitados de este pedido ({anulados.length})
                  </summary>
                  <div className="mt-1.5 max-h-40 overflow-y-auto">
                    <ListaAnulaciones anulaciones={anulados} compacta />
                  </div>
                </details>
              )}
            </div>
          )}

          {/* Buscador universal: busca en toda la carta mientras se escribe */}
          {todos.length > 0 && (
            <div className="px-5 pt-3">
              <div className="relative">
                <Search size={16} className="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-500 pointer-events-none" />
                <input
                  ref={inputBusqueda}
                  type="search"
                  inputMode="search"
                  enterKeyHint="search"
                  autoComplete="off"
                  autoCorrect="off"
                  spellCheck={false}
                  value={consulta}
                  onChange={(e) => setConsulta(e.target.value)}
                  onKeyDown={(e) => { if (e.key === 'Escape') setConsulta(''); }}
                  placeholder="Buscar plato o bebida…"
                  aria-label="Buscar en la carta"
                  className="w-full bg-background border border-gray-700/50 rounded-2xl pl-10 pr-10 py-2.5 text-sm text-ink placeholder-gray-600 focus:outline-none focus:border-primary transition-colors [&::-webkit-search-cancel-button]:hidden"
                />
                {buscando && (
                  <button
                    type="button"
                    onClick={limpiarBusqueda}
                    className="absolute right-2 top-1/2 -translate-y-1/2 p-1.5 rounded-full text-gray-500 hover:text-ink hover:bg-cardHighlight"
                    aria-label="Borrar búsqueda"
                  >
                    <X size={15} />
                  </button>
                )}
              </div>
              {buscando && (
                <p className="text-[11px] text-gray-500 mt-2 px-1">
                  {items.length === 0
                    ? 'Sin resultados'
                    : `${items.length} resultado${items.length === 1 ? '' : 's'} en toda la carta`}
                </p>
              )}
            </div>
          )}

          {/* Categories (se ocultan mientras se busca) */}
          {categories.length > 0 && !buscando && (
            <div className="px-5 py-2.5 flex gap-2 overflow-x-auto no-scrollbar border-b border-gray-800/40">
              <motion.button
                whileTap={{ scale: 0.93 }}
                onClick={() => setActiveCategory(TODAS)}
                className={`px-3.5 py-1.5 rounded-full whitespace-nowrap font-semibold text-sm transition-all flex-shrink-0 flex items-center gap-1.5 ${
                  activeCategory === TODAS
                    ? 'bg-primary text-white shadow-glow-orange'
                    : 'bg-background text-gray-400 hover:bg-cardHighlight hover:text-ink'
                }`}
              >
                <LayoutGrid size={14} />
                <span>Todos</span>
              </motion.button>
              {categories.map((cat) => (
                <motion.button
                  key={cat.id}
                  whileTap={{ scale: 0.93 }}
                  onClick={() => setActiveCategory(cat.id)}
                  className={`px-3.5 py-1.5 rounded-full whitespace-nowrap font-semibold text-sm transition-all flex-shrink-0 flex items-center gap-1.5 ${
                    activeCategory === cat.id
                      ? 'bg-primary text-white shadow-glow-orange'
                      : 'bg-background text-gray-400 hover:bg-cardHighlight hover:text-ink'
                  }`}
                >
                  <span>{cat.icon}</span>
                  <span>{cat.label}</span>
                </motion.button>
              ))}
            </div>
          )}

          {/* Productos en cuadrícula (más platos por pantalla, menos scroll) */}
          <div className="flex-1 overflow-y-auto px-4 py-3 pb-4">
            {buscando && items.length === 0 && (
              <div className="flex flex-col items-center justify-center py-12 text-center">
                <SearchX size={30} className="text-gray-700 mb-3" />
                <p className="text-gray-500 text-sm">No hay platos que coincidan con “{consulta.trim()}”.</p>
                <p className="text-gray-600 text-xs mt-1">Prueba con otra palabra (no importan las tildes).</p>
              </div>
            )}

            {categories.length === 0 && !isLoading && (
              <div className="flex flex-col items-center justify-center py-16 text-center">
                <UtensilsCrossed size={32} className="text-gray-700 mb-3" />
                <p className="text-gray-500 text-sm">
                  Aún no hay categorías ni productos cargados en la carta.
                </p>
                <p className="text-gray-600 text-xs mt-1">
                  Pídele al administrador que los agregue desde el panel.
                </p>
              </div>
            )}

            <div className="grid grid-cols-3 sm:grid-cols-4 md:grid-cols-5 lg:grid-cols-4 xl:grid-cols-5 2xl:grid-cols-7 gap-2">
              {items.map((item) => {
                const qty = getCartQuantity(item.id);
                const justAdded = addedIds.has(item.id);
                return (
                  <motion.div
                    key={item.id}
                    initial={{ opacity: 0 }}
                    animate={justAdded ? { opacity: 1, scale: [1, 1.04, 1] } : { opacity: 1 }}
                    transition={{ duration: 0.2 }}
                    className={`relative bg-background rounded-xl overflow-hidden border flex flex-col ${
                      qty > 0 ? 'border-primary/70' : 'border-gray-800/50'
                    }`}
                  >
                    {/* Toque en la tarjeta = agregar 1 */}
                    <button
                      type="button"
                      onClick={() => handleAdd(item)}
                      className="flex flex-col text-left flex-1 active:bg-cardHighlight/60"
                      aria-label={`Agregar ${item.name}`}
                    >
                      {/* Foto del plato (o el ícono si aún no tiene) */}
                      <FotoProducto src={item.image} alt={item.name} className="w-full aspect-[4/3]" iconSize={20} />
                      <div className="px-1.5 pt-1 pb-1.5 flex flex-col flex-1">
                        <h3 className="font-bold text-ink text-[12px] leading-tight line-clamp-2">
                          {buscando
                            ? resaltarCoincidencias(item.name, consulta).map((t, i) =>
                                t.resaltar
                                  ? <mark key={i} className="bg-transparent text-primary">{t.texto}</mark>
                                  : <span key={i}>{t.texto}</span>
                              )
                            : item.name}
                        </h3>
                        {(buscando || activeCategory === TODAS) && item.categoryName && (
                          <span className="text-[9px] font-semibold uppercase tracking-wide text-gray-500 truncate mt-0.5">
                            {item.categoryName}
                          </span>
                        )}
                        <p className="text-primary font-extrabold text-[12px] mt-auto pt-0.5">S/ {item.price.toFixed(2)}</p>
                      </div>
                    </button>

                    {/* Ver la foto en grande (para mostrársela al cliente) */}
                    {item.image && (
                      <button
                        type="button"
                        onClick={() => setVistaPrevia(item)}
                        className="absolute top-1 left-1 p-1 rounded-md bg-black/55 text-white"
                        aria-label={`Ver foto de ${item.name}`}
                      >
                        <Maximize2 size={12} />
                      </button>
                    )}

                    {/* Cantidad en el pedido y botón para quitar */}
                    {qty > 0 && (
                      <>
                        <span className="absolute top-1 right-1 min-w-6 h-6 px-1.5 rounded-full bg-primary text-white text-xs font-extrabold flex items-center justify-center shadow-glow-orange">
                          {qty}
                        </span>
                        <button
                          type="button"
                          onClick={() => quitarUnoDelProducto(item.id)}
                          className="absolute right-1 bottom-1 w-7 h-7 rounded-full bg-black/70 text-white flex items-center justify-center border border-white/20"
                          aria-label={`Quitar uno de ${item.name}`}
                        >
                          <Minus size={14} />
                        </button>
                      </>
                    )}
                  </motion.div>
                );
              })}
            </div>
          </div>

          {/* ── Cart summary bar (solo celular; en monitor está el panel) ── */}
          <AnimatePresence>
            {!esMonitor && cartItemCount > 0 && (
              <motion.div
                initial={{ y: 80, opacity: 0 }}
                animate={{ y: 0, opacity: 1 }}
                exit={{ y: 80, opacity: 0 }}
                transition={{ type: 'spring', stiffness: 300, damping: 28 }}
                className="px-5 pb-6 pt-3 border-t border-gray-800/40"
              >
                <motion.button
                  whileTap={{ scale: 0.97 }}
                  onClick={() => navigate('/cart')}
                  className="w-full bg-primary hover:bg-primaryHover text-white font-bold py-4 rounded-2xl flex justify-between items-center px-5 shadow-glow-orange transition-all"
                >
                  <span className="bg-white/20 text-white text-sm font-bold px-2.5 py-1 rounded-full">
                    {cartItemCount}
                  </span>
                  <span className="flex items-center gap-2">
                    <ShoppingBag size={18} />
                    Ver pedido
                  </span>
                  <span className="font-extrabold">S/ {total.toFixed(2)}</span>
                </motion.button>
              </motion.div>
            )}
          </AnimatePresence>
          </div>

          {esMonitor && (
            <PanelPedido
              mesaTexto={etiquetaMesa(activeTable?.salonNombre, activeTable?.numeroMesa)}
              lado={ladoPanel}
              pedidoActual={pedidoActual}
              cargando={isLoadingPedido}
              cart={cart}
              totalCarrito={total}
              onCantidad={updateQuantity}
              onQuitarDelCarrito={removeFromCart}
              onEditarPrecioCarrito={abrirPrecioCarrito}
              onNotaCarrito={setNotaDe}
              puedeCambiarPrecio={puedeCambiarPrecio}
              puedeQuitar={puedeQuitar}
              puedeAgregar={esSuMesa()}
              onQuitarEnviado={quitarEnviado}
              onQuitarEnviadoConVentana={abrirQuitar}
              onMasEnviado={masDeEnviado}
              onGuardarPrecioEnviado={guardarPrecioEnviado}
              removingId={removingId}
              anulados={anulados}
              puedeComentar={esSuMesa()}
              onComentarioGuardado={(comentario) => setPedidoActual((p) => (p ? { ...p, comentario } : p))}
              onContinuar={() => navigate('/cart')}
              onEnviar={enviarDesdePanel}
              enviando={enviando}
            />
          )}
          </div>
        </motion.div>

        {/* Nota de un plato del carrito (vista de monitor) */}
        <NotaPlatoDialog
          item={notaDe}
          fijo={false}
          onCerrar={() => setNotaDe(null)}
          onGuardar={(nota, cantidad) => {
            setItemNote(notaDe.linea, nota, cantidad);
            setNotaDe(null);
          }}
        />

        {/* Cambiar el precio (solo cajero / admin): queda registrado */}
        <AnimatePresence>
          {editPrecio && (
            <motion.div
              key="precio"
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="absolute inset-0 z-30 bg-black/80 flex items-end sm:items-center justify-center p-4"
              onClick={() => !guardandoPrecio && setEditPrecio(null)}
            >
              <motion.form
                initial={{ y: 30 }}
                animate={{ y: 0 }}
                onClick={(e) => e.stopPropagation()}
                onSubmit={(e) => { e.preventDefault(); guardarPrecio(); }}
                className="w-full max-w-sm bg-card border border-gray-700/50 rounded-3xl p-5 space-y-4"
                role="dialog"
                aria-label="Cambiar precio"
              >
                <div>
                  <p className="text-[11px] text-amber-400 font-bold uppercase tracking-wider">Cambiar precio</p>
                  <h3 className="text-lg font-extrabold text-ink leading-tight">{editPrecio.nombre}</h3>
                  <p className="text-xs text-gray-500 mt-0.5">
                    {editPrecio.tipo === 'carrito' ? `Precio de carta: S/ ${Number(editPrecio.precioCarta).toFixed(2)}` : `Precio actual: S/ ${Number(editPrecio.precioCarta).toFixed(2)}`}
                    {' · '}queda registrado a tu nombre.
                  </p>
                </div>
                <div className="relative">
                  <span className="absolute left-4 top-1/2 -translate-y-1/2 text-gray-500 text-sm">S/</span>
                  <input
                    type="number"
                    inputMode="decimal"
                    step="0.10"
                    min="0"
                    autoFocus
                    value={editPrecio.valor}
                    onChange={(e) => setEditPrecio((p) => ({ ...p, valor: e.target.value }))}
                    aria-label="Nuevo precio por unidad"
                    className="w-full bg-background border border-gray-700/50 rounded-2xl pl-10 pr-4 py-3.5 text-lg font-bold text-ink focus:outline-none focus:border-amber-400"
                  />
                </div>
                {editPrecio.tipo === 'enviado' && (
                  <input
                    value={editPrecio.motivo}
                    onChange={(e) => setEditPrecio((p) => ({ ...p, motivo: e.target.value }))}
                    maxLength={200}
                    placeholder="Motivo (opcional): cortesía, promoción…"
                    aria-label="Motivo del cambio de precio"
                    className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3 text-sm text-ink placeholder-gray-600 focus:outline-none focus:border-amber-400"
                  />
                )}
                <div className="flex gap-3">
                  <button type="button" onClick={() => setEditPrecio(null)} disabled={guardandoPrecio} className="flex-1 py-3 rounded-2xl border border-gray-700 text-gray-300 font-bold text-sm">
                    Cancelar
                  </button>
                  <button type="submit" disabled={guardandoPrecio} className="flex-1 py-3 rounded-2xl bg-amber-500 hover:bg-amber-400 text-black font-bold text-sm disabled:opacity-50">
                    {guardandoPrecio ? 'Guardando…' : 'Guardar precio'}
                  </button>
                </div>
              </motion.form>
            </motion.div>
          )}
        </AnimatePresence>

        {/* Quitar un producto ya enviado: motivo obligatorio, queda registrado */}
        <AnimatePresence>
          {quitando && (
            <motion.div
              key="quitar"
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="absolute inset-0 z-30 bg-black/80 flex items-end sm:items-center justify-center p-4"
              onClick={() => removingId === null && setQuitando(null)}
            >
              <motion.div
                initial={{ y: 30 }}
                animate={{ y: 0 }}
                onClick={(e) => e.stopPropagation()}
                className="w-full max-w-md bg-card border border-gray-700/50 rounded-3xl p-5 space-y-4"
                role="dialog"
                aria-label="Quitar producto del pedido"
              >
                <div>
                  <p className="text-[11px] text-red-400 font-bold uppercase tracking-wider">Quitar del pedido</p>
                  <h3 className="text-lg font-extrabold text-ink leading-tight">{quitando.detalle.productoNombre}</h3>
                  <p className="text-xs text-gray-500 mt-0.5">
                    Queda registrado a nombre de <span className="text-gray-300 font-semibold">{user?.nombreCompleto}</span> y a cocina le sale un aviso “ANULADO”.
                  </p>
                </div>

                {quitando.detalle.cantidad > 1 && (
                  <div className="flex items-center justify-between">
                    <span className="text-sm text-gray-300">¿Cuántos quitar?</span>
                    <div className="flex items-center gap-2 bg-background rounded-full px-1.5 py-1 border border-gray-700/50">
                      <button
                        type="button"
                        onClick={() => setQuitando((q) => ({ ...q, cantidad: Math.max(1, q.cantidad - 1) }))}
                        className="w-8 h-8 rounded-full bg-card flex items-center justify-center text-gray-300"
                        aria-label="Menos"
                      >
                        <Minus size={14} />
                      </button>
                      <span className="w-12 text-center font-extrabold text-ink">
                        {quitando.cantidad}/{quitando.detalle.cantidad}
                      </span>
                      <button
                        type="button"
                        onClick={() => setQuitando((q) => ({ ...q, cantidad: Math.min(q.detalle.cantidad, q.cantidad + 1) }))}
                        className="w-8 h-8 rounded-full bg-card flex items-center justify-center text-gray-300"
                        aria-label="Más"
                      >
                        <Plus size={14} />
                      </button>
                    </div>
                  </div>
                )}

                <div>
                  <p className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Motivo</p>
                  <div className="flex flex-wrap gap-2 mb-2">
                    {MOTIVOS_RAPIDOS.map((m) => (
                      <button
                        key={m}
                        type="button"
                        onClick={() => setQuitando((q) => ({ ...q, motivo: m }))}
                        className={`px-3 py-1.5 rounded-full text-xs font-semibold border ${
                          quitando.motivo === m ? 'bg-red-500/20 border-red-500/60 text-red-300' : 'bg-background border-gray-700/50 text-gray-400'
                        }`}
                      >
                        {m}
                      </button>
                    ))}
                  </div>
                  <input
                    value={quitando.motivo}
                    onChange={(e) => setQuitando((q) => ({ ...q, motivo: e.target.value }))}
                    maxLength={200}
                    placeholder="u otro motivo…"
                    aria-label="Motivo"
                    className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3 text-sm text-ink placeholder-gray-600 focus:outline-none focus:border-red-500/60"
                  />
                </div>

                {esUltimoPlato && (
                  <p className="flex items-start gap-2 text-sm font-semibold text-red-300 bg-red-500/10 border border-red-500/40 rounded-2xl px-3 py-2.5" role="alert">
                    <Ban size={16} className="mt-0.5 flex-shrink-0" />
                    Es el último plato: se anulará el pedido completo y la mesa quedará libre.
                  </p>
                )}

                <div className="flex gap-3">
                  <button
                    type="button"
                    onClick={() => setQuitando(null)}
                    disabled={removingId !== null}
                    className="flex-1 py-3 rounded-2xl border border-gray-700 text-gray-300 font-bold text-sm"
                  >
                    Cancelar
                  </button>
                  <button
                    type="button"
                    onClick={confirmarQuitar}
                    disabled={removingId !== null || quitando.motivo.trim().length < 3}
                    className="flex-1 py-3 rounded-2xl bg-red-600 hover:bg-red-500 text-white font-bold text-sm disabled:opacity-40"
                  >
                    {removingId !== null ? 'Quitando…' : esUltimoPlato ? 'Anular pedido' : `Quitar ${quitando.cantidad}`}
                  </button>
                </div>
              </motion.div>
            </motion.div>
          )}
        </AnimatePresence>

        {/* Foto ampliada del plato (para mostrarla al cliente) */}
        <AnimatePresence>
          {vistaPrevia && (
            <motion.div
              key="vista-previa"
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="absolute inset-0 z-20 bg-background/95 backdrop-blur-sm flex flex-col items-center justify-center p-5"
              onClick={() => setVistaPrevia(null)}
              role="dialog"
              aria-label={`Foto de ${vistaPrevia.name}`}
            >
              <button
                type="button"
                onClick={() => setVistaPrevia(null)}
                className="absolute top-4 right-4 p-2.5 bg-gray-50/10 rounded-full text-ink hover:bg-gray-50/20"
                aria-label="Cerrar foto"
              >
                <X size={20} />
              </button>
              <motion.div
                initial={{ scale: 0.94 }}
                animate={{ scale: 1 }}
                className="w-full max-w-md"
                onClick={(e) => e.stopPropagation()}
              >
                <img
                  src={vistaPrevia.image}
                  alt={vistaPrevia.name}
                  className="w-full max-h-[60dvh] object-contain rounded-2xl bg-card"
                />
                <div className="mt-4 flex items-start justify-between gap-3">
                  <div className="min-w-0">
                    <h3 className="text-lg font-extrabold text-ink leading-tight">{vistaPrevia.name}</h3>
                    {vistaPrevia.description && (
                      <p className="text-sm text-gray-400 mt-1">{vistaPrevia.description}</p>
                    )}
                  </div>
                  <p className="text-primary font-extrabold text-lg flex-shrink-0">S/ {vistaPrevia.price.toFixed(2)}</p>
                </div>
                <motion.button
                  whileTap={{ scale: 0.97 }}
                  type="button"
                  onClick={() => { handleAdd(vistaPrevia); setVistaPrevia(null); }}
                  className="mt-4 w-full bg-primary hover:bg-primaryHover text-white font-bold py-3.5 rounded-2xl flex items-center justify-center gap-2 shadow-glow-orange"
                >
                  <Plus size={18} /> Agregar al pedido
                </motion.button>
              </motion.div>
            </motion.div>
          )}
        </AnimatePresence>
      </motion.div>
      )}
    </AnimatePresence>
  );
}
