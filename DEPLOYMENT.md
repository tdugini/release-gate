# ReleaseGate deployment

ReleaseGate includes a production-like Docker Compose stack that runs PostgreSQL, the ASP.NET Core API and the React control plane behind nginx.

The goal of this setup is repeatability and a clear deployment boundary. It is suitable for a single-host self-hosted deployment or as a reference for moving the same containers to another platform.

## Architecture

```text
Browser / SDK
     |
     v
nginx :80
  |      \
  |       \ static React assets
  v
API :8080
  |
  v
PostgreSQL :5432
```

Only nginx is published to the host by default. The API and PostgreSQL remain on the internal Compose network.

The web app uses same-origin `/api/*` requests in production, and nginx proxies them to the API container. This keeps the browser deployment free from a hard-coded API hostname and avoids a production CORS dependency for the control plane.

## Configuration

Copy the example environment file:

```bash
cp .env.production.example .env.production
```

Replace every placeholder secret before starting the stack. At minimum, set strong values for:

- `POSTGRES_PASSWORD`;
- `CONTROL_PLANE_OPERATOR_TOKEN`;
- `CONTROL_PLANE_REVIEWER_TOKEN`;
- `RUNTIME_API_KEY`.

The Compose file fails configuration when required secret values are missing.

Do not commit `.env.production`.

The production-like stack uses a dedicated `releasegate-prod-postgres` Docker volume. It is intentionally separate from the development Compose volume so production credentials and schema state cannot accidentally collide with an existing local development database.

## Start

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml up -d --build
```

The control plane is available on `http://localhost:8080` by default. Change `WEB_PORT` in `.env.production` to publish a different host port.

Check the API through the nginx proxy:

```bash
curl http://localhost:8080/health
```

## Database migrations

The deployed API applies committed EF Core migrations during startup before accepting normal application traffic.

The legacy pre-v0.7 database baselining path remains development-only. A production deployment is expected to use a database whose schema is managed through normal EF migration history.

For a single API instance this keeps the self-hosted deployment simple and repeatable. A future horizontally scaled deployment should move migration execution into a dedicated release step so multiple replicas do not compete to migrate the same database.

## Runtime access

Application consumers access runtime snapshots through the same public nginx endpoint and must send the configured runtime key:

```bash
curl \
  -H "X-ReleaseGate-Key: <runtime-key>" \
  "http://localhost:8080/api/runtime/projects/<project>/environments/production/snapshot?subjectKey=<subject>"
```

Runtime credentials remain separate from human control-plane bearer tokens.

## Stop

Stop the application while keeping the PostgreSQL volume:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml down
```

To also delete the production-like database volume:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml down -v
```

The second command permanently removes the production-like deployment data. It does not remove the separate development PostgreSQL volume.

## CI deployment smoke test

Pull requests build and start the production-like Compose stack after the API, web and SDK jobs pass. CI then:

1. waits for `/health` through nginx;
2. creates a project and flag through the authenticated control-plane API;
3. enables the flag in development;
4. fetches a runtime snapshot through nginx using a runtime API key;
5. verifies that the same runtime route returns `401` without a key.

This means container buildability, nginx proxying, database migrations, control-plane authentication and runtime authentication are exercised together rather than validated only as isolated build steps.

## SDK publishing

The JavaScript SDK has an independent package version. Tags use the form:

```text
sdk-js-v0.1.0
```

The SDK release workflow requires the tag version to match `packages/sdk-js/package.json`, runs type checking/tests/package validation, and then publishes to npm with provenance.

Publishing requires an `NPM_TOKEN` repository secret and npm publish permission for the `@releasegate/sdk-js` package scope. The application can be deployed without publishing the SDK.
