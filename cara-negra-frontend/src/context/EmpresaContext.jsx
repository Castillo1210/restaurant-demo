import { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { getEmpresaPublica } from '../services/empresaService';

/**
 * Datos públicos de la empresa (nombre comercial, logo, mensaje al pie) para toda la app:
 * login, recibos, título de la pestaña. Vienen de la BD (Admin → Datos de la empresa), así que
 * cambiar de restaurante no requiere tocar código.
 */
const EmpresaContext = createContext({
  nombreComercial: '',
  ruc: '',
  razonSocial: '',
  direccion: '',
  logo: null,
  mensajePie: '',
  recargar: () => {},
});

const TITULO_POR_DEFECTO = 'Gestión de sala';

export function EmpresaProvider({ children }) {
  const [empresa, setEmpresa] = useState({ nombreComercial: '', ruc: '', razonSocial: '', direccion: '', logo: null, mensajePie: '' });

  const recargar = useCallback(() => {
    getEmpresaPublica()
      .then((d) =>
        setEmpresa({
          nombreComercial: d?.nombreComercial ?? '',
          ruc: d?.ruc ?? '',
          razonSocial: d?.razonSocial ?? '',
          direccion: d?.direccion ?? '',
          logo: d?.logoBase64 || null,
          mensajePie: d?.mensajePie ?? '',
        })
      )
      .catch(() => {
        // Sin backend todavía: se usan textos genéricos.
      });
  }, []);

  useEffect(() => {
    recargar();
  }, [recargar]);

  useEffect(() => {
    document.title = empresa.nombreComercial
      ? `${empresa.nombreComercial} — ${TITULO_POR_DEFECTO}`
      : TITULO_POR_DEFECTO;
  }, [empresa.nombreComercial]);

  // Ícono de la pestaña: el logo de la empresa (si se cargó uno); si no, el genérico de index.html.
  useEffect(() => {
    const link = document.querySelector('link[rel="icon"]');
    if (!link) return;
    if (!link.dataset.porDefecto) {
      link.dataset.porDefecto = link.getAttribute('href') ?? '';
      link.dataset.tipoPorDefecto = link.getAttribute('type') ?? '';
    }
    if (empresa.logo) {
      link.setAttribute('href', empresa.logo);
      link.removeAttribute('type');
    } else {
      link.setAttribute('href', link.dataset.porDefecto);
      if (link.dataset.tipoPorDefecto) link.setAttribute('type', link.dataset.tipoPorDefecto);
    }
  }, [empresa.logo]);

  return <EmpresaContext.Provider value={{ ...empresa, recargar }}>{children}</EmpresaContext.Provider>;
}

export const useEmpresa = () => useContext(EmpresaContext);
