import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Utensils, Eye, EyeOff } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import toast from 'react-hot-toast';

export default function Login() {
  const navigate = useNavigate();
  const { login } = useAuth();
  const [nombreUsuario, setNombreUsuario] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [shake, setShake] = useState(false);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!nombreUsuario.trim() || !password) {
      toast.error('Ingresa tu usuario y contraseña');
      return;
    }

    setIsLoading(true);
    try {
      const auth = await login(nombreUsuario.trim(), password);
      navigate(auth?.rol === 'COCINA' ? '/cocina' : '/dashboard');
    } catch (err) {
      setShake(true);
      toast.error(err.message ?? 'Usuario o contraseña incorrectos');
      setTimeout(() => setShake(false), 600);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex flex-col items-center justify-center p-6 bg-background overflow-hidden">
      {/* Logo */}
      <motion.div
        initial={{ opacity: 0, y: -20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.4 }}
        className="flex flex-col items-center mb-10"
      >
        <div className="w-20 h-20 bg-primary rounded-3xl flex items-center justify-center mb-4 shadow-glow-orange">
          <Utensils className="text-white w-10 h-10" />
        </div>
        <h1 className="text-3xl font-bold text-white tracking-tight">Cara Negra</h1>
        <p className="text-gray-500 text-sm mt-1">Sistema de gestión de sala</p>
      </motion.div>

      <motion.form
        onSubmit={handleSubmit}
        animate={shake ? { x: [0, -10, 10, -10, 10, 0] } : {}}
        transition={{ duration: 0.4 }}
        className="w-full max-w-xs space-y-4"
      >
        <div>
          <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">
            Usuario
          </label>
          <input
            type="text"
            value={nombreUsuario}
            onChange={(e) => setNombreUsuario(e.target.value)}
            autoFocus
            autoCapitalize="none"
            autoCorrect="off"
            placeholder="Ej: cperez"
            className="w-full bg-card border border-gray-700/50 rounded-2xl px-4 py-4 text-white text-lg placeholder-gray-600 focus:outline-none focus:border-primary transition-colors"
          />
        </div>

        <div>
          <label className="block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2">
            Contraseña
          </label>
          <div className="relative">
            <input
              type={showPassword ? 'text' : 'password'}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••"
              className="w-full bg-card border border-gray-700/50 rounded-2xl px-4 py-4 pr-12 text-white text-lg placeholder-gray-600 focus:outline-none focus:border-primary transition-colors"
            />
            <button
              type="button"
              onClick={() => setShowPassword((v) => !v)}
              className="absolute right-4 top-1/2 -translate-y-1/2 text-gray-500 hover:text-gray-300"
            >
              {showPassword ? <EyeOff size={19} /> : <Eye size={19} />}
            </button>
          </div>
        </div>

        <motion.button
          whileTap={{ scale: 0.97 }}
          type="submit"
          disabled={isLoading}
          className="w-full bg-primary hover:bg-primaryHover disabled:opacity-60 text-white font-bold py-4 rounded-2xl transition-all shadow-glow-orange text-base"
        >
          {isLoading ? 'Ingresando…' : 'Ingresar'}
        </motion.button>
      </motion.form>
    </div>
  );
}
