import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Ban, Printer } from 'lucide-react';
import toast from 'react-hot-toast';
import { anularPedido } from '../services/ordersService';
import { useAuth } from '../context/AuthContext';
import { etiquetaMesa } from '../utils/mesaLabel';
import { imprimirAnulacionEnCaja } from '../utils/comandaCaja';
import { IMPRIMEN_SU_COMANDA } from '../hooks/useEnviarPedido';

const MOTIVOS = ['El cliente se fue', 'Error al tomar el pedido', 'Mesa equivocada', 'Pedido duplicado'];

// "Avisar a cocina": se recuerda la última elección de cada usuario en este equipo, para no tener que
// desmarcarla en cada anulación. Si el navegador no permite guardar, vuelve a "marcada" (lo seguro).
const claveAvisar = (usuarioId) => `caraNegra_anularAvisarCocina_${usuarioId ?? 'anon'}`;
const leerAvisar = (usuarioId) => {
  try {
    return localStorage.getItem(claveAvisar(usuarioId)) !== 'no';
  } catch {
    return true;
  }
};
const guardarAvisar = (usuarioId, valor) => {
  try {
    localStorage.setItem(claveAvisar(usuarioId), valor ? 'si' : 'no');
  } catch {
    /* sin almacenamiento: no pasa nada */
  }
};

/**
 * Anula un pedido COMPLETO (Caja y cierre de jornada). Igual que quitar un plato: pide el motivo,
 * cada plato queda registrado a nombre de quien anula, a cocina le sale "PEDIDO ANULADO" (salvo que
 * se desmarque "Avisar a cocina") y la mesa queda libre. No se puede si el pedido tiene un pago
 * registrado (primero se anula el pago).
 *
 * @param {{ pedido: {id:number, salonNombre?:string, mesaNumero?:string, total?:number} | null,
 *           quien?: string, onAnulado: (pedido:object) => void, onCerrar: () => void }} props
 */
export default function AnularPedidoDialog({ pedido, quien, onAnulado, onCerrar }) {
  return (
    <AnimatePresence>
      {pedido && (
        <motion.div
          key="anular-pedido"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          className="fixed inset-0 z-[70] bg-black/80 flex items-end sm:items-center justify-center p-4"
          onClick={onCerrar}
        >
          <Formulario key={pedido.id} pedido={pedido} quien={quien} onAnulado={onAnulado} onCerrar={onCerrar} />
        </motion.div>
      )}
    </AnimatePresence>
  );
}

