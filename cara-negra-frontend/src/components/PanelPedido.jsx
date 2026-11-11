import { Minus, Plus, Trash2, Pencil, Send, ShoppingBag, StickyNote } from 'lucide-react';
import ListaAnulaciones from './ListaAnulaciones';
import ComentarioPedido from './ComentarioPedido';
import FilaEnviado from './FilaEnviado';

const S = (n) => `S/ ${Number(n ?? 0).toFixed(2)}`;

/**
 * Listado del pedido al costado de la carta (pantallas grandes: el monitor de la caja).
 * Muestra lo ya enviado a cocina y lo que se está por enviar, con sus totales. Lo enviado se
 * maneja con FilaEnviado (− cantidad +, precio editable ahí mismo por caja/admin y el tacho);
 * quitar algo ya enviado pide motivo y queda registrado.
 */
export default function PanelPedido({
  mesaTexto, pedidoActual, cargando, cart, totalCarrito,
  onCantidad, onQuitarDelCarrito, onEditarPrecioCarrito, onNotaCarrito,
  puedeCambiarPrecio, puedeQuitar, puedeAgregar, removingId,
  onQuitarEnviado, onQuitarEnviadoConVentana, onMasEnviado, onGuardarPrecioEnviado,
  anulados, puedeComentar, onComentarioGuardado, onContinuar, onEnviar, enviando = false, lado = 'derecha',
}) {
  const enviados = pedidoActual?.detalles ?? [];
  const totalEnviado = pedidoActual?.total ?? 0;
  const unidadesCarrito = cart.reduce((a, i) => a + i.quantity, 0);

  return (
    <aside className={`w-[380px] xl:w-[420px] flex-shrink-0 ${lado === 'izquierda' ? 'border-r' : 'border-l'} border-gray-800/60 bg-background/40 flex flex-col min-h-0`}>
      <div className="px-4 pt-3 pb-2 border-b border-gray-800/60">
        <p className="text-[11px] text-gray-500 font-bold uppercase tracking-wider">Pedido de {mesaTexto}</p>
        <p className="text-xs text-gray-500 mt-0.5">
          {pedidoActual ? `#${pedidoActual.id} · atiende ${pedidoActual.usuarioNombre ?? '—'}` : 'Pedido nuevo'}
        </p>
        {/* Comentario general: ya no se ofrece "Agregar comentario"; solo se muestra si el pedido trae uno. */}
        {pedidoActual?.comentario && (
          <div className="mt-2">
            <ComentarioPedido pedido={pedidoActual} editable={puedeComentar} compacto onGuardado={onComentarioGuardado} />
          </div>
        )}
      </div>

      <div className="flex-1 overflow-y-auto px-4 py-3 space-y-4">
        {cargando && <p className="text-xs text-gray-600">Cargando pedido…</p>}

        {/* Ya enviado a cocina */}
        {enviados.length > 0 && (
          <section>
            <div className="flex justify-between items-baseline mb-1.5">
              <h3 className="text-[11px] font-bold uppercase tracking-wider text-gray-400">Ya enviado a cocina</h3>
              <span className="text-xs font-bold text-gray-300">{S(totalEnviado)}</span>
            </div>
            <div className="space-y-1">
              {enviados.map((d) => (
                <FilaEnviado
                  key={d.id}
                  d={d}
                  puedeQuitar={puedeQuitar(d)}
                  puedeAgregar={puedeAgregar}
                  puedeCambiarPrecio={puedeCambiarPrecio}
                  ocupado={removingId === d.id}
                  esUnicoPlato={enviados.length === 1}
                  onQuitar={onQuitarEnviado}
                  onQuitarConVentana={onQuitarEnviadoConVentana}
                  onMas={onMasEnviado}
                  onGuardarPrecio={onGuardarPrecioEnviado}
                />
              ))}
            </div>
            {anulados?.length > 0 && (
              <details className="mt-2">
                <summary className="text-[11px] text-red-400/90 font-bold cursor-pointer select-none">
                  Quitados de este pedido ({anulados.length})
                </summary>
                <div className="mt-1.5"><ListaAnulaciones anulaciones={anulados} compacta /></div>
              </details>
            )}
          </section>
        )}

        {/* Por enviar */}
        <section>
          <div className="flex justify-between items-baseline mb-1.5">
            <h3 className="text-[11px] font-bold uppercase tracking-wider text-primary">Por enviar</h3>
            {unidadesCarrito > 0 && <span className="text-xs font-bold text-primary">{S(totalCarrito)}</span>}
          </div>
          {cart.length === 0 ? (
            <div className="text-center py-6 border border-dashed border-gray-800 rounded-xl">
              <ShoppingBag size={22} className="mx-auto text-gray-700 mb-1.5" />
              <p className="text-xs text-gray-600">Toca los productos de la carta para agregarlos.</p>
            </div>
          ) : (
            <div className="space-y-1">
              {cart.map((i) => (
                // Dos filas: arriba el nombre completo (puede ocupar varias líneas) con su subtotal;
                // abajo la cantidad, el precio unitario y las acciones. Así el nombre usa todo el ancho.
                <div key={i.linea ?? i.id} className="bg-card border border-primary/30 rounded-xl px-2.5 py-1.5">
                  <div className="flex items-start gap-2">
                    <p className="flex-1 min-w-0 text-sm leading-5 text-ink break-words">{i.name}</p>
                    <span className="text-sm leading-5 font-bold text-ink whitespace-nowrap">{S(i.price * i.quantity)}</span>
                  </div>
                  {i.nota && <p className="text-[11px] font-bold text-amber-400 break-words">» {i.nota}</p>}
                  <div className="flex items-center gap-1 mt-1">
                    <button onClick={() => onCantidad(i.linea, -1)} className="w-6 h-6 rounded-full bg-background flex items-center justify-center text-gray-300" aria-label={`Menos ${i.name}`}>
                      <Minus size={12} />
                    </button>
                    <span className="w-6 text-center text-sm font-extrabold text-ink">{i.quantity}</span>
                    <button onClick={() => onCantidad(i.linea, 1)} className="w-6 h-6 rounded-full bg-primary flex items-center justify-center text-white" aria-label={`Más ${i.name}`}>
                      <Plus size={12} />
                    </button>
                    <p className={`flex-1 min-w-0 ml-1.5 text-[11px] truncate ${i.precioEditado ? 'text-amber-400 font-bold' : 'text-gray-500'}`}>
                      {S(i.price)} c/u{i.precioEditado ? ` (carta ${S(i.precioCarta)})` : ''}
                    </p>
                    <button
                      onClick={() => onNotaCarrito(i)}
                      className={`p-1 ${i.nota ? 'text-amber-400' : 'text-gray-500 hover:text-primary'}`}
                      title="Nota para cocina"
                      aria-label={`Nota de ${i.name}`}
                    >
                      <StickyNote size={13} />
                    </button>
                    {puedeCambiarPrecio && (
                      <button onClick={() => onEditarPrecioCarrito(i)} className="p-1 text-gray-500 hover:text-amber-400" title="Cambiar precio">
                        <Pencil size={13} />
                      </button>
                    )}
                    <button onClick={() => onQuitarDelCarrito(i.linea)} className="p-1 text-gray-500 hover:text-red-500" title="Quitar">
                      <Trash2 size={13} />
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </section>
      </div>

      <div className="px-4 py-3 border-t border-gray-800/60 space-y-2">
        <div className="flex justify-between text-sm">
          <span className="text-gray-400">Total de la mesa</span>
          <span className="font-extrabold text-ink">{S(totalEnviado + totalCarrito)}</span>
        </div>
        {/* Un solo clic: envía a cocina sin pasar por la pantalla de revisión */}
        <button
          onClick={onEnviar}
          disabled={cart.length === 0 || enviando}
          className="w-full bg-primary hover:bg-primaryHover disabled:opacity-40 text-white font-bold py-3.5 rounded-2xl flex items-center justify-center gap-2 shadow-glow-orange"
        >
          <Send size={16} />
          {enviando
            ? 'Enviando…'
            : cart.length === 0 ? 'Nada por enviar' : `Enviar a cocina (${unidadesCarrito}) · ${S(totalCarrito)}`}
        </button>
        {/* La pantalla de revisión sigue disponible (cremas, comentario del pedido) */}
        {cart.length > 0 && !enviando && (
          <button onClick={onContinuar} className="w-full text-center text-xs font-semibold text-gray-500 hover:text-primary">
            Revisar antes de enviar
          </button>
        )}
      </div>
    </aside>
  );
}
