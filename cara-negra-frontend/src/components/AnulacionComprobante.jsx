import { useState } from 'react';
import { Ban, FileMinus2, RefreshCw, ExternalLink, CheckCheck, PencilLine } from 'lucide-react';
import toast from 'react-hot-toast';
import {
  anularComprobante,
  reintentarAnulacion,
  confirmarAnulacion,
  emitirNotaCorreccion,
  reenviarNotaCredito,
} from '../services/comprobantesService';
import { infoEstado, TONO_CLASES } from '../utils/estadoComprobante';
import { parseFechaUtc } from '../utils/documentos';

/**
 * Anulación y notas de crédito de un comprobante (pantalla, no se imprime).
 *  - Todos ven el estado de una anulación y las notas de crédito emitidas.
 *  - Solo el ADMIN (esAdmin) puede anular, corregir descripciones, reintentar y confirmar.
 *
 * onActualizado(c): el comprobante actualizado que devuelve el backend.
 */

const METODO = {
  BAJA: 'comunicación de baja',
  RESUMEN: 'resumen diario',
  NOTA_CREDITO: 'nota de crédito',
};

const PLAZO = {
  BAJA: 'factura emitida hace 3 días o menos',
  RESUMEN: 'boleta emitida hace 5 días o menos',
  NOTA_CREDITO: 'ya pasó el plazo de la baja / resumen',
};

const ESTADO_ANULACION = {
  PorEnviar: { texto: 'Anulación sin enviar: no llegó al proveedor. Se reintenta sola.', tono: 'espera' },
  Pendiente: { texto: 'Anulación enviada: falta que SUNAT la confirme. Se consulta sola.', tono: 'espera' },
  Aceptada: { texto: 'Anulación aceptada.', tono: 'neutro' },
  Rechazada: { texto: 'La anulación no fue aceptada.', tono: 'error' },
};

const boton =
  'flex items-center gap-1 text-[11px] font-bold px-2.5 py-1 rounded-full bg-white border border-gray-300 hover:bg-gray-50 disabled:opacity-50';

