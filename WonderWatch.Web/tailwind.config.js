/** @type {import('tailwindcss').Config} */
module.exports = {
  content:[
    './Views/**/*.cshtml',
    './wwwroot/js/**/*.js'
  ],
  theme: {
    // OVERRIDING colors completely to forbid default zinc/gray/slate tokens
    colors: {
      void: '#0A0A0A',
      surface: '#1A1A1A',
      'surface-alt': '#181818',
      gold: '#C9A74A',
      'gold-dim': '#8A816C',
      parchment: '#F5F0E8',
      body: '#F1F5F9',
      muted: '#B4AFA2',
      'gold-faint': 'rgba(201,167,74,0.2)',
      'white-faint': 'rgba(255,255,255,0.05)',
      danger: '#EF4444',
      success: '#22C55E',
      transparent: 'transparent',
      current: 'currentColor',
      white: '#FFFFFF',
      black: '#000000'
    },
    // OVERRIDING border radius to enforce sharp edges everywhere except avatars
    borderRadius: {
      none: '0',
      full: '9999px'
    },
    fontFamily: {
      serif: ['"Playfair Display"', 'serif'],
      'serif-italic':['"Liberation Serif"', 'serif'],
      mono: ['"Liberation Mono"', 'monospace'],
      sans:['"Manrope"', 'sans-serif']
    },
    extend: {
      transitionTimingFunction: {
        DEFAULT: 'cubic-bezier(0.16, 1, 0.3, 1)',
      },
      transitionDuration: {
        DEFAULT: '600ms',
      },
      boxShadow: {
        DEFAULT: '0 25px 50px -12px rgba(0,0,0,0.25)',
      },
      spacing: {
        // Explicitly defining specific pixel values requested in the Figma spec
        '3px': '3px',
        '4px': '4px',
        '8px': '8px',
        '9px': '9px',
        '11px': '11px',
        '12px': '12px',
        '16px': '16px',
        '17px': '17px',
        '24px': '24px',
        '32px': '32px',
        '131px': '131px',
        '256px': '256px',
        '288px': '288px',
      }
    },
  },
  plugins: [],
}