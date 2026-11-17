# Tasks

## 1. Manual evaluation workflow

- [x] 1.1 Move the `evals` job from `.github/workflows/ci.yml` into `.github/workflows/evals.yml` with `on: workflow_dispatch:` and validate both YAML files
- [x] 1.2 Update `tests/JobFit.Api.Tests/EvalSuiteTests.cs` to verify `.github/workflows/evals.yml` and update `README.md`; run `dotnet test tests/JobFit.Api.Tests` and `openspec validate --all --strict`
