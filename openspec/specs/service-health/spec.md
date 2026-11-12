# service-health Specification

## Purpose
Lets operators and orchestrators know whether the Job Fit API process is running and whether it is
ready to serve requests, including reachability of the database it depends on.

## Requirements

### Requirement: Liveness endpoint
The API SHALL expose `GET /alive` returning HTTP 200 whenever the process is running and able to
handle HTTP requests, independently of the state of external dependencies.

#### Scenario: Process running with database available
- **WHEN** a client calls `GET /alive` and the database is reachable
- **THEN** the response status is 200

#### Scenario: Process running with database unavailable
- **WHEN** a client calls `GET /alive` and the database is unreachable
- **THEN** the response status is 200

### Requirement: Readiness endpoint includes the database
The API SHALL expose `GET /health` returning HTTP 200 only when the API and its database are both
healthy, and HTTP 503 when any dependency is unhealthy.

#### Scenario: All dependencies healthy
- **WHEN** a client calls `GET /health` and the database accepts connections
- **THEN** the response status is 200

#### Scenario: Database unreachable
- **WHEN** a client calls `GET /health` and the database does not accept connections
- **THEN** the response status is 503

### Requirement: Health endpoints available in every environment
The health endpoints SHALL be available in all hosting environments, including Production, so that
container orchestrators can probe them.

#### Scenario: Production environment
- **WHEN** the API runs with environment `Production` and a client calls `GET /alive`
- **THEN** the response status is 200
