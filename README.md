# NPI Factory War Room

**A full-stack prototype for new product introduction (NPI) and cross-functional project tracking.**

以新產品導入情境為例，探索如何把專案進度、階段、負責單位與交付物串成可逐層檢視的管理資訊，協助主管定位進度問題，並為資料整合與權限管理建立基礎。

This project explores how engineering and manufacturing teams can navigate from a project overview to a specific stage, responsible unit and deliverable. It also explores the data contracts, import services and access controls needed to support that workflow across different source systems.

## Demonstration

The recorded walkthrough (103 seconds, shared separately) uses synthetic project data. The source in this repository includes a corresponding example navigation path:

**Project NPI-2601 → DVT (design verification) → SQE (supplier quality) → PPAP document**

The five-level navigation and breadcrumb implementation are available for inspection in [the Vue views](src/Web/src/views/deep/) and [the backend routing service](src/Core/Projects/DeepLinkService.cs). Some links and item labels in this prototype are predefined demonstration examples; the recording is not an assertion that every workflow is supported by this source snapshot.

## Technical highlights

| Area | Source code and implementation focus |
| --- | --- |
| Project navigation | [Project APIs and services](src/Core/Projects/) · [Vue drill-down views](src/Web/src/views/deep/) · [Stage timeline](src/Web/src/components/StageTimeline.vue) |
| Data integration | [Versioned JSON contracts](contracts/v1/) · [Batch import](src/Core/Import/BatchImportService.cs) · [Import history](src/Web/src/views/import/ImportHistory.vue) |
| Identity and data access | [OpenIddict host](src/Platform.Host/) · [Department scope checks](src/Core/Security/DepartmentScopeService.cs) · [PostgreSQL row-level security example](src/Core/Data/DemoItemRls.cs) |
| Management and traceability | [Stage settings](src/Web/src/views/settings/StageSettings.vue) · [Audit log service](src/Core/Audit/AuditLogService.cs) |
| Reproducibility | [Synthetic scenario generator](src/MockData/DvtScenarioGenerator.cs) · [Backend tests](tests/) · [Frontend unit tests](src/Web/tests/unit/) |

The repository demonstrates how these concerns can be separated into reusable components. Configurable stage definitions, data interfaces and traceability are intended to reduce dependence on any one source system or presentation layer.

## Architecture

| Layer | Technology |
| --- | --- |
| Frontend | Vue 3, TypeScript, Vite, Element Plus |
| Backend | C#, .NET 10, ASP.NET Core, ABP Framework, EF Core, OpenIddict |
| Data | PostgreSQL, JSON data contracts, synthetic data generator |
| Development environment | Docker Compose; xUnit and Vitest test sources |

See [Architecture](ARCHITECTURE.md) for the data flow, component relationships and implementation boundaries.

## Run locally

Requirements: Git, Docker Desktop (with Docker Compose), and a local shell. Run from the repository root. The supplied Compose configuration binds services to localhost and is intended for local evaluation only.

Choose unique local-only passwords for the database and demo account; do not reuse real credentials or commit them. Replace the example placeholders before running. For this local demonstration, use a long, randomly generated alphanumeric database password to avoid connection-string escaping issues.

**Windows (PowerShell)**

```powershell
$env:POSTGRES_PASSWORD = '<local-only-value>'
$env:DEMO_USER_PASSWORD = '<local-only-value>'
.\scripts\dev-up.ps1
```

**macOS / Linux**

```bash
export POSTGRES_PASSWORD='<local-only-value>'
export DEMO_USER_PASSWORD='<local-only-value>'
bash scripts/dev-up.sh
```

- Frontend: <http://localhost:5173>
- Backend health check: <http://localhost:5000/health>
- Local demo account: `demo`, using the password configured for the local environment.

Stop the environment with `.\scripts\dev-down.ps1` (PowerShell) or `bash scripts/dev-down.sh` (macOS/Linux). The optional reset mode removes the local database volume; use it only if that data can be discarded.

## Verification and project status

This is an experimental, synthetic-data prototype for technical review, not a production deployment. JSON parsing and Docker Compose configuration checks have passed in the release environment. Full backend and frontend build/test execution and container startup remain to be verified; dependency restoration and the local container engine were unavailable during release validation. The repository includes test sources, but their presence is not a test result.

For engineering validation, use .NET 10 SDK, Node.js 24, npm and Docker. Run the following from the repository root; integration tests require Docker.

```bash
dotnet restore NpiPlatform.sln
dotnet build NpiPlatform.sln --no-restore
dotnet test NpiPlatform.sln --no-build
cd src/Web
npm ci
npm run build
npm run test:unit
npm run lint
```

The project uses fictional data and has no configured connection to a production ERP, MES or other enterprise system. Broader workflow coverage, production identity integration, comprehensive security validation and dependency-license review remain outside the verified scope.

## License

The source is available for technical evaluation. No project license is included; public access does not by itself grant reuse or redistribution rights. Third-party dependencies remain subject to their respective licenses.
