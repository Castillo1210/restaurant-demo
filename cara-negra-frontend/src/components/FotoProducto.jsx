import { useState } from 'react';
import { UtensilsCrossed } from 'lucide-react';

/**
 * Foto de un producto con respaldo: si no tiene foto (o no carga), muestra el ícono de plato.
 * En listas largas usa `loading="lazy"` (solo se descargan las fotos que se ven en pantalla);
 * para una vista previa puntual, pasar `diferida={false}`.
 */
export default function FotoProducto({ src, alt = '', className = '', iconSize = 24, diferida = true }) {
  // Se recuerda QUÉ foto falló: si cambia la foto (otra versión), se vuelve a intentar.
  const [srcFallido, setSrcFallido] = useState(null);

  if (!src || srcFallido === src) {
    return (
      <div className={`bg-cardHighlight/60 flex items-center justify-center ${className}`}>
        <UtensilsCrossed size={iconSize} className="text-gray-600" />
      </div>
    );
  }

  return (
    <img
      src={src}
      alt={alt}
      loading={diferida ? 'lazy' : 'eager'}
      decoding="async"
      onError={() => setSrcFallido(src)}
      className={`object-cover bg-cardHighlight/60 ${className}`}
    />
  );
}
