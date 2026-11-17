# Architecture overview

```text
Vue 3 + TypeScript (src/Web)
        │ HTTP / OpenID Connect
        ▼
ASP.NET Core host (src/Platform.Host)
        │
        ├── Core services and domain models (src/Core)
        ├── Synthetic data generation (src/MockData)
        ├── Shared contracts (contracts/v1)
        └── PostgreSQL
```

The Web project contains the browser application and generated API types. The Host project exposes HTTP endpoints, authentication, health checks, and database initialization. Core contains project, stage, import, settings, localization, and audit services. MockData generates fictional records for local demonstrations and tests. The contract JSON files document example payload shapes.

This is a local demonstration architecture. It does not include a configured connection to any production system.
