# Zhadina Budget App

Personal budgeting app. Iteration 1: sign up, log in, sign out, and a protected dashboard.

| Part      | Tech                                                             |
| --------- | ---------------------------------------------------------------- |
| Frontend  | Next.js 16 (App Router, TypeScript, Tailwind CSS 4), `frontend/` |
| Backend   | ASP.NET Core 10 Minimal API, `backend/Zhadina.Api/`              |
| Auth      | ASP.NET Core Identity with an HttpOnly cookie (`zhadina_auth`)   |
| Database  | PostgreSQL via EF Core + Npgsql                                  |

The browser only talks to Next.js. Next.js rewrites `/api/*` to the C# API on `127.0.0.1:5000`, so there's one origin, no CORS, and the API is never exposed publicly.

## API

| Method | Route                | Body                  | Result                                   |
| ------ | -------------------- | --------------------- | ---------------------------------------- |
| POST   | `/api/auth/sign-up`  | `{ email, password }` | `201` + cookie, or `400` field errors    |
| POST   | `/api/auth/login`    | `{ email, password }` | `200` + cookie, `401`, or `423` locked   |
| POST   | `/api/auth/sign-out` | none                  | `204` (clears cookie, no DB access)      |
| GET    | `/api/auth/me`       | none                  | `200 { id, email }` or `401`             |
| GET    | `/api/health`        | none                  | `200`                                    |

Login and sign-up are rate limited per IP (10/min). Five failed logins lock the account for 5 minutes.

## Local development

Prerequisites: .NET 10 SDK, Node 22, and PostgreSQL on `localhost:5432` with a `postgres` user that can log in without a password (trust auth). The connection string is in `backend/Zhadina.Api/appsettings.Development.json`.

### PostgreSQL on Linux (container)

On Linux, Postgres runs in a Podman container. With Docker, use the same commands and replace `podman` with `docker`.

Create the container once. It runs a trust-auth server on `127.0.0.1:5432` with a `zhadina_dev` database, and stores the data in the `zhadina-pgdata` volume, so the data survives restarts:

```bash
podman run -d --name zhadina-postgres \
  -p 127.0.0.1:5432:5432 \
  -e POSTGRES_HOST_AUTH_METHOD=trust \
  -e POSTGRES_DB=zhadina_dev \
  -v zhadina-pgdata:/var/lib/postgresql/data \
  postgres:17
```

Trust auth means there's no password, so keep the port bound to `127.0.0.1`.

After that, use `start` instead of `run`. The container doesn't start by itself after a reboot, and `dev.sh` doesn't start it either:

```bash
podman start zhadina-postgres
podman exec zhadina-postgres pg_isready -U postgres   # expect "accepting connections"
podman stop zhadina-postgres                          # when you're done
```

Running `podman run` a second time fails, because a container with that name already exists. If `start` fails because port 5432 is in use, another Postgres server or container is using that port. Stop it first.

To wipe the local database, run `podman rm -f zhadina-postgres && podman volume rm zhadina-pgdata`, then run the create command again. The API re-applies migrations the next time it starts.

### Running the app

With Postgres running:

```bash
bash scripts/linux/dev.sh    # API on 127.0.0.1:5000 (applies migrations on start) + Next.js on :3000
```

On Windows (PowerShell), the same scripts live in `scripts/windows/`. You only need the .NET 10 SDK and Node 22: `dev.ps1` first runs `db.ps1`, which on first use downloads a portable PostgreSQL 17 into `.postgres/` (gitignored, ~140 MB) and creates a trust-auth server on `localhost:5432`. The server keeps running after `dev.ps1` exits.

```powershell
powershell -ExecutionPolicy Bypass -File scripts\windows\dev.ps1           # DB + API + Next.js
powershell -ExecutionPolicy Bypass -File scripts\windows\db.ps1 stop       # also: start, status
```

To wipe the local database, stop it and delete `.postgres\data`; the next start re-creates it and the API re-applies migrations.

> **Switching between WSL and Windows:** `node_modules` contains platform-specific binaries (Next's SWC compiler, the `.bin` launchers). Run `npm ci` in `frontend/` from the OS you're about to use. An install from WSL won't run on Windows (`'next' is not recognized`), and vice versa.

Then open http://localhost:3000.

Tests (each run creates and drops its own `zhadina_test_*` database):

```bash
cd backend && dotnet test
```

New migration after changing the model:

```bash
cd backend && dotnet tool restore && dotnet ef migrations add <Name> --project Zhadina.Api --output-dir Data/Migrations
```

> **WSL without libicu:** if `dotnet` crashes with "Couldn't find a valid ICU package", export `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`.

## Deploying on Replit

1. Import the repo into Replit and add a PostgreSQL database. Replit sets `DATABASE_URL`, which the API converts to an Npgsql connection string.
2. `.replit` / `replit.nix` provide Node 22 and the .NET 10 SDK.
3. Deploy. The build runs `scripts/linux/build.sh`, and the app starts with `scripts/linux/start.sh`. Only port 3000 (Next.js) is public.

Migrations run automatically on API startup against whichever database `DATABASE_URL` points to.
