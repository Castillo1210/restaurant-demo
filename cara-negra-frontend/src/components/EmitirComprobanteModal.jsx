import { useState } from 'react';
import { motion } from 'framer-motion';
import { FileText, Building2, User, X, ReceiptText, CheckCircle2, AlertCircle, Wand2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { emitirComprobante } from '../services/comprobantesService';
import { esDniValido, esRucValido, esCarnetExtranjeriaValido } from '../utils/documentos';

// SUNAT exige identificar al cliente en boletas mayores a S/ 700.
const MONTO_BOLETA_REQUIERE_DOC = 700;

const TIPOS_DOC_BOLETA = [
  { value: 'SinDocumento', label: 'Sin documento' },
  { value: 'DNI', label: 'DNI' },
  { value: 'CarnetExtranjeria', label: 'C.E.' },
];

// Datos ficticios para agilizar la presentación (RUC con dígito verificador válido).
const EJEMPLO_BOLETA = { tipoDoc: 'DNI', numero: '45678912', nombre: 'Juan Pérez Rojas' };
const EJEMPLO_FACTURA = { ruc: '20555666773', razonSocial: 'Cliente Empresa Demo S.A.C.', direccion: 'Jr. Los Olivos 456 - Lima' };

const inputCls =
  'w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3 text-white placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm';

/**
 * Se abre en Caja cuando un pedido queda pagado por completo: el cajero elige Boleta o Factura,
 * ingresa los datos del cliente y se emite el comprobante electrónico (SIMULADO en la demo).
 * También permite seguir con el recibo interno de siempre, sin comprobante.
 */
export default function EmitirComprobanteModal({ pedido, onEmitido, onReciboInterno, onClose }) {
  const [tipo, setTipo] = useState('Boleta');
  const [tipoDoc, setTipoDoc] = useState('SinDocumento');
  const [numeroDoc, setNumeroDoc] = useState('');
  const [nombre, setNombre] = useState('');
  const [ruc, setRuc] = useState('');
  const [razonSocial, setRazonSocial] = useState('');
  const [direccion, setDireccion] = useState('');
  const [isEmitiendo, setIsEmitiendo] = useState(false);

  const total = pedido?.total ?? 0;
  const requiereDoc = tipo === 'Boleta' && total > MONTO_BOLETA_REQUIERE_DOC;

  const docValido =
    tipoDoc === 'DNI' ? esDniValido(numeroDoc)
      : tipoDoc === 'CarnetExtranjeria' ? esCarnetExtranjeriaValido(numeroDoc)
        : true;
  const rucValido = esRucValido(ruc);

  const autocompletar = () => {
    if (tipo === 'Boleta') {
      setTipoDoc(EJEMPLO_BOLETA.tipoDoc);
      setNumeroDoc(EJEMPLO_BOLETA.numero);
      setNombre(EJEMPLO_BOLETA.nombre);
    } else {
      setRuc(EJEMPLO_FACTURA.ruc);
      setRazonSocial(EJEMPLO_FACTURA.razonSocial);
      setDireccion(EJEMPLO_FACTURA.direccion);
    }
  };

  const handleEmitir = async () => {
    let payload;
    if (tipo === 'Factura') {
      if (!rucValido) return toast.error('Ingresa un RUC válido (11 dígitos)');
      if (!razonSocial.trim()) return toast.error('Ingresa la razón social');
      payload = {
        pedidoId: pedido.id,
        tipoComprobante: 'Factura',
        clienteTipoDocumento: 'RUC',
        clienteNumeroDocumento: ruc,
        clienteNombre: razonSocial.trim(),
        clienteDireccion: direccion.trim() || null,
      };
    } else {
      if (requiereDoc && tipoDoc === 'SinDocumento') {
        return toast.error(`Para boletas mayores a S/ ${MONTO_BOLETA_REQUIERE_DOC} se requiere DNI o C.E.`);
      }
      if (tipoDoc !== 'SinDocumento') {
        if (!docValido) return toast.error(tipoDoc === 'DNI' ? 'El DNI debe tener 8 dígitos' : 'C.E. no válido');
        if (!nombre.trim()) return toast.error('Ingresa el nombre del cliente');
      }
      payload = {
        pedidoId: pedido.id,
        tipoComprobante: 'Boleta',
        clienteTipoDocumento: tipoDoc,
        clienteNumeroDocumento: tipoDoc === 'SinDocumento' ? null : numeroDoc,
        clienteNombre: nombre.trim() || null,
      };
    }

    setIsEmitiendo(true);
    try {
      const comprobante = await emitirComprobante(payload);
      toast.success(`${comprobante.tipoComprobante} ${comprobante.numero} emitida`);
      onEmitido(comprobante);
    } catch (err) {
      toast.error(err.message ?? 'No se pudo emitir el comprobante');
    } finally {
      setIsEmitiendo(false);
    }
  };

  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      className="fixed inset-0 z-[55] flex flex-col justify-end sm:justify-center sm:items-center"
    >
      <div className="absolute inset-0 bg-black/75 backdrop-blur-sm" onClick={onClose} />

      <motion.div
        initial={{ y: 40, opacity: 0 }}
        animate={{ y: 0, opacity: 1 }}
        exit={{ y: 40, opacity: 0 }}
        transition={{ type: 'spring', stiffness: 300, damping: 30 }}
        className="relative z-10 bg-card w-full sm:max-w-md rounded-t-3xl sm:rounded-3xl border-t sm:border border-gray-700/40 shadow-2xl flex flex-col"
        style={{ maxHeight: '94dvh' }}
      >
        <div className="px-5 pt-5 pb-3 flex justify-between items-start border-b border-gray-800/60">
          <div>
            <div className="flex items-center gap-2 text-accentGreen text-xs font-bold">
              <CheckCircle2 size={14} /> Pedido pagado · S/ {total.toFixed(2)}
            </div>
            <h2 className="text-xl font-extrabold tracking-tight mt-1">Comprobante electrónico</h2>
            <p className="text-xs text-gray-500">Mesa {pedido?.mesaNumero} · Pedido #{pedido?.id}</p>
          </div>
          <button onClick={onClose} className="p-2 bg-background/60 rounded-full hover:bg-gray-700 transition-colors">
            <X size={18} className="text-gray-400" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto px-5 py-4 space-y-4">
          {/* Tipo de comprobante */}
          <div className="grid grid-cols-2 gap-2">
            {[
              { value: 'Boleta', label: 'Boleta', icon: User, hint: 'Consumidor final' },
              { value: 'Factura', label: 'Factura', icon: Building2, hint: 'Empresa con RUC' },
            ].map((opt) => {
              const Icon = opt.icon;
              const activo = tipo === opt.value;
              return (
                <button
                  key={opt.value}
                  onClick={() => setTipo(opt.value)}
                  className={`flex flex-col items-center gap-1 py-3.5 rounded-2xl border transition-colors ${
                    activo ? 'bg-primary/20 border-primary text-primary' : 'bg-background border-gray-800/60 text-gray-400 hover:border-gray-600'
                  }`}
                >
                  <Icon size={20} />
                  <span className="font-extrabold text-sm">{opt.label}</span>
                  <span className="text-[10px] opacity-80">{opt.hint}</span>
                </button>
              );
            })}
          </div>

          {tipo === 'Boleta' ? (
            <div className="space-y-3">
              <div className="flex gap-2">
                {TIPOS_DOC_BOLETA.map((t) => (
                  <button
                    key={t.value}
                    onClick={() => setTipoDoc(t.value)}
                    className={`flex-1 py-2 rounded-xl border text-xs font-bold transition-colors ${
                      tipoDoc === t.value ? 'bg-white/10 border-white/40 text-white' : 'border-gray-800 text-gray-500 hover:border-gray-600'
                    }`}
                  >
                    {t.label}
                  </button>
                ))}
              </div>

              {tipoDoc !== 'SinDocumento' && (
                <input
                  value={numeroDoc}
                  onChange={(e) => setNumeroDoc(e.target.value.replace(tipoDoc === 'DNI' ? /\D/g : /[^A-Za-z0-9]/g, ''))}
                  maxLength={tipoDoc === 'DNI' ? 8 : 12}
                  inputMode={tipoDoc === 'DNI' ? 'numeric' : 'text'}
                  placeholder={tipoDoc === 'DNI' ? 'DNI (8 dígitos)' : 'Carné de extranjería'}
                  className={inputCls}
                />
              )}

              <input
                value={nombre}
                onChange={(e) => setNombre(e.target.value)}
                placeholder={tipoDoc === 'SinDocumento' ? 'Nombre (opcional) — "Clientes varios"' : 'Nombre del cliente'}
                className={inputCls}
              />

              {requiereDoc && (
                <p className="text-xs text-accentYellow flex items-start gap-1.5">
                  <AlertCircle size={14} className="mt-0.5 shrink-0" />
                  El total supera S/ {MONTO_BOLETA_REQUIERE_DOC}: SUNAT exige DNI o C.E. del cliente.
                </p>
              )}
            </div>
          ) : (
            <div className="space-y-3">
              <div className="relative">
                <input
                  value={ruc}
                  onChange={(e) => setRuc(e.target.value.replace(/\D/g, ''))}
                  maxLength={11}
                  inputMode="numeric"
                  placeholder="RUC (11 dígitos)"
                  className={inputCls}
                />
                {ruc.length === 11 && (
                  <span className={`absolute right-4 top-1/2 -translate-y-1/2 text-xs font-bold ${rucValido ? 'text-accentGreen' : 'text-red-500'}`}>
                    {rucValido ? 'RUC válido' : 'RUC inválido'}
                  </span>
                )}
              </div>
              <input value={razonSocial} onChange={(e) => setRazonSocial(e.target.value)} placeholder="Razón social" className={inputCls} />
              <input value={direccion} onChange={(e) => setDireccion(e.target.value)} placeholder="Dirección fiscal (opcional)" className={inputCls} />
            </div>
          )}

          <button onClick={autocompletar} className="text-xs text-gray-500 hover:text-gray-300 flex items-center gap-1.5">
            <Wand2 size={13} /> Autocompletar con datos de ejemplo (demo)
          </button>

          <p className="text-[11px] text-gray-500 bg-background/60 border border-gray-800/60 rounded-xl px-3 py-2">
            Modo demostración: el comprobante se genera con el formato de SUNAT (serie, IGV, QR) pero
            <span className="text-gray-300 font-semibold"> no se envía a SUNAT</span> y no tiene validez tributaria.
          </p>
        </div>

        <div className="px-5 pb-5 pt-3 space-y-2 border-t border-gray-800/60">
          <motion.button
            whileTap={{ scale: 0.97 }}
            onClick={handleEmitir}
            disabled={isEmitiendo}
            className="w-full bg-primary hover:bg-primaryHover disabled:opacity-60 text-white font-extrabold py-3.5 rounded-2xl flex justify-center items-center gap-2 transition-colors shadow-glow-orange"
          >
            {isEmitiendo ? (
              <span className="animate-pulse">Emitiendo…</span>
            ) : (
              <>
                <FileText size={18} />
                Emitir {tipo === 'Factura' ? 'factura' : 'boleta'} electrónica
              </>
            )}
          </motion.button>
          <button
            onClick={onReciboInterno}
            className="w-full py-3 rounded-2xl border border-gray-700/60 text-gray-400 hover:text-white hover:border-gray-500 text-sm font-bold flex items-center justify-center gap-2 transition-colors"
          >
            <ReceiptText size={16} />
            Solo recibo interno (sin comprobante)
          </button>
        </div>
      </motion.div>
    </motion.div>
  );
}
