# Identity Service: phase 1 rollout

## Ownership and current boundary

For accounts already migrated, `Clinic.Identity.Service` is the authoritative owner of credentials, account state, roles, permissions, user-role links, refresh tokens, and password-reset tokens in `ClinicIdentityDB`. The API and other services validate Identity JWTs with the Identity service's internal discovery document and JWKS; they do not read `ClinicIdentityDB`.

Patient registration is coordinated by the Clinic API: Identity creates the account first, then Clinic provisions the Patient profile. Patient, Employee, Doctor, and their operational data remain Clinic-owned for now. Clinic stores only the `Person.IdentityUserId` link; credentials, users, roles, permissions, and refresh tokens are Identity-owned. Role and permission checks use signed JWT claims and API authorization policies. Application handlers retain resource-ownership checks that require appointment, schedule, or profile data.

Employee/doctor credentials, roles, and account reads are now sent to Identity. The API provisions new staff accounts as inactive, creates the Clinic-owned business profile, then links the employee/doctor/patient IDs and requested roles in Identity; Identity activates the account only after that final link. Existing-user conversion and employee role updates use the same Identity role endpoint. Clinic keeps an opaque-password `User` projection for current domain relationships; the projection is not used to authenticate or authorize requests. Admin user listing, detail, and role catalog routes are backed by Identity APIs.

The Clinic-side `Person`, `Patient`, `Employee`, and `Doctor` records remain the business-profile source. The API coordinates Identity and Clinic writes without a distributed transaction. If provisioning stops after Identity account creation, the account remains inactive. The internal provisioning endpoint is idempotent for the same username, person, and password so a retry can resume before Clinic profile creation; if the Clinic profile has already been created, the public create route detects the existing profile and retries the Identity link. Review inactive accounts during operations if a provisioning request repeatedly fails.

The API now routes login, refresh, logout/revoke, password change/reset, and `/auth/me` to Identity; no credential or profile fallback reads Clinic account data on those routes. `/auth/permissions` returns the permission claims already present in the validated JWT. The obsolete Clinic login, refresh, logout, Patient-registration, and account-query handlers are removed, along with the Clinic symmetric-token generation services and startup creation of local admin credentials. Keep Clinic account tables while transitional projections or the rollback window still require them. Local `Person` profile ownership may be consolidated after Patient and Staff profile boundaries are ready.

## Authentication and password recovery

Identity access tokens use RS256; access and refresh lifetimes are configurable. The private signing keys live in the persistent `identity_signing_keys` volume. Rotation adds a new active key and keeps old public keys in JWKS until prior tokens expire.

Password reset uses a cryptographically random, one-time code. Identity stores only its SHA-256 hash, expires it after 30 minutes, consumes it on use, and revokes the user's refresh tokens after a successful reset. The API returns a generic account lookup response and queues the code through the existing notification pipeline. The frontend collects the code and new password; invalid or expired codes receive a clear validation error. Requesting a new code invalidates earlier outstanding codes.

## Local Docker configuration

Set a unique `IDENTITY_SERVICE_TOKEN` in `.env` for API-to-Identity internal requests. The API and Identity Service must share the same value. Public metadata settings are configurable:

- `IDENTITY_ISSUER`: issuer embedded in tokens and exposed by gateway discovery; default `http://localhost:5000`.
- `IDENTITY_PUBLIC_BASE_URL`: public gateway base used for discovery/JWKS links; default `http://localhost:5000`.
- `IDENTITY_ACCESS_TOKEN_MINUTES`: access-token lifetime; default 15 minutes.
- `IDENTITY_REFRESH_TOKEN_DAYS`: refresh-token lifetime; default 7 days.

Start the stack with `docker compose up -d --build`. The API is on port 5000 and Identity Service is on port 5001. For local front-end/client discovery use `http://localhost:5000/.well-known/openid-configuration`; its `jwks_uri` points to the raw JSON endpoint `http://localhost:5000/.well-known/jwks.json`. Identity's own metadata remains on the Docker network so the API can validate tokens without a host-network round trip. Configure an HTTPS issuer and public gateway base URL when deploying elsewhere. The current endpoints publish signing-key discovery for JWT validation; a full OAuth authorization-code OIDC flow is outside this phase.

Identity applies EF migrations at startup. `/healthz` is liveness; `/readyz` checks database connectivity. Compose health checks use readiness before starting dependent services. Keep the signing-key volume persistent and restrict access to the internal service token and key-rotation secret.

## Existing-data migration

1. Back up `ClinicDB` and `ClinicIdentityDB`.
2. Deploy Identity Service and allow its schema migrations to finish.
3. Run `backend/Clinic.Identity.Service/Persistence/import-from-clinicdb.sql` or let the Compose `identity-data-importer` run. The import is additive and repeatable; it accepts both pre-cutover ClinicDBs with auth tables and post-cutover ClinicDBs where those tables have been removed.
4. Run `backend/Clinic.Identity.Service/Persistence/verify-identity-import.sql`. Require zero missing users, linked persons, role links, permission links, refresh tokens, and user/person/active-state mismatches. Credential-hash differences are informational after cutover because Identity owns password resets and new accounts use opaque Clinic projections. The report returns totals only and never prints credential hashes or tokens.
5. Patient login, registration, refresh, revoke, change-password, and reset-password traffic now uses Identity. The API also proxies `/auth/me` to Identity. Imported symmetric-key access tokens are not valid with the RS256 key; users may need to sign in again.
6. Apply the Clinic `RemoveClinicAuthEntities` migration after the import and verification window. It removes the legacy `Users`, `Roles`, `Permissions`, `UserRoles`, `RolePermissions`, and `RefreshTokens` tables; keep a database backup for rollback because the migration does not recreate those tables.

