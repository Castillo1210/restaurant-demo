import { useEffect, useRef, useState } from 'react';
import { Minus, Plus, Trash2, Pencil, Lock, Check, X, Loader2 } from 'lucide-react';

const S = (n) => `S/ ${Number(n ?? 0).toFixed(2)}`;

// Motivos de un toque para bajar la cantidad; "Otro motivo…" abre la ventana completa.
const MOTIVOS = ['Ya no lo quiere', 'Error al tomar', 'Se acabó'];

// Botón redondo fino para − / + (mismo tamaño en celular y monitor).
const BOTON_PASO =
  'w-7 h-7 rounded-full border border-gray-700/70 bg-card text-gray-300 flex items-center justify-center transition-colors disabled:opacity-30 disabled:pointer-events-none';

/**
 * Un producto YA ENVIADO a cocina, en dos líneas: arriba el nombre completo y el importe;
 * abajo − cantidad +, el precio unitario (editable ahí mismo por caja/admin) y el tacho.
 *
 *  · "−" baja la cantidad sin abrir ventana: debajo aparecen los motivos y un toque confirma.
 *    Varios "−" antes de confirmar se quitan juntos (un solo aviso ANULADO a cocina).
 *  · "+" agrega una unidad a "Por enviar": sale a cocina como ADICIONAL al enviar.
 *  · El tacho quita la línea completa (ventana con motivo, como antes).
 *
 * @param {{
 *   d: { id:number, productoId:number, productoNombre:string, cantidad:number, monto:number, notas?:string },
 *   puedeQuitar: boolean, puedeAgregar: boolean, puedeCambiarPrecio: boolean,
 *   ocupado: boolean, esUnicoPlato: boolean,
 *   onQuitar: (d, cantidad:number, motivo:string) => Promise<void>,
 *   onQuitarConVentana: (d, cantidad:number) => void,
 *   onMas: (d) => void,
 *   onGuardarPrecio: (d, precio:number) => Promise<void>,
 *   compacta?: boolean,
 * }} props
 */
