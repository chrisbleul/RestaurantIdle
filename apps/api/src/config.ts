import { z } from 'zod';

/**
 * Umgebung einmal beim Start pruefen und danach nur noch dieses Objekt benutzen.
 * Fehlt etwas, soll der Prozess sofort und mit klarer Meldung sterben -- nicht
 * erst bei der ersten Anfrage.
 */
const EnvSchema = z.object({
  NODE_ENV: z.enum(['development', 'test', 'production']).default('development'),
  PORT: z.coerce.number().int().positive().default(3000),
  HOST: z.string().default('0.0.0.0'),
  DATABASE_URL: z.string().min(1, 'DATABASE_URL fehlt'),
  /** Wird beim Bauen des Images gesetzt, lokal leer. */
  GIT_REVISION: z.string().default('dev'),
});

export type Config = Readonly<z.infer<typeof EnvSchema>>;

export function loadConfig(env: NodeJS.ProcessEnv = process.env): Config {
  const parsed = EnvSchema.safeParse(env);

  if (!parsed.success) {
    const details = parsed.error.issues
      .map((issue) => `  ${issue.path.join('.')}: ${issue.message}`)
      .join('\n');
    throw new Error(`Konfiguration unvollstaendig:\n${details}`);
  }

  return Object.freeze(parsed.data);
}
