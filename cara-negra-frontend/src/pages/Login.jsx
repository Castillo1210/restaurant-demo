import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { Utensils, Eye, EyeOff, Delete, ChevronLeft, KeyRound, Smartphone } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import toast from 'react-hot-toast';
import { useEmpresa } from '../context/EmpresaContext';
import { getPantallaPin, getTokenDispositivo } from '../services/authService';

const LARGO_PIN = 4;

const COLORES_AVATAR = [
  'bg-orange-500/20 text-orange-300',
  'bg-emerald-500/20 text-emerald-300',
  'bg-sky-500/20 text-sky-300',
  'bg-violet-500/20 text-violet-300',
  'bg-rose-500/20 text-rose-300',
  'bg-amber-500/20 text-amber-300',
  'bg-teal-500/20 text-teal-300',
  'bg-fuchsia-500/20 text-fuchsia-300',
];

const iniciales = (nombre = '') =>
  nombre.trim().split(/\s+/).slice(0, 2).map((p) => p[0]?.toUpperCase() ?? '').join('') || '?';

/** "Juan Carlos Pérez Díaz" → "Juan Pérez" (nombre + primer apellido aproximado). */
const nombreVisible = (nombre = '') => {
  const partes = nombre.trim().split(/\s+/);
  if (partes.length <= 2) return nombre.trim();
  return `${partes[0]} ${partes[partes.length >= 4 ? 2 : 1]}`;
};

function Avatar({ usuario, grande = false }) {
  const color = COLORES_AVATAR[usuario.usuarioId % COLORES_AVATAR.length];
  return (
    <div
      className={`${grande ? 'w-16 h-16 text-xl' : 'w-12 h-12 text-base'} ${color} rounded-2xl flex items-center justify-center font-extrabold flex-shrink-0`}
    >
      {iniciales(usuario.nombreCompleto)}
    </div>
  );
}

