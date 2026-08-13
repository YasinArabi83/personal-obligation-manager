# 0001 — Repo scaffolding (.NET 10 solution + React app skeleton)

- Status: Done
- Date: 2026-08-13
- Related issue/task: PROGRESS.md Phase 0 `0001` — Repo scaffolding: .NET 10 solution (Domain/Application/Infrastructure/Api) + React project in `src/Web`

## Goal

Stand up the empty-but-compilable monorepo skeleton: a .NET 10 solution with the four DDD layer projects wired with the correct *one-directional* project references, plus a React + TypeScript + Vite frontend. No domain entities, no DbContext, no migrations — those are `0002`/`0003`. When this task is done, `dotnet build`, `dotnet test`, and the frontend `lint`/`test`/`build` scripts all pass green, and the CI workflow (`.github/workflows/ci.yml`) can run end-to-end.

## Affected modules/layers

- Domain: empty class library created; namespace root `POM`.
- Application: empty class library created; references Domain only.
- Infrastructure: empty class library created; references Application (+ Domain transitively).
- API: minimal ASP.NET Core host with a `/healthz` endpoint; references Application only (DI composition will reference Infrastructure later in `0002`, kept out of controllers).
- Frontend: new React + TS + Vite app in `src/Web` (Tailwind + RTL, Vitest + RTL).
- Tests: one xUnit sanity project `src/tests/POM.Domain.Tests` so `dotnet test` is runnable today.

## Solution / folder layout

```
/ (repo root)
  Pom.sln                          # solution (net10.0)
  Directory.Build.props            # shared: TargetFramework net10.0, Nullable enable, ImplicitUsings, TreatWarningsAsErrors, LangVersion latest
  Directory.Packages.props         # central package management (optional in this task — see open question)
  .gitignore                       # append .NET + node ignores
  src/
    Domain/      POM.Domain.csproj       (net10.0; references NOTHING)
    Application/ POM.Application.csproj  (→ POM.Domain)
    Infrastructure/ POM.Infrastructure.csproj (→ POM.Application)
    Api/         POM.Api.csproj          (→ POM.Application)  + Program.cs + Dockerfile
    Web/         Vite React app          + package.json + Dockerfile
  tests/
    Domain.Tests/ POM.Domain.Tests.csproj (xUnit, → POM.Domain)  # lives under src/tests per TESTING.md
```

Note: `TESTING.md` §6 names the path `src/tests/POM.Domain.Tests`, so tests live under `src/tests/` (not repo-root `tests/`). Correcting the sketch above accordingly.

## Files to add

