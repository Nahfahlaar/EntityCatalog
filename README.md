# EntityCatalog

## Local configuration

The project reads the database connection string from `ConnectionStrings:DefaultConnection` and JWT settings from `Jwt:Issuer`, `Jwt:Audience`, and `Jwt:SigningKey`. Keep these values in .NET User Secrets locally and in the deployment environment for hosted instances. Do not add secret values to `appsettings.json` or commit them.

The project already has a `UserSecretsId` in its project file. Set local values from the project directory:

```powershell
dotnet user-secrets set "Jwt:Issuer" "EntityCatalog"
dotnet user-secrets set "Jwt:Audience" "EntityCatalog.Client"
dotnet user-secrets set "Jwt:SigningKey" "<random value with at least 32 UTF-8 bytes>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your local PostgreSQL connection string>"
```

Generate a fresh signing key with a cryptographically secure random generator, for example `openssl rand -hex 32`. The application accepts only HMAC-SHA256 JWTs and requires a signing key of at least 32 UTF-8 bytes.

For deployment, provide these environment variables through the hosting platform's secret manager:

```text
Jwt__Issuer
Jwt__Audience
Jwt__SigningKey
ConnectionStrings__DefaultConnection
```

The app fails at startup if issuer, audience, or a sufficiently long signing key is missing.

## Identity migrations and first Editor

The migration chain is ordered: `20260920174454_InitialCreate`, `20261017141933_AddProductPriceImagesAndPublication`, then the Identity migration generated for this stage. Applying the latest migration also applies any earlier pending migrations. Review the SQL and database target before applying them; the application does not run migrations automatically.

Once the intended database has all migrations applied, provision the first Editor interactively:

```text
dotnet run -- --create-editor
```

This explicit administrative command creates the `Editor` role if needed, prompts for a new username and password (password characters are not displayed), and assigns the new user to the role. It refuses an existing username, refuses to run if an Editor account already exists, and never promotes an existing account. The password is stored only as an ASP.NET Core Identity password hash. Ordinary application startup does not create users or roles.

## Authentication API

`POST /api/auth/login` accepts a username and password:

```json
{
  "userName": "editor-name",
  "password": "<editor password>"
}
```

Successful authentication returns a 15-minute JWT in `accessToken`. The token contains the user's ID, username, and current Identity roles, including the `Editor` role. Invalid credentials return `401 Unauthorized`. JWT authentication and authorization middleware are enabled; access rules for product, category, and tag endpoints are not configured yet.
