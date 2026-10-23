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

```bash
bash scripts/dev.sh          # API on 127.0.0.1:5000 (applies migrations on start) + Next.js on :3000
```

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
3. Deploy. The build runs `scripts/build.sh`, and the app starts with `scripts/start.sh`. Only port 3000 (Next.js) is public.

Migrations run automatically on API startup against whichever database `DATABASE_URL` points to.
