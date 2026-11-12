# AGENTS.md

Guidance for AI coding agents working in this repository.

## Workflow (spec-driven)

- Every behavior change starts as an OpenSpec change in `openspec/changes/<change-name>/`
  (proposal → specs → design → tasks). Use the skills in `.agents/skills/` (`openspec-propose`,
  `openspec-apply-change`, `openspec-archive-change`).
- Do not implement behavior that is not described in a spec scenario; update the change first.
- Every spec scenario must have an automated test (and, for AI behavior, an eval case).
- Tick tasks in `tasks.md` as they are completed. Run `openspec validate --all --strict` before committing.

## Commands

```bash
dotnet build JobFit.slnx
dotnet test JobFit.slnx                          # requires Docker for integration tests
dotnet run --project src/JobFit.AppHost          # API + Postgres + Aspire dashboard
npx @fission-ai/openspec@2 validate --all --strict
```

## Conventions

- .NET 10, Minimal APIs, nullable enabled. Code, specs and docs in English.
- New services call `builder.AddServiceDefaults()` and `app.MapDefaultEndpoints()`.
- Infrastructure (databases, containers) is declared in `src/JobFit.AppHost/AppHost.cs`, never hard-coded.
- LLM access goes through `Microsoft.Extensions.AI` abstractions (`IChatClient`), never a vendor SDK directly.
- Never commit secrets; use user secrets or Aspire parameters.

## Gotchas

- Aspire HTTP health checks and test clients must use the `http` endpoint: the HTTPS dev certificate
  is not trusted on Linux/CI.
- `JobFit.Api.Tests` uses an unreachable database on purpose (failure paths without Docker);
  happy paths belong in `JobFit.IntegrationTests`.
- `JobFit.Api.Tests` replace the model with `FakeChatClient` behind the real pipeline (`UseJobFitPipeline`);
  tests never need an API key. Real-model checks go in `JobFit.Evals`.
- The AppHost `http` launch profile needs `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true`.
- Model calls exceed the standard HTTP resilience timeouts (10 s per attempt); the API raises them in
  `ChatClientRegistration`. Keep that in mind before adding other outgoing HTTP clients.
