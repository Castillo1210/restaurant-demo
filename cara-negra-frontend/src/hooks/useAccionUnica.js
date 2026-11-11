import { useCallback, useRef } from 'react';

/**
 * Evita el "doble toque": mientras una acción (cobrar, emitir, enviar pedido...) está en
 * curso, cualquier otro intento se ignora. A diferencia de `disabled={cargando}`, funciona
 * aunque los dos toques lleguen antes de que React vuelva a pintar el botón.
 *
 *   const unaVez = useAccionUnica();
 *   const handleCobrar = () => unaVez(async () => { ... });
 */
export default function useAccionUnica() {
  const enCurso = useRef(false);
  return useCallback(async (accion) => {
    if (enCurso.current) return undefined;
    enCurso.current = true;
    try {
      return await accion();
    } finally {
      enCurso.current = false;
    }
  }, []);
}
