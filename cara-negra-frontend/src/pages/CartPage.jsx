import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { useCart } from '../CartContext';
import { createPedido, agregarDetalle, getActivePedidoForMesa } from '../services/ordersService';
import { getCremas } from '../services/cremasService';
import { useAuth } from '../context/AuthContext';
import { ChevronLeft, Trash2, Plus, Minus, FileText, Send, ShoppingBag, UtensilsCrossed } from 'lucide-react';
import toast from 'react-hot-toast';

export default function CartPage() {
  const navigate = useNavigate();
  const { cart, activeTable, updateQuantity, removeFromCart, total, clearCart } = useCart();
  const { user } = useAuth();
  const [cremasDisponibles, setCremasDisponibles] = useState([]);
  const [cremasSeleccionadas, setCremasSeleccionadas] = useState(new Set());
  const [note, setNote] = useState('');
  const [isSending, setIsSending] = useState(false);

  // Cremas/toppings del catálogo administrable (Fase 8): chips de un solo tap para no
  // tener que escribir todo a mano cada vez. Se pueden combinar entre sí y con una nota
  // libre adicional. El admin controla desde su panel cuáles están disponibles.
  useEffect(() => {
    getCremas({ soloActivas: true }).then((cremas) => setCremasDisponibles(cremas.map((c) => c.nombre)));
  }, []);

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

  // Combina las cremas elegidas por tap con lo que se haya escrito a mano en el campo libre.
  const notaCompuesta = () => {
    const partes = [...cremasSeleccionadas];
    if (note.trim()) partes.push(note.trim());
    return partes.join(', ');
  };

  const buildDetalles = () => {
    const nota = notaCompuesta();
    return cart.map((item, idx) => ({
      productoId: item.id,
      cantidad: item.quantity,
      // El backend solo soporta notas por línea (no una nota global del pedido);
      // como compromiso pragmático, la nota general se adjunta a la primera línea.
      notas: idx === 0 && nota ? nota : undefined,
    }));
  };

  const handleSendOrder = async () => {
    if (!activeTable) {
      toast.error('No hay una mesa seleccionada');
      return;
    }
    if (!user?.usuarioId) {
      toast.error('No se pudo identificar al mozo. Vuelve a iniciar sesión.');
      return;
    }

    setIsSending(true);
    try {
      const detalles = buildDetalles();
      let pedido;

      if (activeTable.status === 'occupied') {
        // La mesa ya tiene un pedido activo: se agregan los ítems a ese pedido
        // en vez de crear uno nuevo (una mesa no puede tener dos pedidos abiertos).
        const activo = await getActivePedidoForMesa(activeTable.id);
        if (!activo) {
          throw new Error('La mesa figura ocupada pero no se encontró su pedido activo. Actualiza la pantalla e intenta de nuevo.');
        }
        for (const detalle of detalles) {
          pedido = await agregarDetalle(activo.id, detalle);
        }
      } else {
        pedido = await createPedido({
          mesaId: activeTable.id,
          usuarioId: user.usuarioId,
          detalles,
        });
      }

      clearCart();
      setCremasSeleccionadas(new Set());
      setNote('');
      navigate('/success', {
        state: { pedidoId: pedido.id, mesaNumero: pedido.mesaNumero },
      });
    } catch (err) {
      toast.error(err.message ?? 'Error al enviar el pedido. Intenta de nuevo.');
    } finally {
      setIsSending(false);
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
        <h2 className="text-xl font-extrabold text-white mb-2">Carrito vacío</h2>
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
            onClick={() => navigate('/dashboard')}
            className="p-2 bg-card rounded-full hover:bg-cardHighlight border border-gray-800/50"
          >
            <ChevronLeft size={22} />
          </motion.button>
          <div>
            <h1 className="text-lg font-extrabold tracking-tight">
              Pedido · Mesa {activeTable?.numeroMesa}
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
              key={item.id}
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
                <h3 className="font-bold text-sm leading-tight truncate">{item.name}</h3>
                <p className="text-primary text-xs font-bold mt-0.5">
                  S/ {item.price.toFixed(2)} / ud.
                </p>
                {/* Counter */}
                <div className="flex items-center gap-2 mt-2.5">
                  <div className="flex items-center gap-1.5 bg-background rounded-full px-1.5 py-1 border border-gray-700/40">
                    <motion.button
                      whileTap={{ scale: 0.8 }}
                      onClick={() => updateQuantity(item.id, -1)}
                      className="w-7 h-7 rounded-full flex items-center justify-center text-gray-400 hover:text-white hover:bg-gray-700 transition-colors"
                    >
                      <Minus size={13} />
                    </motion.button>
                    <motion.span
                      key={item.quantity}
                      initial={{ scale: 1.3 }}
                      animate={{ scale: 1 }}
                      className="w-5 text-center text-sm font-extrabold text-white"
                    >
                      {item.quantity}
                    </motion.span>
                    <motion.button
                      whileTap={{ scale: 0.8 }}
                      onClick={() => updateQuantity(item.id, 1)}
                      className="w-7 h-7 bg-primary rounded-full flex items-center justify-center hover:bg-primaryHover transition-colors"
                    >
                      <Plus size={13} className="text-white" />
                    </motion.button>
                  </div>
                </div>
              </div>

              <div className="flex flex-col items-end gap-3 flex-shrink-0">
                <p className="font-extrabold text-base">
                  S/ {(item.price * item.quantity).toFixed(2)}
                </p>
                <motion.button
                  whileTap={{ scale: 0.85 }}
                  onClick={() => {
                    removeFromCart(item.id);
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
          <p className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">
            Cremas / observaciones
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

        {/* Nota al cocinero (para lo que no cubren las chips de arriba) */}
        <div className="flex items-center gap-3 bg-background p-3.5 rounded-2xl border border-gray-700/30">
          <FileText size={17} className="text-gray-500 flex-shrink-0" />
          <input
            type="text"
            value={note}
            onChange={(e) => setNote(e.target.value)}
            placeholder="Otra indicación (opcional)…"
            className="bg-transparent w-full text-sm focus:outline-none placeholder-gray-600 text-white"
          />
        </div>

        {/* Total */}
        <div className="flex justify-between items-center">
          <span className="text-gray-400 text-base">Total estimado</span>
          <motion.span
            key={total}
            initial={{ scale: 1.05 }}
            animate={{ scale: 1 }}
            className="text-white font-extrabold text-2xl"
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
    </div>
  );
}
