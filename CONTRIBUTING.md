# Contributing

Thank you for considering an improvement to Commerce Microservices.

## Development setup

You need Git, the .NET 8 SDK, and Docker Desktop. Fork or clone the repository,
create a focused branch, and restore the solution:

```bash
dotnet restore CommerceMicroservices.sln
```

Run the stack with:

```bash
docker compose up --build
```

## Before opening a pull request

Run the complete validation sequence:

```bash
dotnet build CommerceMicroservices.sln --configuration Release
dotnet test CommerceMicroservices.sln --configuration Release --no-build
docker compose config
docker compose build
```

Alternatively, run the tests in the pinned SDK environment:

```bash
docker build -f Dockerfile.tests -t commerce-microservices-tests . && docker run --rm commerce-microservices-tests
```

## Contribution guidelines

- Keep changes focused and preserve service ownership boundaries.
- Add or update integration tests for observable behavior.
- Keep public routes and response contracts backward compatible unless a change
  is explicitly proposed and documented.
- Do not commit generated output, SQLite files, credentials, local settings, or
  editor state.
- Use clear commit messages and explain design tradeoffs in the pull request.
- Update the README or files under `docs/` when behavior or setup changes.

CI must pass before a change is ready to merge.
