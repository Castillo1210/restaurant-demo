import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { ChevronLeft, UtensilsCrossed, Table2, BarChart3, Users, Package, Percent, Utensils, ChevronRight, ChefHat, FileText, Trees, Building2, Smartphone, CalendarClock, ListOrdered } from 'lucide-react';
import { getAmbienteFacturacion } from '../services/comprobantesService';

// Texto de la tarjeta de comprobantes según dónde se emiten de verdad (lo dice el servidor).
const DESCRIPCION_COMPROBANTES = {
  SIMULADO: 'Boletas y facturas emitidas (modo demo: no se envían a SUNAT)',
  SANDBOX: 'Boletas y facturas emitidas (pruebas de APISUNAT: sin validez tributaria)',
  PRODUCCION: 'Boletas y facturas emitidas y su estado en SUNAT',
};

const SECCIONES = [
  {
    to: '/jornada',
    icon: CalendarClock,
    title: 'Jornada y cierre de caja',
    description: 'Abrir / cerrar el día, movimientos de caja e historial de cierres',
  },
  {
    to: '/pedidos-dia',
    icon: ListOrdered,
    title: 'Pedidos del día',
    description: 'Todos los pedidos de la jornada: abiertos, cobrados y anulados',
  },
  {
    to: '/admin/empresa',
    icon: Building2,
    title: 'Datos de la empresa',
    description: 'RUC, razón social, logo, series de comprobantes e IGV',
  },
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
    description: 'Boletas y facturas emitidas',
  },
  {
    to: '/admin/menu',
    icon: UtensilsCrossed,
    title: 'Carta y menú',
    description: 'Categorías y productos',
  },
  {
    to: '/admin/salones',
    icon: Trees,
    title: 'Salones y áreas',
    description: 'Terraza, jardín, salón principal… (agrupan las mesas)',
  },
  {
    to: '/admin/mesas',
    icon: Table2,
    title: 'Mesas',
    description: 'Mesas de cada salón: crear, editar y desactivar',
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
    to: '/admin/dispositivos',
    icon: Smartphone,
    title: 'Dispositivos con PIN',
    description: 'Celulares y tablets donde mozos y cocina entran con PIN de 4 dígitos',
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
  const [ambiente, setAmbiente] = useState(null);

  useEffect(() => {
    let vivo = true;
    // Si no responde, la tarjeta queda con el texto neutro ("Boletas y facturas emitidas").
    getAmbienteFacturacion().then((a) => vivo && setAmbiente(a?.ambiente ?? null)).catch(() => {});
    return () => { vivo = false; };
  }, []);

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
        {SECCIONES.map(({ to, icon: Icon, title, description: descripcionFija }) => {
          const description = to === '/comprobantes' ? (DESCRIPCION_COMPROBANTES[ambiente] ?? descripcionFija) : descripcionFija;
          return (
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
                <p className="font-bold text-ink text-sm">{title}</p>
                <p className="text-xs text-gray-500 mt-0.5">{description}</p>
              </div>
              <ChevronRight size={18} className="text-gray-600 flex-shrink-0" />
            </motion.button>
          );
        })}
      </div>
    </div>
  );
}
