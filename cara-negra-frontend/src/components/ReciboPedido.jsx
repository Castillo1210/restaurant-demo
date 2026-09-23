import { motion } from 'framer-motion';
import { Printer, X, CheckCircle2 } from 'lucide-react';
import { agruparDetallesPorProducto } from '../utils/agruparDetalles';

/**
 * Comprobante interno simple (no es un comprobante SUNAT/electrónico —
 * el usuario confirmó que por ahora basta con un recibo interno para
 * control de caja). Pensado para imprimirse desde el navegador
 * (window.print()) en una impresora térmica o de hoja normal.
 */
export default function ReciboPedido({ pedido, pagos, onClose }) {
  if (!pedido) return null;

  const fecha = new Date().toLocaleString('es-PE', {
    dateStyle: 'short',
    timeStyle: 'short',
  });

  const pagosActivos = (pagos ?? []).filter((p) => !p.anulado);
  const totalPagado = pagosActivos.reduce((acc, p) => acc + p.monto, 0);
  // Si el mismo producto se pidió en más de un momento (ítem adicional agregado luego),
  // se consolida en una sola línea con la cantidad sumada en vez de mostrarlo repetido.
  const detallesAgrupados = agruparDetallesPorProducto(pedido.detalles);

  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      className="fixed inset-0 z-[60] bg-black/80 backdrop-blur-sm flex items-center justify-center p-4 print:bg-white print:p-0"
    >
      <motion.div
        initial={{ opacity: 0, scale: 0.94, y: 16 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        exit={{ opacity: 0, scale: 0.94 }}
        className="w-full max-w-sm bg-white text-black rounded-3xl overflow-hidden shadow-2xl print:rounded-none print:shadow-none print:max-w-full"
        id="recibo-imprimible"
      >
        {/* Encabezado de éxito (no se imprime) */}
        <div className="bg-emerald-50 px-6 pt-6 pb-4 flex flex-col items-center border-b border-dashed border-gray-300 print:hidden">
          <CheckCircle2 size={40} className="text-emerald-500 mb-2" />
          <p className="font-extrabold text-emerald-700">Pago registrado</p>
        </div>

        {/* Ticket */}
        <div className="px-6 py-5 font-mono text-sm">
          <div className="text-center mb-4">
            <p className="font-extrabold text-base tracking-wide">CARA NEGRA</p>
            <p className="text-xs text-gray-500">Comprobante interno de cobro</p>
            <p className="text-xs text-gray-500">(no válido como comprobante SUNAT)</p>
          </div>

          <div className="flex justify-between text-xs text-gray-600 mb-3">
            <span>Pedido #{pedido.id}</span>
            <span>{fecha}</span>
          </div>
          <div className="text-xs text-gray-600 mb-3">Mesa {pedido.mesaNumero}</div>

          <div className="border-t border-dashed border-gray-300 my-2" />

          <div className="space-y-1.5">
            {detallesAgrupados.map((d) => (
              <div key={d.id} className="flex justify-between gap-2">
                <span className="flex-1">
                  {d.cantidad}x {d.productoNombre}
                </span>
                <span>S/ {(d.monto * d.cantidad).toFixed(2)}</span>
              </div>
            ))}
          </div>

          <div className="border-t border-dashed border-gray-300 my-2" />

          {pedido.descuento && (
            <>
              <div className="flex justify-between text-xs text-gray-600">
                <span>Sub total</span>
                <span>S/ {pedido.subTotal.toFixed(2)}</span>
              </div>
              <div className="flex justify-between text-xs text-emerald-700">
                <span>
                  Descuento: {pedido.descuento.nombre}
                  {pedido.descuento.esPorcentaje ? ` (${pedido.descuento.valor}%)` : ''}
                </span>
                <span>- S/ {pedido.descuento.montoDescuento.toFixed(2)}</span>
              </div>
            </>
          )}

          <div className="flex justify-between font-bold text-base">
            <span>TOTAL</span>
            <span>S/ {pedido.total.toFixed(2)}</span>
          </div>

          <div className="border-t border-dashed border-gray-300 my-2" />

          <p className="text-xs font-bold text-gray-600 mb-1">Pagos</p>
          <div className="space-y-1">
            {pagosActivos.map((p) => (
              <div key={p.id} className="flex justify-between text-xs">
                <span>{p.metodoPagoNombre}{p.referencia ? ` (${p.referencia})` : ''}</span>
                <span>S/ {p.monto.toFixed(2)}</span>
              </div>
            ))}
          </div>
          <div className="flex justify-between text-xs font-bold mt-1">
            <span>Total pagado</span>
            <span>S/ {totalPagado.toFixed(2)}</span>
          </div>

          <div className="text-center text-xs text-gray-400 mt-5">
            ¡Gracias por su visita!
          </div>
        </div>

        {/* Acciones (no se imprimen) */}
        <div className="px-6 pb-6 pt-2 flex gap-3 print:hidden">
          <button
            onClick={onClose}
            className="flex-1 flex items-center justify-center gap-2 py-3 rounded-2xl border border-gray-300 text-gray-600 font-bold text-sm hover:bg-gray-50 transition-colors"
          >
            <X size={16} />
            Cerrar
          </button>
          <button
            onClick={() => window.print()}
            className="flex-1 flex items-center justify-center gap-2 py-3 rounded-2xl bg-primary text-white font-bold text-sm hover:bg-primaryHover transition-colors"
          >
            <Printer size={16} />
            Imprimir
          </button>
        </div>
      </motion.div>
    </motion.div>
  );
}
