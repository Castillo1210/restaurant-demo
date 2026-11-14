import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { ChevronLeft, FileText, Building2, User, RefreshCw, AlertTriangle, Clock, CheckCircle2, XCircle, MinusCircle, Download } from 'lucide-react';
import toast from 'react-hot-toast';
import { getComprobantes, getComprobanteById, exportarComprobantes } from '../services/comprobantesService';
import ComprobanteElectronico from '../components/ComprobanteElectronico';
import EmitirComprobanteModal from '../components/EmitirComprobanteModal';
import { parseFechaUtc } from '../utils/documentos';
import { infoEstado, TONO_BADGE } from '../utils/estadoComprobante';

// Fecha de hoy en Lima como "yyyy-MM-dd" (para el <input type="date">).
const hoyLima = () => new Date().toLocaleDateString('en-CA', { timeZone: 'America/Lima' });
// Primer día del mes actual en Lima ("yyyy-MM-01").
const inicioMesLima = () => `${hoyLima().slice(0, 8)}01`;

const CLASE_FECHA =
  'bg-card border border-gray-700/50 rounded-xl px-3 py-2 text-sm text-ink focus:outline-none focus:border-primary [color-scheme:dark]';
const claseAtajo = (activo) =>
  `px-3 py-2 rounded-xl text-xs font-bold border transition-colors ${
    activo ? 'bg-primary/20 border-primary text-primary' : 'bg-card border-gray-800 text-gray-400 hover:border-gray-600'
  }`;

const FILTROS = [
  { value: '', label: 'Todos' },
  { value: 'Boleta', label: 'Boletas' },
  { value: 'Factura', label: 'Facturas' },
];

// Filtro por estado frente a SUNAT (se aplica en pantalla sobre los comprobantes del día).
const FILTROS_ESTADO = [
  { value: 'todos', label: 'Todos los estados', incluye: () => true },
  { value: 'ok', label: 'Aceptados', incluye: (e) => infoEstado(e).tono === 'ok' },
  { value: 'espera', label: 'Por confirmar', incluye: (e) => infoEstado(e).tono === 'espera' },
  { value: 'error', label: 'Rechazados', incluye: (e) => infoEstado(e).tono === 'error' },
  { value: 'neutro', label: 'Anulados', incluye: (e) => infoEstado(e).tono === 'neutro' },
];

const ICONO_TONO = { ok: CheckCircle2, espera: Clock, error: XCircle, neutro: MinusCircle };

/** Estado frente a SUNAT como etiqueta de color; al pasar el mouse explica qué significa. */
function EstadoBadge({ estado, mensaje }) {
  const Icono = ICONO_TONO[estado.tono];
  const ayuda = [estado.descripcion, mensaje].filter(Boolean).join('\n');
  return (
    <span
      title={ayuda}
      className={`inline-flex items-center gap-1 max-w-full text-[11px] font-bold px-2 py-0.5 rounded-full border whitespace-nowrap ${estado.claseBadge ?? TONO_BADGE[estado.tono]}`}
    >
      <Icono size={12} className="flex-shrink-0" />
      <span className="truncate">{estado.titulo}</span>
    </span>
  );
}

/**
 * Registro de comprobantes electrónicos emitidos (boletas y facturas) por día, con totales de
 * base imponible e IGV, estado frente a SUNAT, reintento de envío y reimpresión.
 * Los rechazados o con error de envío no suman en los totales.
 */
