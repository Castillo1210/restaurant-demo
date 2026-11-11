import { motion } from 'framer-motion';
import { Printer, X } from 'lucide-react';
import { useEmpresa } from '../context/EmpresaContext';
import { soles, fechaOperativaTexto, fechaHoraLima, horaLima } from '../utils/formato';
import { imprimirTicket } from '../utils/imprimir';

const Linea = ({ izq, der, fuerte = false }) => (
  <div className={`flex justify-between gap-2 ${fuerte ? 'font-extrabold' : ''}`}>
    <span className="min-w-0 break-words">{izq}</span>
    <span className="flex-shrink-0 text-right">{der}</span>
  </div>
);

const Seccion = ({ titulo, children }) => (
  <>
    <div className="border-t border-dashed border-gray-400 my-2" />
    <p className="font-bold text-[11px] mb-1">{titulo}</p>
    {children}
  </>
);

const difTexto = (d) => {
  const n = Number(d ?? 0);
  if (Math.abs(n) < 0.005) return '0.00';
  return n > 0 ? `+${n.toFixed(2)}` : n.toFixed(2);
};

/**
 * Reporte de cierre de caja (formato ticket 80 mm, se imprime con utils/imprimir.imprimirTicket).
 * Recibe el ResumenJornadaDto de una jornada cerrada.
 */
