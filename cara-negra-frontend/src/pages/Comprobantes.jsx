import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { ChevronLeft, FileText, Building2, User, RefreshCw } from 'lucide-react';
import toast from 'react-hot-toast';
import { getComprobantes, getComprobanteById } from '../services/comprobantesService';
import ComprobanteElectronico from '../components/ComprobanteElectronico';
import { parseFechaUtc } from '../utils/documentos';

// Fecha de hoy en Lima como "yyyy-MM-dd" (para el <input type="date">).
const hoyLima = () => new Date().toLocaleDateString('en-CA', { timeZone: 'America/Lima' });

const FILTROS = [
  { value: '', label: 'Todos' },
  { value: 'Boleta', label: 'Boletas' },
  { value: 'Factura', label: 'Facturas' },
];

/**
 * Registro de comprobantes electrónicos emitidos (boletas y facturas) por día, con totales de
 * base imponible e IGV y opción de reimprimir. DEMO: comprobantes simulados, sin envío a SUNAT.
 */
export default function Comprobantes() {
  const navigate = useNavigate();
  const [fecha, setFecha] = useState(hoyLima());
  const [tipo, setTipo] = useState('');
  const [data, setData] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [seleccionado, setSeleccionado] = useState(null);

  const cargar = useCallback(() => {
    setIsLoading(true);
    getComprobantes({ fechaDesde: fecha, fechaHasta: fecha, tipo: tipo || undefined })
      .then(setData)
      .catch(() => toast.error('No se pudieron cargar los comprobantes'))
      .finally(() => setIsLoading(false));
  }, [fecha, tipo]);

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
          <p className="text-xs text-gray-500 mt-0.5">Boletas y facturas emitidas · modo demo (sin envío a SUNAT)</p>
        </div>
        <button onClick={cargar} className="p-2 bg-card rounded-full hover:bg-cardHighlight border border-gray-800/50" title="Actualizar">
          <RefreshCw size={18} className={isLoading ? 'animate-spin text-gray-500' : 'text-gray-300'} />
        </button>
      </div>

      <div className="px-5 flex flex-wrap gap-2 items-center">
        <input
          type="date"
          value={fecha}
          onChange={(e) => setFecha(e.target.value)}
          className="bg-card border border-gray-700/50 rounded-xl px-3 py-2 text-sm text-white focus:outline-none focus:border-primary [color-scheme:dark]"
        />
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
      </div>

      <div className="px-5 mt-4 grid grid-cols-2 sm:grid-cols-4 gap-2">
        {resumen.map((r) => (
          <div key={r.label} className="bg-card border border-gray-800/50 rounded-2xl p-3">
            <p className="text-[11px] text-gray-500 uppercase tracking-wider font-bold">{r.label}</p>
            <p className="text-lg font-extrabold text-white mt-0.5">{r.value}</p>
          </div>
        ))}
      </div>

      <div className="px-5 mt-3 flex justify-between items-center bg-orange-950/30 border border-orange-900/30 rounded-2xl mx-5 py-3">
        <span className="text-primary text-sm font-bold">Total facturado</span>
        <span className="text-primary font-extrabold text-xl">S/ {(data?.totalVendido ?? 0).toFixed(2)}</span>
      </div>

      <div className="px-5 mt-4 space-y-2">
        {!isLoading && (data?.items?.length ?? 0) === 0 && (
          <div className="flex flex-col items-center text-center mt-10">
            <FileText size={32} className="text-gray-700 mb-2" />
            <p className="text-sm text-gray-500">No hay comprobantes emitidos en esta fecha.</p>
          </div>
        )}

        {data?.items?.map((c) => {
          const esFactura = c.tipoComprobante === 'Factura';
          const Icon = esFactura ? Building2 : User;
          const hora = parseFechaUtc(c.fechaEmision)?.toLocaleTimeString('es-PE', {
            timeZone: 'America/Lima',
            hour: '2-digit',
            minute: '2-digit',
          });
          return (
            <motion.button
              key={c.comprobanteId}
              whileTap={{ scale: 0.98 }}
              onClick={() => abrir(c.comprobanteId)}
              className="w-full bg-card border border-gray-800/50 rounded-2xl p-3.5 flex items-center gap-3 text-left hover:border-primary/40 transition-colors"
            >
              <div className={`w-10 h-10 rounded-xl flex items-center justify-center ${esFactura ? 'bg-sky-950/60 text-sky-400' : 'bg-emerald-950/60 text-accentGreen'}`}>
                <Icon size={18} />
              </div>
              <div className="flex-1 min-w-0">
                <p className="font-extrabold text-white text-sm font-mono">{c.numero}</p>
                <p className="text-xs text-gray-500 truncate">
                  {c.clienteNombre}
                  {c.clienteNumeroDocumento ? ` · ${c.clienteNumeroDocumento}` : ''}
                </p>
              </div>
              <div className="text-right">
                <p className="text-white font-bold">S/ {c.total.toFixed(2)}</p>
                <p className="text-[11px] text-gray-500">{hora} · IGV {c.igv.toFixed(2)}</p>
              </div>
            </motion.button>
          );
        })}
      </div>

      <AnimatePresence>
        {seleccionado && (
          <ComprobanteElectronico comprobante={seleccionado} onClose={() => setSeleccionado(null)} />
        )}
      </AnimatePresence>
    </div>
  );
}
