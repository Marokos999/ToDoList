# ToDoList

Blazor Server todo app (ASP.NET Core 10, PostgreSQL, SignalR, Radzen).

## Run locally with Docker

```bash
docker compose up --build
```

App: http://localhost:8081 (Postgres is exposed on `localhost:5433`).

EF Core migrations are applied automatically on startup. New accounts require email confirmation; since no email is sent, the confirmation link is shown on the registration confirmation page.

Settings (DB credentials, app port) can be overridden by copying `.env.example` to `.env`.

```bash
docker compose down        # stop, keep data
docker compose down -v     # stop and delete the database
```
