import { useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import { Printer, X, BadgeCheck, AlertTriangle, Clock, XCircle, RefreshCw, FilePlus2, ExternalLink } from 'lucide-react';
import toast from 'react-hot-toast';
import QRCode from 'qrcode';
import { numeroALetras } from '../utils/numeroALetras';
import { parseFechaUtc } from '../utils/documentos';
import { etiquetaMesa } from '../utils/mesaLabel';
import { infoEstado, leyendaAmbiente, TONO_CLASES } from '../utils/estadoComprobante';
import { reenviarComprobante } from '../services/comprobantesService';
import { imprimirTicket } from '../utils/imprimir';

/**
 * Representación impresa de una boleta/factura electrónica (formato ticket 80 mm), con el estado
 * frente a SUNAT arriba (no se imprime).
 *
 * Si el comprobante no tiene validez tributaria (modo demo o sandbox de APISUNAT) se imprime
 * la leyenda correspondiente ("DEMO…" o "PRUEBA (SANDBOX)…"). En producción no lleva leyenda.
 *
 * onActualizado(c): tras "Reintentar envío" (el padre reemplaza el comprobante mostrado).
 * onReemitir(c):    si fue rechazado, abrir la emisión de un comprobante nuevo para el pedido.
 */

const S = (n) => `S/ ${Number(n ?? 0).toFixed(2)}`;

const ICONO_TONO = { ok: BadgeCheck, espera: Clock, error: XCircle, neutro: AlertTriangle };

const ETIQUETA_DOC = {
  DNI: 'DNI',
  CarnetExtranjeria: 'C.E.',
  RUC: 'RUC',
};

export default function ComprobanteElectronico({ comprobante, onClose, onActualizado, onReemitir }) {
  const [qr, setQr] = useState(null);
  const [reintentando, setReintentando] = useState(false);

  useEffect(() => {
    if (!comprobante?.cadenaQr) return;
    QRCode.toDataURL(comprobante.cadenaQr, { margin: 1, width: 180, errorCorrectionLevel: 'M' })
      .then(setQr)
      .catch(() => setQr(null));
  }, [comprobante?.cadenaQr]);

  if (!comprobante) return null;

  const c = comprobante;
  const esFactura = c.tipoComprobante === 'Factura';
  const titulo = esFactura ? 'FACTURA ELECTRÓNICA' : 'BOLETA DE VENTA ELECTRÓNICA';
  const fecha = parseFechaUtc(c.fechaEmision);
  const fechaTexto = fecha
    ? fecha.toLocaleString('es-PE', { timeZone: 'America/Lima', dateStyle: 'short', timeStyle: 'short' })
    : '';
  const etiquetaDoc = ETIQUETA_DOC[c.clienteTipoDocumento];
  const estado = infoEstado(c.estado);
  const IconoEstado = ICONO_TONO[estado.tono];
  const leyenda = leyendaAmbiente(c.ambiente);
  const nombreTipo = esFactura ? 'Factura' : 'Boleta';

  const reintentar = async () => {
    setReintentando(true);
    try {
      const actualizado = await reenviarComprobante(c.comprobanteId);
      const nuevo = infoEstado(actualizado.estado);
      if (nuevo.tono === 'ok') toast.success(`${nombreTipo} ${actualizado.numero}: ${nuevo.titulo}`);
      else if (nuevo.tono === 'error') toast.error(actualizado.mensajeSunat || nuevo.titulo);
      else toast(nuevo.titulo);
      onActualizado?.(actualizado);
    } catch (err) {
      toast.error(err.message);
    } finally {
      setReintentando(false);
    }
  };

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
        className="paleta-normal w-full max-w-sm max-h-[94dvh] flex flex-col bg-white text-black rounded-3xl overflow-hidden shadow-2xl print:rounded-none print:shadow-none print:max-w-full print:max-h-none"
      >
        {/* Estado frente a SUNAT: franja compacta, solo en pantalla (no se imprime). La explicación
            larga solo se muestra cuando hay un problema que el cajero debe resolver. */}
        <div className={`${estado.clases ?? TONO_CLASES[estado.tono]} px-4 py-2 border-b border-dashed border-gray-300 print:hidden`}>
          <div className="flex items-center gap-2" title={estado.descripcion}>
            <IconoEstado size={16} className="shrink-0" />
            <p className="flex-1 min-w-0 text-[13px] font-extrabold leading-tight">
              {nombreTipo} {c.numero} · {estado.titulo}
            </p>
          </div>
          {estado.tono === 'error' && (
            <p className="text-[11px] opacity-80 mt-1">{estado.descripcion}</p>
          )}
          {estado.nota && (
            <p className="text-[11px] opacity-80 mt-0.5 pl-6">{estado.nota}</p>
          )}
          {c.mensajeSunat && estado.tono === 'error' && (
            <p className="text-[11px] mt-1 font-semibold break-words">{c.mensajeSunat}</p>
          )}
          {c.ambiente === 'SANDBOX' && (
            <p className="text-[10px] mt-0.5 opacity-70">Ambiente de PRUEBAS de APISUNAT (no llega a SUNAT).</p>
          )}
          {(estado.reintentar || (estado.reemitir && onReemitir) || c.pdfUrl) && (
            <div className="flex flex-wrap gap-1.5 mt-1.5">
              {estado.reintentar && (
                <button
                  onClick={reintentar}
                  disabled={reintentando}
                  className="flex items-center gap-1 text-[11px] font-bold px-2.5 py-1 rounded-full bg-white/70 border border-current/20 hover:bg-white disabled:opacity-60"
                >
                  <RefreshCw size={12} className={reintentando ? 'animate-spin' : ''} />
                  {reintentando ? 'Consultando…' : (estado.accion ?? 'Reintentar envío')}
                </button>
              )}
              {estado.reemitir && onReemitir && (
                <button
                  onClick={() => onReemitir(c)}
                  className="flex items-center gap-1 text-[11px] font-bold px-2.5 py-1 rounded-full bg-white/70 border border-current/20 hover:bg-white"
                >
                  <FilePlus2 size={12} /> Emitir de nuevo
                </button>
              )}
              {c.pdfUrl && (
                <a
                  href={c.pdfUrl}
                  target="_blank"
                  rel="noreferrer"
                  className="flex items-center gap-1 text-[11px] font-bold px-2.5 py-1 rounded-full bg-white/70 border border-current/20 hover:bg-white"
                >
                  <ExternalLink size={12} /> PDF del proveedor
                </a>
              )}
            </div>
          )}
        </div>

        <div className="overflow-y-auto print:overflow-visible">
          <div id="recibo-imprimible" className="ticket px-5 py-4 text-[13px] leading-snug bg-white text-black">
            {/* Leyenda de demo / pruebas: visible e impresa mientras no sea un comprobante real */}
            {leyenda && (
              <div className="border-2 border-red-600 text-red-700 text-center font-bold text-[11px] py-0.5 mb-1.5 flex items-center justify-center gap-1">
                <AlertTriangle size={12} className="print:hidden" />
                {leyenda}
              </div>
            )}

            {/* Emisor */}
            <div className="text-center">
              {c.emisor.logoBase64 && <img src={c.emisor.logoBase64} alt="" className="ticket-logo h-14 mx-auto mb-0.5 object-contain" />}
              <p className="font-extrabold text-[15px] leading-tight">{c.emisor.nombreComercial}</p>
              <p>{c.emisor.razonSocial}</p>
              <p>RUC {c.emisor.ruc}</p>
              <p className="ticket-chico text-[11.5px]">{c.emisor.direccion}</p>
              {c.emisor.telefono && <p className="ticket-chico text-[11.5px]">Tel. {c.emisor.telefono}</p>}
            </div>

            <div className="border-y-2 border-black text-center my-1.5 py-0.5">
              <p className="font-bold">{titulo}</p>
              <p className="font-extrabold text-[15px]">{c.numero}</p>
            </div>

            {/* Datos de la venta y del cliente */}
            <div>
              <p>Fecha: {fechaTexto}</p>
              {c.mesaNumero && <p>Mesa: {etiquetaMesa(c.salonNombre, c.mesaNumero)} · Pedido #{c.pedidoId}</p>}
              {c.atendidoPor && <p>Atendido por: {c.atendidoPor}</p>}
              {esFactura ? (
                <>
                  <p>RUC: {c.clienteNumeroDocumento}</p>
                  <p>Razón social: {c.clienteNombre}</p>
                  {c.clienteDireccion && <p>Dirección: {c.clienteDireccion}</p>}
                </>
              ) : (
                <p>
                  Cliente: {c.clienteNombre}
                  {etiquetaDoc && c.clienteNumeroDocumento ? ` · ${etiquetaDoc} ${c.clienteNumeroDocumento}` : ''}
                </p>
              )}
            </div>

            {/* Detalle: una línea por producto (cant · descripción · P.U. · importe) */}
            <div className="grid grid-cols-[2.2rem_1fr_3rem_3.4rem] gap-x-1 font-bold border-y border-black mt-1.5 py-0.5 text-[11.5px] ticket-chico">
              <span>CANT</span>
              <span>DESCRIPCIÓN</span>
              <span className="text-right">P.U.</span>
              <span className="text-right">IMPORTE</span>
            </div>
            <div className="space-y-0.5 mt-0.5">
              {c.items.map((it, i) => (
                <div key={i} className="grid grid-cols-[2.2rem_1fr_3rem_3.4rem] gap-x-1">
                  <span>{it.cantidad}</span>
                  <span className="break-words">{it.descripcion}</span>
                  <span className="text-right">{Number(it.precioUnitario).toFixed(2)}</span>
                  <span className="text-right">{Number(it.importe).toFixed(2)}</span>
                </div>
              ))}
            </div>

            {/* Totales */}
            <div className="border-t border-black mt-1 pt-0.5">
              {c.descuento > 0 && (
                <div className="flex justify-between">
                  <span>Descuento global</span>
                  <span>- {S(c.descuento)}</span>
                </div>
              )}
              <div className="flex justify-between">
                <span>Op. gravada</span>
                <span>{S(c.opGravada)}</span>
              </div>
              <div className="flex justify-between">
                <span>IGV ({Number(c.igvPorcentaje).toFixed(0)}%)</span>
                <span>{S(c.igv)}</span>
              </div>
              <div className="flex justify-between font-extrabold text-[15px]">
                <span>TOTAL</span>
                <span>{S(c.total)}</span>
              </div>
            </div>

            <p className="ticket-chico text-[11.5px]">SON: {numeroALetras(c.total)}</p>

            {c.pagos?.length > 0 && (
              <div className="border-t border-dashed border-black mt-1 pt-0.5">
                <p className="font-bold ticket-chico text-[11.5px]">PAGO AL CONTADO</p>
                {c.pagos.map((p, i) => (
                  <div key={i} className="flex justify-between">
                    <span>{p.metodoPago}</span>
                    <span>{S(p.monto)}</span>
                  </div>
                ))}
              </div>
            )}

            {/* QR + resumen */}
            <div className="flex flex-col items-center text-center border-t border-dashed border-black mt-1 pt-1">
              {qr ? (
                <img src={qr} alt="Código QR del comprobante" className="ticket-qr w-28 h-28" />
              ) : (
                <div className="w-28 h-28 bg-gray-100" />
              )}
              <p className="ticket-chico text-[11px] break-all">Resumen: {c.hashResumen}</p>
              <p className="ticket-chico text-[11px]">
                Representación impresa de la {esFactura ? 'factura' : 'boleta de venta'} electrónica.
              </p>
              {leyenda && (
                <p className="ticket-chico text-[11px] font-bold text-red-700">
                  {c.ambiente === 'SANDBOX'
                    ? 'Comprobante de PRUEBA (sandbox del proveedor): no fue enviado a SUNAT.'
                    : 'Comprobante SIMULADO para demostración: no fue enviado a SUNAT.'}
                </p>
              )}
              <p className="mt-0.5">{c.emisor.mensajePie || '¡Gracias por su preferencia!'}</p>
            </div>
          </div>
        </div>

        {/* Acciones (no se imprimen) */}
        <div className="px-6 pb-6 pt-3 flex gap-3 border-t border-gray-100 print:hidden">
          <button
            onClick={onClose}
            className="flex-1 flex items-center justify-center gap-2 py-3 rounded-2xl border border-gray-300 text-gray-600 font-bold text-sm hover:bg-gray-50 transition-colors"
          >
            <X size={16} />
            Cerrar
          </button>
          <button
            onClick={() => imprimirTicket('recibo-imprimible')}
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
