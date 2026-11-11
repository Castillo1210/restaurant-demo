import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { ChevronLeft, Smartphone, ShieldCheck, Trash2, TabletSmartphone, Info } from 'lucide-react';
import toast from 'react-hot-toast';
import { getDispositivos, registrarEsteDispositivo, revocarDispositivo } from '../services/dispositivosService';
import { getPantallaPin, getTokenDispositivo, setTokenDispositivo } from '../services/authService';
import { parseFechaUtc } from '../utils/documentos';

const inputCls =
  'w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-ink placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm';

const OPCIONES_CIERRE = [
  { valor: 0, texto: 'Nunca (celular personal del mozo)' },
  { valor: 5, texto: 'A los 5 min sin uso (tablet compartida)' },
  { valor: 10, texto: 'A los 10 min sin uso' },
  { valor: 15, texto: 'A los 15 min sin uso' },
  { valor: 30, texto: 'A los 30 min sin uso' },
];

const textoCierre = (min) => (min > 0 ? `Cierra sesión del mozo a los ${min} min sin uso` : 'No cierra sesión sola');

const fecha = (valor) => {
  const d = parseFechaUtc(valor);
  return d ? d.toLocaleString('es-PE', { timeZone: 'America/Lima', dateStyle: 'short', timeStyle: 'short' }) : '—';
};

/**
 * Dispositivos (celulares / tablets) habilitados para el ingreso rápido con PIN.
 * Se registran DESDE el propio dispositivo: el ADMIN entra ahí con su contraseña, abre esta
 * pantalla y pulsa "Registrar este dispositivo". Desde otro equipo no se puede entrar con PIN.
 */
