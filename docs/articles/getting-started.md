# Getting Started

This guide explains how to clone, build, test, run, and locally document the .NET Core Application Template.

## Prerequisites

- .NET SDK 10.0 or later
- Git
- Visual Studio 2026 or another .NET-capable editor
- Optional: EF Core CLI tools
- Optional: DocFX local tool restored through `dotnet tool restore`

## Clone, Restore, Build, and Test

```powershell
git clone https://github.com/AsiBackbone/NetCoreApplicationTemplate.git
cd NetCoreApplicationTemplate
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

## Run the Web Application

```powershell
dotnet run --project src/ProjectTemplate.Web
```

The application uses `src/ProjectTemplate.Web/appsettings.json` and normal environment-specific ASP.NET Core configuration sources.

## Understand the Default Access Posture

The default scaffold enables local cookie authentication. Authentication establishes the caller's identity.

Authorization determines whether that identity may access an endpoint or operation. The default scaffold configures a fallback authorization policy requiring authentication for routed endpoints without authorization metadata. Intentionally public routes use explicit anonymous metadata such as `[AllowAnonymous]` or `.AllowAnonymous()`.

Anonymous browser requests to protected MVC or Razor Page routes are redirected to `/Account/Login`. Protected API routes return an authentication challenge response appropriate to API callers.

The `--authProvider none` template option is an explicit opt-out. It disables application authentication, cookie authentication, and the authenticated fallback policy. Unannotated routed endpoints are public in that generated variant until another authentication and authorization posture is added.

Review [Authentication](authentication.md), [Authorization](authorization.md), and [Production Authentication Hardening](authentication-hardening.md) before deployment.

## Local Database

The default local database provider is SQLite:

```json
"ConnectionStrings": {
  "ApplicationDatabase": "Data Source=application-dev.db"
}
```

Database migrations are handled explicitly through EF Core CLI commands. The application does not automatically run migrations at startup.

## Build Documentation Locally

```powershell
dotnet tool restore
dotnet tool run docfx -- docs/docfx.json
dotnet tool run docfx -- serve docs/_site
```

The local documentation site is usually available at `http://localhost:8080`.

## Development Notes

Keep environment credentials and production configuration outside committed source files.
