import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { ChevronLeft, Building2, Search, Loader2, ImagePlus, Trash2, Save, Receipt, Phone, AlertCircle } from 'lucide-react';
import toast from 'react-hot-toast';
import { getEmpresa, updateEmpresa } from '../services/empresaService';
import { consultarDocumento } from '../services/clientesService';
import { getProximoNumero } from '../services/comprobantesService';
import { esRucValido } from '../utils/documentos';
import { useEmpresa } from '../context/EmpresaContext';

const inputCls =
  'w-full bg-background border border-gray-700/50 rounded-2xl px-4 py-3 text-ink placeholder-gray-600 focus:outline-none focus:border-primary transition-colors text-sm';
const labelCls = 'block text-xs font-bold text-gray-400 uppercase tracking-wider mb-2';

const LOGO_MAX_PX = 320;
const LOGO_MAX_CHARS = 400000;

/** Reduce la imagen en el navegador (máx. 320 px) y la devuelve como data URL PNG. */
const reducirImagen = (file) =>
  new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(new Error('No se pudo leer la imagen'));
    reader.onload = () => {
      const img = new Image();
      img.onerror = () => reject(new Error('El archivo no es una imagen válida'));
      img.onload = () => {
        const escala = Math.min(1, LOGO_MAX_PX / Math.max(img.width, img.height));
        const canvas = document.createElement('canvas');
        canvas.width = Math.round(img.width * escala);
        canvas.height = Math.round(img.height * escala);
        canvas.getContext('2d').drawImage(img, 0, 0, canvas.width, canvas.height);
        resolve(canvas.toDataURL('image/png'));
      };
      img.src = reader.result;
    };
    reader.readAsDataURL(file);
  });

function Seccion({ icon: Icon, titulo, children }) {
  return (
    <section className="bg-card border border-gray-800/50 rounded-3xl p-4 space-y-4">
      <h2 className="text-sm font-extrabold text-ink flex items-center gap-2">
        <Icon size={16} className="text-primary" /> {titulo}
      </h2>
      {children}
    </section>
  );
}

/**
 * Datos de la empresa (se guardan en la BD). Todo el sistema los usa: boletas/facturas,
 * recibos, comanda impresa, pantalla de login y título de la app. Permite instalar el sistema
 * en otro restaurante sin tocar código.
 */
