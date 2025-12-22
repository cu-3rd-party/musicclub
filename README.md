# Music Club Bot

## Running with local image builds
- Copy `.env.example` to `.env` and fill in required secrets and hostnames (`POSTGRES_*`, `JWT_SECRET`, `BOT_TOKEN`, `VITE_GRPC_HOST`, etc.).
- From repo root run `docker compose --env-file .env up --build` to build `backend` and `frontend` images locally and start `db` and `adminer`. Add `-d` to run detached.
- Stop with `docker compose down`; add `--volumes` if you want to remove the `db_data` volume.

## Running locally with only the Dockerized Postgres (no local image builds)
- Copy `.env.example` to `.env` and adjust it for host-based services:
  - Set `POSTGRES_HOST=localhost` and ensure `POSTGRES_URL` points to `localhost:5432` with your chosen database name, user, and password.
  - Set `VITE_GRPC_HOST=http://localhost:6969` so the browser talks to the locally running backend.
- Expose Postgres to the host by adding a local (untracked) override file `docker-compose.override.yml` with:
  ```yaml
  services:
    db:
      ports:
        - "5432:5432"
  ```
- Start only the database (and optionally adminer) with `docker compose -f docker-compose.yml -f docker-compose.override.yml --env-file .env up db adminer -d`.
- Backend: in a new shell, load environment from `.env` (e.g., `set -a; source .env; set +a` in bash or equivalent in PowerShell), then run `cd backend && go run ./cmd/server` to start the API on port 6969.
- Frontend: install deps once with `cd frontend && npm install`, then run `npm run dev -- --host --port 5173` so the app is reachable at `http://localhost:5173` against the locally running backend.
- Stop the database with `docker compose down`; add `--volumes` if you also want to drop the `db_data` volume.
