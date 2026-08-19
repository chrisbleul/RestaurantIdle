import type { FastifyInstance } from 'fastify';
import type { Pool } from 'pg';
import type { HealthResponse } from '../db/types.js';
import type { Config } from '../config.js';

/**
 * Der Endpunkt, an dem das Deploy-Skript erkennt, ob der neue Stand lebt.
 * Antwortet auch dann mit 200, wenn die Datenbank weg ist -- sonst wuerde ein
 * Rollback ausgeloest, obwohl die Anwendung selbst in Ordnung ist. Der Zustand
 * der Datenbank steht im Rumpf und wird dort ausgewertet.
 */
export function registerHealthRoute(app: FastifyInstance, pool: Pool, config: Config): void {
  app.get('/api/health', async (): Promise<HealthResponse> => {
    let database: HealthResponse['database'] = 'down';

    try {
      await pool.query('SELECT 1');
      database = 'up';
    } catch (error) {
      app.log.error({ err: error }, 'Datenbank nicht erreichbar');
    }

    return {
      status: database === 'up' ? 'ok' : 'degraded',
      database,
      uptimeSeconds: Math.floor(process.uptime()),
      revision: config.GIT_REVISION,
    };
  });
}
