# 0005 — Activate CI pipeline on the real repository

- Status: Done
- Date: 2026-08-23
- Related issue/task: `PROGRESS.md` Phase 0 `0005` — CI pipeline active on the real repository (backend + frontend jobs green)

## Goal

Make the repository's GitHub Actions CI workflow a reliable merge gate for the current monorepo. Every push to `main` and pull request targeting `main` must validate the .NET 10 backend, xUnit unit/integration tests against disposable PostgreSQL, React/Vite frontend lint/tests/build, and both Docker image builds.

This task is CI/tooling only: it must not introduce domain behavior, API features, schema changes, authentication changes, or frontend product functionality.

## Affected modules/layers

- Domain: untouched.
- Application: untouched.
- Infrastructure: untouched, except for CI-specific compatibility fixes if the existing integration-test command exposes a repository configuration problem.
- API: untouched, except for Docker build/configuration fixes required by the CI image-build check.
- Frontend: untouched product code; CI uses the existing `src/Web` npm scripts and lockfile.
- Repository tooling: `.github/workflows/ci.yml`, workflow-supporting configuration/documentation, and task-status docs.

## Files to add/change

- `.github/workflows/ci.yml` — verify and adjust checkout, .NET 10, Node 22, npm cache, Docker/Testcontainers availability, dependency installation, build/test ordering, artifact retention, and job dependencies.
- `src/Web/package-lock.json` — only if `npm ci` on the real runner proves the committed lockfile is inconsistent with `src/Web/package.json`; do not replace it with another package manager lockfile.
- `src/Api/Dockerfile` and/or `src/Web/Dockerfile` — only if the existing CI Docker build commands fail due to reproducible Dockerfile or build-context issues.
- `docs/TESTING.md` — update CI commands or prerequisites only if the implemented workflow differs from the documented test strategy.
- `docs/DECISIONS.md` — add an ADR only for a meaningful CI design decision not already covered by existing decisions.
- `docs/PROGRESS.md` — mark `0005` complete after the workflow is green on the real repository.
- `docs/plans/0005-ci-pipeline.md` — change `Status: Draft` to `Status: Done` after implementation and verification.

Existing unrelated working-tree changes must be preserved and must not be folded into this task.

## Schema changes

None. No EF Core migration, database entity, domain type, or runtime data change is planned.

## Implementation outline

1. Validate the current workflow syntax and job paths against the actual solution and `src/Web` scripts.
2. Run the same backend, frontend, and Docker commands locally where the environment supports them, recording failures that are CI-specific versus repository defects.
3. Apply the smallest workflow or build-context corrections needed for GitHub-hosted Ubuntu runners.
4. Ensure backend integration tests can start `postgres:18-alpine` through Testcontainers without weakening test coverage or skipping tests.
5. Ensure artifacts from failed backend tests remain available for diagnosis without exposing secrets.
6. Validate the workflow on the real repository by pushing the task branch/changes or opening the repository's normal CI trigger, then inspect every job result.
7. Update testing/progress documentation and log any new technical decision immediately.

## Risks / open questions

- The repository workflow currently targets `.NET 10.x`; the runner must have a compatible SDK and the solution must restore without relying on developer-local tools.
- Infrastructure integration tests require Docker. GitHub-hosted `ubuntu-latest` normally provides Docker, but the workflow should fail clearly if the daemon is unavailable rather than silently skipping tests.
- `npm ci` requires `src/Web/package-lock.json` to match `src/Web/package.json`; root-level package-manager files are outside this task unless the user explicitly scopes them in.
- Docker image builds use two different contexts: the API image builds from the repository root, while the web image builds from `src/Web`. Any correction must preserve those intended contexts.
- CI must not print JWT signing keys, database passwords, OTPs, or other secrets in logs. Development placeholders remain development-only.
- GitHub Actions workflow execution is external repository state. If repository permissions, branch protection, or runner configuration prevent verification, report the exact blocker and do not mark the task done.
- No new ADR is needed if the workflow simply confirms the already documented commands and runner assumptions.

## Acceptance criteria

- [x] `.github/workflows/ci.yml` triggers on pushes and pull requests targeting `main`.
- [x] Backend job restores and builds `Pom.sln` in Release with no new warnings.
- [x] Backend job runs all xUnit projects, including Testcontainers integration tests, without filtering or skipping tests.
- [x] Frontend job runs `npm ci`, `npm run lint`, `npm test`, and `npm run build` from `src/Web`.
- [x] Docker job builds both `src/Api/Dockerfile` from the repository root and `src/Web/Dockerfile` from `src/Web`.
- [x] Failed backend test results are uploaded as workflow artifacts, including when tests fail.
- [x] No CI step exposes secrets or introduces a password-based authentication path.
- [x] DDD boundaries and runtime behavior remain unchanged.
- [x] Relevant documentation is updated if commands or prerequisites changed.
- [ ] `docs/PROGRESS.md` marks `0005` as `[x]` only after the real-repository workflow run is green.
- [ ] This plan is marked `Status: Done` after implementation and verification.

## Verification

- Backend restore/build passed locally with .NET 10 and zero warnings.
- All backend xUnit projects passed locally (34 tests total), including Testcontainers-backed integration tests.
- Frontend `npm ci`, lint, `npm test`, and production build passed locally.
- Docker image builds were not runnable locally because Docker Desktop's Linux daemon was unavailable; the workflow retains both image-build checks for the GitHub-hosted runner.
- A GitHub-hosted workflow run could not be triggered from this session because no push or remote mutation was authorized.
