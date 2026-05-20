---
created_at: 2026-05-19
edited_at: 2026-05-19
creator: ai-ops
editor: lead-dev
ai_gen: true
description: 'Tutor Studio frontend rules for React/Vite work. Apply only when frontend work is explicitly requested.'
applyTo: 'tutor-studio/**/*.{js,jsx,ts,tsx,css,html}'
---

# Tutor Studio Frontend

Use these rules only when the user explicitly requests frontend work.

## Frontend Scope
- Preserve the existing Vite and ESLint setup.
- Use React functional components and hooks.
- Keep UI changes consistent with the existing Tutor Studio patterns unless the request is a redesign.
- Do not introduce new frontend packages without a clear need and explicit validation plan.

## UX And Integration
- Build actual workflow surfaces, not marketing pages, when the task is an app feature.
- Keep API calls aligned with `src/Bitcraft.Api/Contracts` and controller routes.
- Validate responsive behavior and obvious text overflow for new UI.
