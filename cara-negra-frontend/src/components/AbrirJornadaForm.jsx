import { useState } from 'react';
import { motion } from 'framer-motion';
import { Sun, Loader2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { abrirJornada } from '../services/jornadaService';
import { hoyLimaISO } from '../utils/formato';

const inputCls =
  'w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-ink placeholder-gray-600 focus:outline-none focus:border-primary transition-colors';

/**
 * Formulario para abrir la jornada (día operativo): fecha, fondo de caja y observación.
 * Lo usan la pantalla obligatoria al iniciar sesión (JornadaGate) y la página de jornada.
 */
export default function AbrirJornadaForm({ onAbierta, onOmitir, textoOmitir }) {
  const hoy = hoyLimaISO();
  const [fecha, setFecha] = useState(hoy);
  const [fondo, setFondo] = useState('');
  const [observacion, setObservacion] = useState('');
  const [enviando, setEnviando] = useState(false);

  const abrir = async (e) => {
    e.preventDefault();
    const fondoNum = fondo === '' ? 0 : Number(fondo);
    if (Number.isNaN(fondoNum) || fondoNum < 0) {
      toast.error('Ingresa un fondo de caja válido (puede ser 0)');
      return;
    }
    setEnviando(true);
    try {
      const jornada = await abrirJornada({
        fechaOperativa: fecha,
        fondoInicial: fondoNum,
        observacion: observacion.trim() || undefined,
      });
      toast.success('Jornada abierta. ¡Buen día de trabajo!');
      onAbierta?.(jornada);
    } catch (err) {
      toast.error(err.message);
    } finally {
      setEnviando(false);
    }
  };

  return (
    <form onSubmit={abrir} className="space-y-4">
      <div>
        <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Fecha de la jornada</label>
        <input
          type="date"
          value={fecha}
          max={hoy}
          onChange={(e) => setFecha(e.target.value)}
          className={`${inputCls} [color-scheme:dark]`}
        />
        {fecha !== hoy && <p className="text-[11px] text-accentYellow mt-1.5">Ojo: no es la fecha de hoy.</p>}
      </div>

      <div>
        <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Fondo de caja (efectivo inicial)</label>
        <div className="relative">
          <span className="absolute left-4 top-1/2 -translate-y-1/2 text-gray-500 font-bold">S/</span>
          <input
            type="number"
            inputMode="decimal"
            step="0.10"
            min="0"
            value={fondo}
            onChange={(e) => setFondo(e.target.value)}
            placeholder="0.00"
            autoFocus
            className={`${inputCls} pl-11 text-lg font-bold`}
          />
        </div>
        <p className="text-[11px] text-gray-500 mt-1.5">El sencillo con el que empieza la caja para dar vuelto. Se suma al efectivo esperado del cierre.</p>
      </div>

      <div>
        <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Observación (opcional)</label>
        <input
          value={observacion}
          onChange={(e) => setObservacion(e.target.value)}
          maxLength={300}
          placeholder="Ej: feriado, evento privado…"
          className={`${inputCls} text-sm`}
        />
      </div>

      <motion.button
        whileTap={{ scale: 0.97 }}
        type="submit"
        disabled={enviando}
        className="w-full bg-primary hover:bg-primaryHover disabled:opacity-60 text-white font-bold py-4 rounded-2xl transition-all shadow-glow-orange flex items-center justify-center gap-2"
      >
        {enviando ? <Loader2 size={18} className="animate-spin" /> : <Sun size={18} />}
        {enviando ? 'Abriendo…' : 'Abrir jornada'}
      </motion.button>

      {onOmitir && (
        <button type="button" onClick={onOmitir} className="w-full text-sm text-gray-500 hover:text-gray-300 font-semibold py-1">
          {textoOmitir ?? 'Ahora no'}
        </button>
      )}
    </form>
  );
}
