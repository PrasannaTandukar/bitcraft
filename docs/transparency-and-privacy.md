# Transparency and Privacy Statement

Bitcraft aims to remain open-source-first and deployment-transparent. This document is the operator-facing record of that posture, the current data-handling model, and the process for disclosing exceptions.

## Open-Source Posture

- Bitcraft should prefer open-source runtime dependencies and self-hostable infrastructure by default.
- Any mandatory proprietary runtime dependency or hosted platform dependency must be disclosed here before it becomes part of the standard stack.
- Development tooling may vary by contributor. Proprietary IDE tooling such as GitHub Copilot is not part of the deployed runtime and is tracked separately from runtime dependencies.

## Repository License Options

Bitcraft does not currently include a repository-level `LICENSE` file. The practical options under consideration are:

| Option | Fit | Notes |
|---|---|---|
| `Apache-2.0` | Recommended default | Permissive, commercially friendly, and includes an explicit patent grant. |
| `MIT` | Minimal alternative | Very common and easy to adopt, but lighter on patent language. |
| `MPL-2.0` | File-level reciprocity | Useful if future contributors should share changes to modified source files while still allowing broader combination. |

## Current Runtime Dependency Disclosure

The current runtime stack is open-source-first. No mandatory proprietary runtime dependency is currently approved.

Key upstream components used by the repository include:

| Component | Use In Bitcraft | Reference |
|---|---|---|
| ASP.NET Core + ASP.NET Core Identity | API host, authentication, authorization, user/role persistence | https://github.com/dotnet/aspnetcore |
| Entity Framework Core | ORM, migrations, persistence abstractions | https://github.com/dotnet/efcore |
| PostgreSQL | Primary relational data store | https://www.postgresql.org/about/licence/ |
| pgvector | Vector extension for PostgreSQL knowledge storage and retrieval | https://github.com/pgvector/pgvector |
| Ollama | Embedding/model endpoint for local or self-hosted inference | https://github.com/ollama/ollama |
| DeepEval | Benchmarking/evaluation library used by the Python sidecar | https://github.com/confident-ai/deepeval |

Transparency note: ASP.NET Core Identity is called out explicitly because it handles user accounts and role membership. It is not a proprietary exception; it is part of the disclosed open-source runtime stack.

## Current Exception Register

- Mandatory proprietary runtime exceptions: none approved.
- Optional proprietary development tooling: GitHub Copilot or similar editor tooling may be used by contributors, but it is not required to build, run, or deploy Bitcraft.

Any future exception should be recorded with:

1. Product or service name.
2. Scope of use.
3. Data classes affected.
4. Reason an open-source alternative was not selected.
5. Upstream or vendor reference.
6. Replacement or exit path, if known.

## Privacy and Data Handling

Bitcraft is intended to be deployable in a self-hosted posture. Operators should still review and disclose where user content leaves local infrastructure.

### Data Categories

- User accounts and role assignments are stored in PostgreSQL through ASP.NET Core Identity.
- Source documents and their raw text may be stored in PostgreSQL through the Documents capability.
- Knowledge chunks and embeddings are stored in PostgreSQL + pgvector.
- Benchmark inputs and outputs are stored through the benchmarking domain and processed by the benchmark sidecar.

### Runtime Data Flows

- Document vectorization sends document text to the configured Ollama endpoint at `OLLAMA_BASE_URL`.
- In the default Docker stack, the API sends benchmark requests to the sidecar over a Unix domain socket mounted at `/var/run/bitcraft/sidecar.sock`.
- The current sidecar benchmark tool executes deterministic local scoring inside the sidecar process and does not require a DeepEval cloud login.

### No First-Party Analytics By Default

The current codebase does not configure first-party product analytics or telemetry collectors. If deployment-specific telemetry is added later, it should be documented in this file and in the relevant deployment instructions.

### Operator Responsibility

If an operator points Bitcraft at external hosted inference or evaluation services, prompts, source documents, benchmark inputs, or outputs may leave local infrastructure. That is a deployment decision and should be documented in the operator's environment-specific runbook or privacy documentation.

## Documentation Maintenance Rules

- Update this file whenever a new runtime dependency materially changes data flow, licensing posture, or external service exposure.
- Keep this file aligned with `README.md`, `docs/authorization-roles.md`, and deployment instructions.
- Prefer explicit disclosure over marketing shorthand. If a dependency, service, or workflow deserves explanation, list it.
