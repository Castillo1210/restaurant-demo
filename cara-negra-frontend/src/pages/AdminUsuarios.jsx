import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { ChevronLeft, Plus, Pencil, Ban, RotateCcw, X, Users, KeyRound, Search } from 'lucide-react';
import toast from 'react-hot-toast';
import {
  getUsuarios, createUsuario, updateUsuario, deleteUsuario, resetPassword,
} from '../services/usuariosAdminService';
import { getRoles } from '../services/rolesService';
import { useAuth } from '../context/AuthContext';

const ROL_STYLES = {
  ADMIN: 'text-primary',
  CAJERO: 'text-accentYellow',
  MOZO: 'text-accentGreen',
  COCINA: 'text-sky-400',
};

export default function AdminUsuarios() {
  const navigate = useNavigate();
  const { user: currentUser } = useAuth();

  const [usuarios, setUsuarios] = useState([]);
  const [roles, setRoles] = useState([]);
  const [search, setSearch] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [busyId, setBusyId] = useState(null);

  // form: { type: 'usuario'|'reset', mode: 'crear'|'editar', data }
  const [form, setForm] = useState(null);
  const [isSaving, setIsSaving] = useState(false);

  const cargarUsuarios = useCallback((busqueda = search) => {
    setIsLoading(true);
    getUsuarios({ search: busqueda, pageSize: 100 })
      .then((res) => setUsuarios(res.items))
      .catch((err) => toast.error(err.message ?? 'No se pudieron cargar los usuarios'))
      .finally(() => setIsLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    cargarUsuarios('');
    getRoles().then(setRoles).catch((err) => toast.error(err.message ?? 'No se pudieron cargar los roles'));
  }, [cargarUsuarios]);

  const handleBuscar = (e) => {
    e.preventDefault();
    cargarUsuarios(search);
  };

  // ─── Crear / editar usuario ─────────────────────────────────────────────

  const abrirFormNuevo = () => {
    setForm({
      type: 'usuario',
      mode: 'crear',
      data: { nombreUsuario: '', nombreCompleto: '', password: '', rolId: roles[0]?.id ?? '' },
    });
  };

  const abrirFormEditar = (usuario) => {
    setForm({
      type: 'usuario',
      mode: 'editar',
      data: {
        id: usuario.id,
        nombreUsuario: usuario.nombreUsuario,
        nombreCompleto: usuario.nombreCompleto,
        rolId: usuario.rolId,
        estaActivo: usuario.estaActivo,
      },
    });
  };

  const guardarUsuario = async () => {
    const { nombreUsuario, nombreCompleto, password, rolId } = form.data;

    if (!nombreCompleto.trim()) {
      toast.error('El nombre completo es obligatorio');
      return;
    }
    if (!rolId) {
      toast.error('Selecciona un rol');
      return;
    }

    setIsSaving(true);
    try {
      if (form.mode === 'crear') {
        if (!nombreUsuario.trim() || nombreUsuario.trim().length < 3) {
          toast.error('El nombre de usuario debe tener al menos 3 caracteres');
          setIsSaving(false);
          return;
        }
        if (!password) {
          toast.error('La contraseña es obligatoria');
          setIsSaving(false);
          return;
        }
        await createUsuario({ nombreUsuario: nombreUsuario.trim(), nombreCompleto: nombreCompleto.trim(), password, rolId });
        toast.success('Usuario creado');
      } else {
        await updateUsuario(form.data.id, {
          nombreCompleto: nombreCompleto.trim(),
          rolId,
          estaActivo: form.data.estaActivo,
        });
        toast.success('Usuario actualizado');
      }
      setForm(null);
      cargarUsuarios();
    } catch (err) {
      toast.error(err.message ?? 'No se pudo guardar el usuario');
    } finally {
      setIsSaving(false);
    }
  };

  const toggleActivo = async (usuario) => {
    if (usuario.id === currentUser?.usuarioId) {
      toast.error('No puedes desactivar tu propia cuenta');
      return;
    }
    setBusyId(usuario.id);
    try {
      await updateUsuario(usuario.id, {
        nombreCompleto: usuario.nombreCompleto,
        rolId: usuario.rolId,
        estaActivo: !usuario.estaActivo,
      });
      toast.success(usuario.estaActivo ? 'Usuario desactivado' : 'Usuario reactivado');
      cargarUsuarios();
    } catch (err) {
      toast.error(err.message ?? 'No se pudo actualizar el usuario');
    } finally {
      setBusyId(null);
    }
  };

  // ─── Resetear contraseña ─────────────────────────────────────────────────

  const abrirReset = (usuario) => {
    setForm({
      type: 'reset',
      data: { id: usuario.id, nombreUsuario: usuario.nombreUsuario, newPassword: '', confirmPassword: '' },
    });
  };

  const guardarReset = async () => {
    const { id, newPassword, confirmPassword } = form.data;
    if (newPassword !== confirmPassword) {
      toast.error('Las contraseñas no coinciden');
      return;
    }
    if (newPassword.length < 8) {
      toast.error('La contraseña debe tener al menos 8 caracteres');
      return;
    }

    setIsSaving(true);
    try {
      await resetPassword(id, newPassword);
      toast.success('Contraseña reseteada correctamente');
      setForm(null);
    } catch (err) {
      toast.error(err.message ?? 'No se pudo resetear la contraseña');
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
          <h1 className="text-2xl font-extrabold tracking-tight">Usuarios</h1>
          <p className="text-xs text-gray-500 mt-0.5">Personal con acceso al sistema</p>
        </div>
      </div>

      <div className="px-5 space-y-3 mb-1">
        <form onSubmit={handleBuscar} className="relative">
          <Search size={16} className="absolute left-4 top-1/2 -translate-y-1/2 text-gray-500" />
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Buscar por nombre o usuario…"
            className="w-full bg-card border border-gray-800/50 rounded-2xl pl-10 pr-4 py-3 text-white placeholder-gray-600 text-sm focus:outline-none focus:border-primary transition-colors"
          />
        </form>

        <motion.button
          whileTap={{ scale: 0.97 }}
          onClick={abrirFormNuevo}
          disabled={roles.length === 0}
          className="w-full flex items-center justify-center gap-2 py-3.5 rounded-2xl border border-dashed border-gray-700 text-gray-400 hover:text-white hover:border-primary/50 transition-colors font-semibold text-sm disabled:opacity-40"
        >
          <Plus size={16} />
          Nuevo usuario
        </motion.button>
      </div>

      <div className="px-5 space-y-3 mt-3">
        {isLoading ? (
          <p className="text-sm text-gray-600">Cargando…</p>
        ) : usuarios.length === 0 ? (
          <div className="flex flex-col items-center text-center py-10">
            <Users size={28} className="text-gray-700 mb-3" />
            <p className="text-gray-500 text-sm">No se encontraron usuarios.</p>
          </div>
        ) : (
          usuarios.map((usuario) => (
            <div
              key={usuario.id}
              className={`bg-card border rounded-2xl p-4 flex items-center gap-3 ${
                usuario.estaActivo ? 'border-gray-800/50' : 'border-gray-800/30 opacity-60'
              }`}
            >
              <div className="flex-1 min-w-0">
                <p className="font-bold text-white text-sm truncate">{usuario.nombreCompleto}</p>
                <p className="text-xs text-gray-500 truncate">
                  @{usuario.nombreUsuario} · <span className={`font-semibold ${ROL_STYLES[usuario.rolNombre] ?? 'text-gray-400'}`}>{usuario.rolNombre}</span>
                </p>
                {!usuario.estaActivo && (
                  <span className="inline-block text-[10px] font-bold text-gray-500 uppercase tracking-wider mt-1">Desactivado</span>
                )}
              </div>
              <div className="flex items-center gap-1 flex-shrink-0">
                <button
                  onClick={() => abrirReset(usuario)}
                  className="p-2 hover:bg-cardHighlight rounded-full text-gray-500 hover:text-white transition-colors"
                  title="Resetear contraseña"
                >
                  <KeyRound size={15} />
                </button>
                <button
                  onClick={() => abrirFormEditar(usuario)}
                  className="p-2 hover:bg-cardHighlight rounded-full text-gray-500 hover:text-white transition-colors"
                  title="Editar"
                >
                  <Pencil size={15} />
                </button>
                <button
                  onClick={() => toggleActivo(usuario)}
                  disabled={busyId === usuario.id || usuario.id === currentUser?.usuarioId}
                  className={`p-2 rounded-full transition-colors disabled:opacity-30 ${
                    usuario.estaActivo
                      ? 'hover:bg-red-500/20 text-gray-500 hover:text-red-500'
                      : 'hover:bg-emerald-500/20 text-gray-500 hover:text-emerald-500'
                  }`}
                  title={usuario.id === currentUser?.usuarioId ? 'No puedes desactivarte a ti mismo' : (usuario.estaActivo ? 'Desactivar' : 'Reactivar')}
                >
                  {usuario.estaActivo ? <Ban size={15} /> : <RotateCcw size={15} />}
                </button>
              </div>
            </div>
          ))
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
                  {form.type === 'reset'
                    ? `Resetear contraseña de @${form.data.nombreUsuario}`
                    : `${form.mode === 'crear' ? 'Nuevo' : 'Editar'} usuario`}
                </h2>
                <motion.button
                  whileTap={{ scale: 0.9 }}
                  onClick={() => setForm(null)}
                  className="p-2 bg-background/60 rounded-full hover:bg-gray-700 transition-colors flex-shrink-0 ml-3"
                >
                  <X size={18} className="text-gray-400" />
                </motion.button>
              </div>

              {form.type === 'usuario' ? (
                <form
                  onSubmit={(e) => { e.preventDefault(); guardarUsuario(); }}
                  className="flex-1 overflow-y-auto px-5 py-4 space-y-4 pb-8"
                >
                  {form.mode === 'crear' && (
                    <div>
                      <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Nombre de usuario</label>
                      <input
                        type="text"
                        value={form.data.nombreUsuario}
                        onChange={(e) => setForm({ ...form, data: { ...form.data, nombreUsuario: e.target.value } })}
                        autoFocus
                        placeholder="Ej: jperez"
                        className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm"
                      />
                      <p className="text-xs text-gray-600 mt-1.5">Solo letras, números y guiones bajos. No se puede cambiar después.</p>
                    </div>
                  )}

                  <div>
                    <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Nombre completo</label>
                    <input
                      type="text"
                      value={form.data.nombreCompleto}
                      onChange={(e) => setForm({ ...form, data: { ...form.data, nombreCompleto: e.target.value } })}
                      placeholder="Ej: Juan Pérez"
                      className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm"
                    />
                  </div>

                  {form.mode === 'crear' && (
                    <div>
                      <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Contraseña</label>
                      <input
                        type="password"
                        value={form.data.password}
                        onChange={(e) => setForm({ ...form, data: { ...form.data, password: e.target.value } })}
                        placeholder="••••••••"
                        className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm"
                      />
                      <p className="text-xs text-gray-600 mt-1.5">
                        Mínimo 8 caracteres, con mayúscula, minúscula, número y carácter especial.
                      </p>
                    </div>
                  )}

                  <div>
                    <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Rol</label>
                    <select
                      value={form.data.rolId}
                      onChange={(e) => setForm({ ...form, data: { ...form.data, rolId: Number(e.target.value) } })}
                      className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white focus:outline-none focus:border-primary transition-colors text-sm"
                    >
                      <option value="" disabled>Selecciona un rol</option>
                      {roles.map((r) => (
                        <option key={r.id} value={r.id}>{r.nombre}</option>
                      ))}
                    </select>
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
              ) : (
                <form
                  onSubmit={(e) => { e.preventDefault(); guardarReset(); }}
                  className="px-5 py-4 space-y-4 pb-8"
                >
                  <div>
                    <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Nueva contraseña</label>
                    <input
                      type="password"
                      value={form.data.newPassword}
                      onChange={(e) => setForm({ ...form, data: { ...form.data, newPassword: e.target.value } })}
                      autoFocus
                      placeholder="••••••••"
                      className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm"
                    />
                    <p className="text-xs text-gray-600 mt-1.5">
                      Mínimo 8 caracteres, con mayúscula, minúscula, número y carácter especial.
                    </p>
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">Confirmar nueva contraseña</label>
                    <input
                      type="password"
                      value={form.data.confirmPassword}
                      onChange={(e) => setForm({ ...form, data: { ...form.data, confirmPassword: e.target.value } })}
                      placeholder="••••••••"
                      className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm"
                    />
                  </div>

                  <motion.button
                    whileTap={{ scale: 0.97 }}
                    type="submit"
                    disabled={isSaving}
                    className="w-full bg-primary hover:bg-primaryHover disabled:opacity-60 text-white font-bold py-4 rounded-2xl transition-all shadow-glow-orange text-sm mt-2"
                  >
                    {isSaving ? 'Guardando…' : 'Resetear contraseña'}
                  </motion.button>
                </form>
              )}
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
