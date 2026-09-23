import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { ChevronLeft, Plus, Pencil, Ban, RotateCcw, X, Package, History, ArrowDownCircle, ArrowUpCircle, Settings2 } from 'lucide-react';
import toast from 'react-hot-toast';
import {
  getArticulosAdmin, createArticulo, updateArticulo, deleteArticulo,
  getMovimientosArticulo, registrarMovimiento,
} from '../services/inventarioService';
import { getCategoriasAdmin } from '../services/adminMenuService';

const TIPOS_SUGERIDOS = ['Insumo', 'Bebida', 'Empaque', 'Limpieza', 'Otro'];

const TIPO_MOVIMIENTO_STYLES = {
  Entrada: { icon: ArrowDownCircle, color: 'text-accentGreen' },
  Salida: { icon: ArrowUpCircle, color: 'text-primary' },
  Ajuste: { icon: Settings2, color: 'text-accentYellow' },
};

export default function AdminInventario() {
  const navigate = useNavigate();

  const [articulos, setArticulos] = useState([]);
  const [categorias, setCategorias] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [busyId, setBusyId] = useState(null);

  // form: { type: 'articulo'|'movimiento', mode?, data, historial? }
  const [form, setForm] = useState(null);
  const [isSaving, setIsSaving] = useState(false);

  const categoriasActivas = categorias.filter((c) => c.estaActivo);

  const cargarTodo = useCallback(() => {
    setIsLoading(true);
    Promise.all([getArticulosAdmin(), getCategoriasAdmin()])
      .then(([arts, cats]) => {
        setArticulos(arts);
        setCategorias(cats);
      })
      .catch((err) => toast.error(err.message ?? 'No se pudo cargar el inventario'))
      .finally(() => setIsLoading(false));
  }, []);

  useEffect(() => {
    cargarTodo();
  }, [cargarTodo]);

  // ─── Crear / editar artículo ────────────────────────────────────────────

  const abrirFormNuevo = () => {
    setForm({
      type: 'articulo',
      mode: 'crear',
      data: { nombre: '', descripcion: '', precio: '', tipo: '', categoriaId: categoriasActivas[0]?.id ?? '', stockInicial: '0' },
    });
  };

  const abrirFormEditar = (articulo) => {
    setForm({
      type: 'articulo',
      mode: 'editar',
      data: {
        id: articulo.id,
        nombre: articulo.nombre,
        descripcion: articulo.descripcion,
        precio: String(articulo.precio),
        tipo: articulo.tipo,
        categoriaId: articulo.categoriaId,
        activo: articulo.activo,
      },
    });
  };

  const guardarArticulo = async () => {
    const { nombre, descripcion, precio, tipo, categoriaId } = form.data;
    const precioNum = parseFloat(precio);

    if (!nombre.trim()) { toast.error('El nombre es obligatorio'); return; }
    if (Number.isNaN(precioNum) || precioNum < 0) { toast.error('Ingresa un precio válido'); return; }
    if (!tipo.trim()) { toast.error('Indica el tipo de artículo (ej. Insumo, Bebida)'); return; }
    if (!categoriaId) { toast.error('Selecciona una categoría'); return; }

    setIsSaving(true);
    try {
      if (form.mode === 'crear') {
        const stockInicial = parseInt(form.data.stockInicial, 10) || 0;
        await createArticulo({ nombre: nombre.trim(), descripcion, precio: precioNum, tipo: tipo.trim(), categoriaId, stockInicial });
        toast.success('Artículo creado');
      } else {
        await updateArticulo(form.data.id, {
          nombre: nombre.trim(), descripcion, precio: precioNum, tipo: tipo.trim(), categoriaId, activo: form.data.activo,
        });
        toast.success('Artículo actualizado');
      }
      setForm(null);
      cargarTodo();
    } catch (err) {
      toast.error(err.message ?? 'No se pudo guardar el artículo');
    } finally {
      setIsSaving(false);
    }
  };

  const toggleActivo = async (articulo) => {
    setBusyId(articulo.id);
    try {
      await updateArticulo(articulo.id, {
        nombre: articulo.nombre, descripcion: articulo.descripcion, precio: articulo.precio,
        tipo: articulo.tipo, categoriaId: articulo.categoriaId, activo: !articulo.activo,
      });
      toast.success(articulo.activo ? 'Artículo desactivado' : 'Artículo reactivado');
      cargarTodo();
    } catch (err) {
      toast.error(err.message ?? 'No se pudo actualizar el artículo');
    } finally {
      setBusyId(null);
    }
  };

  // ─── Movimientos de stock ───────────────────────────────────────────────

  const abrirMovimientos = async (articulo) => {
    setForm({
      type: 'movimiento',
      articulo,
      historial: null,
      data: { tipoMovimiento: 'Entrada', cantidad: '', referenciaCod: '', notas: '' },
    });
    try {
      const historial = await getMovimientosArticulo(articulo.id);
      setForm((f) => (f && f.type === 'movimiento' ? { ...f, historial } : f));
    } catch (err) {
      toast.error(err.message ?? 'No se pudo cargar el historial');
    }
  };

  const guardarMovimiento = async () => {
    const { tipoMovimiento, cantidad, referenciaCod, notas } = form.data;
    const cantidadNum = parseInt(cantidad, 10);

    if (Number.isNaN(cantidadNum) || cantidadNum < 0) {
      toast.error('Ingresa una cantidad válida');
      return;
    }

    setIsSaving(true);
    try {
      await registrarMovimiento(form.articulo.id, { tipoMovimiento, cantidad: cantidadNum, referenciaCod, notas });
      toast.success('Movimiento registrado');
      const historial = await getMovimientosArticulo(form.articulo.id);
      setForm((f) => (f && f.type === 'movimiento' ? { ...f, historial, data: { tipoMovimiento: 'Entrada', cantidad: '', referenciaCod: '', notas: '' } } : f));
      cargarTodo();
    } catch (err) {
      toast.error(err.message ?? 'No se pudo registrar el movimiento');
    } finally {
      setIsSaving(false);
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
          <h1 className="text-2xl font-extrabold tracking-tight">Inventario</h1>
          <p className="text-xs text-gray-500 mt-0.5">Insumos, stock y movimientos</p>
        </div>
      </div>

      <div className="px-5 space-y-3">
        <motion.button
          whileTap={{ scale: 0.97 }}
          onClick={abrirFormNuevo}
          disabled={categoriasActivas.length === 0}
          className="w-full flex items-center justify-center gap-2 py-3.5 rounded-2xl border border-dashed border-gray-700 text-gray-400 hover:text-white hover:border-primary/50 transition-colors font-semibold text-sm disabled:opacity-40"
        >
          <Plus size={16} />
          Nuevo artículo
        </motion.button>
        {categoriasActivas.length === 0 && (
          <p className="text-xs text-gray-600 text-center">
            Crea al menos una categoría activa (en "Carta y menú") antes de agregar artículos.
          </p>
        )}

        {isLoading ? (
          <p className="text-sm text-gray-600">Cargando…</p>
        ) : articulos.length === 0 ? (
          <div className="flex flex-col items-center text-center py-10">
            <Package size={28} className="text-gray-700 mb-3" />
            <p className="text-gray-500 text-sm">Aún no hay artículos de inventario creados.</p>
          </div>
        ) : (
          articulos.map((articulo) => (
            <div
              key={articulo.id}
              className={`bg-card border rounded-2xl p-4 flex items-center gap-3 ${
                articulo.activo ? 'border-gray-800/50' : 'border-gray-800/30 opacity-60'
              }`}
            >
              <div className="flex-1 min-w-0">
                <p className="font-bold text-white text-sm truncate">{articulo.nombre}</p>
                <p className="text-xs text-gray-500 mt-0.5">{articulo.categoriaNombre} · {articulo.tipo}</p>
                {!articulo.activo && (
                  <span className="inline-block text-[10px] font-bold text-gray-500 uppercase tracking-wider mt-1">Inactivo</span>
                )}
              </div>
              <div className="text-right flex-shrink-0">
                <p className="font-extrabold text-white text-sm">{articulo.stock} <span className="text-[10px] text-gray-500 font-normal">stock</span></p>
                <p className="text-xs text-gray-500">S/ {articulo.precio.toFixed(2)}</p>
              </div>
              <div className="flex items-center gap-1 flex-shrink-0">
                <button
                  onClick={() => abrirMovimientos(articulo)}
                  className="p-2 hover:bg-cardHighlight rounded-full text-gray-500 hover:text-white transition-colors"
                  title="Movimientos de stock"
                >
                  <History size={15} />
                </button>
                <button
                  onClick={() => abrirFormEditar(articulo)}
                  className="p-2 hover:bg-cardHighlight rounded-full text-gray-500 hover:text-white transition-colors"
                  title="Editar"
                >
                  <Pencil size={15} />
                </button>
                <button
                  onClick={() => toggleActivo(articulo)}
                  disabled={busyId === articulo.id}
                  className={`p-2 rounded-full transition-colors disabled:opacity-50 ${
                    articulo.activo
                      ? 'hover:bg-red-500/20 text-gray-500 hover:text-red-500'
                      : 'hover:bg-emerald-500/20 text-gray-500 hover:text-emerald-500'
                  }`}
                  title={articulo.activo ? 'Desactivar' : 'Reactivar'}
                >
                  {articulo.activo ? <Ban size={15} /> : <RotateCcw size={15} />}
                </button>
              </div>
            </div>
          ))
        )}
      </div>

      {/* ── Panel: crear/editar artículo ─────────────────────────────── */}
      <AnimatePresence>
        {form?.type === 'articulo' && (
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
              <div className="flex justify-center pt-3 pb-1"><div className="w-10 h-1 rounded-full bg-gray-700" /></div>

              <div className="px-5 pb-3 pt-2 flex justify-between items-start border-b border-gray-800/60">
                <h2 className="text-lg font-extrabold tracking-tight">
                  {form.mode === 'crear' ? 'Nuevo' : 'Editar'} artículo
                </h2>
                <motion.button whileTap={{ scale: 0.9 }} onClick={() => setForm(null)} className="p-2 bg-background/60 rounded-full hover:bg-gray-700 transition-colors">
                  <X size={18} className="text-gray-400" />
                </motion.button>
              </div>

              <form onSubmit={(e) => { e.preventDefault(); guardarArticulo(); }} className="flex-1 overflow-y-auto px-5 py-4 space-y-4 pb-8">
                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Nombre</label>
                  <input
                    type="text" value={form.data.nombre} autoFocus
                    onChange={(e) => setForm({ ...form, data: { ...form.data, nombre: e.target.value } })}
                    placeholder="Ej: Pan de hamburguesa"
                    className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm"
                  />
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Descripción (opcional)</label>
                  <textarea
                    value={form.data.descripcion} rows={2}
                    onChange={(e) => setForm({ ...form, data: { ...form.data, descripcion: e.target.value } })}
                    className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm resize-none"
                  />
                </div>
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Precio (costo)</label>
                    <div className="relative">
                      <span className="absolute left-4 top-1/2 -translate-y-1/2 text-gray-500 text-sm">S/</span>
                      <input
                        type="number" step="0.01" min="0" value={form.data.precio}
                        onChange={(e) => setForm({ ...form, data: { ...form.data, precio: e.target.value } })}
                        className="w-full bg-background border border-gray-700/50 rounded-2xl pl-9 pr-3 py-3.5 text-white focus:outline-none focus:border-primary transition-colors text-sm"
                      />
                    </div>
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Tipo</label>
                    <input
                      type="text" list="tipos-articulo-sugeridos" value={form.data.tipo}
                      onChange={(e) => setForm({ ...form, data: { ...form.data, tipo: e.target.value } })}
                      placeholder="Ej: Insumo"
                      className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm"
                    />
                    <datalist id="tipos-articulo-sugeridos">
                      {TIPOS_SUGERIDOS.map((t) => <option key={t} value={t} />)}
                    </datalist>
                  </div>
                </div>
                <div>
                  <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Categoría</label>
                  <select
                    value={form.data.categoriaId}
                    onChange={(e) => setForm({ ...form, data: { ...form.data, categoriaId: Number(e.target.value) } })}
                    className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white focus:outline-none focus:border-primary transition-colors text-sm"
                  >
                    <option value="" disabled>Selecciona una categoría</option>
                    {categoriasActivas.map((c) => <option key={c.id} value={c.id}>{c.nombre}</option>)}
                  </select>
                </div>
                {form.mode === 'crear' && (
                  <div>
                    <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Stock inicial</label>
                    <input
                      type="number" min="0" value={form.data.stockInicial}
                      onChange={(e) => setForm({ ...form, data: { ...form.data, stockInicial: e.target.value } })}
                      className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white focus:outline-none focus:border-primary transition-colors text-sm"
                    />
                    <p className="text-xs text-gray-600 mt-1.5">
                      Después de crear el artículo, el stock solo cambia registrando movimientos.
                    </p>
                  </div>
                )}
                <motion.button whileTap={{ scale: 0.97 }} type="submit" disabled={isSaving} className="w-full bg-primary hover:bg-primaryHover disabled:opacity-60 text-white font-bold py-4 rounded-2xl transition-all shadow-glow-orange text-sm mt-2">
                  {isSaving ? 'Guardando…' : 'Guardar'}
                </motion.button>
              </form>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* ── Panel: movimientos de stock ──────────────────────────────── */}
      <AnimatePresence>
        {form?.type === 'movimiento' && (
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
              <div className="flex justify-center pt-3 pb-1"><div className="w-10 h-1 rounded-full bg-gray-700" /></div>

              <div className="px-5 pb-3 pt-2 flex justify-between items-start border-b border-gray-800/60">
                <div>
                  <h2 className="text-lg font-extrabold tracking-tight">{form.articulo.nombre}</h2>
                  <p className="text-xs text-gray-500 mt-0.5">Stock actual: {form.articulo.stock}</p>
                </div>
                <motion.button whileTap={{ scale: 0.9 }} onClick={() => setForm(null)} className="p-2 bg-background/60 rounded-full hover:bg-gray-700 transition-colors">
                  <X size={18} className="text-gray-400" />
                </motion.button>
              </div>

              <div className="flex-1 overflow-y-auto px-5 py-4 space-y-5 pb-8">
                <form onSubmit={(e) => { e.preventDefault(); guardarMovimiento(); }} className="space-y-3 bg-background rounded-2xl p-4 border border-gray-800/40">
                  <p className="text-xs font-bold text-gray-400 uppercase tracking-wider">Registrar movimiento</p>
                  <div className="grid grid-cols-3 gap-2">
                    {['Entrada', 'Salida', 'Ajuste'].map((t) => (
                      <button
                        type="button" key={t}
                        onClick={() => setForm({ ...form, data: { ...form.data, tipoMovimiento: t } })}
                        className={`py-2.5 rounded-xl text-xs font-bold transition-colors ${
                          form.data.tipoMovimiento === t
                            ? 'bg-primary text-white shadow-glow-orange'
                            : 'bg-card text-gray-400 border border-gray-800/50 hover:text-white'
                        }`}
                      >
                        {t}
                      </button>
                    ))}
                  </div>
                  <div>
                    <input
                      type="number" min="0" value={form.data.cantidad}
                      onChange={(e) => setForm({ ...form, data: { ...form.data, cantidad: e.target.value } })}
                      placeholder={form.data.tipoMovimiento === 'Ajuste' ? 'Nuevo stock exacto' : 'Cantidad'}
                      className="w-full bg-card border border-gray-700/50 rounded-2xl px-4 py-3 text-white placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm"
                    />
                    {form.data.tipoMovimiento === 'Ajuste' && (
                      <p className="text-xs text-gray-600 mt-1.5">
                        El ajuste fija el stock exactamente en este valor (útil tras un conteo físico).
                      </p>
                    )}
                  </div>
                  <input
                    type="text" value={form.data.referenciaCod}
                    onChange={(e) => setForm({ ...form, data: { ...form.data, referenciaCod: e.target.value } })}
                    placeholder="Referencia (opcional, ej. Nº de guía)"
                    className="w-full bg-card border border-gray-700/50 rounded-2xl px-4 py-3 text-white placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm"
                  />
                  <input
                    type="text" value={form.data.notas}
                    onChange={(e) => setForm({ ...form, data: { ...form.data, notas: e.target.value } })}
                    placeholder="Notas (opcional)"
                    className="w-full bg-card border border-gray-700/50 rounded-2xl px-4 py-3 text-white placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm"
                  />
                  <motion.button whileTap={{ scale: 0.97 }} type="submit" disabled={isSaving} className="w-full bg-primary hover:bg-primaryHover disabled:opacity-60 text-white font-bold py-3.5 rounded-2xl transition-all shadow-glow-orange text-sm">
                    {isSaving ? 'Guardando…' : 'Registrar movimiento'}
                  </motion.button>
                </form>

                <div>
                  <p className="text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Historial</p>
                  {form.historial === null ? (
                    <p className="text-sm text-gray-600">Cargando…</p>
                  ) : form.historial.length === 0 ? (
                    <p className="text-sm text-gray-600">Sin movimientos registrados todavía.</p>
                  ) : (
                    <div className="space-y-2">
                      {form.historial.map((m) => {
                        const estilo = TIPO_MOVIMIENTO_STYLES[m.tipo] ?? {};
                        const Icono = estilo.icon ?? History;
                        return (
                          <div key={m.id} className="bg-background border border-gray-800/40 rounded-2xl p-3 flex items-start gap-3">
                            <Icono size={16} className={`${estilo.color ?? 'text-gray-400'} flex-shrink-0 mt-0.5`} />
                            <div className="flex-1 min-w-0">
                              <p className="text-sm text-gray-200">
                                <span className="font-bold">{m.tipo}</span>
                                {' · '}{m.tipo === 'Ajuste' ? `nuevo stock: ${m.cantidad}` : `${m.cantidad} unidades`}
                              </p>
                              <p className="text-xs text-gray-500 mt-0.5">
                                {m.usuarioNombre} · {new Date(m.creadoEn).toLocaleString('es-PE', { dateStyle: 'short', timeStyle: 'short' })}
                              </p>
                              {(m.referenciaCod || m.notas) && (
                                <p className="text-xs text-gray-600 mt-0.5 truncate">
                                  {[m.referenciaCod, m.notas].filter(Boolean).join(' · ')}
                                </p>
                              )}
                            </div>
                            <p className="text-xs font-bold text-gray-400 flex-shrink-0">Balance: {m.balance}</p>
                          </div>
                        );
                      })}
                    </div>
                  )}
                </div>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
