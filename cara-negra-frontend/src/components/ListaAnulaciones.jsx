import { Ban } from 'lucide-react';
import { horaLima } from '../utils/formato';

const ESTADO_ITEM = {
  Pendiente: { texto: 'aún no se preparaba', cls: 'text-gray-500' },
  EnPreparacion: { texto: 'estaba en preparación', cls: 'text-amber-400' },
  Listo: { texto: 'ya estaba preparado', cls: 'text-red-400' },
  Entregado: { texto: 'ya se había entregado', cls: 'text-red-400' },
};

/**
 * Productos quitados de un pedido / de la jornada: quién, cuándo, cuánto y por qué.
 * Es la "marca" que deja un mozo (o cajero / admin) al quitar algo ya enviado a cocina.
 */
export default function ListaAnulaciones({ anulaciones, mostrarMesa = false, mostrarMontos = true, compacta = false }) {
  if (!anulaciones?.length) return null;
  return (
    <div className={compacta ? 'space-y-1' : 'space-y-1.5'}>
      {anulaciones.map((a) => {
        const estado = ESTADO_ITEM[a.estadoItem] ?? { texto: a.estadoItem, cls: 'text-gray-500' };
        return (
          <div
            key={a.productoAnuladoId}
            className={`flex gap-2.5 bg-background/60 border border-red-900/30 rounded-xl ${compacta ? 'px-2.5 py-1.5' : 'px-3 py-2'}`}
          >
            <Ban size={14} className="text-red-400 flex-shrink-0 mt-0.5" />
            <div className="flex-1 min-w-0">
              <div className="flex justify-between gap-2">
                <p className="text-sm text-gray-300 line-through decoration-red-500/70 truncate">
                  {a.cantidad}x {a.productoNombre}
                  {mostrarMesa && a.mesa ? <span className="no-underline text-gray-500"> · {a.mesa}</span> : null}
                </p>
                {mostrarMontos && Number(a.importe) > 0 && (
                  <span className="text-xs font-bold text-red-400 flex-shrink-0">− S/ {Number(a.importe).toFixed(2)}</span>
                )}
              </div>
              <p className="text-[11px] text-gray-500">
                {horaLima(a.anuladoEn)} · <span className="text-gray-300 font-semibold">{a.usuarioNombre}</span>
                {a.usuarioRol ? ` (${a.usuarioRol})` : ''} · <span className={estado.cls}>{estado.texto}</span>
              </p>
              <p className="text-[11px] text-gray-400 italic break-words">“{a.motivo}”</p>
            </div>
          </div>
        );
      })}
    </div>
  );
}
