import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { ChevronLeft, Plus, Pencil, Ban, RotateCcw, X, FolderOpen, UtensilsCrossed } from 'lucide-react';
import toast from 'react-hot-toast';
import {
  getCategoriasAdmin, createCategoria, updateCategoria, deleteCategoria,
  getProductosAdmin, createProducto, updateProducto, deleteProducto,
} from '../services/adminMenuService';
import { getCategoryIcon } from '../services/categoryIcons';

const TIPOS_SUGERIDOS = ['Plato', 'Bebida', 'Entrada', 'Postre', 'Acompañamiento'];

export default function AdminMenu() {
  const navigate = useNavigate();
  const [tab, setTab] = useState('categorias');

  const [categorias, setCategorias] = useState([]);
  const [productos, setProductos] = useState([]);
  const [isLoading, setIsLoading] = useState(true);

  // `form` describe el panel abierto: { type: 'categoria'|'producto', mode: 'crear'|'editar', data }
  const [form, setForm] = useState(null);
  const [isSaving, setIsSaving] = useState(false);
  const [busyId, setBusyId] = useState(null);

  const cargarTodo = useCallback(() => {
    setIsLoading(true);
    Promise.all([getCategoriasAdmin(), getProductosAdmin()])
      .then(([cats, prods]) => {
        setCategorias(cats);
        setProductos(prods);
      })
      .catch((err) => toast.error(err.message ?? 'No se pudo cargar la carta'))
      .finally(() => setIsLoading(false));
  }, []);

  useEffect(() => {
    cargarTodo();
  }, [cargarTodo]);

  const categoriasActivas = categorias.filter((c) => c.estaActivo);

  // ─── Categorías ───────────────────────────────────────────────────────────

  const abrirFormCategoria = (categoria = null) => {
    setForm({
      type: 'categoria',
      mode: categoria ? 'editar' : 'crear',
      data: categoria
        ? { id: categoria.id, nombre: categoria.nombre, descripcion: categoria.descripcion, estaActivo: categoria.estaActivo }
        : { nombre: '', descripcion: '' },
    });
  };

  const guardarCategoria = async () => {
    const { nombre, descripcion } = form.data;
    if (!nombre.trim()) {
      toast.error('El nombre de la categoría es obligatorio');
      return;
    }
    setIsSaving(true);
    try {
      if (form.mode === 'crear') {
        await createCategoria({ nombre: nombre.trim(), descripcion });
        toast.success('Categoría creada');
      } else {
        await updateCategoria(form.data.id, {
          nombre: nombre.trim(),
          descripcion,
          estaActivo: form.data.estaActivo,
        });
        toast.success('Categoría actualizada');
      }
      setForm(null);
      cargarTodo();
    } catch (err) {
      toast.error(err.message ?? 'No se pudo guardar la categoría');
    } finally {
      setIsSaving(false);
    }
  };

  const toggleActivoCategoria = async (categoria) => {
    setBusyId(categoria.id);
    try {
      await updateCategoria(categoria.id, {
        nombre: categoria.nombre,
        descripcion: categoria.descripcion,
        estaActivo: !categoria.estaActivo,
      });
      toast.success(categoria.estaActivo ? 'Categoría desactivada' : 'Categoría reactivada');
      cargarTodo();
    } catch (err) {
      toast.error(err.message ?? 'No se pudo actualizar la categoría');
    } finally {
      setBusyId(null);
    }
  };

  // ─── Productos ────────────────────────────────────────────────────────────

  const abrirFormProducto = (producto = null) => {
    setForm({
      type: 'producto',
      mode: producto ? 'editar' : 'crear',
      data: producto
        ? {
            id: producto.id,
            nombre: producto.nombre,
            descripcion: producto.descripcion,
            precio: String(producto.precio),
            tipo: producto.tipo,
            categoriaId: producto.categoriaId,
            estaDisponible: producto.estaDisponible,
          }
        : {
            nombre: '',
            descripcion: '',
            precio: '',
            tipo: '',
            categoriaId: categoriasActivas[0]?.id ?? '',
          },
    });
  };

  const guardarProducto = async () => {
    const { nombre, descripcion, precio, tipo, categoriaId } = form.data;
    const precioNum = parseFloat(precio);

    if (!nombre.trim()) {
      toast.error('El nombre del producto es obligatorio');
      return;
    }
    if (!precioNum || precioNum <= 0) {
      toast.error('Ingresa un precio válido');
      return;
    }
    if (!tipo.trim()) {
      toast.error('Indica el tipo de producto (ej. Plato, Bebida)');
      return;
    }
    if (!categoriaId) {
      toast.error('Selecciona una categoría');
      return;
    }

    setIsSaving(true);
    try {
      if (form.mode === 'crear') {
        await createProducto({ nombre: nombre.trim(), descripcion, precio: precioNum, tipo: tipo.trim(), categoriaId });
        toast.success('Producto creado');
      } else {
        await updateProducto(form.data.id, {
          nombre: nombre.trim(),
          descripcion,
          precio: precioNum,
          tipo: tipo.trim(),
          categoriaId,
          estaDisponible: form.data.estaDisponible,
        });
        toast.success('Producto actualizado');
      }
      setForm(null);
      cargarTodo();
    } catch (err) {
      toast.error(err.message ?? 'No se pudo guardar el producto');
    } finally {
      setIsSaving(false);
    }
  };

  const toggleDisponibleProducto = async (producto) => {
    setBusyId(producto.id);
    try {
      await updateProducto(producto.id, {
        nombre: producto.nombre,
        descripcion: producto.descripcion,
        precio: producto.precio,
        tipo: producto.tipo,
        categoriaId: producto.categoriaId,
        estaDisponible: !producto.estaDisponible,
      });
      toast.success(producto.estaDisponible ? 'Producto desactivado' : 'Producto reactivado');
      cargarTodo();
    } catch (err) {
      toast.error(err.message ?? 'No se pudo actualizar el producto');
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="min-h-screen bg-background pb-10">
      {/* ── Header ──────────────────────────────────────── */}
      <div className="px-5 pt-6 pb-4 flex items-center gap-3">
        <motion.button
          whileTap={{ scale: 0.9 }}
          onClick={() => navigate('/admin')}
          className="p-2 bg-card rounded-full hover:bg-cardHighlight border border-gray-800/50"
        >
          <ChevronLeft size={22} />
        </motion.button>
        <div>
          <h1 className="text-2xl font-extrabold tracking-tight">Administrar carta</h1>
          <p className="text-xs text-gray-500 mt-0.5">Categorías y productos del menú</p>
        </div>
      </div>

      {/* ── Tabs ────────────────────────────────────────── */}
      <div className="px-5 flex gap-2 mb-4">
        {[
          { key: 'categorias', label: 'Categorías' },
          { key: 'productos', label: 'Productos' },
        ].map((t) => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className={`px-4 py-2 rounded-full text-sm font-semibold transition-colors ${
              tab === t.key
                ? 'bg-primary text-white shadow-glow-orange'
                : 'bg-card text-gray-400 border border-gray-800/50 hover:text-white'
            }`}
          >
            {t.label}
          </button>
        ))}
      </div>

      {isLoading ? (
        <p className="px-5 text-sm text-gray-600">Cargando…</p>
      ) : tab === 'categorias' ? (
        <div className="px-5 space-y-3">
          <motion.button
            whileTap={{ scale: 0.97 }}
            onClick={() => abrirFormCategoria()}
            className="w-full flex items-center justify-center gap-2 py-3.5 rounded-2xl border border-dashed border-gray-700 text-gray-400 hover:text-white hover:border-primary/50 transition-colors font-semibold text-sm"
          >
            <Plus size={16} />
            Nueva categoría
          </motion.button>

          {categorias.length === 0 && (
            <div className="flex flex-col items-center text-center py-10">
              <FolderOpen size={28} className="text-gray-700 mb-3" />
              <p className="text-gray-500 text-sm">Aún no hay categorías creadas.</p>
            </div>
          )}

          {categorias.map((cat) => (
            <div
              key={cat.id}
              className={`bg-card border rounded-2xl p-4 flex items-center gap-3 ${
                cat.estaActivo ? 'border-gray-800/50' : 'border-gray-800/30 opacity-60'
              }`}
            >
              <span className="text-2xl flex-shrink-0">{getCategoryIcon(cat.nombre)}</span>
              <div className="flex-1 min-w-0">
                <p className="font-bold text-white text-sm truncate">{cat.nombre}</p>
                {cat.descripcion && (
                  <p className="text-xs text-gray-500 truncate mt-0.5">{cat.descripcion}</p>
                )}
                {!cat.estaActivo && (
                  <span className="inline-block text-[10px] font-bold text-gray-500 uppercase tracking-wider mt-1">Inactiva</span>
                )}
              </div>
              <div className="flex items-center gap-1.5 flex-shrink-0">
                <button
                  onClick={() => abrirFormCategoria(cat)}
                  className="p-2 hover:bg-cardHighlight rounded-full text-gray-500 hover:text-white transition-colors"
                  title="Editar"
                >
                  <Pencil size={15} />
                </button>
                <button
                  onClick={() => toggleActivoCategoria(cat)}
                  disabled={busyId === cat.id}
                  className={`p-2 rounded-full transition-colors disabled:opacity-50 ${
                    cat.estaActivo
                      ? 'hover:bg-red-500/20 text-gray-500 hover:text-red-500'
                      : 'hover:bg-emerald-500/20 text-gray-500 hover:text-emerald-500'
                  }`}
                  title={cat.estaActivo ? 'Desactivar' : 'Reactivar'}
                >
                  {cat.estaActivo ? <Ban size={15} /> : <RotateCcw size={15} />}
                </button>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <div className="px-5 space-y-3">
          <motion.button
            whileTap={{ scale: 0.97 }}
            onClick={() => abrirFormProducto()}
            disabled={categoriasActivas.length === 0}
            className="w-full flex items-center justify-center gap-2 py-3.5 rounded-2xl border border-dashed border-gray-700 text-gray-400 hover:text-white hover:border-primary/50 transition-colors font-semibold text-sm disabled:opacity-40"
          >
            <Plus size={16} />
            Nuevo producto
          </motion.button>
          {categoriasActivas.length === 0 && (
            <p className="text-xs text-gray-600 text-center">
              Crea al menos una categoría activa antes de agregar productos.
            </p>
          )}

          {productos.length === 0 && (
            <div className="flex flex-col items-center text-center py-10">
              <UtensilsCrossed size={28} className="text-gray-700 mb-3" />
              <p className="text-gray-500 text-sm">Aún no hay productos creados.</p>
            </div>
          )}

          {productos.map((prod) => (
            <div
              key={prod.id}
              className={`bg-card border rounded-2xl p-4 flex items-center gap-3 ${
                prod.estaDisponible ? 'border-gray-800/50' : 'border-gray-800/30 opacity-60'
              }`}
            >
              <div className="flex-1 min-w-0">
                <p className="font-bold text-white text-sm truncate">{prod.nombre}</p>
                <p className="text-xs text-gray-500 mt-0.5">
                  {prod.categoriaNombre} · {prod.tipo}
                </p>
                {!prod.estaDisponible && (
                  <span className="inline-block text-[10px] font-bold text-gray-500 uppercase tracking-wider mt-1">No disponible</span>
                )}
              </div>
              <p className="text-primary font-extrabold text-sm flex-shrink-0">S/ {prod.precio.toFixed(2)}</p>
              <div className="flex items-center gap-1.5 flex-shrink-0">
                <button
                  onClick={() => abrirFormProducto(prod)}
                  className="p-2 hover:bg-cardHighlight rounded-full text-gray-500 hover:text-white transition-colors"
                  title="Editar"
                >
                  <Pencil size={15} />
                </button>
                <button
                  onClick={() => toggleDisponibleProducto(prod)}
                  disabled={busyId === prod.id}
                  className={`p-2 rounded-full transition-colors disabled:opacity-50 ${
                    prod.estaDisponible
                      ? 'hover:bg-red-500/20 text-gray-500 hover:text-red-500'
                      : 'hover:bg-emerald-500/20 text-gray-500 hover:text-emerald-500'
                  }`}
                  title={prod.estaDisponible ? 'Desactivar' : 'Reactivar'}
                >
                  {prod.estaDisponible ? <Ban size={15} /> : <RotateCcw size={15} />}
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* ── Panel de formulario (categoría o producto) ─────────────────── */}
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
                  {form.mode === 'crear' ? 'Nueva' : 'Editar'} {form.type === 'categoria' ? 'categoría' : 'producto'}
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
                onSubmit={(e) => {
                  e.preventDefault();
                  form.type === 'categoria' ? guardarCategoria() : guardarProducto();
                }}
                className="flex-1 overflow-y-auto px-5 py-4 space-y-4 pb-8"
              >
                {form.type === 'categoria' ? (
                  <>
                    <div>
                      <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Nombre</label>
                      <input
                        type="text"
                        value={form.data.nombre}
                        onChange={(e) => setForm({ ...form, data: { ...form.data, nombre: e.target.value } })}
                        autoFocus
                        placeholder="Ej: Hamburguesas"
                        className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm"
                      />
                    </div>
                    <div>
                      <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Descripción (opcional)</label>
                      <textarea
                        value={form.data.descripcion}
                        onChange={(e) => setForm({ ...form, data: { ...form.data, descripcion: e.target.value } })}
                        rows={2}
                        placeholder="Ej: Hamburguesas clásicas y especiales"
                        className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm resize-none"
                      />
                    </div>
                  </>
                ) : (
                  <>
                    <div>
                      <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Nombre</label>
                      <input
                        type="text"
                        value={form.data.nombre}
                        onChange={(e) => setForm({ ...form, data: { ...form.data, nombre: e.target.value } })}
                        autoFocus
                        placeholder="Ej: Cara Negra Clásica"
                        className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm"
                      />
                    </div>
                    <div>
                      <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Descripción (opcional)</label>
                      <textarea
                        value={form.data.descripcion}
                        onChange={(e) => setForm({ ...form, data: { ...form.data, descripcion: e.target.value } })}
                        rows={2}
                        placeholder="Ej: Doble carne, queso cheddar, tocino"
                        className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm resize-none"
                      />
                    </div>
                    <div className="grid grid-cols-2 gap-3">
                      <div>
                        <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Precio</label>
                        <div className="relative">
                          <span className="absolute left-4 top-1/2 -translate-y-1/2 text-gray-500 text-sm">S/</span>
                          <input
                            type="number"
                            step="0.01"
                            min="0"
                            value={form.data.precio}
                            onChange={(e) => setForm({ ...form, data: { ...form.data, precio: e.target.value } })}
                            className="w-full bg-background border border-gray-700/50 rounded-2xl pl-9 pr-3 py-3.5 text-white focus:outline-none focus:border-primary transition-colors text-sm"
                          />
                        </div>
                      </div>
                      <div>
                        <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Tipo</label>
                        <input
                          type="text"
                          list="tipos-sugeridos"
                          value={form.data.tipo}
                          onChange={(e) => setForm({ ...form, data: { ...form.data, tipo: e.target.value } })}
                          placeholder="Ej: Plato"
                          className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm"
                        />
                        <datalist id="tipos-sugeridos">
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
                        {categoriasActivas.map((c) => (
                          <option key={c.id} value={c.id}>{c.nombre}</option>
                        ))}
                      </select>
                    </div>
                  </>
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