export default function ReporteCierre({ resumen, onClose }) {
  const { nombreComercial } = useEmpresa();
  if (!resumen) return null;
  const r = resumen;
  const j = r.jornada;
  const diferencia = Number(r.diferenciaTotal ?? 0);
  // Desde que se quitó el arqueo, la jornada se cierra con lo registrado (contado = esperado).
  // Los cierres antiguos con diferencias siguen mostrando su arqueo.
  const conArqueo = (r.arqueo ?? []).some((a) => Math.abs(Number(a.diferencia ?? 0)) >= 0.005);

  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      className="fixed inset-0 z-[80] bg-black/80 backdrop-blur-sm flex items-center justify-center p-4 print:bg-white print:p-0"
    >
      <motion.div
        initial={{ opacity: 0, scale: 0.95, y: 16 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        className="paleta-normal w-full max-w-sm max-h-[94dvh] flex flex-col bg-white text-black rounded-3xl overflow-hidden shadow-2xl print:rounded-none print:shadow-none print:max-h-none"
      >
        <div className={`px-6 py-4 text-center print:hidden ${!conArqueo ? 'bg-emerald-50 text-emerald-700' : diferencia < 0 ? 'bg-red-50 text-red-700' : 'bg-amber-50 text-amber-700'}`}>
          <p className="font-extrabold">Jornada {fechaOperativaTexto(j.fechaOperativa)} cerrada</p>
          <p className="text-xs mt-0.5">
            {!conArqueo
              ? 'Cerrada con lo registrado en el sistema.'
              : diferencia < 0
                ? `Faltante de ${soles(-diferencia)}`
                : `Sobrante de ${soles(diferencia)}`}
          </p>
        </div>

        <div className="overflow-y-auto print:overflow-visible">
          <div id="recibo-imprimible" className="ticket px-5 py-4 font-mono text-[12px] leading-snug bg-white text-black">
            <div className="text-center">
              <p className="font-extrabold text-[14px]">{nombreComercial || 'Restaurante'}</p>
              <p className="font-bold">REPORTE DE CIERRE DE CAJA</p>
              <p>Jornada: {fechaOperativaTexto(j.fechaOperativa, true)}</p>
            </div>

            <Seccion titulo="JORNADA">
              <Linea izq="Apertura" der={fechaHoraLima(j.abiertaEn)} />
              <Linea izq="  por" der={j.abiertaPor} />
              <Linea izq="Cierre" der={fechaHoraLima(j.cerradaEn)} />
              <Linea izq="  por" der={j.cerradaPor} />
              <Linea izq="Fondo inicial" der={soles(j.fondoInicial)} />
            </Seccion>

            <Seccion titulo="VENTAS">
              <Linea izq="Total cobrado" der={soles(r.totalVentas)} fuerte />
              <Linea izq="Pedidos cobrados" der={r.cantidadPedidos} />
              <Linea izq="Ticket promedio" der={soles(r.ticketPromedio)} />
              {r.totalDescuentos > 0 && <Linea izq="Descuentos" der={`- ${soles(r.totalDescuentos)}`} />}
              {r.pedidosCancelados > 0 && <Linea izq="Pedidos anulados" der={r.pedidosCancelados} />}
              {r.pagosAnulados > 0 && <Linea izq={`Pagos anulados (${r.pagosAnulados})`} der={soles(r.montoPagosAnulados)} />}
              {r.productosAnulados > 0 && <Linea izq={`Productos quitados (${r.productosAnulados})`} der={soles(r.montoProductosAnulados)} />}
            </Seccion>

            {r.anulaciones?.length > 0 && (
              <Seccion titulo="PRODUCTOS QUITADOS DE PEDIDOS">
                {r.anulaciones.map((a) => (
                  <div key={a.productoAnuladoId} className="mb-1">
                    <Linea izq={`${horaLima(a.anuladoEn)} ${a.cantidad}x ${a.productoNombre}`} der={soles(a.importe)} />
                    <p className="text-[11px] pl-2">{a.mesa} · {a.usuarioNombre} · {a.motivo}{a.estadoItem === 'Listo' ? ' (ya preparado)' : ''}</p>
                  </div>
                ))}
              </Seccion>
            )}

            {r.cambiosPrecio?.length > 0 && (
              <Seccion titulo="PRECIOS CAMBIADOS (vs. carta)">
                {r.cambiosPrecio.map((c) => (
                  <div key={c.cambioPrecioPedidoId} className="mb-1">
                    <Linea izq={`${horaLima(c.cambiadoEn)} ${c.cantidad}x ${c.productoNombre}`} der={`${Number(c.precioAnterior).toFixed(2)} > ${Number(c.precioNuevo).toFixed(2)}`} />
                    <p className="text-[11px] pl-2">{c.mesa} · {c.usuarioNombre}{c.motivo ? ` · ${c.motivo}` : ''}</p>
                  </div>
                ))}
              </Seccion>
            )}

            {!conArqueo ? (
              <Seccion titulo="CAJA SEGÚN EL SISTEMA">
                <div className="grid grid-cols-[1fr_auto_auto] gap-x-3 text-[11px] font-bold">
                  <span>Medio</span><span className="text-right">Cobrado</span><span className="text-right">En caja</span>
                </div>
                {r.arqueo.map((a) => (
                  <div key={`${a.metodoPagoId}-${a.metodoPago}`} className="grid grid-cols-[1fr_auto_auto] gap-x-3 text-[11px]">
                    <span className="min-w-0 truncate">{a.metodoPago}</span>
                    <span className="text-right">{Number(a.ventas).toFixed(2)}</span>
                    <span className="text-right font-bold">{Number(a.esperado).toFixed(2)}</span>
                  </div>
                ))}
                {r.arqueo.some((a) => a.esEfectivo) && (
                  <p className="text-[10px] text-gray-600 mt-1">
                    Efectivo en caja = fondo {Number(j.fondoInicial).toFixed(2)} + cobros en efectivo
                    {r.totalIngresos > 0 ? ` + ingresos ${Number(r.totalIngresos).toFixed(2)}` : ''}
                    {r.totalEgresos > 0 ? ` − egresos ${Number(r.totalEgresos).toFixed(2)}` : ''}
                  </p>
                )}
                <p className="text-[10px] text-gray-600 mt-0.5">Montos registrados en el sistema (sin conteo de caja).</p>
              </Seccion>
            ) : (
            <Seccion titulo="ARQUEO (contado − esperado)">
              <div className="grid grid-cols-[1fr_auto_auto_auto] gap-x-2 text-[11px] font-bold">
                <span>Medio</span><span className="text-right">Esper.</span><span className="text-right">Cont.</span><span className="text-right">Dif.</span>
              </div>
              {r.arqueo.map((a) => (
                <div key={`${a.metodoPagoId}-${a.metodoPago}`} className="grid grid-cols-[1fr_auto_auto_auto] gap-x-2 text-[11px]">
                  <span className="min-w-0 truncate">{a.metodoPago}</span>
                  <span className="text-right">{Number(a.esperado).toFixed(2)}</span>
                  <span className="text-right">{Number(a.contado ?? 0).toFixed(2)}</span>
                  <span className={`text-right ${Math.abs(a.diferencia ?? 0) >= 0.005 ? 'font-extrabold' : ''}`}>{difTexto(a.diferencia)}</span>
                </div>
              ))}
              <div className="mt-1">
                <Linea izq={diferencia < 0 ? 'FALTANTE' : diferencia > 0 ? 'SOBRANTE' : 'DIFERENCIA'} der={soles(Math.abs(diferencia))} fuerte />
              </div>
              {r.arqueo.some((a) => a.esEfectivo) && (
                <p className="text-[10px] text-gray-600 mt-1">
                  Efectivo esperado = fondo {Number(j.fondoInicial).toFixed(2)} + cobros en efectivo
                  {r.totalIngresos > 0 ? ` + ingresos ${Number(r.totalIngresos).toFixed(2)}` : ''}
                  {r.totalEgresos > 0 ? ` − egresos ${Number(r.totalEgresos).toFixed(2)}` : ''}
                </p>
              )}
            </Seccion>
            )}

            {r.porMetodo.length > 0 && (
              <Seccion titulo="COBROS POR MEDIO DE PAGO">
                {r.porMetodo.map((l) => <Linea key={l.nombre} izq={`${l.nombre} (${l.cantidad})`} der={soles(l.total)} />)}
              </Seccion>
            )}

            {r.movimientos.length > 0 && (
              <Seccion titulo="MOVIMIENTOS DE CAJA">
                {r.movimientos.map((m) => (
                  <Linea
                    key={m.movimientoCajaId}
                    izq={`${m.tipo === 'Egreso' ? '−' : '+'} ${m.motivo}${m.estaAnulado ? ' (ANULADO)' : ''}`}
                    der={m.estaAnulado ? <s>{Number(m.monto).toFixed(2)}</s> : Number(m.monto).toFixed(2)}
                  />
                ))}
                <Linea izq="Total ingresos" der={soles(r.totalIngresos)} />
                <Linea izq="Total egresos" der={soles(r.totalEgresos)} />
              </Seccion>
            )}

            {r.porMozo.length > 0 && (
              <Seccion titulo="VENTAS POR MOZO">
                {r.porMozo.map((l) => <Linea key={l.nombre} izq={`${l.nombre} (${l.cantidad})`} der={soles(l.total)} />)}
              </Seccion>
            )}

            {r.porSalon.length > 1 && (
              <Seccion titulo="VENTAS POR SALÓN">
                {r.porSalon.map((l) => <Linea key={l.nombre} izq={`${l.nombre} (${l.cantidad})`} der={soles(l.total)} />)}
              </Seccion>
            )}

            {r.topProductos.length > 0 && (
              <Seccion titulo="MÁS VENDIDOS">
                {r.topProductos.map((l) => <Linea key={l.nombre} izq={`${l.cantidad} × ${l.nombre}`} der={Number(l.total).toFixed(2)} />)}
              </Seccion>
            )}

            <Seccion titulo="COMPROBANTES ELECTRÓNICOS">
              <Linea izq="Boletas" der={r.comprobantes.boletas} />
              {r.comprobantes.rangoBoletas && <p className="text-[10px]">{r.comprobantes.rangoBoletas}</p>}
              <Linea izq="Facturas" der={r.comprobantes.facturas} />
              {r.comprobantes.rangoFacturas && <p className="text-[10px]">{r.comprobantes.rangoFacturas}</p>}
              <Linea izq="Total comprobantes" der={soles(r.comprobantes.totalFacturado)} />
              {r.comprobantes.porConfirmar > 0 && <Linea izq="Por confirmar en SUNAT" der={r.comprobantes.porConfirmar} />}
              {r.comprobantes.conError > 0 && <Linea izq="Rechazados / con error" der={r.comprobantes.conError} />}
            </Seccion>

            {j.observacionCierre && (
              <Seccion titulo="OBSERVACIÓN">
                <p className="break-words">{j.observacionCierre}</p>
              </Seccion>
            )}

            <div className="mt-8 grid grid-cols-2 gap-4 text-center text-[10px]">
              <div className="border-t border-black pt-1">Cajero</div>
              <div className="border-t border-black pt-1">Administrador</div>
            </div>
          </div>
        </div>

        <div className="px-6 pb-6 pt-3 flex gap-3 border-t border-gray-100 print:hidden">
          <button
            onClick={onClose}
            className="flex-1 flex items-center justify-center gap-2 py-3 rounded-2xl border border-gray-300 text-gray-600 font-bold text-sm hover:bg-gray-50"
          >
            <X size={16} /> Cerrar
          </button>
          <button
            onClick={() => imprimirTicket('recibo-imprimible')}
            className="flex-1 flex items-center justify-center gap-2 py-3 rounded-2xl bg-primary text-white font-bold text-sm hover:bg-primaryHover"
          >
            <Printer size={16} /> Imprimir
          </button>
        </div>
      </motion.div>
    </motion.div>
  );
}
