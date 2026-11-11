/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Inter', 'system-ui', '-apple-system', 'sans-serif'],
      },
      // Colores por variables CSS (src/tema.css): tema claro por defecto y oscuro opcional.
      colors: {
        background: 'rgb(var(--c-fondo) / <alpha-value>)',
        card: 'rgb(var(--c-card) / <alpha-value>)',
        cardHighlight: 'rgb(var(--c-card-hl) / <alpha-value>)',
        ink: 'rgb(var(--c-tinta) / <alpha-value>)',
        primary: '#ff7a00',
        primaryHover: '#e66c00',
        accentGreen: 'rgb(var(--c-verde) / <alpha-value>)',
        accentYellow: 'rgb(var(--c-amarillo) / <alpha-value>)',
        gray: { 50: 'rgb(var(--gray-50) / <alpha-value>)', 100: 'rgb(var(--gray-100) / <alpha-value>)', 200: 'rgb(var(--gray-200) / <alpha-value>)', 300: 'rgb(var(--gray-300) / <alpha-value>)', 400: 'rgb(var(--gray-400) / <alpha-value>)', 500: 'rgb(var(--gray-500) / <alpha-value>)', 600: 'rgb(var(--gray-600) / <alpha-value>)', 700: 'rgb(var(--gray-700) / <alpha-value>)', 800: 'rgb(var(--gray-800) / <alpha-value>)', 900: 'rgb(var(--gray-900) / <alpha-value>)', 950: 'rgb(var(--gray-950) / <alpha-value>)' },
        red: { 50: 'rgb(var(--red-50) / <alpha-value>)', 100: 'rgb(var(--red-100) / <alpha-value>)', 200: 'rgb(var(--red-200) / <alpha-value>)', 300: 'rgb(var(--red-300) / <alpha-value>)', 400: 'rgb(var(--red-400) / <alpha-value>)', 500: 'rgb(var(--red-500) / <alpha-value>)', 600: 'rgb(var(--red-600) / <alpha-value>)', 700: 'rgb(var(--red-700) / <alpha-value>)', 800: 'rgb(var(--red-800) / <alpha-value>)', 900: 'rgb(var(--red-900) / <alpha-value>)', 950: 'rgb(var(--red-950) / <alpha-value>)' },
        orange: { 50: 'rgb(var(--orange-50) / <alpha-value>)', 100: 'rgb(var(--orange-100) / <alpha-value>)', 200: 'rgb(var(--orange-200) / <alpha-value>)', 300: 'rgb(var(--orange-300) / <alpha-value>)', 400: 'rgb(var(--orange-400) / <alpha-value>)', 500: 'rgb(var(--orange-500) / <alpha-value>)', 600: 'rgb(var(--orange-600) / <alpha-value>)', 700: 'rgb(var(--orange-700) / <alpha-value>)', 800: 'rgb(var(--orange-800) / <alpha-value>)', 900: 'rgb(var(--orange-900) / <alpha-value>)', 950: 'rgb(var(--orange-950) / <alpha-value>)' },
        amber: { 50: 'rgb(var(--amber-50) / <alpha-value>)', 100: 'rgb(var(--amber-100) / <alpha-value>)', 200: 'rgb(var(--amber-200) / <alpha-value>)', 300: 'rgb(var(--amber-300) / <alpha-value>)', 400: 'rgb(var(--amber-400) / <alpha-value>)', 500: 'rgb(var(--amber-500) / <alpha-value>)', 600: 'rgb(var(--amber-600) / <alpha-value>)', 700: 'rgb(var(--amber-700) / <alpha-value>)', 800: 'rgb(var(--amber-800) / <alpha-value>)', 900: 'rgb(var(--amber-900) / <alpha-value>)', 950: 'rgb(var(--amber-950) / <alpha-value>)' },
        yellow: { 50: 'rgb(var(--yellow-50) / <alpha-value>)', 100: 'rgb(var(--yellow-100) / <alpha-value>)', 200: 'rgb(var(--yellow-200) / <alpha-value>)', 300: 'rgb(var(--yellow-300) / <alpha-value>)', 400: 'rgb(var(--yellow-400) / <alpha-value>)', 500: 'rgb(var(--yellow-500) / <alpha-value>)', 600: 'rgb(var(--yellow-600) / <alpha-value>)', 700: 'rgb(var(--yellow-700) / <alpha-value>)', 800: 'rgb(var(--yellow-800) / <alpha-value>)', 900: 'rgb(var(--yellow-900) / <alpha-value>)', 950: 'rgb(var(--yellow-950) / <alpha-value>)' },
        emerald: { 50: 'rgb(var(--emerald-50) / <alpha-value>)', 100: 'rgb(var(--emerald-100) / <alpha-value>)', 200: 'rgb(var(--emerald-200) / <alpha-value>)', 300: 'rgb(var(--emerald-300) / <alpha-value>)', 400: 'rgb(var(--emerald-400) / <alpha-value>)', 500: 'rgb(var(--emerald-500) / <alpha-value>)', 600: 'rgb(var(--emerald-600) / <alpha-value>)', 700: 'rgb(var(--emerald-700) / <alpha-value>)', 800: 'rgb(var(--emerald-800) / <alpha-value>)', 900: 'rgb(var(--emerald-900) / <alpha-value>)', 950: 'rgb(var(--emerald-950) / <alpha-value>)' },
        sky: { 50: 'rgb(var(--sky-50) / <alpha-value>)', 100: 'rgb(var(--sky-100) / <alpha-value>)', 200: 'rgb(var(--sky-200) / <alpha-value>)', 300: 'rgb(var(--sky-300) / <alpha-value>)', 400: 'rgb(var(--sky-400) / <alpha-value>)', 500: 'rgb(var(--sky-500) / <alpha-value>)', 600: 'rgb(var(--sky-600) / <alpha-value>)', 700: 'rgb(var(--sky-700) / <alpha-value>)', 800: 'rgb(var(--sky-800) / <alpha-value>)', 900: 'rgb(var(--sky-900) / <alpha-value>)', 950: 'rgb(var(--sky-950) / <alpha-value>)' },
        teal: { 50: 'rgb(var(--teal-50) / <alpha-value>)', 100: 'rgb(var(--teal-100) / <alpha-value>)', 200: 'rgb(var(--teal-200) / <alpha-value>)', 300: 'rgb(var(--teal-300) / <alpha-value>)', 400: 'rgb(var(--teal-400) / <alpha-value>)', 500: 'rgb(var(--teal-500) / <alpha-value>)', 600: 'rgb(var(--teal-600) / <alpha-value>)', 700: 'rgb(var(--teal-700) / <alpha-value>)', 800: 'rgb(var(--teal-800) / <alpha-value>)', 900: 'rgb(var(--teal-900) / <alpha-value>)', 950: 'rgb(var(--teal-950) / <alpha-value>)' },
        violet: { 50: 'rgb(var(--violet-50) / <alpha-value>)', 100: 'rgb(var(--violet-100) / <alpha-value>)', 200: 'rgb(var(--violet-200) / <alpha-value>)', 300: 'rgb(var(--violet-300) / <alpha-value>)', 400: 'rgb(var(--violet-400) / <alpha-value>)', 500: 'rgb(var(--violet-500) / <alpha-value>)', 600: 'rgb(var(--violet-600) / <alpha-value>)', 700: 'rgb(var(--violet-700) / <alpha-value>)', 800: 'rgb(var(--violet-800) / <alpha-value>)', 900: 'rgb(var(--violet-900) / <alpha-value>)', 950: 'rgb(var(--violet-950) / <alpha-value>)' },
        fuchsia: { 50: 'rgb(var(--fuchsia-50) / <alpha-value>)', 100: 'rgb(var(--fuchsia-100) / <alpha-value>)', 200: 'rgb(var(--fuchsia-200) / <alpha-value>)', 300: 'rgb(var(--fuchsia-300) / <alpha-value>)', 400: 'rgb(var(--fuchsia-400) / <alpha-value>)', 500: 'rgb(var(--fuchsia-500) / <alpha-value>)', 600: 'rgb(var(--fuchsia-600) / <alpha-value>)', 700: 'rgb(var(--fuchsia-700) / <alpha-value>)', 800: 'rgb(var(--fuchsia-800) / <alpha-value>)', 900: 'rgb(var(--fuchsia-900) / <alpha-value>)', 950: 'rgb(var(--fuchsia-950) / <alpha-value>)' },
        rose: { 50: 'rgb(var(--rose-50) / <alpha-value>)', 100: 'rgb(var(--rose-100) / <alpha-value>)', 200: 'rgb(var(--rose-200) / <alpha-value>)', 300: 'rgb(var(--rose-300) / <alpha-value>)', 400: 'rgb(var(--rose-400) / <alpha-value>)', 500: 'rgb(var(--rose-500) / <alpha-value>)', 600: 'rgb(var(--rose-600) / <alpha-value>)', 700: 'rgb(var(--rose-700) / <alpha-value>)', 800: 'rgb(var(--rose-800) / <alpha-value>)', 900: 'rgb(var(--rose-900) / <alpha-value>)', 950: 'rgb(var(--rose-950) / <alpha-value>)' },
      },
      boxShadow: {
        'glow-green': '0 0 20px rgba(16, 185, 129, 0.2)',
        'glow-orange': '0 0 20px rgba(255, 122, 0, 0.25)',
        'glow-yellow': '0 0 20px rgba(234, 179, 8, 0.2)',
      },
      animation: {
        'pop': 'pop 0.15s ease-out',
        'slide-up': 'slideUp 0.3s ease-out',
        'fade-in': 'fadeIn 0.2s ease-out',
      },
      keyframes: {
        pop: {
          '0%': { transform: 'scale(1)' },
          '50%': { transform: 'scale(1.2)' },
          '100%': { transform: 'scale(1)' },
        },
        slideUp: {
          '0%': { transform: 'translateY(100%)', opacity: 0 },
          '100%': { transform: 'translateY(0)', opacity: 1 },
        },
        fadeIn: {
          '0%': { opacity: 0 },
          '100%': { opacity: 1 },
        },
      },
    },
  },
  plugins: [],
}