function Formulario({ pedido, quien, onAnulado, onCerrar }) {
  const [motivo, setMotivo] = useState('');
  const { user } = useAuth();
  const [avisarCocina, setAvisarCocina] = useState(() => leerAvisar(user?.usuarioId));
  const [enviando, setEnviando] = useState(false);
  const mesa = etiquetaMesa(pedido.salonNombre, pedido.mesaNumero);
  const imprimeAqui = IMPRIMEN_SU_COMANDA.includes(user?.rol);

  const confirmar = async () => {
    if (motivo.trim().length < 3) {
      toast.error('Indica el motivo (queda registrado)');
      return;
    }
    setEnviando(true);
    try {
      const anulado = await anularPedido(pedido.id, motivo.trim(), avisarCocina);
      guardarAvisar(user?.usuarioId, avisarCocina);
      // Administrador y cajero: el aviso sale en la ticketera de este equipo (no en la de cocina).
      if (avisarCocina && imprimeAqui) {
        imprimirAnulacionEnCaja({
          salonNombre: anulado.salonNombre ?? pedido.salonNombre,
          mesaNumero: anulado.mesaNumero ?? pedido.mesaNumero,
          anulo: user?.nombreCompleto,
          motivo: motivo.trim(),
          pedidoCompleto: true,
          items: (anulado.detalles ?? []).map((d) => ({ cantidad: d.cantidad, nombre: d.productoNombre })),
        }).catch((e) => console.error('No se pudo imprimir el aviso de anulación:', e));
      }
      toast.success(`Pedido #${pedido.id} anulado · ${mesa} quedó libre${avisarCocina ? '' : ' · sin aviso a cocina'}`);
      onAnulado(anulado);
    } catch (err) {
      toast.error(err.message ?? 'No se pudo anular el pedido');
    } finally {
      setEnviando(false);
    }
  };

  return (
    <motion.div
      initial={{ y: 30 }}
      animate={{ y: 0 }}
      onClick={(e) => e.stopPropagation()}
      className="w-full max-w-md bg-card border border-gray-700/50 rounded-3xl p-5 space-y-4"
      role="dialog"
      aria-label="Anular pedido completo"
    >
      <div>
        <p className="text-[11px] text-red-400 font-bold uppercase tracking-wider flex items-center gap-1.5">
          <Ban size={13} /> Anular pedido completo
        </p>
        <h3 className="text-lg font-extrabold text-ink leading-tight">
          {mesa} · Pedido #{pedido.id}
        </h3>
        <p className="text-xs text-gray-500 mt-0.5">
          Se anulan todos sus platos y la mesa queda libre. Queda registrado
          {quien ? <> a nombre de <span className="text-gray-300 font-semibold">{quien}</span></> : ''}.
        </p>
      </div>

      <div>
        <p className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Motivo</p>
        <div className="flex flex-wrap gap-2 mb-2">
          {MOTIVOS.map((m) => (
            <button
              key={m}
              type="button"
              onClick={() => setMotivo(m)}
              className={`px-3 py-1.5 rounded-full text-xs font-semibold border ${
                motivo === m ? 'bg-red-500/20 border-red-500/60 text-red-300' : 'bg-background border-gray-700/50 text-gray-400'
              }`}
            >
              {m}
            </button>
          ))}
        </div>
        <input
          value={motivo}
          onChange={(e) => setMotivo(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Enter') confirmar(); }}
          maxLength={200}
          placeholder="u otro motivo…"
          aria-label="Motivo de la anulación"
          className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3 text-sm text-ink placeholder-gray-600 focus:outline-none focus:border-red-500/60"
        />
      </div>

      <label className="flex items-start gap-3 bg-background border border-gray-700/50 rounded-2xl px-4 py-3 cursor-pointer select-none">
        <input
          type="checkbox"
          checked={avisarCocina}
          onChange={(e) => setAvisarCocina(e.target.checked)}
          className="mt-0.5 w-5 h-5 shrink-0 accent-red-500"
          aria-describedby="avisar-cocina-ayuda"
        />
        <span className="min-w-0">
          <span className="flex items-center gap-1.5 text-sm font-bold text-ink">
            <Printer size={14} /> {imprimeAqui ? 'Imprimir aquí el aviso “PEDIDO ANULADO” (para cocina)' : 'Avisar a cocina (imprimir “PEDIDO ANULADO”)'}
          </span>
          <span
            id="avisar-cocina-ayuda"
            className={`block text-xs mt-0.5 ${avisarCocina ? 'text-gray-500' : 'text-amber-400 font-semibold'}`}
          >
            {avisarCocina
              ? 'Déjalo marcado si la cocina ya recibió la comanda: así no prepara estos platos.'
              : 'No se imprimirá nada (se recuerda tu última elección). Márcalo si la cocina ya empezó estos platos.'}
          </span>
        </span>
      </label>

      <div className="flex gap-3">
        <button
          type="button"
          onClick={onCerrar}
          disabled={enviando}
          className="flex-1 py-3 rounded-2xl border border-gray-700 text-gray-300 font-bold text-sm"
        >
          Volver
        </button>
        <button
          type="button"
          onClick={confirmar}
          disabled={enviando || motivo.trim().length < 3}
          className="flex-1 py-3 rounded-2xl bg-red-600 hover:bg-red-500 text-white font-bold text-sm disabled:opacity-40"
        >
          {enviando ? 'Anulando…' : 'Anular pedido'}
        </button>
      </div>
    </motion.div>
  );
}
