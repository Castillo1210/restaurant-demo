import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { ChevronLeft, Plus, Pencil, Ban, RotateCcw, X, ChevronUp, ChevronDown, Utensils } from 'lucide-react';
import toast from 'react-hot-toast';
import { getCremas, createCrema, updateCrema, deleteCrema } from '../services/cremasService';

export default function AdminCremas() {
  const navigate = useNavigate();
  const [cremas, setCremas] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [busyId, setBusyId] = useState(null);

  const [form, setForm] = useState(null); // { mode: 'crear'|'editar', data }
  const [isSaving, setIsSaving] = useState(false);

  const cargarCremas = useCallback(() => {
    setIsLoading(true);
    // soloActivas=false: acá el admin necesita ver también las desactivadas para poder
    // reactivarlas.
    getCremas({ soloActivas: false })
      .then(setCremas)
      .catch((err) => toast.error(err.message ?? 'No se pudieron cargar las cremas'))
      .finally(() => setIsLoading(false));
  }, []);

  useEffect(() => {
    cargarCremas();
  }, [cargarCremas]);

  const abrirFormNueva = () => setForm({ mode: 'crear', data: { nombre: '' } });
  const abrirFormEditar = (c) =>
    setForm({ mode: 'editar', data: { id: c.id, nombre: c.nombre, orden: c.orden, estaActivo: c.estaActivo } });

  const guardar = async () => {
    const nombre = form.data.nombre.trim();
    if (!nombre) {
      toast.error('Ingresa un nombre para la crema');
      return;
    }

    setIsSaving(true);
    try {
      if (form.mode === 'crear') {
        await createCrema(nombre);
        toast.success('Crema creada');
      } else {
        await updateCrema(form.data.id, { nombre, orden: form.data.orden, estaActivo: form.data.estaActivo });
        toast.success('Crema actualizada');
      }
      setForm(null);
      cargarCremas();
    } catch (err) {
      toast.error(err.message ?? 'No se pudo guardar la crema');
    } finally {
      setIsSaving(false);
    }
  };

  const toggleActiva = async (c) => {
    setBusyId(c.id);
    try {
      if (c.estaActivo) {
        await deleteCrema(c.id);
        toast.success('Crema desactivada');
      } else {
        await updateCrema(c.id, { nombre: c.nombre, orden: c.orden, estaActivo: true });
        toast.success('Crema reactivada');
      }
      cargarCremas();
    } catch (err) {
      toast.error(err.message ?? 'No se pudo actualizar la crema');
    } finally {
      setBusyId(null);
    }
  };

  // Sube o baja una crema en la lista intercambiando su "orden" con el de la vecina.
  const mover = async (index, direccion) => {
    const otroIndex = index + direccion;
    if (otroIndex < 0 || otroIndex >= cremas.length) return;

    const actual = cremas[index];
    const vecina = cremas[otroIndex];
    setBusyId(actual.id);
    try {
      await Promise.all([
        updateCrema(actual.id, { nombre: actual.nombre, orden: vecina.orden, estaActivo: actual.estaActivo }),
        updateCrema(vecina.id, { nombre: vecina.nombre, orden: actual.orden, estaActivo: vecina.estaActivo }),
      ]);
      cargarCremas();
    } catch (err) {
      toast.error(err.message ?? 'No se pudo reordenar');
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
          <h1 className="text-2xl font-extrabold tracking-tight">Cremas</h1>
          <p className="text-xs text-gray-500 mt-0.5">Chips de cremas/toppings disponibles al tomar un pedido</p>
        </div>
      </div>

      <div className="px-5 space-y-3">
        <motion.button
          whileTap={{ scale: 0.97 }}
          onClick={abrirFormNueva}
          className="w-full flex items-center justify-center gap-2 py-3.5 rounded-2xl border border-dashed border-gray-700 text-gray-400 hover:text-white hover:border-primary/50 transition-colors font-semibold text-sm"
        >
          <Plus size={16} />
          Nueva crema
        </motion.button>

        {isLoading ? (
          <p className="text-sm text-gray-600">Cargando…</p>
        ) : cremas.length === 0 ? (
          <div className="flex flex-col items-center text-center py-10">
            <Utensils size={28} className="text-gray-700 mb-3" />
            <p className="text-gray-500 text-sm">Aún no hay cremas creadas.</p>
          </div>
        ) : (
          <div className="space-y-2">
            {cremas.map((c, index) => (
              <div
                key={c.id}
                className={`bg-card border rounded-2xl p-3.5 flex items-center gap-3 ${
                  c.estaActivo ? 'border-gray-800/50' : 'border-gray-800/30 opacity-60'
                }`}
              >
                <div className="flex flex-col">
                  <button
                    onClick={() => mover(index, -1)}
                    disabled={index === 0 || busyId === c.id}
                    className="p-0.5 text-gray-600 hover:text-white disabled:opacity-30 transition-colors"
                    title="Subir"
                  >
                    <ChevronUp size={14} />
                  </button>
                  <button
                    onClick={() => mover(index, 1)}
                    disabled={index === cremas.length - 1 || busyId === c.id}
                    className="p-0.5 text-gray-600 hover:text-white disabled:opacity-30 transition-colors"
                    title="Bajar"
                  >
                    <ChevronDown size={14} />
                  </button>
                </div>

                <p className="flex-1 font-bold text-white text-sm truncate">{c.nombre}</p>

                <div className="flex items-center gap-1 flex-shrink-0">
                  <button
                    onClick={() => abrirFormEditar(c)}
                    className="p-1.5 hover:bg-cardHighlight rounded-full text-gray-500 hover:text-white transition-colors"
                    title="Editar"
                  >
                    <Pencil size={13} />
                  </button>
                  <button
                    onClick={() => toggleActiva(c)}
                    disabled={busyId === c.id}
                    className={`p-1.5 rounded-full transition-colors disabled:opacity-50 ${
                      !c.estaActivo
                        ? 'hover:bg-emerald-500/20 text-gray-500 hover:text-emerald-500'
                        : 'hover:bg-red-500/20 text-gray-500 hover:text-red-500'
                    }`}
                    title={!c.estaActivo ? 'Reactivar' : 'Desactivar'}
                  >
                    {!c.estaActivo ? <RotateCcw size={13} /> : <Ban size={13} />}
                  </button>
                </div>
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
                  {form.mode === 'crear' ? 'Nueva crema' : 'Editar crema'}
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
                    Nombre
                  </label>
                  <input
                    type="text"
                    maxLength={50}
                    placeholder="Ej: Mayonesa, BBQ, Sin cebolla"
                    value={form.data.nombre}
                    onChange={(e) => setForm({ ...form, data: { ...form.data, nombre: e.target.value } })}
                    autoFocus
                    className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white focus:outline-none focus:border-primary transition-colors text-sm"
                  />
                </div>

                {form.mode === 'editar' && (
                  <label className="flex items-center justify-between bg-background border border-gray-800/50 rounded-2xl px-4 py-3.5 cursor-pointer">
                    <span className="text-sm text-gray-300 font-semibold">Crema activa</span>
                    <input
                      type="checkbox"
                      checked={form.data.estaActivo}
                      onChange={(e) => setForm({ ...form, data: { ...form.data, estaActivo: e.target.checked } })}
                      className="w-5 h-5 accent-primary"
                    />
                  </label>
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
