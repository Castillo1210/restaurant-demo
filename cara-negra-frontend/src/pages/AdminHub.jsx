import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { ChevronLeft, UtensilsCrossed, Table2, BarChart3, Users, Package, Percent, Utensils, ChevronRight, ChefHat, FileText } from 'lucide-react';

const SECCIONES = [
  {
    to: '/cocina',
    icon: ChefHat,
    title: 'Pantalla de cocina',
    description: 'Pedidos en vivo para cocina (tablet / TV)',
  },
  {
    to: '/comprobantes',
    icon: FileText,
    title: 'Comprobantes electrónicos',
    description: 'Boletas y facturas emitidas (modo demo)',
  },
  {
    to: '/admin/menu',
    icon: UtensilsCrossed,
    title: 'Carta y menú',
    description: 'Categorías y productos',
  },
  {
    to: '/admin/mesas',
    icon: Table2,
    title: 'Mesas',
    description: 'Crear, editar y desactivar mesas',
  },
  {
    to: '/admin/reportes',
    icon: BarChart3,
    title: 'Reportes de ventas',
    description: 'Resumen de ventas y productos más vendidos',
  },
  {
    to: '/admin/descuentos',
    icon: Percent,
    title: 'Descuentos',
    description: 'Catálogo de descuentos aplicables a un pedido',
  },
  {
    to: '/admin/cremas',
    icon: Utensils,
    title: 'Cremas',
    description: 'Chips de cremas/toppings disponibles al tomar un pedido',
  },
  {
    to: '/admin/usuarios',
    icon: Users,
    title: 'Usuarios y roles',
    description: 'Personal con acceso, roles y contraseñas',
  },
  {
    to: '/admin/inventario',
    icon: Package,
    title: 'Inventario',
    description: 'Insumos, stock y movimientos',
  },
];

export default function AdminHub() {
  const navigate = useNavigate();

  return (
    <div className="min-h-screen bg-background pb-10">
      <div className="px-5 pt-6 pb-4 flex items-center gap-3">
        <motion.button
          whileTap={{ scale: 0.9 }}
          onClick={() => navigate('/dashboard')}
          className="p-2 bg-card rounded-full hover:bg-cardHighlight border border-gray-800/50"
        >
          <ChevronLeft size={22} />
        </motion.button>
        <div>
          <h1 className="text-2xl font-extrabold tracking-tight">Panel de administración</h1>
          <p className="text-xs text-gray-500 mt-0.5">Gestiona la carta, las mesas y las ventas</p>
        </div>
      </div>

      <div className="px-5 space-y-3">
        {SECCIONES.map(({ to, icon: Icon, title, description }) => (
          <motion.button
            key={to}
            whileTap={{ scale: 0.98 }}
            onClick={() => navigate(to)}
            className="w-full bg-card border border-gray-800/50 rounded-2xl p-4 flex items-center gap-4 text-left hover:border-primary/40 transition-colors"
          >
            <div className="w-12 h-12 bg-primary/15 rounded-2xl flex items-center justify-center flex-shrink-0">
              <Icon size={22} className="text-primary" />
            </div>
            <div className="flex-1 min-w-0">
              <p className="font-bold text-white text-sm">{title}</p>
              <p className="text-xs text-gray-500 mt-0.5">{description}</p>
            </div>
            <ChevronRight size={18} className="text-gray-600 flex-shrink-0" />
          </motion.button>
        ))}
      </div>
    </div>
  );
}
