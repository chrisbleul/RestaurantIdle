import Fastify from 'fastify';
import pg from 'pg';
import { loadConfig } from './config.js';
import { runMigrations } from './db/migrate.js';
import { registerHealthRoute } from './routes/health.js';
import { registerPlatformAuth } from '@cgo/platform-auth/fastify';
import { registerSaveRoutes } from './routes/save.js';

const config = loadConfig();

const app = Fastify({
  logger: {
    level: config.NODE_ENV === 'production' ? 'info' : 'debug',
  },
  // nginx steht davor und setzt X-Forwarded-*; ohne das steht in jedem
  // Logeintrag die IP des Proxys statt die des Aufrufers.
  trustProxy: true,
});

const pool = new pg.Pool({
  connectionString: config.DATABASE_URL,
  max: 10,
  // Lieber schnell scheitern als Anfragen minutenlang haengen lassen.
  connectionTimeoutMillis: 5_000,
});

await runMigrations(pool);

registerHealthRoute(app, pool, config);
registerPlatformAuth(app);
registerSaveRoutes(app, pool);

async function shutdown(signal: string): Promise<void> {
  app.log.info({ signal }, 'Beende');
  await app.close();
  await pool.end();
  process.exit(0);
}

for (const signal of ['SIGINT', 'SIGTERM'] as const) {
  process.on(signal, () => {
    void shutdown(signal);
  });
}

try {
  await app.listen({ port: config.PORT, host: config.HOST });
} catch (error) {
  app.log.error({ err: error }, 'Start fehlgeschlagen');
  process.exit(1);
}
