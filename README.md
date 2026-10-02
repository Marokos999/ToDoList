# ToDoList

Real-time shared to-do lists built with **Blazor Server** on **.NET 10**. Create lists, share them with other users by email, and every change shows up instantly in all open tabs, without a refresh.

**Live demo:** https://todoapp-marokos-d6hbh6h2hjgmhaby.westeurope-01.azurewebsites.net
<sub>Hosted on Azure App Service (Free tier), so the first request after a while can take a few seconds.</sub>

![Real-time sync between two browser tabs](docs/realtime-sync.gif)

## Features

- Registration and login with ASP.NET Core Identity, including email confirmation and password reset over SMTP
- Multiple lists per user, renamed and deleted from the list header
- Tasks with priority, due date and description; everything is editable inline, and deleting asks for confirmation
- Sharing a list with another registered user by email (owner only, with a clear message for every failure case)
- Real-time updates: a change made by anyone on a shared list appears immediately for everyone viewing it
- Access checks in the service layer, so only the owner and users the list is shared with can read or change it
- Serbian UI for lists and tasks (`sr-Latn-RS` culture, `dd.MM.yyyy` dates)

## Tech stack

| Area | Technology |
| --- | --- |
| UI | Blazor Server (Interactive Server render mode over SignalR), Radzen.Blazor |
| Backend | ASP.NET Core 10, ASP.NET Core Identity |
| Data | EF Core 10 + Npgsql, PostgreSQL (17 in Docker, Neon in production) |
| Email | MailKit (SMTP) |
| DevOps | Docker Compose, GitHub Actions, Azure App Service |

## Architecture

```text
TodoListPage (Blazor component, runs on the server)
   │  add / toggle / edit / delete
   ▼
TodoService ──── IDbContextFactory ────► PostgreSQL
   │  after every change
   ▼
TodoNotifier (singleton) ── ListChanged(listId) ──► every open page of that list
                                                        │  reloads its data
                                                        ▼
                                  Blazor sends the UI update over the page's circuit
```

- **One DbContext per operation.** A Blazor Server circuit lives as long as the tab is open, so a scoped `DbContext` would keep stale entities and fail when two events overlap. `TodoService` creates a short-lived context for every call through `IDbContextFactory`.
- **No extra SignalR connection for real-time.** Blazor Server components already run on the server, so `TodoService` raises an in-memory event on a singleton `TodoNotifier`. Every open page of that list reloads itself, and Blazor pushes the change to its browser over the circuit it already has. The notifier doesn't wait for subscribers and isolates their failures. This works within one server instance; scaling out would need a backplane such as Redis.
- **Own changes are shown immediately.** The page reloads its data right after its own actions; notifications only bring in changes from other tabs and users.

## Project structure

```text
TodoApp/
├── Application/   TodoService, ITodoService, TodoNotifier, ListSummary, ShareResult
├── Domain/        TodoList, TodoItem, TodoListShare, Priority
├── Data/          ApplicationDbContext, ApplicationUser
├── Components/    Pages (Home, TodoListPage), Account (Identity UI, SMTP sender), Layout
└── Migrations/    EF Core migrations, applied automatically on startup
TodoApp.Tests/     xUnit tests for TodoService against a real PostgreSQL (Testcontainers)
```

## Run locally with Docker

```bash
docker compose up --build
```

App: http://localhost:8081 (Postgres is exposed on `localhost:5433`).

To send real emails, copy `.env.example` to `.env` and set `SMTP_USERNAME` and `SMTP_PASSWORD` (for example a Gmail address and an app password). Without them, the confirmation link is shown on the registration confirmation page. The same `.env` file can change the database credentials and the app port.

```bash
docker compose down        # stop, keep data
docker compose down -v     # stop and delete the database
```

## Run without Docker

Requirements: .NET 10 SDK and PostgreSQL on `localhost:5432` (credentials in `TodoApp/appsettings.Development.json`). The database is created on the first run.

```bash
dotnet run --project TodoApp
```

App: http://localhost:5023 (or https://localhost:7202 with `--launch-profile https`). Optional SMTP credentials go into user secrets:

```bash
dotnet user-secrets set "Smtp:UserName" "you@gmail.com" --project TodoApp
dotnet user-secrets set "Smtp:Password" "your-app-password" --project TodoApp
```

## Tests

```bash
dotnet test TodoApp.Tests
```

The tests start a PostgreSQL 17 container with Testcontainers (Docker must be running), apply the real migrations and cover access rules, sharing, task and list changes, and change notifications.

## Configuration

| Setting | Description |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `Smtp__UserName`, `Smtp__Password` | SMTP credentials; without them no emails are sent |
| `Smtp__Host`, `Smtp__Port` | Optional, default to `smtp.gmail.com` and `587` |
| `Smtp__FromAddress`, `Smtp__FromName` | Optional sender address (defaults to the user name) and display name (defaults to `TodoApp`) |

## Deployment

Every push to `main` runs [`.github/workflows/deploy.yml`](.github/workflows/deploy.yml): first the tests, then, only if they pass, it publishes the app and deploys it to Azure App Service with a publish profile stored in GitHub secrets. In production, the Neon PostgreSQL connection string and the SMTP credentials are App Service environment variables, and migrations run on startup.
