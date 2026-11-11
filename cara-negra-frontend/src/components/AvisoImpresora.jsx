import { useEffect } from 'react';
import toast from 'react-hot-toast';
import { Printer, X } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import { connectPedidosHub, onHubEvent } from '../services/signalrService';
import { reimprimirComanda } from '../services/ordersService';
import { etiquetaMesa } from '../utils/mesaLabel';

/**
 * Aviso en cualquier pantalla cuando una comanda NO salió en la impresora de cocina (apagada,
 * sin conexión…). La cocina trabaja con los tickets: si no sale, el pedido se pierde sin que
 * nadie lo note. En caja/administración el aviso queda hasta que alguien lo cierra o reimprime;
 * en el celular del mozo dura un minuto para no taparle la pantalla.
 */
export default function AvisoImpresora() {
  const { user } = useAuth();
  const puedeVer = ['MOZO', 'CAJERO', 'ADMIN'].includes(user?.rol);
  const esMozo = user?.rol === 'MOZO';

  useEffect(() => {
    if (!puedeVer) return undefined;
    connectPedidosHub();
    return onHubEvent('ImpresionFallida', (e) => {
      const mesa = etiquetaMesa(e?.salonNombre, e?.mesaNumero);
      const id = `impresion-${e?.pedidoId}-${e?.tipo}`;
      toast.custom(
        (t) => (
          <div
            role="alert"
            className="max-w-sm w-full flex items-start gap-3 rounded-2xl border border-red-500/60 bg-card px-4 py-3 shadow-xl"
          >
            <Printer size={20} className="shrink-0 text-red-400 mt-0.5" />
            <div className="flex-1 min-w-0 text-sm">
              <p className="font-extrabold text-red-400">No salió en cocina</p>
              <p className="text-ink">
                {e?.tipo} de {mesa} (pedido #{e?.pedidoId}). Revisa la impresora y avisa a cocina.
              </p>
              {e?.platos && <p className="text-gray-400 truncate">{e.platos}</p>}
              {e?.puedeReimprimir && (
                <button
                  type="button"
                  className="mt-2 font-bold text-primary"
                  onClick={async () => {
                    try {
                      await reimprimirComanda(e.pedidoId);
                      toast.dismiss(t.id);
                      toast.success(`Comanda de ${mesa} reimpresa`);
                    } catch (err) {
                      toast.error(err.message);
                    }
                  }}
                >
                  Reimprimir
                </button>
              )}
            </div>
            <button type="button" onClick={() => toast.dismiss(t.id)} className="p-1 text-gray-500" aria-label="Cerrar aviso">
              <X size={16} />
            </button>
          </div>
        ),
        { id, duration: esMozo ? 60000 : Infinity },
      );
    });
  }, [puedeVer, esMozo]);

  return null;
}
