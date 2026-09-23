import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { ChevronLeft, Plus, Pencil, Ban, RotateCcw, X, Percent } from 'lucide-react';
import toast from 'react-hot-toast';
import { getDescuentos, createDescuento, updateDescuento, deleteDescuento } from '../services/descuentosService';

const formatValor = (d) => (d.esPorcentaje ? `${d.valor}%` : `S/ ${Number(d.valor).toFixed(2)}`);

const formatVigencia = (d) => {
  if (!d.fechaInicio && !d.fechaFin) return 'Sin fecha de vencimiento';
  const desde = d.fechaInicio ? new Date(d.fechaInicio).toLocaleDateString('es-PE') : '—';
  const hasta = d.fechaFin ? new Date(d.fechaFin).toLocaleDateString('es-PE') : '—';
  return `Del ${desde} al ${hasta}`;
};

// Los inputs type="date" trabajan con "YYYY-MM-DD"; el backend recibe/envía DateTime ISO.
const toDateInputValue = (iso) => (iso ? iso.slice(0, 10) : '');

const emptyForm = { nombre: '', esPorcentaje: true, valor: '', fechaInicio: '', fechaFin: '' };

export default function AdminDescuentos() {
  const navigate = useNavigate();
  const [descuentos, setDescuentos] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [busyId, setBusyId] = useState(null);

  const [form, setForm] = useState(null); // { mode: 'crear'|'editar', data }
  const [isSaving, setIsSaving] = useState(false);

  const cargarDescuentos = useCallback(() => {
    setIsLoading(true);
    getDescuentos()
      .then(setDescuentos)
      .catch((err) => toast.error(err.message ?? 'No se pudieron cargar los descuentos'))
      .finally(() => setIsLoading(false));
  }, []);

  useEffect(() => {
    cargarDescuentos();
  }, [cargarDescuentos]);

  const abrirFormNueva = () => setForm({ mode: 'crear', data: { ...emptyForm } });
  const abrirFormEditar = (d) =>
    setForm({
      mode: 'editar',
      data: {
        id: d.id,
        nombre: d.nombre,
        esPorcentaje: d.esPorcentaje,
        valor: String(d.valor),
        estaActivo: d.estaActivo,
        fechaInicio: toDateInputValue(d.fechaInicio),
        fechaFin: toDateInputValue(d.fechaFin),
      },
    });

  const guardar = async () => {
    const nombre = form.data.nombre.trim();
    const valorNum = parseFloat(form.data.valor);

    if (!nombre) {
      toast.error('Ingresa un nombre para el descuento');
      return;
    }
    if (!valorNum || valorNum <= 0) {
      toast.error('Ingresa un valor válido');
      return;
    }
    if (form.data.esPorcentaje && valorNum > 100) {
      toast.error('Un descuento porcentual no puede ser mayor a 100%');
      return;
    }
    if (form.data.fechaInicio && form.data.fechaFin && form.data.fechaFin < form.data.fechaInicio) {
      toast.error('La fecha de fin no puede ser anterior a la fecha de inicio');
      return;
    }

    const payload = {
      nombre,
      esPorcentaje: form.data.esPorcentaje,
      valor: valorNum,
      fechaInicio: form.data.fechaInicio || null,
      fechaFin: form.data.fechaFin || null,
    };

    setIsSaving(true);
    try {
      if (form.mode === 'crear') {
        await createDescuento(payload);
        toast.success('Descuento creado');
      } else {
        await updateDescuento(form.data.id, { ...payload, estaActivo: form.data.estaActivo });
        toast.success('Descuento actualizado');
      }
      setForm(null);
      cargarDescuentos();
    } catch (err) {
      toast.error(err.message ?? 'No se pudo guardar el descuento');
    } finally {
      setIsSaving(false);
    }
  };

  const toggleActivo = async (d) => {
    setBusyId(d.id);
    try {
      if (d.estaActivo) {
        await deleteDescuento(d.id);
        toast.success('Descuento desactivado');
      } else {
        await updateDescuento(d.id, {
          nombre: d.nombre,
          esPorcentaje: d.esPorcentaje,
          valor: d.valor,
          estaActivo: true,
          fechaInicio: d.fechaInicio,
          fechaFin: d.fechaFin,
        });
        toast.success('Descuento reactivado');
      }
      cargarDescuentos();
    } catch (err) {
      toast.error(err.message ?? 'No se pudo actualizar el descuento');
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
          <h1 className="text-2xl font-extrabold tracking-tight">Descuentos</h1>
          <p className="text-xs text-gray-500 mt-0.5">Catálogo de descuentos aplicables a un pedido</p>
        </div>
      </div>

      <div className="px-5 space-y-3">
        <motion.button
          whileTap={{ scale: 0.97 }}
          onClick={abrirFormNueva}
          className="w-full flex items-center justify-center gap-2 py-3.5 rounded-2xl border border-dashed border-gray-700 text-gray-400 hover:text-white hover:border-primary/50 transition-colors font-semibold text-sm"
        >
          <Plus size={16} />
          Nuevo descuento
        </motion.button>

        {isLoading ? (
          <p className="text-sm text-gray-600">Cargando…</p>
        ) : descuentos.length === 0 ? (
          <div className="flex flex-col items-center text-center py-10">
            <Percent size={28} className="text-gray-700 mb-3" />
            <p className="text-gray-500 text-sm">Aún no hay descuentos creados.</p>
          </div>
        ) : (
          <div className="space-y-2.5">
            {descuentos.map((d) => (
              <div
                key={d.id}
                className={`bg-card border rounded-2xl p-4 ${
                  d.estaActivo ? 'border-gray-800/50' : 'border-gray-800/30 opacity-60'
                }`}
              >
                <div className="flex justify-between items-start gap-3">
                  <div className="min-w-0">
                    <p className="font-bold text-white text-sm truncate">{d.nombre}</p>
                    <p className="text-primary font-extrabold text-lg mt-0.5">{formatValor(d)}</p>
                    <p className="text-xs text-gray-500 mt-1">{formatVigencia(d)}</p>
                  </div>
                  <div className="flex items-center gap-1 flex-shrink-0">
                    <button
                      onClick={() => abrirFormEditar(d)}
                      className="p-1.5 hover:bg-cardHighlight rounded-full text-gray-500 hover:text-white transition-colors"
                      title="Editar"
                    >
                      <Pencil size={13} />
                    </button>
                    <button
                      onClick={() => toggleActivo(d)}
                      disabled={busyId === d.id}
                      className={`p-1.5 rounded-full transition-colors disabled:opacity-50 ${
                        !d.estaActivo
                          ? 'hover:bg-emerald-500/20 text-gray-500 hover:text-emerald-500'
                          : 'hover:bg-red-500/20 text-gray-500 hover:text-red-500'
                      }`}
                      title={!d.estaActivo ? 'Reactivar' : 'Desactivar'}
                    >
                      {!d.estaActivo ? <RotateCcw size={13} /> : <Ban size={13} />}
                    </button>
                  </div>
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
              style={{ maxHeight: '92dvh' }}
            >
              <div className="flex justify-center pt-3 pb-1">
                <div className="w-10 h-1 rounded-full bg-gray-700" />
              </div>

              <div className="px-5 pb-3 pt-2 flex justify-between items-start border-b border-gray-800/60">
                <h2 className="text-lg font-extrabold tracking-tight">
                  {form.mode === 'crear' ? 'Nuevo descuento' : 'Editar descuento'}
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
                className="px-5 py-4 space-y-4 pb-8 overflow-y-auto"
              >
                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">
                    Nombre
                  </label>
                  <input
                    type="text"
                    maxLength={100}
                    placeholder="Ej: Descuento empleado, Promoción 2x1"
                    value={form.data.nombre}
                    onChange={(e) => setForm({ ...form, data: { ...form.data, nombre: e.target.value } })}
                    autoFocus
                    className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white focus:outline-none focus:border-primary transition-colors text-sm"
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">
                    Tipo de descuento
                  </label>
                  <div className="grid grid-cols-2 gap-2">
                    <button
                      type="button"
                      onClick={() => setForm({ ...form, data: { ...form.data, esPorcentaje: true } })}
                      className={`py-3 rounded-2xl border text-sm font-semibold transition-colors ${
                        form.data.esPorcentaje
                          ? 'bg-primary/20 border-primary text-primary'
                          : 'bg-background border-gray-800/50 text-gray-400 hover:border-gray-600'
                      }`}
                    >
                      Porcentaje (%)
                    </button>
                    <button
                      type="button"
                      onClick={() => setForm({ ...form, data: { ...form.data, esPorcentaje: false } })}
                      className={`py-3 rounded-2xl border text-sm font-semibold transition-colors ${
                        !form.data.esPorcentaje
                          ? 'bg-primary/20 border-primary text-primary'
                          : 'bg-background border-gray-800/50 text-gray-400 hover:border-gray-600'
                      }`}
                    >
                      Monto fijo (S/)
                    </button>
                  </div>
                </div>

                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">
                    Valor {form.data.esPorcentaje ? '(%)' : '(S/)'}
                  </label>
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    max={form.data.esPorcentaje ? 100 : undefined}
                    value={form.data.valor}
                    onChange={(e) => setForm({ ...form, data: { ...form.data, valor: e.target.value } })}
                    className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white text-lg font-bold focus:outline-none focus:border-primary transition-colors"
                  />
                </div>

                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">
                      Vigente desde
                    </label>
                    <input
                      type="date"
                      value={form.data.fechaInicio}
                      onChange={(e) => setForm({ ...form, data: { ...form.data, fechaInicio: e.target.value } })}
                      className="w-full bg-background border border-gray-700/50 rounded-2xl px-3 py-3.5 text-white focus:outline-none focus:border-primary transition-colors text-sm"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">
                      Vigente hasta
                    </label>
                    <input
                      type="date"
                      value={form.data.fechaFin}
                      onChange={(e) => setForm({ ...form, data: { ...form.data, fechaFin: e.target.value } })}
                      className="w-full bg-background border border-gray-700/50 rounded-2xl px-3 py-3.5 text-white focus:outline-none focus:border-primary transition-colors text-sm"
                    />
                  </div>
                </div>
                <p className="text-xs text-gray-600 -mt-2">
                  Deja ambas fechas vacías para que el descuento no venza nunca.
                </p>

                {form.mode === 'editar' && (
                  <label className="flex items-center justify-between bg-background border border-gray-800/50 rounded-2xl px-4 py-3.5 cursor-pointer">
                    <span className="text-sm text-gray-300 font-semibold">Descuento activo</span>
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
