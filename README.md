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
- Serbian and English UI with a language dropdown in the top bar; Serbian is the default, and dates and the calendar follow the selected language

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

To send real emails, copy `.env.example` to `.env` and set `SMTP_USERNAME` and `SMTP_PASSWORD` (for example a Gmail address and an app password). Without them no email can be sent; for local use only, set `ACCOUNT_SHOW_CONFIRMATION_LINK=true` in `.env` to show the confirmation link on the registration confirmation page. The same `.env` file can change the database credentials and the app port.

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
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string (required) |
| `Smtp__UserName`, `Smtp__Password` | SMTP credentials; without them no emails are sent and new users cannot confirm their account |
| `Smtp__Host`, `Smtp__Port` | Optional, default to `smtp.gmail.com` and `587` |
| `Smtp__FromAddress`, `Smtp__FromName` | Optional sender address (defaults to the user name) and display name (defaults to `TodoApp`) |
| `Account__ShowConfirmationLink` | Local use only, without SMTP: shows the confirmation link on the page. **Never enable on a public site.** |
| `App__Name` | Application name shown in the navigation, titles and legal pages (default `TodoApp`) |
| `App__PublicUrl` | Public address (for example `https://example.com`) used in `robots.txt` and `sitemap.xml`; defaults to the request address |
| `Legal__OperatorName`, `Legal__ContactEmail` | Operator details shown on the privacy policy and terms pages. Fill these in before going public |
| `Analytics__Provider`, `Analytics__SiteId`, `Analytics__ScriptUrl` | Optional cookieless analytics: `Plausible` (site id is the domain) or `Umami` (website id). `ScriptUrl` is only needed for self-hosting |

## Deployment (Render + Neon)

The app runs on [Render](https://render.com) as a Docker web service, and the database is a [Neon](https://neon.tech) PostgreSQL project. Render builds the `Dockerfile` on every push to `main`; the tests run in GitHub Actions ([`.github/workflows/test.yml`](.github/workflows/test.yml)).

### First deploy

1. **Neon:** create a project, copy the connection details and build the string
   `Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require;GSS Encryption Mode=Disable`.
   To move existing data, restore a dump into the empty database before the first start:
   `docker run --rm -i postgres:17-alpine psql "postgresql://USER:PASSWORD@HOST/DB?sslmode=require" < backups/backup.sql`.
2. **SMTP:** create SMTP credentials with an email provider (for example Brevo, or Gmail with an app password).
3. **Render:** New → Web Service → connect the repository → runtime **Docker**. Set the health check path to `/health`. Add these environment variables:
   - `ASPNETCORE_ENVIRONMENT` = `Production`
   - `ConnectionStrings__DefaultConnection` = the Neon string from step 1
   - `Smtp__Host`, `Smtp__Port`, `Smtp__UserName`, `Smtp__Password`, `Smtp__FromAddress`
   - `Legal__OperatorName`, `Legal__ContactEmail`
   - optionally `App__Name`, `Analytics__*`
4. Deploy. Migrations run on startup, so the database schema is created or updated automatically. Check the logs for `SMTP is not configured`: that line means no emails can be sent.
5. Open the site and run the smoke test below.

Render's free plan puts the service to sleep after about 15 minutes without traffic, so the first request afterwards is slow.

### Smoke test

1. `/` shows the landing page; `/robots.txt`, `/sitemap.xml`, `/privacy`, `/terms` and `/faq` open; an unknown address shows the 404 page.
2. Register a new account and confirm it with the emailed link, then log in.
3. Create a list and a task, drag a task, add an attachment and download it, export CSV and PDF.
4. Check a phone-sized screen, then log out and make sure `/list/...` redirects to the login page.

### Backups and restore

Neon keeps point-in-time history (enable and size it in the project settings). For an extra copy on your machine:

```bash
DATABASE_URL='postgresql://USER:PASSWORD@HOST/DB?sslmode=require' ./scripts/backup-db.sh
```

Dumps go to `backups/`, which is git-ignored because it contains user data and password hashes. Never store them in a public place. To restore, apply a dump to an **empty** database with `psql ... < backups/file.sql`.

### Security notes

- The app trusts the proxy's `X-Forwarded-*` headers (needed behind Render), so only run it behind a trusted proxy.
- Login, registration and password-reset form posts are limited to 10 per minute per IP; accounts lock for 15 minutes after 5 failed sign-ins; passwords need at least 10 characters.
- Data protection keys are stored in the database, so users stay signed in across deploys.
- Deleting an account deletes its lists, tasks, attachments and shares.

### Custom domain

Add the domain in Render → Settings → Custom Domains and create the DNS records Render shows; HTTPS is issued automatically. Then set `App__PublicUrl` to the new address, send email from an address on that domain, and add SPF/DKIM records with your email provider.
