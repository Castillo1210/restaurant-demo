import { useState, useEffect } from 'react';

/**
 * Hook que retorna la hora actual actualizada cada segundo.
 * @param {string} [locale='es-ES']
 * @returns {{ time: string, date: string }}
 */
export function useClock(locale = 'es-ES') {
  const [now, setNow] = useState(new Date());

  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(id);
  }, []);

  const time = now.toLocaleTimeString(locale, {
    hour: '2-digit',
    minute: '2-digit',
  });

  const date = now.toLocaleDateString(locale, {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
  });

  return { time, date };
}
