# AStudio ↔ local Docker hub

**Updated:** 2026-08-10

Desktop apps keep **SQLite** `firm.db` locally. Docker Postgres/Mongo belong to the
**hub** (`esti` compose). Binding = Activate → `syncToken` → Flush over HTTP —
not a direct Postgres connection from WinUI.

```text
AStudio firm.db (SQLite)
        │ Activate / Flush
        ▼
esti-backend :4000  ──► esti-db :5432 · esti-mongo :27017
```

## One-time / after reset

From the **esti** repo (compose already up):

```bat
docker compose up -d
docker compose exec backend pnpm --filter @esti/backend seed:desktop-local-hub
```

Seed prints `ESTI_PRODUCT_API_KEY` and `ESTI_LICENSE_KEY` (demo HLP-…).

## Launch / desktop “login”

There is **no email login** in AStudio — bind is **Activate** with an HLP key.

```bat
cd AStudio
run-local-hub.cmd
```

Writes `%LocalAppData%\AStudio\local-hub.json` and auto-Activates on launch when unbound.
Sync chip should read **Synced**. Manual path: taskbar **Activate** → `HLP-5JNZ-445W-M59T`.

## Smoke (optional)

```bat
set ESTI_HUB_URL=http://127.0.0.1:4000
set ESTI_LICENSE_API_URL=http://127.0.0.1:4000/platform
set ESTI_PRODUCT_API_KEY=hlp_sk_local_desktop_dev_do_not_use_in_prod
set ESTI_LICENSE_KEY=HLP-…   REM from seed output
dotnet run --project vendor\AQC\BBSDesktop\Aorms.Bridge.Smoke -c Release
```

Expect: `OK hub activate → meta Flush`.

## Demo data (hub → desktop → Ops)

Hub Postgres demo (`esti_projectoffice`) does **not** auto-appear on desktop.
Local-first: export → import firm.db → Flush `projectStatus` meta.

```bat
sync-demo-from-hub.cmd   REM writes hub-demo-projects.json + Connect catalog
run-local-hub.cmd        REM Activate + import empty firm.db + Flush
```

In-app: Home → **Sync hub demo**. Ops: web `/ops-db` after Flush (`esti_meta_event`).

## Not this

- Do **not** point AStudio at `postgres://esti:esti@127.0.0.1:5432` — that is hub DB only.
- Connect catalog import still uses `%LocalAppData%\AORMS-Connect\catalog.json` when Connect is present.
