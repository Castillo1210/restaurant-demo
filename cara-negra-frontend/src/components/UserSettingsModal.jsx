import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { X, Key, LogOut, ChevronLeft, Eye, EyeOff } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import { cambiarPassword } from '../services/authService';
import toast from 'react-hot-toast';

// ─── Variantes definidas FUERA del componente para evitar recreaciones ──────
const overlayVariants = {
  hidden: { opacity: 0 },
  show:   { opacity: 1, transition: { duration: 0.2 } },
  exit:   { opacity: 0, transition: { duration: 0.15 } },
};

const panelVariants = {
  hidden: { opacity: 0, scale: 0.94, y: 16 },
  show:   { opacity: 1, scale: 1, y: 0, transition: { type: 'spring', stiffness: 300, damping: 26 } },
  exit:   { opacity: 0, scale: 0.94, y: 12, transition: { duration: 0.15 } },
};

const slideVariants = {
  enterFromRight: { opacity: 0, x: 40 },
  center:         { opacity: 1, x: 0, transition: { type: 'spring', stiffness: 300, damping: 28 } },
  exitToLeft:     { opacity: 0, x: -40, transition: { duration: 0.15 } },
  enterFromLeft:  { opacity: 0, x: -40 },
  exitToRight:    { opacity: 0, x: 40, transition: { duration: 0.15 } },
};

