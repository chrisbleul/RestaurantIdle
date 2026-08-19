-- Save-Schema aus PLAN.md Abschnitt 5. Ein Save pro Nutzer (user_id kommt aus
-- dem X-Auth-User-Id-Header von cgo-auth, es gibt keine eigene users-Tabelle --
-- siehe @cgo/platform-auth).
--
-- prestige_stars als numeric statt der im Plan skizzierten "INT": k*sqrt(revenue)
-- ist BalancingCore.Prestige zufolge ein BigDouble, kein int -- Abschnitt 2 verlangt
-- ausdruecklich, von Anfang an durchgaengig BigDouble-faehige Spalten zu verwenden,
-- ein Nachziehen spaeter "zieht sich durch die komplette Codebase".

create table saves (
  user_id text primary key,
  state jsonb not null default '{}'::jsonb,
  lifetime_revenue numeric not null default 0,
  prestige_stars numeric not null default 0,
  last_seen_at timestamptz not null default now()
);
