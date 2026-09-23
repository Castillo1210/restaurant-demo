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
      colors: {
        background: '#0c0c0c',
        card: '#1a1a1a',
        cardHighlight: '#262626',
        primary: '#ff7a00',
        primaryHover: '#e66c00',
        accentGreen: '#10b981',
        accentYellow: '#eab308',
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