export default function UserSettingsModal({ onClose }) {
  const navigate = useNavigate();
  const { user, logout } = useAuth();

  const [view, setView] = useState('main');
  const [passwordForm, setPasswordForm] = useState({ current: '', next: '', confirm: '' });
  const [showCurrent, setShowCurrent] = useState(false);
  const [showNew, setShowNew] = useState(false);
  const [isSaving, setIsSaving] = useState(false);

  const initial = (user?.nombreCompleto?.[0] ?? 'U').toUpperCase();

  const goTo = (nextView) => setView(nextView);

  const handleLogout = () => {
    logout();
    onClose();
    navigate('/login');
    toast.success('Sesión cerrada');
  };

  const handleSavePassword = async () => {
    if (passwordForm.next !== passwordForm.confirm) {
      toast.error('Las contraseñas nuevas no coinciden');
      return;
    }
    if (passwordForm.next.length < 6) {
      toast.error('La nueva contraseña debe tener al menos 6 caracteres');
      return;
    }
    if (!user?.usuarioId) {
      toast.error('No se pudo identificar tu usuario. Vuelve a iniciar sesión.');
      return;
    }

    setIsSaving(true);
    try {
      await cambiarPassword(user.usuarioId, {
        currentPassword: passwordForm.current,
        newPassword: passwordForm.next,
        confirmPassword: passwordForm.confirm,
      });
      toast.success('Contraseña actualizada correctamente');
      goTo('main');
      setPasswordForm({ current: '', next: '', confirm: '' });
    } catch (err) {
      toast.error(err.message ?? 'No se pudo cambiar la contraseña');
    } finally {
      setIsSaving(false);
    }
  };

  // ─── Render ─────────────────────────────────────────────────────────────────
  return (
    <motion.div
      variants={overlayVariants}
      initial="hidden"
      animate="show"
      exit="exit"
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/75 backdrop-blur-sm"
      onClick={(e) => { if (e.target === e.currentTarget) onClose(); }}
    >
      <motion.div
        variants={panelVariants}
        initial="hidden"
        animate="show"
        exit="exit"
        className="relative bg-card w-full max-w-sm rounded-3xl shadow-2xl border border-gray-700/40 overflow-hidden"
      >
        <AnimatePresence mode="wait" initial={false}>

          {/* ── Main ───────────────────────────────────────────────── */}
          {view === 'main' && (
            <motion.div
              key="main"
              variants={slideVariants}
              initial="enterFromLeft"
              animate="center"
              exit="exitToLeft"
              className="p-6"
            >
              {/* Close */}
              <button
                onClick={onClose}
                className="absolute right-4 top-4 p-2 bg-background rounded-full hover:bg-cardHighlight transition-colors"
              >
                <X size={17} className="text-gray-400" />
              </button>

              {/* Avatar */}
              <div className="flex items-center gap-4 mb-6 pb-5 border-b border-gray-700/30">
                <div className="w-14 h-14 bg-primary/20 rounded-full flex items-center justify-center text-primary text-xl font-extrabold border-2 border-primary/30">
                  {initial}
                </div>
                <div>
                  <h2 className="text-lg font-extrabold text-white">{user?.nombreCompleto}</h2>
                  <p className="text-xs text-gray-500">@{user?.nombreUsuario} · {user?.rol}</p>
                </div>
              </div>

              <div className="space-y-2">
                <motion.button
                  whileTap={{ scale: 0.97 }}
                  onClick={() => goTo('password')}
                  className="w-full flex items-center gap-3 p-3.5 bg-background rounded-2xl hover:bg-cardHighlight transition-colors group border border-gray-800/40"
                >
                  <Key size={17} className="text-gray-500 group-hover:text-primary transition-colors" />
                  <span className="text-sm font-medium text-gray-200">Cambiar contraseña</span>
                </motion.button>

                <div className="h-px bg-gray-800/60 my-1" />

                <motion.button
                  whileTap={{ scale: 0.97 }}
                  onClick={handleLogout}
                  className="w-full flex items-center gap-3 p-3.5 bg-red-500/10 rounded-2xl hover:bg-red-500/20 transition-colors text-red-400 border border-red-900/20"
                >
                  <LogOut size={17} />
                  <span className="text-sm font-bold">Cerrar sesión</span>
                </motion.button>
              </div>
            </motion.div>
          )}

          {/* ── Cambiar contraseña ─────────────────────────────────── */}
          {view === 'password' && (
            <motion.div
              key="password"
              variants={slideVariants}
              initial="enterFromRight"
              animate="center"
              exit="exitToRight"
              className="p-6"
            >
              <button
                onClick={() => goTo('main')}
                className="absolute left-4 top-4 p-2 bg-background rounded-full hover:bg-cardHighlight transition-colors"
              >
                <ChevronLeft size={17} className="text-gray-400" />
              </button>
              <button
                onClick={onClose}
                className="absolute right-4 top-4 p-2 bg-background rounded-full hover:bg-cardHighlight transition-colors"
              >
                <X size={17} className="text-gray-400" />
              </button>

              <div className="mt-8 mb-6 text-center">
                <h2 className="text-xl font-extrabold text-white">Cambiar contraseña</h2>
                <p className="text-xs text-gray-500 mt-1">Debe tener al menos 6 caracteres</p>
              </div>

              <form
                onSubmit={(e) => { e.preventDefault(); handleSavePassword(); }}
                className="space-y-4"
              >
                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1.5 ml-1">Contraseña actual</label>
                  <div className="relative">
                    <input
                      type={showCurrent ? 'text' : 'password'}
                      value={passwordForm.current}
                      onChange={(e) => setPasswordForm({ ...passwordForm, current: e.target.value })}
                      className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm"
                      placeholder="••••••••"
                    />
                    <button type="button" onClick={() => setShowCurrent((v) => !v)} className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-500 hover:text-gray-300">
                      {showCurrent ? <EyeOff size={17} /> : <Eye size={17} />}
                    </button>
                  </div>
                </div>

                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1.5 ml-1">Nueva contraseña</label>
                  <div className="relative">
                    <input
                      type={showNew ? 'text' : 'password'}
                      value={passwordForm.next}
                      onChange={(e) => setPasswordForm({ ...passwordForm, next: e.target.value })}
                      className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm"
                      placeholder="••••••••"
                    />
                    <button type="button" onClick={() => setShowNew((v) => !v)} className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-500 hover:text-gray-300">
                      {showNew ? <EyeOff size={17} /> : <Eye size={17} />}
                    </button>
                  </div>
                </div>

                <div>
                  <label className="block text-xs font-medium text-gray-500 mb-1.5 ml-1">Confirmar nueva contraseña</label>
                  <input
                    type="password"
                    value={passwordForm.confirm}
                    onChange={(e) => setPasswordForm({ ...passwordForm, confirm: e.target.value })}
                    className="w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3.5 text-white placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm"
                    placeholder="••••••••"
                  />
                </div>

                <motion.button
                  whileTap={{ scale: 0.97 }}
                  type="submit"
                  disabled={isSaving}
                  className="w-full bg-primary hover:bg-primaryHover disabled:opacity-60 text-white font-bold py-4 rounded-2xl transition-all shadow-glow-orange mt-2 text-sm"
                >
                  {isSaving ? 'Guardando…' : 'Guardar cambios'}
                </motion.button>
              </form>
            </motion.div>
          )}

        </AnimatePresence>
      </motion.div>
    </motion.div>
  );
}
