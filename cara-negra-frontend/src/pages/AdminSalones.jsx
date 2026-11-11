import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { ChevronLeft, Plus, Pencil, Ban, RotateCcw, X, Trees, Table2, ChevronRight } from 'lucide-react';
import toast from 'react-hot-toast';
import { getSalones, createSalon, updateSalon, deleteSalon } from '../services/salonesService';

// Nombres típicos de áreas en un restaurante campestre, para crear salones con un toque.
const SUGERENCIAS = ['Salón principal', 'Terraza', 'Jardín', 'Zona de piscina', 'Kiosko', 'Zona de juegos'];

const inputCls =
  'w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-ink placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm';

/**
 * Administración de salones / áreas del restaurante. Cada mesa pertenece a un salón; el mozo ve
 * las mesas agrupadas por salón (pestañas) y cocina/caja/tickets muestran "Salón · Mesa N".
 */
export default function AdminSalones() {
  const navigate = useNavigate();
  const [salones, setSalones] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [busyId, setBusyId] = useState(null);
  const [form, setForm] = useState(null); // { mode: 'crear'|'editar', data }
  const [isSaving, setIsSaving] = useState(false);

  const cargar = useCallback(() => {
    setIsLoading(true);
    getSalones()
      .then(setSalones)
      .catch((err) => toast.error(err.message))
      .finally(() => setIsLoading(false));
  }, []);

  useEffect(() => {
    cargar();
  }, [cargar]);

  const siguienteOrden = salones.length ? Math.max(...salones.map((s) => s.orden)) + 1 : 1;

  const abrirNuevo = () =>
    setForm({ mode: 'crear', data: { nombre: '', descripcion: '', orden: siguienteOrden } });

  const abrirEditar = (s) =>
    setForm({ mode: 'editar', data: { id: s.id, nombre: s.nombre, descripcion: s.descripcion, orden: s.orden, estaActivo: s.estaActivo } });

  const guardar = async () => {
    const nombre = form.data.nombre.trim();
    if (!nombre) {
      toast.error('Ingresa el nombre del salón');
      return;
    }
    setIsSaving(true);
    try {
      if (form.mode === 'crear') {
        await createSalon({ ...form.data, nombre });
        toast.success(`Salón "${nombre}" creado`);
      } else {
        await updateSalon(form.data.id, { ...form.data, nombre });
        toast.success('Salón actualizado');
      }
      setForm(null);
      cargar();
    } catch (err) {
      toast.error(err.message);
    } finally {
      setIsSaving(false);
    }
  };

  const toggleActivo = async (s) => {
    setBusyId(s.id);
    try {
      if (s.estaActivo) {
        await deleteSalon(s.id);
        toast.success(`"${s.nombre}" desactivado: sus mesas ya no se muestran al mozo`);
      } else {
        await updateSalon(s.id, { ...s, estaActivo: true });
        toast.success(`"${s.nombre}" reactivado`);
      }
      cargar();
    } catch (err) {
      toast.error(err.message);
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
          <h1 className="text-2xl font-extrabold tracking-tight">Salones y áreas</h1>
          <p className="text-xs text-gray-500 mt-0.5">Terraza, jardín, salón principal… cada mesa pertenece a uno</p>
        </div>
      </div>

      <div className="px-5 space-y-3">
        <motion.button
          whileTap={{ scale: 0.97 }}
          onClick={abrirNuevo}
          className="w-full flex items-center justify-center gap-2 py-3.5 rounded-2xl border border-dashed border-gray-700 text-gray-400 hover:text-ink hover:border-primary/50 transition-colors font-semibold text-sm"
        >
          <Plus size={16} />
          Nuevo salón
        </motion.button>

        {isLoading ? (
          <p className="text-sm text-gray-600">Cargando…</p>
        ) : salones.length === 0 ? (
          <div className="flex flex-col items-center text-center py-10">
            <Trees size={30} className="text-gray-700 mb-3" />
            <p className="text-gray-400 text-sm font-semibold">Aún no hay salones</p>
            <p className="text-gray-600 text-xs mt-1 max-w-xs">
              Crea primero las áreas del restaurante (p.ej. Terraza, Jardín) y luego asígnales mesas.
            </p>
          </div>
        ) : (
          salones.map((s) => (
            <div
              key={s.id}
              className={`bg-card border rounded-2xl p-4 flex items-center gap-4 ${
                s.estaActivo ? 'border-gray-800/50' : 'border-gray-800/30 opacity-60'
              }`}
            >
              <div className="w-12 h-12 rounded-2xl bg-emerald-950/50 border border-emerald-900/40 flex items-center justify-center shrink-0">
                <Trees size={22} className="text-accentGreen" />
              </div>
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
                  <p className="font-bold text-ink truncate">{s.nombre}</p>
                  {!s.estaActivo && (
                    <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-gray-800 text-gray-400">Inactivo</span>
                  )}
                </div>
                <p className="text-xs text-gray-500 mt-0.5 truncate">
                  {s.cantidadMesas} {s.cantidadMesas === 1 ? 'mesa' : 'mesas'}
                  {s.descripcion ? ` · ${s.descripcion}` : ''}
                </p>
              </div>
              <div className="flex items-center gap-1">
                <button
                  onClick={() => abrirEditar(s)}
                  className="p-2 hover:bg-cardHighlight rounded-full text-gray-500 hover:text-ink transition-colors"
                  title="Editar"
                >
                  <Pencil size={15} />
                </button>
                <button
                  onClick={() => toggleActivo(s)}
                  disabled={busyId === s.id}
                  className={`p-2 rounded-full transition-colors disabled:opacity-50 ${
                    s.estaActivo ? 'hover:bg-red-500/20 text-gray-500 hover:text-red-500' : 'hover:bg-emerald-500/20 text-gray-500 hover:text-emerald-500'
                  }`}
                  title={s.estaActivo ? 'Desactivar' : 'Reactivar'}
                >
                  {s.estaActivo ? <Ban size={15} /> : <RotateCcw size={15} />}
                </button>
                <button
                  onClick={() => navigate(`/admin/mesas?salon=${s.id}`)}
                  className="p-2 hover:bg-cardHighlight rounded-full text-gray-500 hover:text-ink transition-colors"
                  title="Ver mesas del salón"
                >
                  <ChevronRight size={16} />
                </button>
              </div>
            </div>
          ))
        )}

        {salones.length > 0 && (
          <button
            onClick={() => navigate('/admin/mesas')}
            className="w-full flex items-center justify-center gap-2 py-3 rounded-2xl bg-card border border-gray-800/50 text-sm font-bold text-gray-300 hover:border-primary/40"
          >
            <Table2 size={16} /> Administrar mesas
          </button>
        )}
      </div>

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
                  {form.mode === 'crear' ? 'Nuevo salón' : 'Editar salón'}
                </h2>
                <button onClick={() => setForm(null)} className="p-2 bg-background/60 rounded-full hover:bg-gray-700 transition-colors">
                  <X size={18} className="text-gray-400" />
                </button>
              </div>

              <form onSubmit={(e) => { e.preventDefault(); guardar(); }} className="px-5 py-4 space-y-4 pb-8">
                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Nombre</label>
                  <input
                    type="text"
                    maxLength={50}
                    placeholder="Ej: Terraza"
                    value={form.data.nombre}
                    onChange={(e) => setForm((f) => ({ ...f, data: { ...f.data, nombre: e.target.value } }))}
                    autoFocus
                    className={inputCls}
                  />
                  {form.mode === 'crear' && (
                    <div className="flex flex-wrap gap-1.5 mt-2">
                      {SUGERENCIAS.filter((n) => !salones.some((s) => s.nombre.toLowerCase() === n.toLowerCase())).map((n) => (
                        <button
                          type="button"
                          key={n}
                          onClick={() => setForm((f) => ({ ...f, data: { ...f.data, nombre: n } }))}
                          className="px-2.5 py-1 rounded-full text-[11px] font-semibold bg-background border border-gray-800 text-gray-400 hover:text-ink hover:border-gray-600"
                        >
                          {n}
                        </button>
                      ))}
                    </div>
                  )}
                </div>

                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Descripción (opcional)</label>
                  <input
                    type="text"
                    maxLength={200}
                    placeholder="Ej: Área techada junto al jardín"
                    value={form.data.descripcion}
                    onChange={(e) => setForm((f) => ({ ...f, data: { ...f.data, descripcion: e.target.value } }))}
                    className={inputCls}
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Orden en pantalla</label>
                  <input
                    type="number"
                    min={0}
                    value={form.data.orden}
                    onChange={(e) => setForm((f) => ({ ...f, data: { ...f.data, orden: e.target.value } }))}
                    className={inputCls}
                  />
                  <p className="text-xs text-gray-600 mt-1.5">Las pestañas del mozo se muestran de menor a mayor.</p>
                </div>

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
