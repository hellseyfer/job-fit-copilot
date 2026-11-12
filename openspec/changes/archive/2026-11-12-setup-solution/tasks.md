# Tasks

## 1. Solution

- [x] 1.1 Create `JobFit.slnx` with AppHost, ServiceDefaults, Api, Api.Tests and IntegrationTests projects
- [x] 1.2 Pin the SDK with `global.json` (.NET 10, latestFeature roll-forward)

## 2. Hosting

- [x] 2.1 Add Postgres (`pgvector/pgvector` image, data volume) and a `jobfit` database to the AppHost
- [x] 2.2 Reference the database from the API and wait for it before starting
- [x] 2.3 Register `NpgsqlDataSource` in the API through the Aspire Npgsql integration
- [x] 2.4 Map `/health` and `/alive` in all environments; remove the template sample endpoint

## 3. Tests

- [x] 3.1 Api.Tests: `/alive` returns 200 when the database is unreachable
- [x] 3.2 Api.Tests: `/health` returns 503 when the database is unreachable
- [x] 3.3 Api.Tests: `/alive` returns 200 in the Production environment
- [x] 3.4 IntegrationTests: with the AppHost running, `/health` and `/alive` return 200

## 4. CI and docs

- [x] 4.1 GitHub Actions workflow: restore, build, test on push and pull request
- [x] 4.2 README with how to run, architecture overview and roadmap
