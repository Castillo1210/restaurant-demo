import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { useCart } from '../CartContext';
import { getActivePedidoForMesa, COMENTARIO_MAXIMO } from '../services/ordersService';
import useEnviarPedido from '../hooks/useEnviarPedido';
import { getCremas } from '../services/cremasService';
import { ChevronLeft, Trash2, Plus, Minus, FileText, Send, ShoppingBag, UtensilsCrossed, StickyNote } from 'lucide-react';
import toast from 'react-hot-toast';
import { etiquetaMesa } from '../utils/mesaLabel';
import NotaPlatoDialog from '../components/NotaPlatoDialog';

export default function CartPage() {
  const navigate = useNavigate();
  const { cart, activeTable, updateQuantity, removeFromCart, setItemNote, total } = useCart();
  // Línea del carrito cuya nota se está escribiendo ("sin cebolla").
  const [notaDe, setNotaDe] = useState(null);
  const [cremasDisponibles, setCremasDisponibles] = useState([]);
  const [cremasSeleccionadas, setCremasSeleccionadas] = useState(new Set());
  // Comentario general del pedido ("todo para llevar", "servir juntos"): uno por pedido, sale
  // destacado arriba en la comanda. Si la mesa ya tiene pedido, se muestra el que tiene y se
  // puede ajustar en este mismo envío.
  const [note, setNote] = useState('');
  const [comentarioOriginal, setComentarioOriginal] = useState(null);
  // Envío compartido con el botón de un clic del monitor (anti doble toque incluido).
  const { enviar, enviando: isSending } = useEnviarPedido();

  // Cremas/toppings del catálogo administrable (Fase 8): chips de un solo tap para no
  // tener que escribir todo a mano cada vez. Se pueden combinar entre sí y con una nota
  // libre adicional. El admin controla desde su panel cuáles están disponibles.
  useEffect(() => {
    getCremas({ soloActivas: true }).then((cremas) => setCremasDisponibles(cremas.map((c) => c.nombre)));
  }, []);

  useEffect(() => {
    if (activeTable?.status !== 'occupied') return undefined;
    let vigente = true;
    getActivePedidoForMesa(activeTable.id)
      .then((activo) => {
        if (!vigente || !activo) return;
        setComentarioOriginal(activo.comentario ?? '');
        setNote((actual) => actual || activo.comentario || '');
      })
      .catch(() => { /* sin conexión: se envía sin tocar el comentario */ });
    return () => { vigente = false; };
  }, [activeTable?.id, activeTable?.status]);

  const itemCount = cart.reduce((acc, i) => acc + i.quantity, 0);
  const itemLabel = itemCount === 1 ? '1 artículo' : `${itemCount} artículos`;

  const toggleCrema = (crema) => {
    setCremasSeleccionadas((prev) => {
      const next = new Set(prev);
      if (next.has(crema)) next.delete(crema);
      else next.add(crema);
      return next;
    });
  };

  // Comentario del pedido = cremas elegidas por tap + lo escrito a mano.
  const notaCompuesta = () => {
    const partes = [...cremasSeleccionadas];
    if (note.trim()) partes.push(note.trim());
    return partes.join(', ');
  };
  const largoComentario = notaCompuesta().length;

  const handleSendOrder = async () => {
    const comentario = notaCompuesta();
    if (comentario.length > COMENTARIO_MAXIMO) {
      toast.error(`El comentario admite hasta ${COMENTARIO_MAXIMO} caracteres`);
      return;
    }
    try {
      const enviado = await enviar({ comentario, comentarioOriginal });
      if (!enviado) return; // ya había un envío en curso (doble toque)
      const { pedido, comandaCaja } = enviado;
      setCremasSeleccionadas(new Set());
      setNote('');
      navigate('/success', {
        state: { pedidoId: pedido.id, mesaNumero: pedido.mesaNumero, salonNombre: pedido.salonNombre, comandaCaja },
      });
    } catch (err) {
      toast.error(err.message ?? 'Error al enviar el pedido. Intenta de nuevo.');
    }
  };

  if (cart.length === 0) {
    return (
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        className="min-h-screen bg-background flex flex-col items-center justify-center p-6"
      >
        <div className="w-20 h-20 bg-card rounded-3xl flex items-center justify-center mb-5 border border-gray-800">
          <ShoppingBag size={36} className="text-gray-600" />
        </div>
        <h2 className="text-xl font-extrabold text-ink mb-2">Carrito vacío</h2>
        <p className="text-gray-500 text-sm mb-8 text-center">
          Aún no has añadido productos a la mesa.
        </p>
        <motion.button
          whileTap={{ scale: 0.96 }}
          onClick={() => navigate('/dashboard')}
          className="bg-primary hover:bg-primaryHover text-white px-8 py-3.5 rounded-2xl font-bold transition-colors shadow-glow-orange"
        >
          Volver al inicio
        </motion.button>
      </motion.div>
    );
  }

  return (
    <div className="min-h-screen bg-background flex flex-col">
      {/* ── Header ──────────────────────────────────────── */}
      <div className="px-4 pt-8 pb-4 border-b border-gray-800/60 bg-background sticky top-0 z-10">
        <div className="flex items-center gap-3">
          <motion.button
            whileTap={{ scale: 0.9 }}
            // Atrás = volver a la carta de esta mesa (para agregar más platos), no al mapa de mesas.
            onClick={() => navigate('/dashboard', { state: { abrirCarta: true } })}
            className="p-2 bg-card rounded-full hover:bg-cardHighlight border border-gray-800/50"
            aria-label="Volver a la carta"
            title="Volver a la carta"
          >
            <ChevronLeft size={22} />
          </motion.button>
          <div>
            <h1 className="text-lg font-extrabold tracking-tight">
              Pedido · {etiquetaMesa(activeTable?.salonNombre, activeTable?.numeroMesa)}
            </h1>
            <p className="text-xs text-gray-500">{itemLabel} seleccionados</p>
          </div>
        </div>
      </div>

      {/* ── Items ───────────────────────────────────────── */}
      <div className="flex-1 overflow-y-auto px-4 py-4 space-y-3 pb-4">
        <AnimatePresence>
          {cart.map((item) => (
            <motion.div
              key={item.linea ?? item.id}
              layout
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, x: -40, scale: 0.95 }}
              transition={{ duration: 0.2 }}
              className="bg-card rounded-2xl p-4 flex gap-3 items-center border border-gray-800/40"
            >
              <div className="w-16 h-16 rounded-xl bg-background flex items-center justify-center flex-shrink-0">
                <UtensilsCrossed size={20} className="text-gray-600" />
              </div>
              <div className="flex-1 min-w-0">
                <h3 className="font-bold text-sm leading-tight break-words">{item.name}</h3>
                <p className="text-primary text-xs font-bold mt-0.5">
                  S/ {item.price.toFixed(2)} / ud.
                  {item.precioEditado && (
                    <span className="ml-1.5 text-[10px] font-bold text-amber-400">
                      (carta S/ {Number(item.precioCarta).toFixed(2)})
                    </span>
                  )}
                </p>
                {item.nota && (
                  <p className="text-xs font-bold text-amber-400 mt-1 flex items-start gap-1 break-words">
                    <StickyNote size={12} className="mt-0.5 flex-shrink-0" /> {item.nota}
                  </p>
                )}
                {/* Counter */}
                <div className="flex items-center gap-2 mt-2.5">
                  <div className="flex items-center gap-1.5 bg-background rounded-full px-1.5 py-1 border border-gray-700/40">
                    <motion.button
                      whileTap={{ scale: 0.8 }}
                      onClick={() => updateQuantity(item.linea, -1)}
                      className="w-7 h-7 rounded-full flex items-center justify-center text-gray-400 hover:text-ink hover:bg-gray-700 transition-colors"
                    >
                      <Minus size={13} />
                    </motion.button>
                    <motion.span
                      key={item.quantity}
                      initial={{ scale: 1.3 }}
                      animate={{ scale: 1 }}
                      className="w-5 text-center text-sm font-extrabold text-ink"
                    >
                      {item.quantity}
                    </motion.span>
                    <motion.button
                      whileTap={{ scale: 0.8 }}
                      onClick={() => updateQuantity(item.linea, 1)}
                      className="w-7 h-7 bg-primary rounded-full flex items-center justify-center hover:bg-primaryHover transition-colors"
                    >
                      <Plus size={13} className="text-white" />
                    </motion.button>
                  </div>
                  <button
                    type="button"
                    onClick={() => setNotaDe(item)}
                    className={`flex items-center gap-1 px-2.5 py-1.5 rounded-full text-xs font-bold border whitespace-nowrap transition-colors ${
                      item.nota
                        ? 'border-amber-700/50 text-amber-400 bg-amber-950/40'
                        : 'border-gray-700/40 text-gray-400 hover:text-primary hover:border-primary/50'
                    }`}
                    aria-label={`Nota de ${item.name}`}
                  >
                    <StickyNote size={13} /> Nota
                  </button>
                </div>
              </div>

              <div className="flex flex-col items-end gap-3 flex-shrink-0">
                <p className="font-extrabold text-base">
                  S/ {(item.price * item.quantity).toFixed(2)}
                </p>
                <motion.button
                  whileTap={{ scale: 0.85 }}
                  onClick={() => {
                    removeFromCart(item.linea);
                    toast(`${item.name} eliminado`, { icon: '🗑️', duration: 1500 });
                  }}
                  className="p-1.5 hover:bg-red-500/20 rounded-full text-gray-600 hover:text-red-500 transition-colors"
                >
                  <Trash2 size={16} />
                </motion.button>
              </div>
            </motion.div>
          ))}
        </AnimatePresence>
      </div>

      {/* ── Footer ──────────────────────────────────────── */}
      <div className="px-4 pb-8 pt-3 border-t border-gray-800/60 bg-card/80 backdrop-blur-md space-y-4">
        {/* Cremas rápidas */}
        <div>
          <p className="text-xs font-bold text-gray-400 uppercase tracking-wider">
            Comentario del pedido
          </p>
          <p className="text-[11px] text-gray-500 mb-2">
            Cremas y lo que cocina debe saber de todo el pedido. Sale arriba en la comanda. Para un solo plato, usa «Nota» en ese plato.
          </p>
          <div className="flex gap-2 flex-wrap">
            {cremasDisponibles.length === 0 && (
              <p className="text-xs text-gray-600">No hay cremas configuradas. Puedes escribir una indicación abajo.</p>
            )}
            {cremasDisponibles.map((crema) => {
              const activa = cremasSeleccionadas.has(crema);
              return (
                <motion.button
                  key={crema}
                  type="button"
                  whileTap={{ scale: 0.93 }}
                  onClick={() => toggleCrema(crema)}
                  className={`px-3 py-1.5 rounded-full text-xs font-semibold border transition-colors ${
                    activa
                      ? 'bg-primary text-white border-primary shadow-glow-orange'
                      : 'bg-background text-gray-400 border-gray-700/50 hover:border-gray-500'
                  }`}
                >
                  {crema}
                </motion.button>
              );
            })}
          </div>
        </div>

        {/* Comentario libre (para lo que no cubren las chips de arriba) */}
        <div>
          <div className="flex items-center gap-3 bg-background p-3.5 rounded-2xl border border-gray-700/30 focus-within:border-primary/60">
            <FileText size={17} className="text-gray-500 flex-shrink-0" />
            <input
              type="text"
              value={note}
              maxLength={COMENTARIO_MAXIMO}
              onChange={(e) => setNote(e.target.value)}
              placeholder="Ej.: todo para llevar, servir juntos…"
              aria-label="Comentario del pedido"
              className="bg-transparent w-full text-sm focus:outline-none placeholder-gray-600 text-ink"
            />
          </div>
          {comentarioOriginal && (
            <p className="text-[11px] text-gray-500 mt-1 px-1">Comentario actual del pedido; puedes cambiarlo o borrarlo.</p>
          )}
          {largoComentario > COMENTARIO_MAXIMO - 50 && (
            <p className={`text-[11px] mt-1 px-1 ${largoComentario > COMENTARIO_MAXIMO ? 'text-red-400 font-bold' : 'text-gray-500'}`}>
              {largoComentario}/{COMENTARIO_MAXIMO} caracteres
            </p>
          )}
        </div>

        {/* Total */}
        <div className="flex justify-between items-center">
          <span className="text-gray-400 text-base">Total estimado</span>
          <motion.span
            key={total}
            initial={{ scale: 1.05 }}
            animate={{ scale: 1 }}
            className="text-ink font-extrabold text-2xl"
          >
            S/ {total.toFixed(2)}
          </motion.span>
        </div>

        {/* Send button */}
        <motion.button
          whileTap={{ scale: 0.97 }}
          onClick={handleSendOrder}
          disabled={isSending}
          className="w-full bg-primary hover:bg-primaryHover disabled:opacity-60 text-white font-extrabold py-4 rounded-2xl flex justify-center items-center gap-2 transition-colors shadow-glow-orange text-base"
        >
          {isSending ? (
            <span className="animate-pulse">Enviando…</span>
          ) : (
            <>
              <Send size={18} />
              Enviar a cocina
            </>
          )}
        </motion.button>
      </div>
      <NotaPlatoDialog
        item={notaDe}
        sugerencias={cremasDisponibles}
        onCerrar={() => setNotaDe(null)}
        onGuardar={(nota, cantidad) => {
          setItemNote(notaDe.linea, nota, cantidad);
          setNotaDe(null);
        }}
      />
    </div>
  );
}
