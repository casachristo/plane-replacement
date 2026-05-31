import './globals.css';
import type { Metadata } from 'next';
import type { ReactNode } from 'react';

export const metadata: Metadata = {
  title: 'Waypoint',
  description: 'Cairn-native issue tracker',
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <body>
        <header className="border-b border-[var(--border)] px-6 py-4 flex items-center justify-between">
          <a href="/" className="text-lg font-semibold tracking-tight">
            <span className="text-[var(--accent)]">⟁</span> Waypoint
          </a>
          <nav className="flex gap-4 text-sm">
            <a href="/projects" className="text-[var(--muted)] hover:text-[var(--foreground)]">Projects</a>
            <a href="/auth/login" className="text-[var(--muted)] hover:text-[var(--foreground)]">Sign in</a>
          </nav>
        </header>
        <main className="max-w-5xl mx-auto px-6 py-10">{children}</main>
      </body>
    </html>
  );
}
