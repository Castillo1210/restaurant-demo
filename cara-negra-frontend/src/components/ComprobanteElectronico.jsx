import { useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import { Printer, X, BadgeCheck, AlertTriangle } from 'lucide-react';
import QRCode from 'qrcode';
import { numeroALetras } from '../utils/numeroALetras';
import { parseFechaUtc } from '../utils/documentos';

/**
 * Representación impresa de una boleta/factura electrónica (formato ticket 80 mm).
 *
 * DEMO: el comprobante es SIMULADO — tiene serie-correlativo, desglose de IGV, monto en letras
 * y QR con el formato de SUNAT, pero no fue firmado ni enviado a SUNAT/OSE. Por eso se imprime
 * siempre la leyenda "DEMO – SIN VALIDEZ TRIBUTARIA".
 */

const S = (n) => `S/ ${Number(n ?? 0).toFixed(2)}`;

const ETIQUETA_DOC = {
  DNI: 'DNI',
  CarnetExtranjeria: 'C.E.',
  RUC: 'RUC',
};

export default function ComprobanteElectronico({ comprobante, onClose }) {
  const [qr, setQr] = useState(null);

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
        className="w-full max-w-sm max-h-[94dvh] flex flex-col bg-white text-black rounded-3xl overflow-hidden shadow-2xl print:rounded-none print:shadow-none print:max-w-full print:max-h-none"
      >
        {/* Estado (no se imprime) */}
        <div className="bg-emerald-50 px-6 pt-5 pb-3 flex flex-col items-center border-b border-dashed border-gray-300 print:hidden">
          <BadgeCheck size={36} className="text-emerald-500 mb-1" />
          <p className="font-extrabold text-emerald-700">{esFactura ? 'Factura' : 'Boleta'} {c.numero} emitida</p>
          <p className="text-[11px] text-emerald-700/80">Aceptada por SUNAT (respuesta simulada)</p>
        </div>

        <div className="overflow-y-auto print:overflow-visible">
          <div id="recibo-imprimible" className="px-5 py-4 font-mono text-[12px] leading-snug bg-white text-black">
            {/* Leyenda de demo: SIEMPRE visible e impresa */}
            {c.esSimulacion && (
              <div className="border-2 border-red-600 text-red-700 text-center font-bold text-[11px] py-1 mb-3 flex items-center justify-center gap-1">
                <AlertTriangle size={12} className="print:hidden" />
                DEMO – SIN VALIDEZ TRIBUTARIA
              </div>
            )}

            {/* Emisor */}
            <div className="text-center">
              <p className="font-extrabold text-[14px] tracking-wide">{c.emisor.nombreComercial}</p>
              <p>{c.emisor.razonSocial}</p>
              <p>RUC {c.emisor.ruc}</p>
              <p className="text-[11px]">{c.emisor.direccion}</p>
              {c.emisor.telefono && <p className="text-[11px]">Tel. {c.emisor.telefono}</p>}
            </div>

            <div className="border border-black text-center my-3 py-1.5">
              <p className="font-bold">{titulo}</p>
              <p className="font-bold text-[14px]">{c.numero}</p>
            </div>

            {/* Datos de la venta */}
            <div className="space-y-0.5">
              <p>Fecha emisión: {fechaTexto}</p>
              {c.mesaNumero && <p>Mesa: {c.mesaNumero} · Pedido #{c.pedidoId}</p>}
              {c.atendidoPor && <p>Atendido por: {c.atendidoPor}</p>}
            </div>

            <div className="border-t border-dashed border-gray-400 my-2" />

            {/* Adquiriente */}
            <div className="space-y-0.5">
              {esFactura ? (
                <>
                  <p>RUC: {c.clienteNumeroDocumento}</p>
                  <p>Razón social: {c.clienteNombre}</p>
                  {c.clienteDireccion && <p>Dirección: {c.clienteDireccion}</p>}
                </>
              ) : (
                <>
                  {etiquetaDoc && c.clienteNumeroDocumento && (
                    <p>{etiquetaDoc}: {c.clienteNumeroDocumento}</p>
                  )}
                  <p>Cliente: {c.clienteNombre}</p>
                </>
              )}
            </div>

            <div className="border-t border-dashed border-gray-400 my-2" />

            {/* Detalle */}
            <div className="grid grid-cols-[2.2rem_1fr_3.6rem] gap-x-1 font-bold text-[11px]">
              <span>CANT</span>
              <span>DESCRIPCIÓN</span>
              <span className="text-right">IMPORTE</span>
            </div>
            <div className="space-y-1 mt-1">
              {c.items.map((it, i) => (
                <div key={i} className="grid grid-cols-[2.2rem_1fr_3.6rem] gap-x-1">
                  <span>{it.cantidad}</span>
                  <span>
                    {it.descripcion}
                    <span className="block text-[10px] text-gray-600">P.U. {Number(it.precioUnitario).toFixed(2)}</span>
                  </span>
                  <span className="text-right">{Number(it.importe).toFixed(2)}</span>
                </div>
              ))}
            </div>

            <div className="border-t border-dashed border-gray-400 my-2" />

            {/* Totales */}
            <div className="space-y-0.5">
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
              <div className="flex justify-between font-extrabold text-[14px] pt-1">
                <span>IMPORTE TOTAL</span>
                <span>{S(c.total)}</span>
              </div>
            </div>

            <p className="mt-2 text-[11px]">SON: {numeroALetras(c.total)}</p>

            {c.pagos?.length > 0 && (
              <>
                <div className="border-t border-dashed border-gray-400 my-2" />
                <p className="font-bold text-[11px]">FORMA DE PAGO: CONTADO</p>
                {c.pagos.map((p, i) => (
                  <div key={i} className="flex justify-between text-[11px]">
                    <span>{p.metodoPago}</span>
                    <span>{S(p.monto)}</span>
                  </div>
                ))}
              </>
            )}

            <div className="border-t border-dashed border-gray-400 my-2" />

            {/* QR + resumen */}
            <div className="flex flex-col items-center text-center">
              {qr ? (
                <img src={qr} alt="Código QR del comprobante" className="w-32 h-32" />
              ) : (
                <div className="w-32 h-32 bg-gray-100" />
              )}
              <p className="text-[10px] mt-1 break-all">Resumen: {c.hashResumen}</p>
              <p className="text-[10px] mt-1">
                Representación impresa de la {esFactura ? 'factura' : 'boleta de venta'} electrónica.
              </p>
              {c.esSimulacion && (
                <p className="text-[10px] mt-1 font-bold text-red-700">
                  Comprobante SIMULADO para demostración: no fue enviado a SUNAT.
                </p>
              )}
              <p className="text-[11px] mt-2">¡Gracias por su preferencia!</p>
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
