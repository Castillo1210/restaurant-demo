import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { ChevronLeft, Plus, Pencil, Ban, RotateCcw, X, Table2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { getMesasAdmin, createMesa, updateMesa, deleteMesa } from '../services/mesasAdminService';

const ESTADOS = ['Disponible', 'Ocupada', 'Reservada', 'Mantenimiento'];

const ESTADO_STYLES = {
  Disponible: 'text-accentGreen',
  Ocupada: 'text-primary',
  Reservada: 'text-accentYellow',
  Mantenimiento: 'text-gray-500',
};

export default function AdminMesas() {
  const navigate = useNavigate();
  const [mesas, setMesas] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [busyId, setBusyId] = useState(null);

  const [form, setForm] = useState(null); // { mode: 'crear'|'editar', data }
  const [isSaving, setIsSaving] = useState(false);

  const cargarMesas = useCallback(() => {
    setIsLoading(true);
    getMesasAdmin()
      .then(setMesas)
      .catch((err) => toast.error(err.message ?? 'No se pudieron cargar las mesas'))
      .finally(() => setIsLoading(false));
  }, []);

  useEffect(() => {
    cargarMesas();
  }, [cargarMesas]);

  const abrirFormNueva = () => setForm({ mode: 'crear', data: { numeroMesa: '' } });
  const abrirFormEditar = (mesa) =>
    setForm({ mode: 'editar', data: { id: mesa.id, numeroMesa: String(mesa.numeroMesa), estado: mesa.estado } });

  const guardar = async () => {
    // El número de mesa es en realidad un código (puede tener letras, ej. "T1", "B-3"),
    // no necesariamente un número correlativo — se guarda tal cual como texto.
    const numero = form.data.numeroMesa.trim();
    if (!numero) {
      toast.error('Ingresa un número/código de mesa válido');
      return;
    }

    setIsSaving(true);
    try {
      if (form.mode === 'crear') {
        await createMesa(numero);
        toast.success('Mesa creada');
      } else {
        await updateMesa(form.data.id, { numeroMesa: numero, estado: form.data.estado });
        toast.success('Mesa actualizada');
      }
      setForm(null);
      cargarMesas();
    } catch (err) {
      toast.error(err.message ?? 'No se pudo guardar la mesa');
    } finally {
      setIsSaving(false);
    }
  };

  const toggleActiva = async (mesa) => {
    setBusyId(mesa.id);
    try {
      if (mesa.estado === 'Mantenimiento') {
        await updateMesa(mesa.id, { numeroMesa: mesa.numeroMesa, estado: 'Disponible' });
        toast.success('Mesa reactivada');
      } else {
        await deleteMesa(mesa.id);
        toast.success('Mesa puesta en mantenimiento');
      }
      cargarMesas();
    } catch (err) {
      toast.error(err.message ?? 'No se pudo actualizar la mesa');
    } finally {
      setBusyId(null);
    }
  };

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
          <h1 className="text-2xl font-extrabold tracking-tight">Mesas</h1>
          <p className="text-xs text-gray-500 mt-0.5">Crea y administra las mesas del salón</p>
        </div>
      </div>

      <div className="px-5 space-y-3">
        <motion.button
          whileTap={{ scale: 0.97 }}
          onClick={abrirFormNueva}
          className="w-full flex items-center justify-center gap-2 py-3.5 rounded-2xl border border-dashed border-gray-700 text-gray-400 hover:text-white hover:border-primary/50 transition-colors font-semibold text-sm"
        >
          <Plus size={16} />
          Nueva mesa
        </motion.button>

        {isLoading ? (
          <p className="text-sm text-gray-600">Cargando…</p>
        ) : mesas.length === 0 ? (
          <div className="flex flex-col items-center text-center py-10">
            <Table2 size={28} className="text-gray-700 mb-3" />
            <p className="text-gray-500 text-sm">Aún no hay mesas creadas.</p>
          </div>
        ) : (
          <div className="grid grid-cols-2 gap-3">
            {mesas.map((mesa) => (
              <div
                key={mesa.id}
                className={`bg-card border rounded-2xl p-4 ${
                  mesa.estado === 'Mantenimiento' ? 'border-gray-800/30 opacity-60' : 'border-gray-800/50'
                }`}
              >
                <div className="flex justify-between items-start mb-2">
                  <p className="text-2xl font-extrabold text-white">{mesa.numeroMesa}</p>
                  <div className="flex items-center gap-1">
                    <button
                      onClick={() => abrirFormEditar(mesa)}
                      className="p-1.5 hover:bg-cardHighlight rounded-full text-gray-500 hover:text-white transition-colors"
                      title="Editar"
                    >
                      <Pencil size={13} />
                    </button>
                    <button
                      onClick={() => toggleActiva(mesa)}
                      disabled={busyId === mesa.id}
                      className={`p-1.5 rounded-full transition-colors disabled:opacity-50 ${
                        mesa.estado === 'Mantenimiento'
                          ? 'hover:bg-emerald-500/20 text-gray-500 hover:text-emerald-500'
                          : 'hover:bg-red-500/20 text-gray-500 hover:text-red-500'
                      }`}
                      title={mesa.estado === 'Mantenimiento' ? 'Reactivar' : 'Poner en mantenimiento'}
                    >
                      {mesa.estado === 'Mantenimiento' ? <RotateCcw size={13} /> : <Ban size={13} />}
                    </button>
                  </div>
                </div>
                <p className={`text-xs font-bold ${ESTADO_STYLES[mesa.estado] ?? 'text-gray-400'}`}>
                  {mesa.estado}
                </p>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* ── Panel de formulario ─────────────────────────────────────── */}
      <AnimatePresence>
        {form && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-50 flex flex-col justify-end"
          >
            <motion.div className="absolute inset-0 bg-black/70 backdrop-blur-sm" onClick={() => setForm(null)} />

            <motion.div
              initial={{ y: '100%' }}
              animate={{ y: 0 }}
              exit={{ y: '100%' }}
              transition={{ type: 'spring', stiffness: 300, damping: 30 }}
              className="relative z-10 bg-card w-full rounded-t-3xl flex flex-col shadow-2xl border-t border-gray-700/40"
            >
              <div className="flex justify-center pt-3 pb-1">
                <div className="w-10 h-1 rounded-full bg-gray-700" />
              </div>

              <div className="px-5 pb-3 pt-2 flex justify-between items-start border-b border-gray-800/60">
                <h2 className="text-lg font-extrabold tracking-tight">
                  {form.mode === 'crear' ? 'Nueva mesa' : 'Editar mesa'}
                </h2>
                <motion.button
                  whileTap={{ scale: 0.9 }}
                  onClick={() => setForm(null)}
                  className="p-2 bg-background/60 rounded-full hover:bg-gray-700 transition-colors"
                >
                  <X size={18} className="text-gray-400" />
                </motion.button>
              </div>

              <form
                onSubmit={(e) => { e.preventDefault(); guardar(); }}
                className="px-5 py-4 space-y-4 pb-8"
              >
                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">
                    Número/código de mesa
                  </label>
                  <input
                    type="text"
                    maxLength={20}
                    placeholder="Ej: 1, T1, Terraza-3"
                    value={form.data.numeroMesa}
                    onChange={(e) => setForm({ ...form, data: { ...form.data, numeroMesa: e.target.value } })}
                    autoFocus
                    className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white focus:outline-none focus:border-primary transition-colors text-sm"
                  />
                </div>

                {form.mode === 'editar' && (
                  <div>
                    <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">
                      Estado
                    </label>
                    <select
                      value={form.data.estado}
                      onChange={(e) => setForm({ ...form, data: { ...form.data, estado: e.target.value } })}
                      className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white focus:outline-none focus:border-primary transition-colors text-sm"
                    >
                      {ESTADOS.map((e) => <option key={e} value={e}>{e}</option>)}
                    </select>
                    <p className="text-xs text-gray-600 mt-1.5">
                      Normalmente el sistema cambia el estado automáticamente al tomar o cobrar
                      un pedido. Cámbialo a mano solo si una mesa quedó en un estado incorrecto.
                    </p>
                  </div>
                )}

                <motion.button
                  whileTap={{ scale: 0.97 }}
                  type="submit"
                  disabled={isSaving}
                  className="w-full bg-primary hover:bg-primaryHover disabled:opacity-60 text-white font-bold py-4 rounded-2xl transition-all shadow-glow-orange text-sm mt-2"
                >
                  {isSaving ? 'Guardando…' : 'Guardar'}
                </motion.button>
              </form>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
