---
description: 'Bitcraft API controller, contract, route, Swagger, and mapping rules.'
applyTo: 'src/Bitcraft.Api/Controllers/**/*.cs,src/Bitcraft.Api/Contracts/**/*.cs,src/Bitcraft.Api/Mapping/**/*.cs'
---

# API Contracts And Controllers

## Contract Layout
- Contracts live in `src/Bitcraft.Api/Contracts/<Context>/<Feature>.cs`.
- Use one contract file per feature, with contextual names such as `TutorPrompts.cs`, `TutorDeployments.cs`, or `TutorKnowledgeSources.cs`.
- Request and response models are records ending with `Request` or `Response`.
- Do not return Domain entities directly from controllers.

## Controller Layout
- Controllers are foldered by bounded context: `Auth`, `Admin`, `Documents`, `Knowledge`, `Ollama`, and `Tutoring`.
- Use one controller per top-level resource. Tutor sub-resources get nested controllers, such as `api/tutors/{tutorId}/prompts`.
- Keep route names REST-shaped and resource-oriented.
- Translate Application `Result<T>` values into HTTP responses consistently.

## Documentation And Security
- Add Swagger annotations for API endpoints when adding or changing behavior.
- Preserve authorization policies and ownership checks; do not bypass them for convenience.
- Avoid leaking internal exception details, secrets, provider credentials, or Domain entity internals in responses.
