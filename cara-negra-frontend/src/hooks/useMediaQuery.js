import { useEffect, useState } from 'react';

/** true mientras la pantalla cumpla la media query (p. ej. '(min-width: 1024px)'). */
export default function useMediaQuery(consulta) {
  const [cumple, setCumple] = useState(() => typeof window !== 'undefined' && window.matchMedia(consulta).matches);
  useEffect(() => {
    const mq = window.matchMedia(consulta);
    const alCambiar = () => setCumple(mq.matches);
    alCambiar();
    mq.addEventListener('change', alCambiar);
    return () => mq.removeEventListener('change', alCambiar);
  }, [consulta]);
  return cumple;
}
