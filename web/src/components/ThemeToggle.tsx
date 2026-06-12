'use client';

import { useEffect, useState } from 'react';

const KEY = 'waypoint-theme';

function initialNightMode() {
  if (typeof window === 'undefined') return false;
  const saved = window.localStorage.getItem(KEY);
  if (saved === 'night') return true;
  if (saved === 'day') return false;
  return window.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false;
}

export function ThemeToggle() {
  const [nightMode, setNightMode] = useState(initialNightMode);

  useEffect(() => {
    const theme = nightMode ? 'night' : 'day';
    document.documentElement.dataset.theme = theme;
    document.documentElement.style.colorScheme = nightMode ? 'dark' : 'light';
    window.localStorage.setItem(KEY, theme);
  }, [nightMode]);

  return (
    <button
      className="theme-toggle"
      type="button"
      aria-label={nightMode ? 'Switch to day mode' : 'Switch to night mode'}
      aria-pressed={nightMode}
      title={nightMode ? 'Day mode' : 'Night mode'}
      onClick={() => setNightMode((value) => !value)}
    >
      <span aria-hidden="true" />
    </button>
  );
}