export default function AdminEmpresa() {
  const navigate = useNavigate();
  const { recargar } = useEmpresa();
  const fileRef = useRef(null);

  const [form, setForm] = useState(null);
  const [original, setOriginal] = useState(null);
  const [isSaving, setIsSaving] = useState(false);
  const [consultando, setConsultando] = useState(false);

  useEffect(() => {
    getEmpresa()
      .then((d) => {
        const datos = {
          ruc: d.ruc ?? '',
          razonSocial: d.razonSocial ?? '',
          nombreComercial: d.nombreComercial ?? '',
          direccion: d.direccion ?? '',
          telefono: d.telefono ?? '',
          email: d.email ?? '',
          serieBoleta: d.serieBoleta ?? 'B001',
          serieFactura: d.serieFactura ?? 'F001',
          igvPorcentaje: d.igvPorcentaje ?? 18,
          mensajePie: d.mensajePie ?? '',
          logoBase64: d.logoBase64 ?? '',
        };
        setForm(datos);
        setOriginal(datos);
      })
      .catch((err) => toast.error(err.message));
  }, []);

  // Próximo número de cada serie tal como lo calcula el servidor (continúa la numeración del
  // sistema anterior si se configuró). Se muestra para confirmar antes de emitir el primero.
  const [proximos, setProximos] = useState({});
  const serieB = form?.serieBoleta;
  const serieF = form?.serieFactura;
  useEffect(() => {
    let vivo = true;
    const series = [serieB, serieF].filter((s) => /^[BF][A-Z0-9]{3}$/.test(s ?? ''));
    Promise.all(series.map((s) => getProximoNumero(s).then((r) => [s, r.texto]).catch(() => [s, null])))
      .then((pares) => vivo && setProximos(Object.fromEntries(pares)));
    return () => { vivo = false; };
  }, [serieB, serieF]);

  const set = (campo) => (e) => {
    const valor = e?.target ? e.target.value : e;
    setForm((f) => ({ ...f, [campo]: valor }));
  };

  if (!form) {
    return (
      <div className="min-h-screen bg-background flex items-center justify-center text-gray-500 text-sm">
        <Loader2 className="animate-spin mr-2" size={16} /> Cargando…
      </div>
    );
  }

  const rucValido = esRucValido(form.ruc);
  const cambioSeries =
    original && (form.serieBoleta !== original.serieBoleta || form.serieFactura !== original.serieFactura);

  const consultarRuc = async () => {
    if (!rucValido) {
      toast.error('Ingresa un RUC válido');
      return;
    }
    setConsultando(true);
    const r = await consultarDocumento('RUC', form.ruc);
    setConsultando(false);
    if (r.estado === 'encontrado') {
      setForm((f) => ({
        ...f,
        razonSocial: r.cliente.nombre,
        direccion: r.cliente.direccion || f.direccion,
      }));
      toast.success('Datos traídos de SUNAT');
      if (r.cliente.estado && r.cliente.estado !== 'ACTIVO') toast(`SUNAT: ${r.cliente.estado}`, { icon: '⚠️' });
    } else {
      toast.error(r.mensaje ?? 'No se pudo consultar el RUC');
    }
  };

  const elegirLogo = async (e) => {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;
    try {
      const dataUrl = await reducirImagen(file);
      if (dataUrl.length > LOGO_MAX_CHARS) {
        toast.error('La imagen es muy pesada incluso reducida; prueba con una más simple');
        return;
      }
      setForm((f) => ({ ...f, logoBase64: dataUrl }));
    } catch (err) {
      toast.error(err.message);
    }
  };

  const guardar = async () => {
    if (!rucValido) return toast.error('El RUC no es válido');
    if (!form.razonSocial.trim()) return toast.error('La razón social es obligatoria');
    if (!form.nombreComercial.trim()) return toast.error('El nombre comercial es obligatorio');
    if (!form.direccion.trim()) return toast.error('La dirección fiscal es obligatoria');
    if (!/^B[A-Z0-9]{3}$/.test(form.serieBoleta)) return toast.error('Serie de boletas: 4 caracteres empezando con B (ej. B001)');
    if (!/^F[A-Z0-9]{3}$/.test(form.serieFactura)) return toast.error('Serie de facturas: 4 caracteres empezando con F (ej. F001)');

    setIsSaving(true);
    try {
      const guardado = await updateEmpresa({ ...form, igvPorcentaje: Number(form.igvPorcentaje) || 0 });
      const datos = { ...form, logoBase64: guardado.logoBase64 ?? '' };
      setOriginal(datos);
      setForm(datos);
      recargar();
      toast.success('Datos de la empresa guardados');
    } catch (err) {
      toast.error(err.message);
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="min-h-screen bg-background pb-28">
      <div className="px-5 pt-6 pb-4 flex items-center gap-3">
        <motion.button
          whileTap={{ scale: 0.9 }}
          onClick={() => navigate('/admin')}
          className="p-2 bg-card rounded-full hover:bg-cardHighlight border border-gray-800/50"
        >
          <ChevronLeft size={22} />
        </motion.button>
        <div>
          <h1 className="text-2xl font-extrabold tracking-tight">Datos de la empresa</h1>
          <p className="text-xs text-gray-500 mt-0.5">Se usan en boletas, facturas, recibos, comanda y login</p>
        </div>
      </div>

      <div className="px-5 grid gap-4 lg:grid-cols-[1fr_320px] lg:items-start">
        <div className="space-y-4">
          <Seccion icon={Building2} titulo="Identificación">
            <div>
              <label className={labelCls}>RUC</label>
              <div className="flex gap-2">
                <div className="relative flex-1">
                  <input
                    value={form.ruc}
                    onChange={(e) => set('ruc')(e.target.value.replace(/\D/g, ''))}
                    maxLength={11}
                    inputMode="numeric"
                    placeholder="11 dígitos"
                    className={inputCls}
                  />
                  {form.ruc.length === 11 && (
                    <span className={`absolute right-4 top-1/2 -translate-y-1/2 text-xs font-bold ${rucValido ? 'text-accentGreen' : 'text-red-500'}`}>
                      {rucValido ? 'Válido' : 'Inválido'}
                    </span>
                  )}
                </div>
                <button
                  type="button"
                  onClick={consultarRuc}
                  disabled={!rucValido || consultando}
                  className="px-4 rounded-2xl bg-primary/15 border border-primary/40 text-primary text-xs font-bold flex items-center gap-1.5 disabled:opacity-40"
                  title="Traer razón social y dirección desde SUNAT"
                >
                  {consultando ? <Loader2 size={14} className="animate-spin" /> : <Search size={14} />} SUNAT
                </button>
              </div>
            </div>
            <div>
              <label className={labelCls}>Razón social</label>
              <input value={form.razonSocial} onChange={set('razonSocial')} maxLength={200} className={inputCls} />
            </div>
            <div>
              <label className={labelCls}>Nombre comercial</label>
              <input value={form.nombreComercial} onChange={set('nombreComercial')} maxLength={100} placeholder="Como lo conocen los clientes" className={inputCls} />
              <p className="text-xs text-gray-600 mt-1.5">Aparece en el login, el título de la app, recibos y comanda de cocina.</p>
            </div>
          </Seccion>

          <Seccion icon={Phone} titulo="Dirección y contacto">
            <div>
              <label className={labelCls}>Dirección fiscal</label>
              <input value={form.direccion} onChange={set('direccion')} maxLength={250} className={inputCls} />
            </div>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div>
                <label className={labelCls}>Teléfono (opcional)</label>
                <input value={form.telefono} onChange={set('telefono')} maxLength={30} className={inputCls} />
              </div>
              <div>
                <label className={labelCls}>Correo (opcional)</label>
                <input value={form.email} onChange={set('email')} maxLength={100} type="email" className={inputCls} />
              </div>
            </div>
          </Seccion>

          <Seccion icon={Receipt} titulo="Comprobantes">
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className={labelCls}>Serie boletas</label>
                <input value={form.serieBoleta} onChange={(e) => set('serieBoleta')(e.target.value.toUpperCase())} maxLength={4} className={inputCls} />
                {proximos[form.serieBoleta] && (
                  <p className="text-xs text-gray-500 mt-1">Próxima: <span className="font-bold text-ink">{proximos[form.serieBoleta]}</span></p>
                )}
              </div>
              <div>
                <label className={labelCls}>Serie facturas</label>
                <input value={form.serieFactura} onChange={(e) => set('serieFactura')(e.target.value.toUpperCase())} maxLength={4} className={inputCls} />
                {proximos[form.serieFactura] && (
                  <p className="text-xs text-gray-500 mt-1">Próxima: <span className="font-bold text-ink">{proximos[form.serieFactura]}</span></p>
                )}
              </div>
            </div>
            {cambioSeries && (
              <p className="text-xs text-accentYellow flex items-start gap-1.5">
                <AlertCircle size={14} className="mt-0.5 shrink-0" />
                Los próximos comprobantes usarán la nueva serie; cada serie lleva su propia numeración. Los ya emitidos no cambian.
              </p>
            )}
            <div>
              <label className={labelCls}>IGV (%)</label>
              <div className="flex gap-2 items-center">
                <input
                  type="number"
                  min={0}
                  max={100}
                  step="0.01"
                  value={form.igvPorcentaje}
                  onChange={set('igvPorcentaje')}
                  className={`${inputCls} max-w-[120px]`}
                />
                {[18, 10].map((v) => (
                  <button
                    key={v}
                    type="button"
                    onClick={() => set('igvPorcentaje')(v)}
                    className={`px-3 py-2 rounded-xl border text-xs font-bold ${
                      Number(form.igvPorcentaje) === v ? 'bg-gray-50/10 border-gray-50/40 text-ink' : 'border-gray-800 text-gray-500'
                    }`}
                  >
                    {v}%
                  </button>
                ))}
              </div>
              <p className="text-xs text-gray-600 mt-1.5">
                Tasa que se desglosa en boletas y facturas (los precios de la carta ya la incluyen). Confirma con tu contador
                la tasa que corresponde al régimen del restaurante.
              </p>
            </div>
            <div>
              <label className={labelCls}>Mensaje al pie (opcional)</label>
              <input value={form.mensajePie} onChange={set('mensajePie')} maxLength={200} placeholder="¡Gracias por su preferencia!" className={inputCls} />
            </div>
          </Seccion>

          <Seccion icon={ImagePlus} titulo="Logo">
            <div className="flex items-center gap-4">
              <div className="w-24 h-24 rounded-2xl bg-white flex items-center justify-center overflow-hidden border border-gray-700">
                {form.logoBase64 ? (
                  <img src={form.logoBase64} alt="Logo" className="max-w-full max-h-full object-contain" />
                ) : (
                  <ImagePlus className="text-gray-400" size={28} />
                )}
              </div>
              <div className="space-y-2">
                <input ref={fileRef} type="file" accept="image/*" onChange={elegirLogo} className="hidden" />
                <button
                  type="button"
                  onClick={() => fileRef.current?.click()}
                  className="px-4 py-2.5 rounded-xl bg-background border border-gray-700 text-sm font-bold text-gray-200 hover:border-gray-500"
                >
                  {form.logoBase64 ? 'Cambiar logo' : 'Subir logo'}
                </button>
                {form.logoBase64 && (
                  <button
                    type="button"
                    onClick={() => set('logoBase64')('')}
                    className="flex items-center gap-1.5 text-xs text-red-400 hover:text-red-300"
                  >
                    <Trash2 size={13} /> Quitar logo
                  </button>
                )}
                <p className="text-[11px] text-gray-600">Se reduce automáticamente a {LOGO_MAX_PX} px. Mejor si tiene fondo blanco o transparente.</p>
              </div>
            </div>
          </Seccion>
        </div>

        {/* Vista previa del encabezado del comprobante */}
        <div className="lg:sticky lg:top-6">
          <p className={labelCls}>Vista previa</p>
          <div className="paleta-normal bg-white text-black rounded-2xl p-5 font-mono text-[12px] text-center shadow-xl">
            {form.logoBase64 && <img src={form.logoBase64} alt="" className="h-14 mx-auto mb-2 object-contain" />}
            <p className="font-extrabold text-[14px]">{form.nombreComercial || 'NOMBRE COMERCIAL'}</p>
            <p>{form.razonSocial || 'RAZÓN SOCIAL'}</p>
            <p>RUC {form.ruc || '—'}</p>
            <p className="text-[11px]">{form.direccion || 'Dirección fiscal'}</p>
            {form.telefono && <p className="text-[11px]">Tel. {form.telefono}</p>}
            <div className="border border-black my-3 py-1.5">
              <p className="font-bold">BOLETA DE VENTA ELECTRÓNICA</p>
              <p className="font-bold">{proximos[form.serieBoleta] ?? `${form.serieBoleta || 'B001'}-00000001`}</p>
            </div>
            <p className="text-[11px] text-left">IGV ({Number(form.igvPorcentaje) || 0}%)</p>
            <div className="border-t border-dashed border-gray-400 my-2" />
            <p className="text-[11px]">{form.mensajePie || '¡Gracias por su preferencia!'}</p>
          </div>
        </div>
      </div>

      {/* Barra de guardar */}
      <div className="fixed bottom-0 inset-x-0 p-4 bg-background/90 backdrop-blur border-t border-gray-800/60">
        <motion.button
          whileTap={{ scale: 0.97 }}
          onClick={guardar}
          disabled={isSaving}
          className="w-full max-w-xl mx-auto bg-primary hover:bg-primaryHover disabled:opacity-60 text-white font-extrabold py-4 rounded-2xl flex items-center justify-center gap-2 shadow-glow-orange"
        >
          {isSaving ? <Loader2 size={18} className="animate-spin" /> : <Save size={18} />}
          Guardar cambios
        </motion.button>
      </div>
    </div>
  );
}
