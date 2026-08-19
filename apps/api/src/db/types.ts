export interface HealthResponse {
  status: 'ok' | 'degraded';
  database: 'up' | 'down';
  uptimeSeconds: number;
  revision: string;
}

/**
 * Spielzustand als undurchsichtiges JSON -- Stationen, Upgrades, Manager.
 * Die API kennt seine Struktur bewusst nicht (Plan Abschnitt 5, Save-Schema):
 * das Format lebt im Unity-Client (BalancingCore) und darf sich aendern,
 * ohne dass die API-Route oder die Migration angefasst werden muss.
 */
export type GameState = Record<string, unknown>;

export interface SaveRow {
  user_id: string;
  state: GameState;
  lifetime_revenue: string;
  prestige_stars: string;
  last_seen_at: string;
}

export interface SaveResponse {
  state: GameState;
  lifetimeRevenue: string;
  prestigeStars: string;
  /**
   * Seit dem letzten Speichern verstrichene Zeit, serverseitig gemessen und
   * bei 24h gedeckelt (Plan Abschnitt 2 und 8: "Systemuhr-Manipulation ->
   * Offline-Progress serverseitig"). Der Client rechnet daraus mit
   * BalancingCore.OfflineEarnings den tatsaechlichen Ertrag aus -- die API
   * kennt die Einkommensrate des Spielstands nicht und soll sie auch nicht
   * kennen muessen.
   */
  offlineSeconds: number;
}
