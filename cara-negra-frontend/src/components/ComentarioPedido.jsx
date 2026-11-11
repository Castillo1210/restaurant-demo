import { useEffect, useState } from 'react';
import { MessageSquareText, Pencil, Check, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { actualizarComentarioPedido, COMENTARIO_MAXIMO } from '../services/ordersService';
import useAccionUnica from '../hooks/useAccionUnica';

/**
 * Comentario general de un pedido ("todo para llevar", "servir juntos"): uno por pedido,
 * distinto de las notas de cada plato. Se muestra y, si `editable`, se puede cambiar o borrar;
 * al guardar un comentario cocina recibe un ticket corto con él.
 *
 * @param {{ pedido: { id:number, comentario?:string }, editable?: boolean,
 *           onGuardado?: (comentario:string) => void, compacto?: boolean }} props
 */
export default function ComentarioPedido({ pedido, editable = false, onGuardado, compacto = false }) {
  const actual = pedido?.comentario ?? '';
  const [editando, setEditando] = useState(false);
  const [texto, setTexto] = useState(actual);
  const [guardando, setGuardando] = useState(false);
  const unaVez = useAccionUnica();

  // Otro equipo pudo cambiarlo (la pantalla se refresca en vivo): si no se está editando, se sigue.
  useEffect(() => {
    if (!editando) setTexto(actual);
  }, [actual, editando]);

  if (!pedido) return null;
  if (!actual && !editable) return null;

  const guardar = () => unaVez(async () => {
    const nuevo = texto.trim();
    if (nuevo === actual.trim()) {
      setEditando(false);
      return;
    }
    setGuardando(true);
    try {
      const actualizado = await actualizarComentarioPedido(pedido.id, nuevo);
      toast.success(nuevo ? 'Comentario guardado · cocina recibe un aviso' : 'Comentario borrado');
      setEditando(false);
      onGuardado?.(actualizado.comentario ?? '');
    } catch (err) {
      toast.error(err.message);
    } finally {
      setGuardando(false);
    }
  });

  if (editando) {
    return (
      <div className={`rounded-xl border border-primary/50 bg-card ${compacto ? 'p-2' : 'p-3'}`}>
        <label className="text-[11px] font-bold uppercase tracking-wider text-gray-400 flex items-center gap-1.5 mb-1.5">
          <MessageSquareText size={13} /> Comentario del pedido
        </label>
        <textarea
          autoFocus
          rows={2}
          value={texto}
          maxLength={COMENTARIO_MAXIMO}
          onChange={(e) => setTexto(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); guardar(); }
            if (e.key === 'Escape') { setTexto(actual); setEditando(false); }
          }}
          placeholder="Ej.: todo para llevar, servir juntos…"
          aria-label="Comentario del pedido"
          className="w-full bg-background rounded-lg px-2.5 py-2 text-sm text-ink border border-gray-700/40 focus:outline-none focus:border-primary resize-none"
        />
        <div className="flex items-center justify-between mt-1.5 gap-2">
          <span className="text-[11px] text-gray-500">{texto.length}/{COMENTARIO_MAXIMO}</span>
          <div className="flex gap-1.5">
            <button
              type="button"
              onClick={() => { setTexto(actual); setEditando(false); }}
              disabled={guardando}
              className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-xs font-bold text-gray-400 bg-background hover:text-ink"
            >
              <X size={13} /> Cancelar
            </button>
            <button
              type="button"
              onClick={guardar}
              disabled={guardando}
              className="flex items-center gap-1 px-2.5 py-1.5 rounded-lg text-xs font-bold text-white bg-primary hover:bg-primaryHover disabled:opacity-60"
            >
              <Check size={13} /> {guardando ? 'Guardando…' : 'Guardar'}
            </button>
          </div>
        </div>
      </div>
    );
  }

  if (!actual) {
    return (
      <button
        type="button"
        onClick={() => setEditando(true)}
        className="flex items-center gap-1.5 text-xs font-bold text-gray-500 hover:text-primary transition-colors"
      >
        <MessageSquareText size={14} /> Agregar comentario al pedido
      </button>
    );
  }

  return (
    <div className={`flex items-start gap-2 rounded-xl border border-amber-700/50 bg-amber-950/40 ${compacto ? 'px-2.5 py-1.5' : 'px-3 py-2'}`}>
      <MessageSquareText size={15} className="text-amber-400 mt-0.5 flex-shrink-0" />
      <p className="flex-1 min-w-0 text-sm font-semibold text-amber-200 break-words" title="Comentario del pedido">
        {actual}
      </p>
      {editable && (
        <button
          type="button"
          onClick={() => setEditando(true)}
          className="p-1 -m-0.5 text-amber-400/80 hover:text-amber-300 flex-shrink-0"
          title="Cambiar comentario"
          aria-label="Cambiar comentario del pedido"
        >
          <Pencil size={13} />
        </button>
      )}
    </div>
  );
}
