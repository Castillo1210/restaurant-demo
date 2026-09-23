import { useState, useEffect, useCallback } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { ShoppingCart, Receipt, UtensilsCrossed, ChefHat } from 'lucide-react';
import toast from 'react-hot-toast';
import { useCart } from '../CartContext';
import { useAuth } from '../context/AuthContext';
import { useClock } from '../hooks/useClock';
import { getTables } from '../services/tablesService';
import { connectPedidosHub, onHubEvent } from '../services/signalrService';
import UserSettingsModal from '../components/UserSettingsModal';
import MenuDrawer from '../components/MenuDrawer';

const STATUS_STYLES = {
  free: {
    dot: 'bg-accentGreen',
    bg: 'bg-emerald-950/40',
    border: 'border-emerald-900/40',
    shadow: 'shadow-glow-green',
    text: 'text-accentGreen',
    label: 'Libre',
  },
  occupied: {
    dot: 'bg-primary',
    bg: 'bg-orange-950/40',
    border: 'border-orange-900/40',
    shadow: 'shadow-glow-orange',
    text: 'text-primary',
    label: 'Ocupada',
  },
  reserved: {
    dot: 'bg-accentYellow',
    bg: 'bg-yellow-950/40',
    border: 'border-yellow-900/40',
    shadow: 'shadow-glow-yellow',
    text: 'text-accentYellow',
    label: 'Reservada',
  },
  maintenance: {
    dot: 'bg-gray-500',
    bg: 'bg-gray-800/40',
    border: 'border-gray-700/40',
    shadow: '',
    text: 'text-gray-400',
    label: 'Mantenimiento',
  },
};

const containerVariants = {
  hidden: {},
  show: { transition: { staggerChildren: 0.05 } },
};

const cardVariants = {
  hidden: { opacity: 0, scale: 0.92, y: 10 },
  show: { opacity: 1, scale: 1, y: 0, transition: { type: 'spring', stiffness: 260, damping: 20 } },
};