export default function Comprobantes() {
  const navigate = useNavigate();
  const [desde, setDesde] = useState(hoyLima());
  const [hasta, setHasta] = useState(hoyLima());
  const [tipo, setTipo] = useState('');
  const [data, setData] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [seleccionado, setSeleccionado] = useState(null);
  const [reemision, setReemision] = useState(null);
  const [filtroEstado, setFiltroEstado] = useState('todos');
  const [exportando, setExportando] = useState(false);

  const cargar = useCallback(() => {
    setIsLoading(true);
    getComprobantes({ fechaDesde: desde, fechaHasta: hasta, tipo: tipo || undefined })
      .then(setData)
      .catch(() => toast.error('No se pudieron cargar los comprobantes'))
      .finally(() => setIsLoading(false));
  }, [desde, hasta, tipo]);

  useEffect(() => {
    cargar();
  }, [cargar]);

  const abrir = async (id) => {
    try {
      setSeleccionado(await getComprobanteById(id));
    } catch {
      toast.error('No se pudo abrir el comprobante');
    }
  };

  const exportarExcel = async () => {
    setExportando(true);
    try {
      await exportarComprobantes({ fechaDesde: desde, fechaHasta: hasta, tipo });
    } catch (err) {
      toast.error(err.message ?? 'No se pudo exportar los comprobantes');
    } finally {
      setExportando(false);
    }
  };

  // Si "Desde" pasa a ser posterior a "Hasta" (o al revés), se mueve la otra fecha para que el rango siga siendo válido.
  const cambiarDesde = (valor) => {
    if (!valor) return;
    setDesde(valor);
    if (valor > hasta) setHasta(valor);
  };
  const cambiarHasta = (valor) => {
    if (!valor) return;
    setHasta(valor);
    if (valor < desde) setDesde(valor);
  };
  const esHoy = desde === hoyLima() && hasta === hoyLima();
  const esEsteMes = desde === inicioMesLima() && hasta === hoyLima();
  const variosDias = desde !== hasta;

  const filtroActual = FILTROS_ESTADO.find((f) => f.value === filtroEstado) ?? FILTROS_ESTADO[0];
  const visibles = (data?.items ?? []).filter((c) => filtroActual.incluye(c.estado));

  const resumen = [
    { label: 'Boletas', value: data?.cantidadBoletas ?? 0 },
    { label: 'Facturas', value: data?.cantidadFacturas ?? 0 },
    { label: 'Op. gravada', value: `S/ ${(data?.totalOpGravada ?? 0).toFixed(2)}` },
    { label: 'IGV', value: `S/ ${(data?.totalIgv ?? 0).toFixed(2)}` },
  ];

  return (
    <div className="min-h-screen bg-background pb-10">
      <div className="px-5 pt-6 pb-4 flex items-center gap-3">
        <motion.button
          whileTap={{ scale: 0.9 }}
          onClick={() => navigate(-1)}
          className="p-2 bg-card rounded-full hover:bg-cardHighlight border border-gray-800/50"
        >
          <ChevronLeft size={22} />
        </motion.button>
        <div className="flex-1">
          <h1 className="text-2xl font-extrabold tracking-tight">Comprobantes electrónicos</h1>
          <p className="text-xs text-gray-500 mt-0.5">Boletas y facturas emitidas y su estado en SUNAT</p>
        </div>
        <button onClick={cargar} className="p-2 bg-card rounded-full hover:bg-cardHighlight border border-gray-800/50" title="Actualizar">
          <RefreshCw size={18} className={isLoading ? 'animate-spin text-gray-500' : 'text-gray-300'} />
        </button>
      </div>

      <div className="px-5 flex flex-wrap gap-2 items-center">
        <label className="flex items-center gap-1.5 text-xs text-gray-500">
          Desde
          <input type="date" value={desde} max={hoyLima()} onChange={(e) => cambiarDesde(e.target.value)} className={CLASE_FECHA} />
        </label>
        <label className="flex items-center gap-1.5 text-xs text-gray-500">
          Hasta
          <input type="date" value={hasta} max={hoyLima()} onChange={(e) => cambiarHasta(e.target.value)} className={CLASE_FECHA} />
        </label>
        <button onClick={() => { setDesde(hoyLima()); setHasta(hoyLima()); }} className={claseAtajo(esHoy)}>
          Hoy
        </button>
        <button onClick={() => { setDesde(inicioMesLima()); setHasta(hoyLima()); }} className={claseAtajo(esEsteMes)}>
          Este mes
        </button>
        <span className="w-px h-6 bg-gray-800 mx-1 hidden sm:block" />
        {FILTROS.map((f) => (
          <button
            key={f.value}
            onClick={() => setTipo(f.value)}
            className={`px-3.5 py-2 rounded-xl text-xs font-bold border transition-colors ${
              tipo === f.value ? 'bg-primary/20 border-primary text-primary' : 'bg-card border-gray-800 text-gray-400 hover:border-gray-600'
            }`}
          >
            {f.label}
          </button>
        ))}
        <button
          onClick={exportarExcel}
          disabled={exportando || isLoading || (data?.items?.length ?? 0) === 0}
          title="Descargar en Excel los comprobantes de las fechas elegidas"
          className="ml-auto flex items-center gap-1.5 px-3.5 py-2 rounded-xl text-xs font-bold border bg-card border-gray-800 text-gray-300 hover:border-primary/60 hover:text-primary transition-colors disabled:opacity-40 disabled:pointer-events-none"
        >
          <Download size={14} />
          {exportando ? 'Exportando…' : 'Exportar a Excel'}
        </button>
      </div>

      <div className="px-5 mt-4 grid grid-cols-2 sm:grid-cols-4 gap-2">
        {resumen.map((r) => (
          <div key={r.label} className="bg-card border border-gray-800/50 rounded-2xl p-3">
            <p className="text-[11px] text-gray-500 uppercase tracking-wider font-bold">{r.label}</p>
            <p className="text-lg font-extrabold text-ink mt-0.5">{r.value}</p>
          </div>
        ))}
      </div>

      <div className="px-5 mt-3 flex justify-between items-center bg-orange-950/30 border border-orange-900/30 rounded-2xl mx-5 py-3">
        <span className="text-primary text-sm font-bold">Total facturado</span>
        <span className="text-primary font-extrabold text-xl">S/ {(data?.totalVendido ?? 0).toFixed(2)}</span>
      </div>

      {(() => {
        const items = data?.items ?? [];
        const esperando = items.filter((c) => c.estado === 'Pendiente').length;
        const sinEnviar = items.filter((c) => c.estado === 'PorEnviar').length;
        const conError = items.filter((c) => infoEstado(c.estado).tono === 'error').length;
        if (!esperando && !sinEnviar && !conError) return null;
        return (
          <div className="mx-5 mt-3 space-y-1.5 text-xs">
            {esperando > 0 && (
              <p className="flex items-start gap-2 bg-sky-500/10 border border-sky-500/30 rounded-2xl px-4 py-2.5 text-sky-300">
                <Clock size={14} className="flex-shrink-0 mt-0.5" />
                <span><b>{esperando} esperando a SUNAT:</b> ya se enviaron correctamente; la demora es de SUNAT. Se actualizan solos.</span>
              </p>
            )}
            {sinEnviar > 0 && (
              <p className="flex items-start gap-2 bg-yellow-950/30 border border-yellow-900/40 rounded-2xl px-4 py-2.5 text-yellow-200/90">
                <AlertTriangle size={14} className="text-accentYellow flex-shrink-0 mt-0.5" />
                <span><b>{sinEnviar} sin enviar:</b> no llegaron al proveedor de facturación (revisa el internet). Se reintentan solos.</span>
              </p>
            )}
            {conError > 0 && (
              <p className="flex items-start gap-2 bg-red-950/30 border border-red-900/40 rounded-2xl px-4 py-2.5 text-red-300">
                <AlertTriangle size={14} className="flex-shrink-0 mt-0.5" />
                <span><b>{conError} rechazado{conError === 1 ? '' : 's'}:</b> ábrelo{conError === 1 ? '' : 's'} para ver el motivo y emitir de nuevo.</span>
              </p>
            )}
          </div>
        );
      })()}

      {/* Filtro por estado */}
      {(data?.items?.length ?? 0) > 0 && (
        <div className="px-5 mt-4 flex flex-wrap gap-2">
          {FILTROS_ESTADO.map((f) => {
            const n = (data?.items ?? []).filter((c) => f.incluye(c.estado)).length;
            return (
              <button
                key={f.value}
                onClick={() => setFiltroEstado(f.value)}
                className={`px-3 py-1.5 rounded-xl text-xs font-bold border transition-colors ${
                  filtroEstado === f.value ? 'bg-primary/20 border-primary text-primary' : 'bg-card border-gray-800 text-gray-400 hover:border-gray-600'
                }`}
              >
                {f.label} <span className="opacity-70">({n})</span>
              </button>
            );
          })}
        </div>
      )}

      <div className="px-5 mt-3">
        {!isLoading && (data?.items?.length ?? 0) === 0 && (
          <div className="flex flex-col items-center text-center mt-10">
            <FileText size={32} className="text-gray-700 mb-2" />
            <p className="text-sm text-gray-500">No hay comprobantes emitidos en {variosDias ? 'estas fechas' : 'esta fecha'}.</p>
          </div>
        )}
        {!isLoading && (data?.items?.length ?? 0) > 0 && visibles.length === 0 && (
          <p className="text-sm text-gray-500 text-center mt-8">Ningún comprobante con ese estado.</p>
        )}

        {visibles.length > 0 && (
          <div className="bg-card border border-gray-800/50 rounded-2xl overflow-hidden">
            {/* Encabezado de columnas (pantallas medianas y grandes) */}
            <div className="hidden md:grid grid-cols-[7rem_10rem_minmax(0,1fr)_5rem_6.5rem_13rem] gap-3 px-4 py-2.5 border-b border-gray-800/60 text-[11px] font-bold uppercase tracking-wider text-gray-500">
              <span>{variosDias ? 'Fecha' : 'Hora'}</span>
              <span>Comprobante</span>
              <span>Cliente</span>
              <span className="text-right">IGV</span>
              <span className="text-right">Total</span>
              <span>Estado</span>
            </div>
            {visibles.map((c) => {
              const esFactura = c.tipoComprobante === 'Factura';
              const Icon = esFactura ? Building2 : User;
              const estado = infoEstado(c.estado);
              // Con un rango de varios días se muestra también el día ("12/11 14:30").
              const hora = parseFechaUtc(c.fechaEmision)?.toLocaleString('es-PE', {
                timeZone: 'America/Lima',
                ...(variosDias ? { day: '2-digit', month: '2-digit' } : {}),
                hour: '2-digit',
                minute: '2-digit',
              });
              const badge = <EstadoBadge estado={estado} mensaje={c.mensajeSunat} />;
              return (
                <button
                  key={c.comprobanteId}
                  onClick={() => abrir(c.comprobanteId)}
                  className="w-full text-left border-b border-gray-800/40 last:border-b-0 hover:bg-cardHighlight/60 transition-colors"
                >
                  {/* Celular: tarjeta compacta, con el estado siempre visible */}
                  <div className="md:hidden flex items-center gap-3 px-3.5 py-3">
                    <div className={`w-9 h-9 rounded-xl flex items-center justify-center flex-shrink-0 ${esFactura ? 'bg-sky-950/60 text-sky-400' : 'bg-emerald-950/60 text-accentGreen'}`}>
                      <Icon size={16} />
                    </div>
                    <div className="flex-1 min-w-0">
                      <p className="font-extrabold text-ink text-sm font-mono">{c.numero}</p>
                      <p className="text-xs text-gray-500 truncate">{hora} · {c.clienteNombre}</p>
                    </div>
                    <div className="text-right flex flex-col items-end gap-1">
                      <p className={`font-bold text-sm ${estado.valido ? 'text-ink' : 'text-gray-500 line-through'}`}>S/ {c.total.toFixed(2)}</p>
                      {badge}
                    </div>
                  </div>

                  {/* Monitor: fila de tabla */}
                  <div className="hidden md:grid grid-cols-[7rem_10rem_minmax(0,1fr)_5rem_6.5rem_13rem] gap-3 items-center px-4 py-2.5 text-sm">
                    <span className="text-gray-400 tabular-nums whitespace-nowrap">{hora}</span>
                    <span className="flex items-center gap-2 font-mono font-extrabold text-ink">
                      <Icon size={14} className={esFactura ? 'text-sky-400' : 'text-accentGreen'} />
                      {c.numero}
                    </span>
                    <span className="min-w-0 truncate text-gray-300">
                      {c.clienteNombre}
                      {c.clienteNumeroDocumento && <span className="text-gray-500"> · {c.clienteNumeroDocumento}</span>}
                    </span>
                    <span className="text-right text-gray-400 tabular-nums">{c.igv.toFixed(2)}</span>
                    <span className={`text-right font-bold tabular-nums ${estado.valido ? 'text-ink' : 'text-gray-500 line-through'}`}>S/ {c.total.toFixed(2)}</span>
                    <span>{badge}</span>
                  </div>
                </button>
              );
            })}
          </div>
        )}
      </div>

      <AnimatePresence>
        {seleccionado && (
          <ComprobanteElectronico
            comprobante={seleccionado}
            onClose={() => setSeleccionado(null)}
            onActualizado={(c) => {
              setSeleccionado(c);
              cargar();
            }}
            onReemitir={(c) => {
              setSeleccionado(null);
              setReemision(c);
            }}
          />
        )}
      </AnimatePresence>

      <AnimatePresence>
        {reemision && (
          <EmitirComprobanteModal
            pedido={{
              id: reemision.pedidoId,
              total: reemision.total,
              salonNombre: reemision.salonNombre,
              mesaNumero: reemision.mesaNumero,
            }}
            inicial={reemision}
            onEmitido={(c) => {
              setReemision(null);
              setSeleccionado(c);
              cargar();
            }}
            onClose={() => setReemision(null)}
          />
        )}
      </AnimatePresence>
    </div>
  );
}
