import { Pencil } from 'lucide-react';
import { horaLima } from '../utils/formato';

const S = (n) => `S/ ${Number(n ?? 0).toFixed(2)}`;

/** Precios distintos a la carta (solo cajero / admin): quién, cuándo, de cuánto a cuánto. */
export default function ListaCambiosPrecio({ cambios, mostrarMesa = false }) {
  if (!cambios?.length) return null;
  return (
    <div className="space-y-1">
      {cambios.map((c) => (
        <div key={c.cambioPrecioPedidoId} className="flex gap-2.5 bg-background/60 border border-amber-900/30 rounded-xl px-2.5 py-1.5">
          <Pencil size={13} className="text-amber-400 flex-shrink-0 mt-0.5" />
          <div className="flex-1 min-w-0">
            <div className="flex justify-between gap-2">
              <p className="text-sm text-gray-300 truncate">
                {c.cantidad}x {c.productoNombre}
                {mostrarMesa && c.mesa ? <span className="text-gray-500"> · {c.mesa}</span> : null}
              </p>
              <span className="text-xs font-bold text-amber-400 flex-shrink-0">
                <span className="line-through text-gray-500 font-normal">{S(c.precioAnterior)}</span> → {S(c.precioNuevo)}
              </span>
            </div>
            <p className="text-[11px] text-gray-500">
              {horaLima(c.cambiadoEn)} · <span className="text-gray-300 font-semibold">{c.usuarioNombre}</span>
              {c.usuarioRol ? ` (${c.usuarioRol})` : ''}{c.motivo ? ` · “${c.motivo}”` : ''}
            </p>
          </div>
        </div>
      ))}
    </div>
  );
}
