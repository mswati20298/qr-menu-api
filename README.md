# QR Menu — API

.NET 8 Web API for QR Menu, a digital ordering SaaS for Indian restaurants. The Angular front end lives in
[qr-menu-web](https://github.com/mswati20298/qr-menu-web).

**Stack:** .NET 8, EF Core (SQL Server), JWT auth, FluentValidation, QuestPDF, QRCoder, Razorpay.

## Layout

```
src/QrMenu.Domain           Entities and enums
src/QrMenu.Application      DTOs, validators, service interfaces, business rules
src/QrMenu.Infrastructure   EF Core, services, JWT, PDF, Razorpay, migrations
src/QrMenu.Api              Controllers, auth policies, filters, middleware
tests/QrMenu.Tests          xUnit tests
tenant-isolation-check.mjs  End-to-end check that one restaurant can never reach another's data
```

## Run locally

1. **Database:** a local SQL Server Express instance works with the checked-in connection string, or start
   the container with `docker compose up -d` (dev-only `sa` password; point `ConnectionStrings:DefaultConnection` at `localhost,14330`).
2. **Secrets:** copy `src/QrMenu.Api/appsettings.Development.example.json` to `appsettings.Development.json`
   (git-ignored) and fill in the JWT secret, first super admin, Gemini key and Razorpay test keys.
   `dotnet user-secrets` or environment variables work too.
3. **Start:**
   ```bash
   dotnet run --project src/QrMenu.Api
   ```
   Migrations are applied on startup; the sample restaurant is seeded only when `Seed:DemoData` is true
   (set in the Development example). Swagger (Development only): http://localhost:5176/swagger

Deploying Prod + Demo: see [deploy/README.md](deploy/README.md).
4. **Tests:** `dotnet test`

## Security notes

- The app refuses to start outside Development with a missing or placeholder `Jwt:Secret`.
- Three token kinds, each limited to its own endpoints: restaurant owner, kitchen screen (PIN login) and super admin.
- Every owner and kitchen request is scoped to the restaurant in the token; suspended restaurants are cut off immediately.
- Online payments are applied only after the Razorpay signature is verified (browser callback or webhook).
- Never commit `appsettings.Development.json`, uploaded images (`wwwroot/uploads`) or live keys — see `.gitignore`.
