# NPI Factory War Room Showcase

A synthetic-data demonstration of an NPI/NPD project tracking platform. The codebase is organized as a .NET 10 / ASP.NET Core backend with PostgreSQL and a Vue 3 + TypeScript frontend.

## What is implemented

The repository includes sign-in through OpenIddict, project and stage APIs, L0–L4 project drill-down views, a stage timeline, stage settings, import history, audit logging, localization, and generated synthetic project data. These are code-level descriptions; see the validation status below before treating a feature as verified.

## Validation status

Release validation is recorded in the private release report, not in this public repository. At the time this README was prepared, .NET, Vue, and Docker execution checks had not completed in the build environment. Do not infer runtime success from the presence of code or tests.

## Run locally

Requirements: Docker Desktop with Compose, Git, and a local shell. Do not expose this development setup to the public internet.

Set local-only passwords before starting. Do not reuse real account credentials.

PowerShell:

```powershell
$env:POSTGRES_PASSWORD = '<local-only-value>'
$env:DEMO_USER_PASSWORD = '<local-only-value>'
.\scripts\dev-up.ps1
```

macOS / Linux:

```bash
export POSTGRES_PASSWORD='<local-only-value>'
export DEMO_USER_PASSWORD='<local-only-value>'
bash scripts/dev-up.sh
```

Open <http://localhost:5173>. The local demo account name is `demo`; its password is the `DEMO_USER_PASSWORD` value you supplied. Stop with `scripts/dev-down.ps1` or `bash scripts/dev-down.sh`. Use a reset option only when you intend to erase the local demo database volume.

## Checks

```bash
dotnet test NpiPlatform.sln
cd src/Web
npm ci
npm run test:unit
npm run build
npm run lint
```

Integration tests require Docker. Results depend on the tools and environment used; this README does not claim these commands have passed.

## Limits and data

The 103-second video shows a synthetic-data demonstration. This repository does not claim to implement every workflow in the video, and it is not connected to Quanta Computer's production systems. Sample data is fictional. No production credentials, company datasets, or real integration endpoints belong here.

No license is included. Public visibility alone does not grant reuse or redistribution rights.