export default function Dashboard() {
  const navigate = useNavigate();
  const location = useLocation();
  const { setActiveTable, cart } = useCart();
  const { user } = useAuth();
  const { time } = useClock();

  const [tables, setTables] = useState([]);
  const [isSettingsOpen, setIsSettingsOpen] = useState(false);
  const [isMenuOpen, setIsMenuOpen] = useState(false);

  const loadTables = useCallback(() => {
    getTables().then(setTables);
  }, []);

  // El personal de cocina no usa el mapa de mesas: su inicio es la pantalla de cocina.
  useEffect(() => {
    if (user?.rol === 'COCINA') navigate('/cocina', { replace: true });
  }, [user?.rol, navigate]);

  // Cargar mesas al montar
  useEffect(() => {
    loadTables();
  }, [loadTables]);

  // Tiempo real: refrescar el mapa de mesas cuando cambia el estado de una mesa
  // o cuando se crea/actualiza un pedido (libera u ocupa mesas indirectamente).
  useEffect(() => {
    let unsubscribers = [];

    connectPedidosHub().then((connection) => {
      if (!connection) return;
      unsubscribers = [
        onHubEvent('MesaEstadoCambiado', loadTables),
        onHubEvent('NuevoPedido', loadTables),
        onHubEvent('PedidoEstadoCambiado', (evento) => {
          loadTables();
          // Aviso al mozo cuando cocina termina un pedido (pantalla de cocina en vivo).
          if (evento?.estadoNuevo === 'Listo') {
            toast.success(`Mesa ${evento.mesaNumero}: pedido listo para servir`, { icon: '🍽️', duration: 6000 });
          }
        }),
        onHubEvent('PagoRecibido', loadTables),
        onHubEvent('PagoAnulado', loadTables),
      ];
    });

    return () => {
      unsubscribers.forEach((unsub) => unsub());
    };
  }, [loadTables]);

  // Cerrar el drawer cuando se navega de vuelta al dashboard
  useEffect(() => {
    setIsMenuOpen(false);
  }, [location.key]);

  const handleTableClick = (table) => {
    if (table.status === 'maintenance') return;
    setActiveTable(table);
    setIsMenuOpen(true);
  };

  const firstName = user?.nombreCompleto?.split(' ')[0] ?? 'Camarero';
  const initial = firstName[0]?.toUpperCase() ?? 'C';
  const puedeCobrar = user?.rol === 'CAJERO' || user?.rol === 'ADMIN';
  const esAdmin = user?.rol === 'ADMIN';

  // Contadores dinámicos
  const freeCount = tables.filter((t) => t.status === 'free').length;
  const occupiedCount = tables.filter((t) => t.status === 'occupied').length;
  const reservedCount = tables.filter((t) => t.status === 'reserved').length;

  const cartItemCount = cart.reduce((acc, item) => acc + item.quantity, 0);

  return (
    <div className="min-h-screen bg-background pb-28 relative overflow-x-hidden">
      {/* ── Header ──────────────────────────────────────── */}
      <div className="p-5 pt-6 flex justify-between items-center">
        <div>
          <p className="text-gray-500 text-sm font-medium tabular-nums">{time}</p>
          <h1 className="text-2xl font-extrabold mt-0.5 tracking-tight">
            Hola, {firstName} 👋
          </h1>
          {user?.rol && (
            <p className="text-xs text-gray-500 mt-0.5">{user.rol}</p>
          )}
        </div>
        <div className="flex items-center gap-2.5">
          {esAdmin && (
            <motion.button
              whileTap={{ scale: 0.9 }}
              onClick={() => navigate('/cocina')}
              className="w-12 h-12 bg-card rounded-full flex items-center justify-center border-2 border-gray-700/50 hover:bg-cardHighlight transition-colors"
              title="Pantalla de cocina"
            >
              <ChefHat size={20} className="text-gray-300" />
            </motion.button>
          )}
          {esAdmin && (
            <motion.button
              whileTap={{ scale: 0.9 }}
              onClick={() => navigate('/admin')}
              className="w-12 h-12 bg-card rounded-full flex items-center justify-center border-2 border-gray-700/50 hover:bg-cardHighlight transition-colors"
              title="Panel de administración"
            >
              <UtensilsCrossed size={20} className="text-gray-300" />
            </motion.button>
          )}
          {puedeCobrar && (
            <motion.button
              whileTap={{ scale: 0.9 }}
              onClick={() => navigate('/caja')}
              className="w-12 h-12 bg-card rounded-full flex items-center justify-center border-2 border-gray-700/50 hover:bg-cardHighlight transition-colors"
              title="Ir a caja"
            >
              <Receipt size={20} className="text-gray-300" />
            </motion.button>
          )}
          <motion.button
            whileTap={{ scale: 0.9 }}
            onClick={() => setIsSettingsOpen(true)}
            className="w-12 h-12 bg-primary/20 rounded-full flex items-center justify-center border-2 border-primary/40 hover:bg-primary/30 transition-colors"
          >
            <span className="font-extrabold text-lg text-primary">{initial}</span>
          </motion.button>
        </div>
      </div>

      {/* ── Stats cards ─────────────────────────────────── */}
      <div className="px-5 grid grid-cols-3 gap-3 mb-6">
        {[
          { count: freeCount, label: 'Libres', color: 'text-accentGreen', border: 'border-emerald-900/30', bg: 'bg-emerald-950/30' },
          { count: occupiedCount, label: 'Ocupadas', color: 'text-primary', border: 'border-orange-900/30', bg: 'bg-orange-950/30' },
          { count: reservedCount, label: 'Reservadas', color: 'text-accentYellow', border: 'border-yellow-900/30', bg: 'bg-yellow-950/30' },
        ].map(({ count, label, color, border, bg }) => (
          <motion.div
            key={label}
            initial={{ opacity: 0, y: -8 }}
            animate={{ opacity: 1, y: 0 }}
            className={`${bg} border ${border} p-4 rounded-2xl text-center`}
          >
            <p className={`text-3xl font-extrabold ${color}`}>{count}</p>
            <p className={`text-[10px] font-bold uppercase tracking-wider mt-1 ${color} opacity-80`}>
              {label}
            </p>
          </motion.div>
        ))}
      </div>

      {/* ── Legend ──────────────────────────────────────── */}
      <div className="px-5 flex gap-5 mb-4 text-xs font-medium text-gray-400 flex-wrap">
        {Object.entries(STATUS_STYLES).map(([key, s]) => (
          <div key={key} className="flex items-center gap-1.5">
            <span className={`w-2 h-2 rounded-full ${s.dot}`} />
            {s.label}
          </div>
        ))}
      </div>

      {/* ── Tables grid ─────────────────────────────────── */}
      <motion.div
        variants={containerVariants}
        initial="hidden"
        animate="show"
        className="px-5 grid grid-cols-3 gap-3"
      >
        {tables.map((table) => {
          const style = STATUS_STYLES[table.status] ?? STATUS_STYLES.free;
          return (
            <motion.button
              key={table.id}
              variants={cardVariants}
              whileTap={{ scale: table.status === 'maintenance' ? 1 : 0.93 }}
              onClick={() => handleTableClick(table)}
              disabled={table.status === 'maintenance'}
              className={`relative p-4 rounded-2xl ${style.bg} border ${style.border} flex flex-col items-center justify-center h-32 transition-shadow ${
                table.status === 'maintenance' ? 'opacity-60 cursor-not-allowed' : `hover:${style.shadow}`
              }`}
            >
              {/* Status dot */}
              <span className={`absolute top-2.5 right-2.5 w-2.5 h-2.5 rounded-full ${style.dot}`} />

              {/* Table number */}
              <h2 className="text-3xl font-extrabold text-white mb-1 tracking-tight">{table.numeroMesa}</h2>

              {/* Status */}
              <p className={`text-[11px] font-bold mt-1 ${style.text}`}>
                {style.label}
              </p>
            </motion.button>
          );
        })}
      </motion.div>

      {tables.length === 0 && (
        <p className="px-5 text-center text-sm text-gray-600 mt-10">
          Aún no hay mesas configuradas. Pídele al administrador que las cree desde el panel.
        </p>
      )}

      {/* ── Modals ──────────────────────────────────────── */}
      <AnimatePresence>
        {isSettingsOpen && <UserSettingsModal onClose={() => setIsSettingsOpen(false)} />}
      </AnimatePresence>
      <MenuDrawer isOpen={isMenuOpen} onClose={() => setIsMenuOpen(false)} />

      {/* ── Floating cart button ─────────────────────────── */}
      <motion.button
        whileTap={{ scale: 0.9 }}
        onClick={() => navigate('/cart')}
        className={`fixed bottom-8 right-5 rounded-full w-16 h-16 flex items-center justify-center shadow-xl z-40 border-2 border-background transition-all ${
          cart.length > 0
            ? 'bg-primary shadow-glow-orange'
            : 'bg-card border-gray-700/50'
        }`}
      >
        <ShoppingCart size={24} className="text-white" />
        {cartItemCount > 0 && (
          <motion.span
            key={cartItemCount}
            initial={{ scale: 0.5 }}
            animate={{ scale: 1 }}
            className="absolute -top-1 -right-1 bg-red-500 text-white rounded-full w-6 h-6 text-xs flex items-center justify-center font-bold border-2 border-background"
          >
            {cartItemCount}
          </motion.span>
        )}
      </motion.button>
    </div>
  );
}