export default function FilaEnviado({
  d, puedeQuitar, puedeAgregar, puedeCambiarPrecio, ocupado, esUnicoPlato,
  onQuitar, onQuitarConVentana, onMas, onGuardarPrecio, compacta = false,
}) {
  // Cantidad que quedaría mientras se elige el motivo (null = sin cambios pendientes).
  const [quedan, setQuedan] = useState(null);
  const [precio, setPrecio] = useState(null); // texto del precio mientras se edita
  const [guardando, setGuardando] = useState(false);
  const inputPrecio = useRef(null);

  // Si el pedido cambia desde otro equipo, se descarta lo que estaba a medio hacer.
  useEffect(() => { setQuedan(null); }, [d.cantidad]);
  useEffect(() => { if (precio !== null) inputPrecio.current?.select(); }, [precio !== null]); // eslint-disable-line react-hooks/exhaustive-deps

  const cantidad = quedan ?? d.cantidad;
  const aQuitar = d.cantidad - cantidad;

  const menos = () => setQuedan((q) => Math.max(0, (q ?? d.cantidad) - 1));
  const mas = () => {
    // Con una baja a medio confirmar, "+" deshace un paso; si no, agrega uno a "Por enviar".
    if (quedan !== null) {
      setQuedan(quedan + 1 >= d.cantidad ? null : quedan + 1);
      return;
    }
    onMas(d);
  };

  const confirmar = async (motivo) => {
    try {
      await onQuitar(d, aQuitar, motivo);
      setQuedan(null);
    } catch {
      // el aviso de error lo muestra quien quita; se deja la baja abierta para reintentar o cancelar
    }
  };

  const guardarPrecio = async () => {
    const valor = Math.round(Number(String(precio).replace(',', '.')) * 100) / 100;
    if (!Number.isFinite(valor) || valor < 0) return;
    if (valor === Number(d.monto)) { setPrecio(null); return; }
    setGuardando(true);
    try {
      await onGuardarPrecio(d, valor);
      setPrecio(null);
    } catch {
      // el aviso de error lo muestra quien guarda; se deja la casilla abierta para corregir
    } finally {
      setGuardando(false);
    }
  };

  return (
    <div className={`bg-card/60 border rounded-xl px-2.5 ${compacta ? 'py-1.5' : 'py-2'} ${
      quedan !== null ? 'border-red-500/50' : precio !== null ? 'border-amber-400/60' : 'border-gray-800/60'}`}
    >
      <div className="flex items-start gap-2">
        <div className="flex-1 min-w-0">
          <p className="text-sm leading-5 text-gray-200 break-words">{d.productoNombre}</p>
          {d.notas && <p className="text-[11px] font-bold text-amber-400 break-words">» {d.notas}</p>}
        </div>
        <span className="text-sm leading-5 font-bold text-gray-200 whitespace-nowrap">{S(d.monto * cantidad)}</span>
      </div>

      <div className="flex items-center gap-2 mt-1">
        {/* − cantidad + */}
        <div className="flex items-center gap-1">
          <button
            type="button"
            onClick={menos}
            disabled={!puedeQuitar || ocupado || cantidad === 0}
            className={`${BOTON_PASO} hover:border-red-400/70 hover:text-red-400`}
            aria-label={`Bajar cantidad de ${d.productoNombre}`}
            title={puedeQuitar ? 'Quitar uno' : undefined}
          >
            <Minus size={13} strokeWidth={2.25} />
          </button>
          <span className="min-w-[2.25rem] text-center text-sm font-extrabold text-ink tabular-nums">
            {quedan !== null && <span className="text-gray-500 font-semibold line-through mr-0.5">{d.cantidad}</span>}
            {quedan !== null && <span className="text-gray-500 mr-0.5">→</span>}
            {cantidad}
          </span>
          <button
            type="button"
            onClick={mas}
            disabled={(!puedeAgregar && quedan === null) || ocupado}
            className={`${BOTON_PASO} hover:border-primary/70 hover:text-primary`}
            aria-label={quedan !== null ? `Deshacer, quitar uno menos de ${d.productoNombre}` : `Agregar uno de ${d.productoNombre} a por enviar`}
            title={quedan !== null ? 'Uno menos para quitar' : 'Agregar uno (sale como adicional al enviar)'}
          >
            <Plus size={13} strokeWidth={2.25} />
          </button>
        </div>

        {/* Precio unitario: caja/admin lo cambia ahí mismo */}
        {precio !== null ? (
          <form
            className="flex items-center gap-1"
            onSubmit={(e) => { e.preventDefault(); guardarPrecio(); }}
          >
            <span className="text-[11px] text-gray-500">S/</span>
            <input
              ref={inputPrecio}
              type="number"
              inputMode="decimal"
              step="0.10"
              min="0"
              autoFocus
              value={precio}
              onChange={(e) => setPrecio(e.target.value)}
              onKeyDown={(e) => { if (e.key === 'Escape') setPrecio(null); }}
              aria-label={`Nuevo precio de ${d.productoNombre}`}
              className="w-[4.5rem] bg-background border border-amber-400/70 rounded-lg px-1.5 py-0.5 text-sm font-bold text-ink focus:outline-none [appearance:textfield] [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none"
            />
            <button type="submit" disabled={guardando} className="w-6 h-6 rounded-md bg-amber-500 text-black flex items-center justify-center" aria-label="Guardar precio">
              {guardando ? <Loader2 size={12} className="animate-spin" /> : <Check size={13} strokeWidth={2.5} />}
            </button>
            <button type="button" onClick={() => setPrecio(null)} disabled={guardando} className="w-6 h-6 rounded-md bg-background text-gray-400 flex items-center justify-center" aria-label="Cancelar cambio de precio">
              <X size={13} strokeWidth={2.5} />
            </button>
          </form>
        ) : puedeCambiarPrecio ? (
          <button
            type="button"
            onClick={() => { setQuedan(null); setPrecio(Number(d.monto).toFixed(2)); }}
            disabled={ocupado}
            className="flex items-center gap-1 px-1.5 py-0.5 rounded-md text-[11px] text-gray-500 hover:text-amber-400 hover:bg-amber-400/10"
            title="Cambiar precio"
            aria-label={`Cambiar precio de ${d.productoNombre} (${S(d.monto)} c/u)`}
          >
            {S(d.monto)} c/u <Pencil size={11} className="opacity-60" />
          </button>
        ) : (
          <span className="text-[11px] text-gray-500">{S(d.monto)} c/u</span>
        )}

        <span className="flex-1" />
        {puedeQuitar ? (
          <button
            type="button"
            onClick={() => { setQuedan(null); onQuitarConVentana(d, d.cantidad); }}
            disabled={ocupado}
            className="p-1 text-gray-500 hover:text-red-500 disabled:opacity-40"
            title="Quitar del pedido"
          >
            <Trash2 size={14} />
          </button>
        ) : (
          <span title="Solo el mozo de la mesa (si el pedido no está listo), el cajero o el admin"><Lock size={12} className="text-gray-700" /></span>
        )}
      </div>

      {/* Confirmación de la baja: un toque en el motivo */}
      {quedan !== null && (
        <div className="mt-2 pt-2 border-t border-dashed border-gray-700/60">
          <p className="text-[11px] text-gray-400 mb-1.5">
            {cantidad === 0 && esUnicoPlato
              ? <span className="text-red-400 font-bold">Es el último plato: se anulará el pedido y la mesa quedará libre. </span>
              : <>Quitar <b className="text-red-400">{aQuitar}</b> · </>}
            elige el motivo (a cocina le sale ANULADO):
          </p>
          <div className="flex flex-wrap gap-1.5">
            {MOTIVOS.map((m) => (
              <button
                key={m}
                type="button"
                disabled={ocupado}
                onClick={() => confirmar(m)}
                className="px-2.5 py-1 rounded-full text-[11px] font-semibold border border-gray-700/70 text-gray-300 hover:border-red-500/60 hover:text-red-300 disabled:opacity-50"
              >
                {m}
              </button>
            ))}
            <button
              type="button"
              disabled={ocupado}
              onClick={() => { const n = aQuitar; setQuedan(null); onQuitarConVentana(d, n); }}
              className="px-2.5 py-1 rounded-full text-[11px] font-semibold border border-dashed border-gray-700/70 text-gray-400"
            >
              Otro motivo…
            </button>
            <button type="button" onClick={() => setQuedan(null)} disabled={ocupado} className="px-2 py-1 text-[11px] font-bold text-gray-500">
              {ocupado ? 'Quitando…' : 'Cancelar'}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
