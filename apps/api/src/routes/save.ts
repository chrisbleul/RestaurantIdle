import type { FastifyInstance } from 'fastify';
import type { Pool } from 'pg';
import { z } from 'zod';
import { requireAuth } from '@cgo/platform-auth/fastify';
import type { GameState, SaveResponse, SaveRow } from '../db/types.js';

/** Deckelt Offline-Ertrag serverseitig bei 24h (Plan Abschnitt 2) -- Autoritaet ueber die Zeit bleibt beim Server. */
const OFFLINE_CAP_SECONDS = 24 * 60 * 60;

/** BigDouble.ToString() kann Dezimal- oder wissenschaftliche Notation liefern (z. B. "1.23e45"). */
const NumericString = z.string().regex(/^-?\d+(\.\d+)?([eE][+-]?\d+)?$/, 'keine gueltige Zahl');

const SaveBody = z.object({
  state: z.record(z.string(), z.unknown()),
  lifetimeRevenue: NumericString,
  prestigeStars: NumericString,
});

/** Serverautoritative Offline-Zeit (Plan Abschnitt 2/8) -- eigene Funktion, damit die Deckelung ohne DB testbar ist. */
export function computeOfflineSeconds(lastSeenAt: Date, now: Date): number {
  const elapsedSeconds = (now.getTime() - lastSeenAt.getTime()) / 1000;
  return Math.max(0, Math.min(elapsedSeconds, OFFLINE_CAP_SECONDS));
}

export function registerSaveRoutes(app: FastifyInstance, pool: Pool): void {
  app.get('/api/save', { preHandler: requireAuth }, async (request): Promise<SaveResponse> => {
    const { rows } = await pool.query<SaveRow>(
      `select user_id, state, lifetime_revenue, prestige_stars, last_seen_at
       from saves where user_id = $1`,
      [request.user!.userId],
    );

    const row = rows[0];
    if (!row) {
      return { state: {}, lifetimeRevenue: '0', prestigeStars: '0', offlineSeconds: 0 };
    }

    const offlineSeconds = computeOfflineSeconds(new Date(row.last_seen_at), new Date());

    return {
      state: row.state,
      lifetimeRevenue: row.lifetime_revenue,
      prestigeStars: row.prestige_stars,
      offlineSeconds,
    };
  });

  app.put('/api/save', { preHandler: requireAuth }, async (request, reply) => {
    const parsed = SaveBody.safeParse(request.body);
    if (!parsed.success) {
      return reply.code(400).send({ error: 'Ungueltiger Speicherstand', issues: parsed.error.issues });
    }
    const body = parsed.data;

    await pool.query(
      `insert into saves (user_id, state, lifetime_revenue, prestige_stars, last_seen_at)
       values ($1, $2, $3, $4, now())
       on conflict (user_id) do update set
         state = excluded.state,
         lifetime_revenue = excluded.lifetime_revenue,
         prestige_stars = excluded.prestige_stars,
         last_seen_at = excluded.last_seen_at`,
      [request.user!.userId, JSON.stringify(body.state satisfies GameState), body.lifetimeRevenue, body.prestigeStars],
    );

    return reply.code(204).send();
  });
}
