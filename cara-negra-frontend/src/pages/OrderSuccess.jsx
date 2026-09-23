import { useEffect, useRef, useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { CheckCircle2, ArrowLeft, Printer, Eye, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { reimprimirComanda, previsualizarComanda } from '../services/ordersService';

export default function OrderSuccess() {
  const navigate = useNavigate();
  const { state } = useLocation();
  const [reimprimiendo, setReimprimiendo] = useState(false);
  const [cargandoPreview, setCargandoPreview] = useState(false);
  const [comandaPreview, setComandaPreview] = useState(null); // texto o null si el modal está cerrado
  const timerRef = useRef(null);

  const pedidoId = state?.pedidoId ?? '—';
  const mesaNumero = state?.mesaNumero;

  // Auto-redirigir al dashboard después de 10 s (se cancela si se abre la
  // previsualización de la comanda, para no interrumpir una demo al cliente).
  useEffect(() => {
    timerRef.current = setTimeout(() => navigate('/dashboard'), 10000);
    return () => clearTimeout(timerRef.current);
  }, [navigate]);

  const handleReimprimir = async () => {
    if (reimprimiendo || pedidoId === '—') return;
    setReimprimiendo(true);
    try {
      await reimprimirComanda(pedidoId);
      toast.success('Comanda reenviada a cocina');
    } catch (error) {
      toast.error(error.message);
    } finally {
      setReimprimiendo(false);
    }
  };

  const handleVerComanda = async () => {
    if (cargandoPreview || pedidoId === '—') return;
    clearTimeout(timerRef.current);
    setCargandoPreview(true);
    try {
      const texto = await previsualizarComanda(pedidoId);
      setComandaPreview(texto);
    } catch (error) {
      toast.error(error.message);
    } finally {
      setCargandoPreview(false);
    }
  };

  return (
    <div className="min-h-screen flex flex-col items-center justify-center p-6 bg-background overflow-hidden">
      <div className="flex flex-col items-center max-w-sm w-full text-center">

        {/* Animated check */}
        <motion.div
          initial={{ scale: 0, opacity: 0 }}
          animate={{ scale: 1, opacity: 1 }}
          transition={{ type: 'spring', stiffness: 260, damping: 18, delay: 0.1 }}
          className="relative mb-8"
        >
          {/* Outer glow ring */}
          <motion.div
            initial={{ scale: 0.6, opacity: 0 }}
            animate={{ scale: 1.4, opacity: 0 }}
            transition={{ duration: 0.8, delay: 0.3, ease: 'easeOut' }}
            className="absolute inset-0 rounded-full bg-accentGreen"
          />
          <div className="w-28 h-28 rounded-full bg-emerald-500/10 border-2 border-emerald-500/40 flex items-center justify-center shadow-glow-green">
            <CheckCircle2 size={56} className="text-emerald-400" strokeWidth={1.5} />
          </div>
        </motion.div>

        {/* Title */}
        <motion.div
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.35 }}
        >
          <h1 className="text-2xl font-extrabold text-white mb-2 tracking-tight">
            ¡Pedido enviado!
          </h1>
          <p className="text-gray-500 text-sm leading-relaxed">
            {mesaNumero
              ? `La comanda de la Mesa ${mesaNumero} ha sido enviada a cocina.`
              : 'La comanda ha sido enviada a cocina correctamente.'}
          </p>
        </motion.div>

        {/* Order card */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.5, type: 'spring', stiffness: 200 }}
          className="w-full bg-orange-950/20 border border-primary/25 rounded-3xl p-6 my-8"
        >
          <p className="text-[10px] text-gray-500 uppercase tracking-widest mb-2">
            Nº de pedido
          </p>
          <motion.p
            initial={{ scale: 0.7 }}
            animate={{ scale: 1 }}
            transition={{ delay: 0.6, type: 'spring', stiffness: 200 }}
            className="text-6xl font-black text-primary mb-3"
          >
            #{pedidoId}
          </motion.p>
          {mesaNumero && (
            <p className="text-sm text-gray-400">
              Mesa {mesaNumero} · En preparación
            </p>
          )}
        </motion.div>

        {/* Reimprimir comanda */}
        <motion.button
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          transition={{ delay: 0.6 }}
          whileTap={{ scale: 0.96 }}
          onClick={handleReimprimir}
          disabled={reimprimiendo}
          className="w-full bg-card border border-gray-700/40 hover:bg-cardHighlight text-white font-bold py-3.5 rounded-2xl flex items-center justify-center gap-2 transition-colors mb-3 disabled:opacity-50"
        >
          <Printer size={18} />
          {reimprimiendo ? 'Reimprimiendo…' : 'Reimprimir comanda'}
        </motion.button>

        {/* Ver formato de comanda (previsualización, sin imprimir de verdad) */}
        <motion.button
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          transition={{ delay: 0.62 }}
          whileTap={{ scale: 0.96 }}
          onClick={handleVerComanda}
          disabled={cargandoPreview}
          className="w-full bg-card border border-gray-700/40 hover:bg-cardHighlight text-white font-bold py-3.5 rounded-2xl flex items-center justify-center gap-2 transition-colors mb-3 disabled:opacity-50"
        >
          <Eye size={18} />
          {cargandoPreview ? 'Cargando…' : 'Ver formato de comanda'}
        </motion.button>

        {/* Back button */}
        <motion.button
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          transition={{ delay: 0.7 }}
          whileTap={{ scale: 0.96 }}
          onClick={() => navigate('/dashboard')}
          className="w-full bg-card border border-gray-700/40 hover:bg-cardHighlight text-white font-bold py-4 rounded-2xl flex items-center justify-center gap-2 transition-colors"
        >
          <ArrowLeft size={18} />
          Volver al mapa de mesas
        </motion.button>

        <motion.p
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          transition={{ delay: 1 }}
          className="text-xs text-gray-700 mt-5"
        >
          Redirigiendo en 10 segundos…
        </motion.p>
      </div>

      {/* Modal de previsualización de la comanda: mismo texto que se envía a la
          impresora térmica, para poder mostrarlo sin depender de tener la
          impresora física conectada (ej. presentación del sistema al cliente). */}
      <AnimatePresence>
        {comandaPreview !== null && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-[70] bg-black/80 backdrop-blur-sm flex items-center justify-center p-4"
            onClick={() => setComandaPreview(null)}
          >
            <motion.div
              initial={{ opacity: 0, scale: 0.94, y: 16 }}
              animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.94 }}
              onClick={(e) => e.stopPropagation()}
              className="w-full max-w-sm bg-white text-black rounded-3xl overflow-hidden shadow-2xl flex flex-col"
              style={{ maxHeight: '85dvh' }}
            >
              <div className="px-5 pt-5 pb-3 flex justify-between items-center border-b border-dashed border-gray-300 flex-shrink-0">
                <p className="font-extrabold text-sm text-gray-700">Formato de la comanda</p>
                <button
                  onClick={() => setComandaPreview(null)}
                  className="p-1.5 hover:bg-gray-100 rounded-full text-gray-500"
                >
                  <X size={16} />
                </button>
              </div>
              <div className="px-5 py-4 overflow-y-auto">
                <pre className="font-mono text-xs leading-relaxed whitespace-pre-wrap break-words">
                  {comandaPreview}
                </pre>
              </div>
              <div className="px-5 py-3 border-t border-dashed border-gray-300 flex-shrink-0">
                <p className="text-[11px] text-gray-500">
                  Así se ve exactamente lo que imprime la ticketera de cocina (texto plano, sin tildes/ñ para compatibilidad con impresoras económicas).
                </p>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
