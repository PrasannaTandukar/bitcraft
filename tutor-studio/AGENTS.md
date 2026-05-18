# AGENTS.md - Tutor Studio Frontend

This nested file gives coding agents focused guidance when working inside `tutor-studio/`. The repository-root `AGENTS.md` and `.github/copilot-instructions.md` still apply; this file adds frontend-specific rules.

## Stack

- React 18, Vite, plain JavaScript (some JSX), no TypeScript at present.
- Zustand-style store under `src/store/`.
- ESLint configured via `eslint.config.js`.
- Nginx is used only for production container image (`Dockerfile.prod`, `nginx.conf`).

## Commands

- Install: `npm install`.
- Dev server: `npm run dev`.
- Lint: `npm run lint`.
- Build: `npm run build`.

Run the narrowest relevant validation first.

## Conventions

- Page-level components live directly under `src/` and are named `<Area>Page.jsx` or `<Area>.jsx` for non-page screens (`TutorsPage.jsx`, `FilesPage.jsx`, `Benchmarking.jsx`).
- Shared state goes in `src/store/`. Avoid prop drilling deeper than two levels when a store exists.
- API integration is centralized; do not hardcode URLs in components.
- Keep components small. Extract a child component when a render branch grows beyond one screen.

## Constraints

- Do not introduce TypeScript without an explicit migration plan.
- Do not introduce a new global state library while a Zustand-style store already exists.
- Do not store secrets, tokens, or production endpoints in source.
- Do not modify root configuration files (`.gitignore`, root Docker compose) without explicit human approval.

## Backend Contract

The frontend talks to `src/Bitcraft.Api`. Contracts live in `src/Bitcraft.Api/Contracts/`. Never expose Domain entities through API contracts; if the backend exposes a Domain entity, raise the issue before changing the frontend.
