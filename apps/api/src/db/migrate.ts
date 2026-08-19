import { readFile, readdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import type { Pool } from 'pg';

const migrationsDir = path.join(
  path.dirname(fileURLToPath(import.meta.url)),
  '../../migrations',
);

/**
 * Wendet neue .sql-Dateien aus migrations/ in Dateinamen-Reihenfolge an, je
 * eine Transaktion. Laeuft beim Start der API -- fuer eine Ein-Personen-App
 * reicht das, ein separates Migrationswerkzeug waere hier nur Ballast.
 */
export async function runMigrations(pool: Pool): Promise<void> {
  await pool.query(`
    create table if not exists schema_migrations (
      name text primary key,
      applied_at timestamptz not null default now()
    )
  `);

  const files = (await readdir(migrationsDir)).filter((f) => f.endsWith('.sql')).sort();
  const { rows } = await pool.query<{ name: string }>('select name from schema_migrations');
  const applied = new Set(rows.map((r) => r.name));

  for (const file of files) {
    if (applied.has(file)) {
      continue;
    }

    const sql = await readFile(path.join(migrationsDir, file), 'utf8');
    const client = await pool.connect();

    try {
      await client.query('begin');
      await client.query(sql);
      await client.query('insert into schema_migrations (name) values ($1)', [file]);
      await client.query('commit');
    } catch (error) {
      await client.query('rollback');
      throw new Error(`Migration ${file} fehlgeschlagen: ${(error as Error).message}`, {
        cause: error,
      });
    } finally {
      client.release();
    }
  }
}
