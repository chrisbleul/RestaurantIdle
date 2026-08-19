import { describe, expect, it } from 'vitest';
import { computeOfflineSeconds } from './save.js';

describe('computeOfflineSeconds', () => {
  it('liefert die verstrichene Zeit in Sekunden', () => {
    const lastSeen = new Date('2026-01-01T00:00:00Z');
    const now = new Date('2026-01-01T01:00:00Z');
    expect(computeOfflineSeconds(lastSeen, now)).toBe(3600);
  });

  it('deckelt bei 24 Stunden (Plan Abschnitt 2)', () => {
    const lastSeen = new Date('2026-01-01T00:00:00Z');
    const now = new Date('2026-01-05T00:00:00Z');
    expect(computeOfflineSeconds(lastSeen, now)).toBe(24 * 60 * 60);
  });

  it('liefert nie einen negativen Wert (last_seen_at in der Zukunft, Uhr-Drift)', () => {
    const lastSeen = new Date('2026-01-01T01:00:00Z');
    const now = new Date('2026-01-01T00:00:00Z');
    expect(computeOfflineSeconds(lastSeen, now)).toBe(0);
  });

  it('ist 0 fuer denselben Zeitpunkt', () => {
    const t = new Date('2026-01-01T00:00:00Z');
    expect(computeOfflineSeconds(t, t)).toBe(0);
  });
});