export default function AnulacionComprobante({ comprobante: c, esAdmin, onActualizado }) {
  const [modo, setModo] = useState(null); // null | 'anular' | 'corregir'
  const [motivo, setMotivo] = useState('');
  const [correcciones, setCorrecciones] = useState('');
  const [ocupado, setOcupado] = useState(false);
  const [confirmando, setConfirmando] = useState(false);

  const ejecutar = async (accion, exito) => {
    setOcupado(true);
    try {
      const actualizado = await accion();
      onActualizado?.(actualizado);
      if (exito) exito(actualizado);
      setModo(null);
      setConfirmando(false);
    } catch (err) {
      toast.error(err.message);
    } finally {
      setOcupado(false);
    }
  };

  const avisarAnulacion = (a) => {
    if (a.anulacionEstado === 'Aceptada') toast.success(`${a.numero} anulado`);
    else if (a.anulacionEstado === 'Rechazada') toast.error(a.anulacionMensaje || 'La anulación no fue aceptada');
    else toast(ESTADO_ANULACION[a.anulacionEstado]?.texto ?? 'Anulación registrada');
  };

  const abrirCorreccion = () => {
    setCorrecciones((c.items ?? []).map((it) => it.descripcion).join('\n'));
    setMotivo('');
    setModo('corregir');
  };

  const estadoAnulacion = c.anulacionEstado ? ESTADO_ANULACION[c.anulacionEstado] : null;
  const notas = c.notasCredito ?? [];
  const metodo = c.metodoAnulacionDisponible;
  const puedeAnularConNota = esAdmin && c.anulacionEstado === 'Rechazada' && c.estado !== 'Anulado';

  if (!estadoAnulacion && notas.length === 0 && !(esAdmin && (metodo || c.puedeCorregir))) return null;

  return (
    <div className="px-4 py-3 border-b border-dashed border-gray-300 space-y-2 text-[12px] print:hidden">
      {/* Estado de la anulación */}
      {estadoAnulacion && (
        <div className={`${TONO_CLASES[estadoAnulacion.tono]} rounded-xl px-3 py-2`}>
          <p className="font-bold">
            {estadoAnulacion.texto}
            {c.anulacionMetodo && <span className="font-normal"> · Por {METODO[c.anulacionMetodo] ?? c.anulacionMetodo}</span>}
          </p>
          {c.anulacionMotivo && <p className="text-[11px] mt-0.5">Motivo: {c.anulacionMotivo}</p>}
          {c.anulacionMensaje && <p className="text-[11px] mt-0.5 break-words opacity-80">{c.anulacionMensaje}</p>}
          {esAdmin && (
            <div className="flex flex-wrap gap-1.5 mt-1.5">
              {['PorEnviar', 'Pendiente'].includes(c.anulacionEstado) && (
                <button
                  className={boton}
                  disabled={ocupado}
                  onClick={() => ejecutar(() => reintentarAnulacion(c.comprobanteId), avisarAnulacion)}
                >
                  <RefreshCw size={12} className={ocupado ? 'animate-spin' : ''} /> Consultar ahora
                </button>
              )}
              {c.anulacionEstado === 'Pendiente' && ['BAJA', 'RESUMEN'].includes(c.anulacionMetodo) && (
                confirmando ? (
                  <button
                    className={`${boton} !bg-gray-800 !text-white !border-gray-800`}
                    disabled={ocupado}
                    onClick={() => ejecutar(() => confirmarAnulacion(c.comprobanteId), () => toast.success('Anulación confirmada'))}
                  >
                    <CheckCheck size={12} /> Sí, ya figura aceptada en APISUNAT
                  </button>
                ) : (
                  <button className={boton} onClick={() => setConfirmando(true)}>
                    <CheckCheck size={12} /> Ya la verifiqué en APISUNAT
                  </button>
                )
              )}
              {puedeAnularConNota && (
                <button className={boton} onClick={() => { setMotivo(c.anulacionMotivo ?? ''); setModo('anular-nota'); }}>
                  <FileMinus2 size={12} /> Anular con nota de crédito
                </button>
              )}
            </div>
          )}
        </div>
      )}

      {/* Acciones del administrador */}
      {esAdmin && !modo && (metodo || c.puedeCorregir) && (
        <div className="flex flex-wrap gap-1.5">
          {metodo && (
            <button className={`${boton} text-red-700`} onClick={() => { setMotivo(''); setModo('anular'); }}>
              <Ban size={12} /> Anular comprobante
            </button>
          )}
          {c.puedeCorregir && (
            <button className={boton} onClick={abrirCorreccion}>
              <PencilLine size={12} /> Corregir descripción (nota de crédito)
            </button>
          )}
        </div>
      )}

      {/* Formulario: anular */}
      {(modo === 'anular' || modo === 'anular-nota') && (
        <div className="rounded-xl border border-red-200 bg-red-50/60 px-3 py-2 space-y-1.5">
          <p className="font-bold text-red-800">
            {modo === 'anular-nota'
              ? 'Se anulará con una nota de crédito (anulación de la operación).'
              : `Se anulará por ${METODO[metodo]} (${PLAZO[metodo]}; emitido hace ${c.diasDesdeEmision} día${c.diasDesdeEmision === 1 ? '' : 's'}).`}
          </p>
          <p className="text-[11px] text-red-900/80">
            Solo se anula el comprobante ante SUNAT: el pago y el pedido siguen igual. Después puedes emitir otro comprobante o anular el pago en Caja.
          </p>
          <textarea
            value={motivo}
            onChange={(e) => setMotivo(e.target.value)}
            maxLength={100}
            rows={2}
            placeholder="Motivo (se envía a SUNAT). Ej.: Error en el RUC del cliente"
            className="w-full rounded-lg border border-gray-300 px-2 py-1.5 text-[12px] bg-white"
          />
          <div className="flex gap-1.5">
            <button className={boton} onClick={() => setModo(null)} disabled={ocupado}>Cancelar</button>
            <button
              className={`${boton} !bg-red-600 !text-white !border-red-600`}
              disabled={ocupado || motivo.trim().length < 3}
              onClick={() =>
                ejecutar(
                  () => anularComprobante(c.comprobanteId, { motivo: motivo.trim(), usarNotaCredito: modo === 'anular-nota' }),
                  avisarAnulacion,
                )
              }
            >
              <Ban size={12} /> {ocupado ? 'Enviando…' : 'Confirmar anulación'}
            </button>
          </div>
        </div>
      )}

      {/* Formulario: corrección de descripción */}
      {modo === 'corregir' && (
        <div className="rounded-xl border border-gray-300 bg-gray-50 px-3 py-2 space-y-1.5">
          <p className="font-bold">Nota de crédito por corrección de descripción</p>
          <p className="text-[11px] text-gray-600">
            Importes en cero: el comprobante sigue vigente. Escribe la descripción correcta, una por línea (deja solo las que corriges).
          </p>
          <textarea
            value={correcciones}
            onChange={(e) => setCorrecciones(e.target.value)}
            rows={Math.min(8, Math.max(3, correcciones.split('\n').length))}
            className="w-full rounded-lg border border-gray-300 px-2 py-1.5 text-[12px] bg-white font-mono"
          />
          <input
            value={motivo}
            onChange={(e) => setMotivo(e.target.value)}
            maxLength={100}
            placeholder="Motivo (opcional): Corrección por error en la descripción"
            className="w-full rounded-lg border border-gray-300 px-2 py-1.5 text-[12px] bg-white"
          />
          <div className="flex gap-1.5">
            <button className={boton} onClick={() => setModo(null)} disabled={ocupado}>Cancelar</button>
            <button
              className={`${boton} !bg-gray-800 !text-white !border-gray-800`}
              disabled={ocupado || correcciones.trim().length < 3}
              onClick={() =>
                ejecutar(
                  () =>
                    emitirNotaCorreccion(c.comprobanteId, {
                      motivo: motivo.trim() || undefined,
                      correcciones: correcciones.split('\n').map((l) => l.trim()).filter(Boolean),
                    }),
                  (a) => {
                    const ultima = a.notasCredito?.[a.notasCredito.length - 1];
                    toast.success(ultima ? `Nota de crédito ${ultima.numero}: ${infoEstado(ultima.estado).titulo}` : 'Nota de crédito emitida');
                  },
                )
              }
            >
              <FileMinus2 size={12} /> {ocupado ? 'Enviando…' : 'Emitir nota de crédito'}
            </button>
          </div>
        </div>
      )}

      {/* Notas de crédito emitidas */}
      {notas.length > 0 && (
        <div className="space-y-1">
          <p className="text-[11px] font-bold uppercase tracking-wider text-gray-500">Notas de crédito</p>
          {notas.map((n) => {
            const est = infoEstado(n.estado);
            const fecha = parseFechaUtc(n.fechaEmision)?.toLocaleString('es-PE', {
              timeZone: 'America/Lima', dateStyle: 'short', timeStyle: 'short',
            });
            return (
              <div key={n.notaCreditoId} className="rounded-xl border border-gray-200 px-3 py-1.5">
                <div className="flex items-center justify-between gap-2">
                  <p className="font-mono font-bold">{n.numero}</p>
                  <span className={`${est.clases ?? TONO_CLASES[est.tono]} text-[10px] font-bold px-2 py-0.5 rounded-full`}>{est.titulo}</span>
                </div>
                <p className="text-[11px] text-gray-600">
                  {n.tipoDescripcion} · S/ {Number(n.total).toFixed(2)} · {fecha}
                </p>
                {n.correcciones?.length > 0 && (
                  <ul className="text-[11px] text-gray-700 list-disc pl-4">
                    {n.correcciones.map((l, i) => <li key={i}>{l}</li>)}
                  </ul>
                )}
                {n.mensajeSunat && est.tono !== 'ok' && <p className="text-[11px] text-gray-600 break-words">{n.mensajeSunat}</p>}
                <div className="flex flex-wrap gap-1.5 mt-1">
                  {esAdmin && est.reintentar && (
                    <button
                      className={boton}
                      disabled={ocupado}
                      onClick={() => ejecutar(() => reenviarNotaCredito(n.notaCreditoId), () => toast('Nota de crédito actualizada'))}
                    >
                      <RefreshCw size={12} className={ocupado ? 'animate-spin' : ''} /> {est.accion ?? 'Reintentar envío'}
                    </button>
                  )}
                  {n.pdfUrl && (
                    <a href={n.pdfUrl} target="_blank" rel="noreferrer" className={boton}>
                      <ExternalLink size={12} /> PDF
                    </a>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
