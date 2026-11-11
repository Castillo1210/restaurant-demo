import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Minus, Plus, StickyNote } from 'lucide-react';

/** Máximo de la nota de un plato (igual que en el servidor). */
export const NOTA_PLATO_MAXIMO = 200;

// Indicaciones frecuentes: un toque las agrega a la nota.
const RAPIDAS = ['Sin cebolla', 'Sin ají', 'Sin sal', 'Poco ají', 'Para llevar', 'Aparte'];

/**
 * Nota de UN plato del carrito ("sin cebolla", "término medio"). Si la línea tiene varias
 * unidades, se elige a cuántas aplica: las demás quedan sin nota en otra línea.
 *
 * @param {{ item: {linea:string, name:string, quantity:number, nota?:string} | null,
 *           onGuardar: (nota:string, cantidad:number) => void, onCerrar: () => void,
 *           sugerencias?: string[], fijo?: boolean }} props
 *   fijo: true para pantallas completas (posición fixed); false dentro del panel de la carta.
 */
export default function NotaPlatoDialog({ item, onGuardar, onCerrar, sugerencias = [], fijo = true }) {
  return (
    <AnimatePresence>
      {item && (
        <motion.div
          key="nota-plato"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          className={`${fijo ? 'fixed' : 'absolute'} inset-0 z-50 bg-black/70 flex items-end sm:items-center justify-center p-4`}
          onClick={onCerrar}
        >
          <Formulario key={item.linea} item={item} onGuardar={onGuardar} onCerrar={onCerrar} sugerencias={sugerencias} />
        </motion.div>
      )}
    </AnimatePresence>
  );
}

function Formulario({ item, onGuardar, onCerrar, sugerencias }) {
  const [texto, setTexto] = useState(item.nota ?? '');
  const [cantidad, setCantidad] = useState(item.quantity);
  const rapidas = [...new Set([...RAPIDAS, ...sugerencias])];

  const agregarRapida = (r) =>
    setTexto((t) => {
      const actual = t.trim();
      if (actual.toLowerCase().includes(r.toLowerCase())) return t;
      return (actual ? `${actual}, ${r.toLowerCase()}` : r).slice(0, NOTA_PLATO_MAXIMO);
    });

  const guardar = () => onGuardar(texto.trim(), cantidad);

  return (
    <motion.form
      initial={{ y: 30 }}
      animate={{ y: 0 }}
      onClick={(e) => e.stopPropagation()}
      onSubmit={(e) => { e.preventDefault(); guardar(); }}
      className="w-full max-w-sm bg-card border border-gray-700/50 rounded-3xl p-5 space-y-4"
      role="dialog"
      aria-label="Nota del plato"
    >
      <div>
        <p className="text-[11px] text-primary font-bold uppercase tracking-wider flex items-center gap-1.5">
          <StickyNote size={13} /> Nota para cocina
        </p>
        <h3 className="text-lg font-extrabold text-ink leading-tight">{item.name}</h3>
        <p className="text-xs text-gray-500 mt-0.5">Sale debajo de este plato en la comanda.</p>
      </div>

      <textarea
        autoFocus
        rows={2}
        value={texto}
        maxLength={NOTA_PLATO_MAXIMO}
        onChange={(e) => setTexto(e.target.value)}
        onKeyDown={(e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); guardar(); } }}
        placeholder="Ej.: sin cebolla, término medio…"
        aria-label="Texto de la nota"
        className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3 text-sm text-ink placeholder-gray-600 focus:outline-none focus:border-primary resize-none"
      />

      <div className="flex flex-wrap gap-1.5">
        {rapidas.map((r) => (
          <button
            key={r}
            type="button"
            onClick={() => agregarRapida(r)}
            className="px-2.5 py-1 rounded-full text-xs font-semibold border border-gray-700/50 bg-background text-gray-400 hover:border-primary hover:text-primary"
          >
            {r}
          </button>
        ))}
      </div>

      {item.quantity > 1 && (
        <div className="flex items-center justify-between bg-background rounded-2xl px-3 py-2 border border-gray-700/40">
          <span className="text-sm text-gray-300">
            ¿A cuántos de los {item.quantity}?
          </span>
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => setCantidad((c) => Math.max(1, c - 1))}
              className="w-7 h-7 rounded-full bg-card border border-gray-700/50 flex items-center justify-center text-gray-300"
              aria-label="Menos unidades con nota"
            >
              <Minus size={13} />
            </button>
            <span className="w-6 text-center font-extrabold text-ink" aria-label="Unidades con nota">{cantidad}</span>
            <button
              type="button"
              onClick={() => setCantidad((c) => Math.min(item.quantity, c + 1))}
              className="w-7 h-7 rounded-full bg-primary flex items-center justify-center text-white"
              aria-label="Más unidades con nota"
            >
              <Plus size={13} />
            </button>
          </div>
        </div>
      )}

      <div className="flex gap-2">
        {item.nota ? (
          <button
            type="button"
            onClick={() => onGuardar('', item.quantity)}
            className="px-3 py-3 rounded-2xl border border-red-900/40 text-red-400 font-bold text-sm"
          >
            Quitar nota
          </button>
        ) : (
          <button type="button" onClick={onCerrar} className="flex-1 py-3 rounded-2xl border border-gray-700 text-gray-300 font-bold text-sm">
            Cancelar
          </button>
        )}
        <button type="submit" className="flex-1 py-3 rounded-2xl bg-primary hover:bg-primaryHover text-white font-bold text-sm">
          Guardar nota
        </button>
      </div>
    </motion.form>
  );
}