export default function Login() {
  const { nombreComercial, logo } = useEmpresa();
  const navigate = useNavigate();
  const { login, loginConPin } = useAuth();

  // 'cargando' (consultando si este dispositivo tiene PIN) | 'pin' | 'password'
  const [modo, setModo] = useState(() => (getTokenDispositivo() ? 'cargando' : 'password'));
  const [pantallaPin, setPantallaPin] = useState(null);

  const [nombreUsuario, setNombreUsuario] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [shake, setShake] = useState(false);

  const [seleccionado, setSeleccionado] = useState(null);
  const [pin, setPin] = useState('');
  const [errorPin, setErrorPin] = useState('');

  const hayPin = (pantallaPin?.usuarios?.length ?? 0) > 0;

  useEffect(() => {
    if (!getTokenDispositivo()) return undefined;
    let vivo = true;
    getPantallaPin()
      .then((datos) => {
        if (!vivo) return;
        setPantallaPin(datos);
        setModo(datos?.usuarios?.length ? 'pin' : 'password');
      })
      .catch(() => vivo && setModo('password'));
    return () => {
      vivo = false;
    };
  }, []);

  const sacudir = () => {
    setShake(true);
    setTimeout(() => setShake(false), 600);
  };

  const irSegunRol = useCallback(
    (auth) => navigate(auth?.rol === 'COCINA' ? '/cocina' : '/dashboard'),
    [navigate]
  );

  // ─── Usuario y contraseña ───────────────────────────────────────────────
  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!nombreUsuario.trim() || !password) {
      toast.error('Ingresa tu usuario y contraseña');
      return;
    }

    setIsLoading(true);
    try {
      const auth = await login(nombreUsuario.trim(), password);
      irSegunRol(auth);
    } catch (err) {
      sacudir();
      toast.error(err.message ?? 'Usuario o contraseña incorrectos');
    } finally {
      setIsLoading(false);
    }
  };

  // ─── PIN ────────────────────────────────────────────────────────────────
  const enviarPin = useCallback(
    async (pinCompleto) => {
      if (!seleccionado) return;
      setIsLoading(true);
      setErrorPin('');
      try {
        const auth = await loginConPin(seleccionado.usuarioId, pinCompleto);
        irSegunRol(auth);
      } catch (err) {
        setPin('');
        sacudir();
        if (err.dispositivoNoAutorizado) {
          toast.error(err.message);
          setPantallaPin(null);
          setSeleccionado(null);
          setModo('password');
        } else {
          setErrorPin(err.message ?? 'PIN incorrecto');
        }
      } finally {
        setIsLoading(false);
      }
    },
    [seleccionado, loginConPin, irSegunRol]
  );

  const pulsarDigito = useCallback(
    (digito) => {
      if (isLoading || pin.length >= LARGO_PIN) return;
      setErrorPin('');
      setPin(pin + digito);
    },
    [isLoading, pin]
  );

  // Al completar los 4 dígitos se envía solo (sin botón "Ingresar").
  useEffect(() => {
    if (pin.length === LARGO_PIN) enviarPin(pin);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pin]);

  const borrarDigito = useCallback(() => {
    if (!isLoading) setPin((actual) => actual.slice(0, -1));
  }, [isLoading]);

  const volverALista = useCallback(() => {
    setSeleccionado(null);
    setPin('');
    setErrorPin('');
  }, []);

  // Teclado físico (tablet con teclado o PC): dígitos, borrar y Esc.
  useEffect(() => {
    if (modo !== 'pin' || !seleccionado) return undefined;
    const onKey = (e) => {
      if (/^[0-9]$/.test(e.key)) pulsarDigito(e.key);
      else if (e.key === 'Backspace') borrarDigito();
      else if (e.key === 'Escape') volverALista();
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [modo, seleccionado, pulsarDigito, borrarDigito, volverALista]);

  const encabezadoCompacto = modo === 'pin';

  return (
    <div className="min-h-screen flex flex-col items-center justify-center p-6 bg-background overflow-hidden">
      {/* Logo */}
      <motion.div
        initial={{ opacity: 0, y: -20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.4 }}
        className={`flex flex-col items-center ${encabezadoCompacto ? 'mb-6' : 'mb-10'}`}
      >
        {logo ? (
          <img
            src={logo}
            alt={nombreComercial}
            className={`${encabezadoCompacto ? 'w-16 h-16' : 'w-24 h-24'} object-contain rounded-3xl bg-white/5 p-2 mb-3`}
          />
        ) : (
          <div className={`${encabezadoCompacto ? 'w-14 h-14' : 'w-20 h-20'} bg-primary rounded-3xl flex items-center justify-center mb-3 shadow-glow-orange`}>
            <Utensils className="text-white w-8 h-8" />
          </div>
        )}
        <h1 className={`${encabezadoCompacto ? 'text-2xl' : 'text-3xl'} font-bold text-ink tracking-tight text-center`}>
          {nombreComercial || 'Bienvenido'}
        </h1>
        <p className="text-gray-500 text-sm mt-1">Sistema de gestión de sala</p>
      </motion.div>

      {modo === 'cargando' && <p className="text-sm text-gray-600">Cargando…</p>}

      {/* ─── Ingreso con PIN ─────────────────────────────────────────── */}
      {modo === 'pin' && (
        <div className="w-full max-w-sm">
          <AnimatePresence mode="wait" initial={false}>
            {!seleccionado ? (
              <motion.div
                key="lista"
                initial={{ opacity: 0, x: -30 }}
                animate={{ opacity: 1, x: 0 }}
                exit={{ opacity: 0, x: -30 }}
                transition={{ duration: 0.18 }}
              >
                <p className="text-center text-sm font-bold text-gray-300 mb-4">¿Quién eres?</p>
                <div className="grid grid-cols-2 gap-3 max-h-[55dvh] overflow-y-auto pb-1">
                  {pantallaPin.usuarios.map((u) => (
                    <motion.button
                      key={u.usuarioId}
                      whileTap={{ scale: 0.96 }}
                      onClick={() => setSeleccionado(u)}
                      className="bg-card border border-gray-800/60 hover:border-primary/50 rounded-2xl p-3 flex items-center gap-3 text-left transition-colors"
                    >
                      <Avatar usuario={u} />
                      <div className="min-w-0">
                        <p className="font-bold text-ink text-sm leading-tight break-words line-clamp-2">{nombreVisible(u.nombreCompleto)}</p>
                        {u.rol !== 'MOZO' && (
                          <p className="text-[10px] font-bold uppercase tracking-wider text-sky-400 mt-0.5">{u.rol}</p>
                        )}
                      </div>
                    </motion.button>
                  ))}
                </div>
              </motion.div>
            ) : (
              <motion.div
                key="teclado"
                initial={{ opacity: 0, x: 30 }}
                animate={{ opacity: 1, x: 0 }}
                exit={{ opacity: 0, x: 30 }}
                transition={{ duration: 0.18 }}
                className="flex flex-col items-center"
              >
                <div className="w-full flex items-center mb-3">
                  <button
                    onClick={volverALista}
                    className="p-2 bg-card rounded-full hover:bg-cardHighlight border border-gray-800/50"
                    title="Elegir otra persona"
                  >
                    <ChevronLeft size={20} />
                  </button>
                </div>

                <Avatar usuario={seleccionado} grande />
                <p className="font-extrabold text-ink text-lg mt-2">{nombreVisible(seleccionado.nombreCompleto)}</p>
                <p className="text-xs text-gray-500">Ingresa tu PIN</p>

                <motion.div
                  animate={shake ? { x: [0, -10, 10, -10, 10, 0] } : {}}
                  transition={{ duration: 0.4 }}
                  className="flex gap-4 my-5"
                  aria-label={`${pin.length} de ${LARGO_PIN} dígitos`}
                >
                  {Array.from({ length: LARGO_PIN }).map((_, i) => (
                    <span
                      key={i}
                      className={`w-4 h-4 rounded-full border-2 transition-colors ${
                        i < pin.length ? 'bg-primary border-primary' : 'border-gray-600'
                      }`}
                    />
                  ))}
                </motion.div>

                <p className={`min-h-[2.5rem] text-center text-xs px-2 ${errorPin ? 'text-red-400' : 'text-transparent'}`}>
                  {errorPin || '.'}
                </p>

                <div className="grid grid-cols-3 gap-3 w-full max-w-[18rem] mt-1">
                  {['1', '2', '3', '4', '5', '6', '7', '8', '9'].map((d) => (
                    <TeclaPin key={d} onClick={() => pulsarDigito(d)} disabled={isLoading}>{d}</TeclaPin>
                  ))}
                  <span />
                  <TeclaPin onClick={() => pulsarDigito('0')} disabled={isLoading}>0</TeclaPin>
                  <TeclaPin onClick={borrarDigito} disabled={isLoading || pin.length === 0} secundaria titulo="Borrar">
                    <Delete size={22} />
                  </TeclaPin>
                </div>

                {isLoading && <p className="text-xs text-gray-500 mt-4">Ingresando…</p>}
              </motion.div>
            )}
          </AnimatePresence>

          <div className="flex flex-col items-center gap-2 mt-8">
            <button
              onClick={() => { volverALista(); setModo('password'); }}
              className="text-sm font-semibold text-gray-400 hover:text-ink flex items-center gap-1.5"
            >
              <KeyRound size={14} /> Ingresar con usuario y contraseña
            </button>
            <p className="text-[11px] text-gray-600 flex items-center gap-1">
              <Smartphone size={11} /> {pantallaPin.dispositivo}
            </p>
          </div>
        </div>
      )}

      {/* ─── Usuario y contraseña ────────────────────────────────────── */}
      {modo === 'password' && (
        <>
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
                className="w-full bg-card border border-gray-700/50 rounded-2xl px-4 py-4 text-ink text-lg placeholder-gray-600 focus:outline-none focus:border-primary transition-colors"
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
                  className="w-full bg-card border border-gray-700/50 rounded-2xl px-4 py-4 pr-12 text-ink text-lg placeholder-gray-600 focus:outline-none focus:border-primary transition-colors"
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

          {hayPin && (
            <button
              onClick={() => setModo('pin')}
              className="mt-6 text-sm font-semibold text-gray-400 hover:text-ink"
            >
              Volver al ingreso con PIN
            </button>
          )}
          {pantallaPin && !hayPin && (
            <p className="mt-6 text-[11px] text-gray-600 text-center max-w-xs">
              Este dispositivo ({pantallaPin.dispositivo}) está habilitado para PIN, pero aún no hay
              mozos con PIN asignado (Admin → Usuarios).
            </p>
          )}
        </>
      )}
    </div>
  );
}

function TeclaPin({ children, onClick, disabled, secundaria = false, titulo }) {
  return (
    <motion.button
      type="button"
      whileTap={{ scale: 0.92 }}
      onClick={onClick}
      disabled={disabled}
      title={titulo}
      aria-label={titulo}
      className={`h-16 rounded-2xl text-2xl font-bold flex items-center justify-center transition-colors disabled:opacity-40 select-none ${
        secundaria
          ? 'text-gray-400 hover:text-ink'
          : 'bg-card border border-gray-800/60 text-ink hover:bg-cardHighlight active:bg-primary/20'
      }`}
    >
      {children}
    </motion.button>
  );
}
