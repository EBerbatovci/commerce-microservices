# Testing Guide

The repository contains integration tests for the Catalog and Order APIs. The
tests boot each Minimal API in memory with `WebApplicationFactory`, use isolated
temporary SQLite databases, and make real HTTP requests against the test host.
Order tests replace Catalog's network transport with a controlled
`HttpMessageHandler`; no running service or container is required.

## Coverage

The suite verifies:

- seeded Catalog queries, missing products, validation, and creation;
- successful order creation and Catalog-owned name and price snapshots;
- missing products, insufficient stock, and Catalog unavailability;
- Problem Details media types and status codes;
- correlation ID generation and Order-to-Catalog propagation;
- liveness and dependency-aware readiness responses;
- the no-retry behavior for Catalog HTTP 404 responses.

## Run with Docker

This is the most reproducible option because it uses the .NET 8 SDK image:

```bash
docker build -f Dockerfile.tests -t commerce-microservices-tests .
docker run --rm commerce-microservices-tests
```

The equivalent one-line command is:

```bash
docker build -f Dockerfile.tests -t commerce-microservices-tests . && docker run --rm commerce-microservices-tests
```

## Run locally

Install the .NET 8 SDK, then run:

```bash
dotnet restore CommerceMicroservices.sln
dotnet build CommerceMicroservices.sln --configuration Release --no-restore
dotnet test CommerceMicroservices.sln --configuration Release --no-build
```

## CI checks

The GitHub Actions workflow runs on pushes and pull requests to `main`. It
restores, builds, and tests the solution, validates the Compose model, and
builds all three production images without publishing them.

When adding behavior, prefer an integration test that exercises the public HTTP
contract. Keep tests independent of local databases, existing containers,
network access, execution order, and machine-specific state.
