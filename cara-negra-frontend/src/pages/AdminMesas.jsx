import { useState, useEffect, useCallback, useMemo } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { ChevronLeft, Plus, Pencil, Ban, RotateCcw, X, Table2, Trees } from 'lucide-react';
import toast from 'react-hot-toast';
import { getMesasAdmin, createMesa, updateMesa, deleteMesa } from '../services/mesasAdminService';
import { getSalones } from '../services/salonesService';
import { etiquetaMesa, prefijoSalon, nombreCorto } from '../utils/mesaLabel';

const ESTADOS = ['Disponible', 'Ocupada', 'Reservada', 'Mantenimiento'];

const ESTADO_STYLES = {
  Disponible: 'text-accentGreen',
  Ocupada: 'text-primary',
  Reservada: 'text-accentYellow',
  Mantenimiento: 'text-gray-500',
};

const MAX_RANGO = 50;

const inputCls =
  'w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-ink focus:outline-none focus:border-primary transition-colors text-sm';

export default function AdminMesas() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const salonFiltro = searchParams.get('salon') ? Number(searchParams.get('salon')) : null;

  const [mesas, setMesas] = useState([]);
  const [salones, setSalones] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [busyId, setBusyId] = useState(null);

  const [form, setForm] = useState(null); // { mode: 'crear'|'editar', data }
  const [isSaving, setIsSaving] = useState(false);

  const cargar = useCallback(() => {
    setIsLoading(true);
    Promise.all([getMesasAdmin(), getSalones()])
      .then(([m, s]) => {
        setMesas(m);
        setSalones(s);
      })
      .catch((err) => toast.error(err.message ?? 'No se pudieron cargar las mesas'))
      .finally(() => setIsLoading(false));
  }, []);

  useEffect(() => {
    cargar();
  }, [cargar]);

  // Mesas agrupadas por salón (en el orden de los salones). Las mesas antiguas sin salón
  // (antes de existir esta función) se agrupan aparte hasta que se les asigne uno.
  const grupos = useMemo(() => {
    const lista = salones
      .filter((s) => !salonFiltro || s.id === salonFiltro)
      .map((s) => ({ salon: s, mesas: mesas.filter((m) => m.salonId === s.id) }));
    const sinSalon = mesas.filter((m) => !m.salonId);
    if (!salonFiltro && sinSalon.length) lista.push({ salon: { id: 0, nombre: 'Sin salón asignado', estaActivo: true }, mesas: sinSalon });
    return lista;
  }, [mesas, salones, salonFiltro]);

  const abrirFormNueva = (salonId) =>
    setForm({
      mode: 'crear',
      data: { numeroMesa: '', salonId: salonId ?? salonFiltro ?? salones.find((s) => s.estaActivo)?.id ?? '', rango: false, desde: 1, hasta: 10 },
    });

  const abrirFormEditar = (mesa) =>
    setForm({
      mode: 'editar',
      data: { id: mesa.id, numeroMesa: String(mesa.numeroMesa), estado: mesa.estado, salonId: mesa.salonId ?? '' },
    });

  const guardar = async () => {
    const salonId = Number(form.data.salonId);
    if (!salonId) {
      toast.error('Selecciona el salón de la mesa');
      return;
    }

    setIsSaving(true);
    try {
      if (form.mode === 'crear' && form.data.rango) {
        // Alta rápida de varias mesas numeradas (p.ej. Terraza 1 a 12).
        const desde = parseInt(form.data.desde, 10);
        const hasta = parseInt(form.data.hasta, 10);
        if (!desde || !hasta || hasta < desde) {
          toast.error('Indica un rango válido (desde ≤ hasta)');
          return;
        }
        if (hasta - desde + 1 > MAX_RANGO) {
          toast.error(`Como máximo ${MAX_RANGO} mesas a la vez`);
          return;
        }
        let creadas = 0;
        const omitidas = [];
        for (let n = desde; n <= hasta; n++) {
          try {
            await createMesa(String(n), salonId);
            creadas++;
          } catch {
            omitidas.push(n); // p.ej. ya existía en ese salón
          }
        }
        toast.success(`${creadas} mesas creadas${omitidas.length ? ` (omitidas por existir: ${omitidas.join(', ')})` : ''}`);
      } else {
        // El número de mesa es un código (puede tener letras, ej. "T1", "B-3").
        const numero = form.data.numeroMesa.trim();
        if (!numero) {
          toast.error('Ingresa un número/código de mesa válido');
          return;
        }
        if (form.mode === 'crear') {
          await createMesa(numero, salonId);
          toast.success('Mesa creada');
        } else {
          await updateMesa(form.data.id, { numeroMesa: numero, estado: form.data.estado, salonId });
          toast.success('Mesa actualizada');
        }
      }
      setForm(null);
      cargar();
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
        await updateMesa(mesa.id, { numeroMesa: mesa.numeroMesa, estado: 'Disponible', salonId: mesa.salonId });
        toast.success('Mesa reactivada');
      } else {
        await deleteMesa(mesa.id);
        toast.success('Mesa puesta en mantenimiento');
      }
      cargar();
    } catch (err) {
      toast.error(err.message ?? 'No se pudo actualizar la mesa');
    } finally {
      setBusyId(null);
    }
  };

  const setFiltro = (id) => {
    if (id) setSearchParams({ salon: String(id) });
    else setSearchParams({});
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
        <div className="flex-1">
          <h1 className="text-2xl font-extrabold tracking-tight">Mesas</h1>
          <p className="text-xs text-gray-500 mt-0.5">Cada mesa pertenece a un salón; la numeración es por salón</p>
        </div>
        <button
          onClick={() => navigate('/admin/salones')}
          className="flex items-center gap-1.5 px-3 py-2 bg-card rounded-full border border-gray-800/50 text-xs font-bold text-gray-300 hover:bg-cardHighlight"
        >
          <Trees size={14} /> Salones
        </button>
      </div>

      {!isLoading && salones.length === 0 ? (
        <div className="px-5 mt-8 flex flex-col items-center text-center">
          <Trees size={32} className="text-gray-700 mb-3" />
          <p className="text-gray-300 font-semibold">Primero crea los salones</p>
          <p className="text-gray-500 text-sm mt-1 max-w-xs">
            Las mesas se amarran a un salón o área (Terraza, Jardín…). Crea al menos uno para poder agregar mesas.
          </p>
          <button
            onClick={() => navigate('/admin/salones')}
            className="mt-5 px-5 py-3 rounded-2xl bg-primary text-white font-bold text-sm shadow-glow-orange"
          >
            Ir a salones
          </button>
        </div>
      ) : (
        <>
          {/* Filtro por salón */}
          <div className="px-5 flex gap-2 overflow-x-auto no-scrollbar pb-1">
            {[{ id: null, nombre: 'Todos' }, ...salones].map((s) => (
              <button
                key={s.id ?? 'todos'}
                onClick={() => setFiltro(s.id)}
                className={`shrink-0 px-3.5 py-2 rounded-xl text-xs font-bold border transition-colors ${
                  (salonFiltro ?? null) === s.id ? 'bg-primary/20 border-primary text-primary' : 'bg-card border-gray-800 text-gray-400 hover:border-gray-600'
                } ${s.estaActivo === false ? 'opacity-60' : ''}`}
              >
                {s.nombre}
              </button>
            ))}
          </div>

          <div className="px-5 mt-3 space-y-6">
            <motion.button
              whileTap={{ scale: 0.97 }}
              onClick={() => abrirFormNueva()}
              className="w-full flex items-center justify-center gap-2 py-3.5 rounded-2xl border border-dashed border-gray-700 text-gray-400 hover:text-ink hover:border-primary/50 transition-colors font-semibold text-sm"
            >
              <Plus size={16} />
              Nueva mesa
            </motion.button>

            {isLoading && <p className="text-sm text-gray-600">Cargando…</p>}

            {!isLoading &&
              grupos.map(({ salon, mesas: mesasSalon }) => (
                <section key={salon.id}>
                  <div className="flex items-center justify-between mb-2">
                    <h2 className="text-sm font-extrabold text-ink flex items-center gap-2">
                      <Trees size={15} className="text-accentGreen" /> {salon.nombre}
                      {salon.estaActivo === false && (
                        <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-gray-800 text-gray-400">Inactivo</span>
                      )}
                    </h2>
                    {salon.id !== 0 && (
                      <button onClick={() => abrirFormNueva(salon.id)} className="text-xs font-bold text-primary flex items-center gap-1">
                        <Plus size={13} /> Mesa
                      </button>
                    )}
                  </div>

                  {mesasSalon.length === 0 ? (
                    <div className="flex items-center gap-2 text-xs text-gray-600 bg-card/50 border border-gray-800/40 rounded-2xl px-4 py-3">
                      <Table2 size={14} /> Sin mesas en este salón todavía.
                    </div>
                  ) : (
                    <div className="grid grid-cols-3 sm:grid-cols-4 gap-3">
                      {mesasSalon.map((mesa) => (
                        <div
                          key={mesa.id}
                          className={`bg-card border rounded-2xl p-3.5 ${
                            mesa.estado === 'Mantenimiento' ? 'border-gray-800/30 opacity-60' : 'border-gray-800/50'
                          }`}
                        >
                          <div className="flex justify-between items-start mb-1">
                            <div className="min-w-0">
                              {prefijoSalon(mesa.salonNombre) && (
                                <p className="text-[10px] font-bold uppercase tracking-wider text-gray-500 leading-none truncate">
                                  {prefijoSalon(mesa.salonNombre)}
                                </p>
                              )}
                              <p className="text-2xl font-extrabold text-ink">{mesa.numeroMesa}</p>
                            </div>
                            <div className="flex items-center">
                              <button
                                onClick={() => abrirFormEditar(mesa)}
                                className="p-1.5 hover:bg-cardHighlight rounded-full text-gray-500 hover:text-ink transition-colors"
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
                          <p className={`text-xs font-bold ${ESTADO_STYLES[mesa.estado] ?? 'text-gray-400'}`}>{mesa.estado}</p>
                          {mesa.estado === 'Ocupada' && mesa.atendidoPor && (
                            <p className="text-[10px] text-gray-400 mt-0.5 truncate" title={`Atendida por ${mesa.atendidoPor}`}>
                              👤 {nombreCorto(mesa.atendidoPor)}
                            </p>
                          )}
                        </div>
                      ))}
                    </div>
                  )}
                </section>
              ))}
          </div>
        </>
      )}

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
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Salón</label>
                  <select
                    value={form.data.salonId}
                    onChange={(e) => setForm((f) => ({ ...f, data: { ...f.data, salonId: e.target.value } }))}
                    className={inputCls}
                  >
                    <option value="">Selecciona un salón…</option>
                    {salones.map((s) => (
                      <option key={s.id} value={s.id}>
                        {s.nombre}{s.estaActivo ? '' : ' (inactivo)'}
                      </option>
                    ))}
                  </select>
                </div>

                {form.mode === 'crear' && (
                  <div className="grid grid-cols-2 gap-2">
                    {[
                      { value: false, label: 'Una mesa' },
                      { value: true, label: 'Varias (rango)' },
                    ].map((o) => (
                      <button
                        type="button"
                        key={String(o.value)}
                        onClick={() => setForm((f) => ({ ...f, data: { ...f.data, rango: o.value } }))}
                        className={`py-2.5 rounded-xl border text-xs font-bold transition-colors ${
                          form.data.rango === o.value ? 'bg-gray-50/10 border-gray-50/40 text-ink' : 'border-gray-800 text-gray-500'
                        }`}
                      >
                        {o.label}
                      </button>
                    ))}
                  </div>
                )}

                {form.mode === 'crear' && form.data.rango ? (
                  <div>
                    <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Numeración</label>
                    <div className="flex items-center gap-3">
                      <input
                        type="number"
                        min={1}
                        value={form.data.desde}
                        onChange={(e) => setForm((f) => ({ ...f, data: { ...f.data, desde: e.target.value } }))}
                        className={inputCls}
                      />
                      <span className="text-gray-500 text-sm">a</span>
                      <input
                        type="number"
                        min={1}
                        value={form.data.hasta}
                        onChange={(e) => setForm((f) => ({ ...f, data: { ...f.data, hasta: e.target.value } }))}
                        className={inputCls}
                      />
                    </div>
                    <p className="text-xs text-gray-600 mt-1.5">Crea las mesas numeradas de una vez (máx. {MAX_RANGO}).</p>
                  </div>
                ) : (
                  <div>
                    <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">
                      Número/código de mesa
                    </label>
                    <input
                      type="text"
                      maxLength={20}
                      placeholder="Ej: 1, 2, T1, VIP"
                      value={form.data.numeroMesa}
                      onChange={(e) => setForm((f) => ({ ...f, data: { ...f.data, numeroMesa: e.target.value } }))}
                      autoFocus
                      className={inputCls}
                    />
                    <p className="text-xs text-gray-600 mt-1.5">
                      Se mostrará como <span className="text-gray-300 font-semibold">{etiquetaMesa(salones.find((x) => x.id === Number(form.data.salonId))?.nombre, form.data.numeroMesa.trim() || '1')}</span>.
                      El número puede repetirse en otro salón (Terraza 1, Jardín 1…).
                    </p>
                  </div>
                )}

                {form.mode === 'editar' && (
                  <div>
                    <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Estado</label>
                    <select
                      value={form.data.estado}
                      onChange={(e) => setForm((f) => ({ ...f, data: { ...f.data, estado: e.target.value } }))}
                      className={inputCls}
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