Rollback before legacy-table removal: restore the previous API deployment and JWT configuration, route auth traffic back to the old implementation, and leave the additive Identity database untouched. Do not delete either database during rollback.

## Verification

Run the Identity integration tests against the local SQL Server:

```powershell
dotnet test backend/Clinic.Identity.Service.Tests/Clinic.Identity.Service.Tests.csproj
```

Run application unit tests and API integration tests:

```powershell
dotnet test backend/Clinic.Application.UnitTests/Clinic.Application.UnitTests.csproj --no-restore
dotnet test backend/Clinic.IntegrationTests/Clinic.IntegrationTests.csproj --no-restore
```

Identity integration tests also cover inactive staff provisioning, role/profile linking, and the internal admin account endpoints. API contract tests verify that `/admin/users` and `/admin/roles` return Identity-backed data.

Smoke-check `http://localhost:5000/readyz`, `http://localhost:5001/readyz`, gateway discovery/JWKS, and registration/login/refresh/reset flows after rebuilding Compose.

## Key rotation

Configure `Jwt:RotationSecret` only in a protected secret store. Call `POST /internal/keys/rotate` with `X-Key-Rotation-Secret`. The new key becomes active immediately; previous public keys remain published until tokens and verifier caches have expired. Remove old PEM files only after the maximum access-token lifetime and verifier cache interval have elapsed.

## Gateway Routing, JWKS Validation, and Observability (Member B Scope)

### 1. Gateway Routing Architecture

`Clinic.API` acts as the API Gateway/BFF routing authentication and session operations to `Clinic.Identity.Service`:
- `POST /auth/login` -> forwards to Identity `/v1/auth/login`. Sets HttpOnly refresh token cookie on response.
- `POST /auth/register` -> coordinates account creation in Identity `/v1/auth/register` and Patient business profile provisioning in Clinic.
- `POST /auth/refresh` -> forwards refresh token from HttpOnly cookie to Identity `/v1/auth/refresh`. Rotates refresh cookie.
- `POST /auth/revoke` -> forwards revocation request to Identity `/v1/auth/revoke`.
- `POST /auth/logout` -> clears HttpOnly refresh cookie and revokes refresh token in Identity.
- `GET /auth/me` -> forwards authorized request with Bearer token to Identity `/v1/auth/me`.
- `GET /auth/permissions` -> extracts and returns dynamic permission claims from validated JWT.
- `POST /forgot-password` -> forwards lookup to Identity `/v1/auth/forgot-password` with internal service token.
- `POST /reset-password` -> forwards single-use reset token to Identity `/v1/auth/reset-password`.
- `POST /change-password` -> forwards authenticated password update to Identity `/v1/auth/change-password`.

### 2. Independent JWT Validation via JWKS

The Gateway and backend services validate access tokens independently via JWKS:
- **Algorithms**: RS256 with 2048/3072-bit RSA key pairs.
- **Issuers**: Configured gateway issuer (`http://localhost:5000` or production URL) and `Clinic.Identity`.
- **Audiences**: `Clinic.Services` and `Clinic.Client`.
- **Public Discovery**: `/.well-known/openid-configuration` and `/.well-known/jwks.json` exposed publicly on Gateway, backed by Identity service.
- **Resiliency**: Signing keys are cached locally in memory upon discovery; temporary upstream JWKS outages (HTTP 503) do not interrupt token verification.

### 3. Correlation ID Propagation

End-to-end distributed tracing and correlation are enforced across all tiers:
- **Frontend Interceptor**: [http.ts](file:///d:/CODE/KHDV/Clinic_Appointment_Booking_Queue_Management_System/frontend/src/lib/api/http.ts) attaches a UUID `X-Correlation-ID` header to all outgoing HTTP requests and token refresh calls.
- **Gateway Middleware & Delegating Handler**: [CorrelationIdDelegatingHandler.cs](file:///d:/CODE/KHDV/Clinic_Appointment_Booking_Queue_Management_System/backend/Clinic.API/Middlewares/CorrelationIdDelegatingHandler.cs) extracts `X-Correlation-ID` from ambient context and attaches it to all outbound HTTP calls to `Clinic.Identity.Service`.
- **Identity Middleware**: Identity Service captures the correlation ID, incorporates it into the ASP.NET Core logging scope, and returns it in the response headers.

### 4. Structured Audit Logging

Structured audit logs are emitted for all authentication and credential operations:
- Prefix: `AUTH_AUDIT` (Gateway) and `IDENTITY_AUDIT` (Identity Service).
- Captured attributes: Operation name, UserId, Username, PersonId, Client IP, CorrelationId, and outcome status.
- Strict security constraint: Access tokens, refresh tokens, passwords, and password reset codes are strictly excluded from all logs.

### 5. Feature Flagging (`Features:UseIdentityService`)

A configuration toggle `"Features:UseIdentityService"` is provided in `appsettings.json` and `appsettings.Development.json`:
- When `true`: All auth requests are dynamically routed to `Clinic.Identity.Service`.
- When `false`: `AuthController` rejects incoming auth operations with HTTP `503 Service Unavailable` and logs a warning with the Correlation ID, enabling instantaneous operational circuit breaking/rollback without redeploying containers.