export default function AdminDispositivos() {
  const navigate = useNavigate();
  const [dispositivos, setDispositivos] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [esteId, setEsteId] = useState(null);
  const [nombre, setNombre] = useState('');
  const [cierre, setCierre] = useState(0);
  const [isSaving, setIsSaving] = useState(false);
  const [confirmarId, setConfirmarId] = useState(null);

  const cargar = useCallback(async () => {
    setIsLoading(true);
    try {
      const [lista, pantalla] = await Promise.all([
        getDispositivos(),
        getTokenDispositivo() ? getPantallaPin().catch(() => null) : Promise.resolve(null),
      ]);
      setDispositivos(lista);
      setEsteId(pantalla?.dispositivoId ?? null);
    } catch (err) {
      toast.error(err.message);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    cargar();
  }, [cargar]);

  const registrar = async (e) => {
    e.preventDefault();
    if (!nombre.trim()) {
      toast.error('Ponle un nombre, por ejemplo "Tablet terraza"');
      return;
    }
    setIsSaving(true);
    try {
      const d = await registrarEsteDispositivo({ nombre: nombre.trim(), cierreInactividadMinutos: Number(cierre) });
      toast.success(`"${d.nombre}" registrado. Ya se puede entrar con PIN desde aquí.`);
      setNombre('');
      setCierre(0);
      cargar();
    } catch (err) {
      toast.error(err.message);
    } finally {
      setIsSaving(false);
    }
  };

  const revocar = async (d) => {
    if (confirmarId !== d.dispositivoId) {
      setConfirmarId(d.dispositivoId);
      return;
    }
    setConfirmarId(null);
    try {
      await revocarDispositivo(d.dispositivoId);
      if (d.dispositivoId === esteId) setTokenDispositivo(null);
      toast.success(`"${d.nombre}" ya no puede entrar con PIN`);
      cargar();
    } catch (err) {
      toast.error(err.message);
    }
  };

  const esteRegistrado = esteId !== null && dispositivos.some((d) => d.dispositivoId === esteId);

  return (
    <div className="min-h-screen bg-background pb-10">
      <div className="px-5 pt-6 pb-4 flex items-center gap-3">
        <motion.button
          whileTap={{ scale: 0.9 }}
          onClick={() => navigate('/admin')}
          className="p-2 bg-card rounded-full hover:bg-cardHighlight border border-gray-800/50"
        >
          <ChevronLeft size={22} />
        </motion.button>
        <div>
          <h1 className="text-2xl font-extrabold tracking-tight">Dispositivos con PIN</h1>
          <p className="text-xs text-gray-500 mt-0.5">Celulares y tablets donde los mozos entran con PIN</p>
        </div>
      </div>

      <div className="px-5 space-y-4 max-w-2xl">
        {/* Este dispositivo */}
        <section className="bg-card border border-gray-800/50 rounded-3xl p-4 space-y-3">
          <h2 className="text-sm font-extrabold text-ink flex items-center gap-2">
            <TabletSmartphone size={16} className="text-primary" /> Este dispositivo
          </h2>

          {isLoading ? (
            <p className="text-sm text-gray-600">Cargando…</p>
          ) : esteRegistrado ? (
            <p className="text-sm text-emerald-400 flex items-center gap-2">
              <ShieldCheck size={16} /> Registrado como “{dispositivos.find((d) => d.dispositivoId === esteId)?.nombre}”.
              Al cerrar sesión aquí aparecerá el ingreso con PIN.
            </p>
          ) : (
            <form onSubmit={registrar} className="space-y-3">
              <p className="text-xs text-gray-400">
                Este navegador aún no está registrado. Regístralo solo si es un celular o tablet del
                restaurante (o el celular de un mozo).
              </p>
              <div>
                <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Nombre</label>
                <input
                  value={nombre}
                  onChange={(e) => setNombre(e.target.value)}
                  maxLength={60}
                  placeholder='Ej: "Tablet terraza" o "Celular de Juan"'
                  className={inputCls}
                />
              </div>
              <div>
                <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Cerrar sesión del mozo</label>
                <select value={cierre} onChange={(e) => setCierre(Number(e.target.value))} className={inputCls}>
                  {OPCIONES_CIERRE.map((o) => (
                    <option key={o.valor} value={o.valor}>{o.texto}</option>
                  ))}
                </select>
              </div>
              <motion.button
                whileTap={{ scale: 0.97 }}
                type="submit"
                disabled={isSaving}
                className="w-full bg-primary hover:bg-primaryHover disabled:opacity-60 text-white font-bold py-3.5 rounded-2xl transition-all shadow-glow-orange text-sm"
              >
                {isSaving ? 'Registrando…' : 'Registrar este dispositivo'}
              </motion.button>
            </form>
          )}
        </section>

        {/* Cómo funciona */}
        <section className="bg-card/60 border border-gray-800/40 rounded-3xl p-4 text-xs text-gray-400 space-y-1.5">
          <p className="font-bold text-gray-300 flex items-center gap-1.5"><Info size={13} /> Cómo se habilita</p>
          <p>1. En el celular o tablet, entra con tu usuario ADMIN y abre esta pantalla.</p>
          <p>2. Pulsa <b>Registrar este dispositivo</b> y cierra sesión.</p>
          <p>3. En <b>Admin → Usuarios</b> asigna un PIN de 4 dígitos a cada mozo (o cocina).</p>
          <p>Desde equipos no registrados se sigue pidiendo usuario y contraseña. CAJERO y ADMIN siempre usan contraseña.</p>
        </section>

        {/* Lista */}
        <section className="space-y-2">
          <h2 className="text-xs font-bold text-gray-500 uppercase tracking-wider px-1">Registrados ({dispositivos.length})</h2>
          {!isLoading && dispositivos.length === 0 && (
            <p className="text-sm text-gray-600 px-1">Todavía no hay dispositivos registrados.</p>
          )}
          {dispositivos.map((d) => (
            <div key={d.dispositivoId} className="bg-card border border-gray-800/50 rounded-2xl p-4 flex items-center gap-3">
              <div className="w-10 h-10 bg-primary/15 rounded-xl flex items-center justify-center flex-shrink-0">
                <Smartphone size={18} className="text-primary" />
              </div>
              <div className="flex-1 min-w-0">
                <p className="font-bold text-ink text-sm truncate">
                  {d.nombre}
                  {d.dispositivoId === esteId && (
                    <span className="ml-2 text-[10px] font-bold uppercase tracking-wider text-emerald-400">Este</span>
                  )}
                </p>
                <p className="text-[11px] text-gray-500">{textoCierre(d.cierreInactividadMinutos)}</p>
                <p className="text-[11px] text-gray-600">
                  Último uso: {d.ultimoUsoEn ? fecha(d.ultimoUsoEn) : 'nunca'} · Registrado {fecha(d.creadoEn)}
                  {d.registradoPor ? ` por ${d.registradoPor}` : ''}
                </p>
              </div>
              <button
                onClick={() => revocar(d)}
                onBlur={() => setConfirmarId(null)}
                className={`flex-shrink-0 rounded-full transition-colors text-xs font-bold ${
                  confirmarId === d.dispositivoId
                    ? 'px-3 py-2 bg-red-500/20 text-red-400'
                    : 'p-2 text-gray-500 hover:bg-red-500/20 hover:text-red-500'
                }`}
                title="Revocar"
              >
                {confirmarId === d.dispositivoId ? '¿Revocar?' : <Trash2 size={15} />}
              </button>
            </div>
          ))}
        </section>
      </div>
    </div>
  );
}
