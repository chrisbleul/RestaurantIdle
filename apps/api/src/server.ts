import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import Fastify from 'fastify';
import fastifyStatic from '@fastify/static';
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

// Der WebGL-Build (Unity, siehe .github/workflows/webgl-build-deploy.yml) landet
// hier als statische Dateien. nginx routet /restaurant/ bereits auf diesen
// Port (Abschnitt 2 der Arbeitsanweisung schneidet den Praefix ab) -- kein
// eigener "web"-Service noetig wie bei den anderen Apps mit Caddy davor.
// Registrierung nach den API-Routen, damit /api/* Vorrang vor dem
// Wildcard-Static-Handler behaelt. Ordner fehlt in lokaler Entwicklung ohne
// Build -- dann bleibt "/" schlicht unbeantwortet statt beim Start zu crashen.
const webglDir = path.join(path.dirname(fileURLToPath(import.meta.url)), '../public/webgl');
if (existsSync(webglDir)) {
  await app.register(fastifyStatic, {
    root: webglDir,
    // Unitys WebGL-Build liefert .js/.wasm/.data bereits gzip-komprimiert als
    // *.gz aus (Player Settings -> Compression Format: Gzip). Der Browser
    // dekomprimiert das nur automatisch, wenn Content-Encoding gesetzt ist --
    // ohne das versucht Unitys eigener Loader, die rohen komprimierten Bytes
    // direkt als JS/Wasm zu parsen ("Unable to parse ... .gz!").
    setHeaders: (res, filePath) => {
      if (!filePath.endsWith('.gz')) {
        return;
      }

      res.header('Content-Encoding', 'gzip');
      const withoutGz = filePath.slice(0, -'.gz'.length);
      if (withoutGz.endsWith('.js')) {
        res.header('Content-Type', 'application/javascript');
      } else if (withoutGz.endsWith('.wasm')) {
        res.header('Content-Type', 'application/wasm');
      } else if (withoutGz.endsWith('.data') || withoutGz.endsWith('.symbols.json')) {
        res.header('Content-Type', 'application/octet-stream');
      }
    },
  });
} else {
  app.log.warn({ webglDir }, 'Kein WebGL-Build gefunden, "/" liefert 404');
}

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