Backend:
- `Pom.sln` (root) referencing the 4 product projects + the test project.
- `Directory.Build.props` — `TargetFramework=net10.0`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<LangVersion>latest</LangVersion>`. Per-project overrides where needed (Api sets the Web SDK).
- `src/Domain/POM.Domain.csproj` — `Microsoft.NET.Sdk`, net10.0. No `<ProjectReference>`. Add a placeholder `DomainMarker.cs` so the project isn't empty.
- `src/Application/POM.Application.csproj` — references `POM.Domain`. Placeholder `ApplicationMarker.cs`.
- `src/Infrastructure/POM.Infrastructure.csproj` — references `POM.Application`. Placeholder `InfrastructureMarker.cs`.
- `src/Api/POM.Api.csproj` — `Microsoft.NET.Sdk.Web`, references `POM.Application`. `Program.cs` with a minimal host: `builder.Build()`, `app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }))`, listen on `8080` (matches `docker-compose.yml` `expose: 8080`). `appsettings.json` + `appsettings.Development.json`.
- `src/Api/Dockerfile` — multi-stage .NET 10 build → runtime image, serves on `8080`. (CI's `docker-build` job builds `-f src/Api/Dockerfile` from repo root.)
- `src/tests/POM.Domain.Tests/POM.Domain.Tests.csproj` — xUnit + `Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio`. One trivial sanity test (`SanityTests.The_truth_is_true`) so `dotnet test` runs and is green.

Frontend (`src/Web`):
- Vite scaffold (`npm create vite@latest` React + TS template) producing: `package.json` (Node 22 — matches CI), `package-lock.json`, `vite.config.ts`, `tsconfig.json`, `index.html`, `src/main.tsx`, `src/App.tsx`.
- Scripts required by CI: `build`, `lint` (eslint), `test` (`vitest --run`). Add `vitest`, `@testing-library/react`, `@testing-library/jest-dom`, `eslint` + plugins as devDeps.
- One RTL sanity test (`src/App.test.tsx`) so `npm test -- --run` passes.
- Tailwind CSS + RTL: `tailwind.config`, `postcss.config`, `index.css` with Tailwind directives, `dir="rtl"` on `<html>`.
- `src/Web/Dockerfile` — multi-stage: build (`npm ci` + `npm run build`) → Nginx serving `dist/` on port `80` (matches compose `expose: 80`). `nginx.conf` with SPA fallback.
- `.gitignore` additions: `bin/`, `obj/`, `*.user`, `src/Web/node_modules/`, `src/Web/dist/`.

## Schema changes

None. No entities, no DbContext, no EF Core packages in this task (deferred to `0002`).

## Risks / open questions

- **Central Package Management (Directory.Packages.props):** cleaner long-term, but adds a little ceremony in the very first task. Default: *skip* central package management in `0001`, let each project pin versions; revisit as a decision later if dependency sprawl appears. (To confirm with reviewer.)
- **`TreatWarningsAsErrors` from day one:** intentional (DoD §"no new warnings"), but the Vite/CRA eslint config must be clean or `npm run lint` fails CI. Mitigation: scaffold the default eslint config and keep the starter component lint-clean.
- **.NET 10 SDK availability:** CI pins `dotnet-version: "10.0.x"` already, so the runner side is fine; locally the dev needs the .NET 10 SDK installed. No code risk, just an environment note.
- **`POM` namespace casing:** `POM` (all-caps) as the root namespace matches the test project name already referenced in `TESTING.md` (`POM.Domain.Tests`). Confirm we keep all-caps `POM` rather than `Pom`.
- Dockerfile build contexts: `src/Api/Dockerfile` must build from repo root (CI does `-f src/Api/Dockerfile -t pom-api:ci .`); `src/Web/Dockerfile` builds from `src/Web` (CI does `… -f src/Web/Dockerfile … src/Web`). The two contexts differ — Dockerfiles must be written to match.

## Acceptance criteria

- [ ] `dotnet restore` + `dotnet build --configuration Release` succeed at repo root with zero warnings.
- [ ] `dotnet test` passes (sanity test green).
- [ ] Project reference graph is exactly: `Domain ← Application`, `Application ← Infrastructure`, `Application ← Api`; Domain references nothing; Infrastructure and Api do not reference each other.
- [ ] `GET /healthz` returns `200 {"status":"ok"}` when running the Api locally.
- [ ] `src/Api/Dockerfile` and `src/Web/Dockerfile` both build successfully (`docker build …` per CI commands).
- [ ] In `src/Web`: `npm ci`, `npm run lint`, `npm test -- --run`, `npm run build` all pass.
- [ ] `.gitignore` covers `bin/`, `obj/`, `node_modules/`, `dist/`.
- [ ] No business entities, DbContext, or migrations introduced (those stay in `0002`/`0003`).
- [ ] Tests added (unit + integration) — scaffolding ships a sanity test on each side; real tests come with their features.
- [ ] Docs updated: `PROGRESS.md` `0001` → `[x]`; note in `DECISIONS.md` only if a non-obvious choice was made (e.g. the central-package-management call).
- [ ] DDD layer boundary preserved: Domain has zero infrastructure/EF/ASP.NET references.
