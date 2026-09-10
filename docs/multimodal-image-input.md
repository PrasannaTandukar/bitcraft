---
created_at: 2026-09-10
edited_at: 2026-09-10
creator: codex
editor: codex
ai_gen: true
---
# Multimodal image input

## Overview

AI Tutor accepts text, images, or both. Image understanding only works when the **activated interaction model snapshot has ImageInput enabled AND the underlying provider/model actually supports images**. Model names never imply capability. Existing models and snapshots default to `None` / `supportsImageInput: false`; existing text chat, benchmarking, embeddings, and RAG retain their text path.

## Architecture and model configuration

`ModelCapabilities` is a Domain flags enum. `InteractionModel.Capabilities` is separate from generation `SettingsJson`. Admin interaction-model create/update/read contracts expose `supportsImageInput`, and the Ant Design catalog drawer provides an Image input / Vision switch and Vision tag. Embedding models have no image capability field.

`TutorConfigurationSnapshot.Capture` copies model capabilities for both revision activation and legacy configuration capture. The snapshot property has a private setter. Changing the live catalog entry does not change historical snapshots. Activate a new revision to capture a capability change. Runtime descriptors and answers expose the capability from the selected snapshot, not the live model. Live enabled-state and provider-connectivity checks still apply.

Attachment interfaces live in Application; Domain owns metadata; Infrastructure implements EF persistence, filesystem storage, and ImageSharp normalization. Application has no image-library dependency. API contracts never expose Domain entities or storage keys.

## Attachment lifecycle and limits

The authenticated owner is `ChatAttachment.CreatedById`, using existing BaseModel audit/user foreign keys. PostgreSQL stores ID, original filename, MIME type, byte length, dimensions, storage key, creation/audit fields, and expiry. Image bytes are stored outside PostgreSQL and the web root.

Defaults:

| Setting | Default | Purpose |
| --- | --- | --- |
| `RootPath` | OS temporary directory / `bitcraft-chat-attachments` | Dedicated private image storage; configure persistent storage for deployments |
| `MaxFileBytes` | 5,242,880 (5 MiB) | Input and normalized image byte limit |
| `MaxImagesPerMessage` | 4 | Current message and each historical user message |
| `MaxImagesPerRequest` | 20 | Distinct images across bounded history plus current message |
| `MaxDimension` | 2048 | Maximum normalized width or height, preserving aspect ratio |
| `MaxDecodedPixels` | 40,000,000 | Pre-decode protection against image expansion attacks |
| `RetentionHours` | 24 | Temporary lifetime from upload; use does not extend it |

Startup rejects unsupported configuration ranges: file bytes 1–20 MiB, images/message 1–20, images/request at least the per-message limit and at most 80, dimension 1–4096, decoded pixels 1–40 million, retention 1–168 hours. Conservative defaults are appropriate for limited-memory hosts; larger settings increase memory and provider payload sizes.

Accepts still JPEG, PNG, and WebP only. The supplied MIME must match the decoded format. The server rejects SVG, GIF, PDF, remote URLs, empty/corrupt images, animations, oversized uploads, and excessive decoded dimensions. It applies EXIF orientation, resizes, copies only pixels into a fresh image, and encodes PNG to remove EXIF/GPS, ICC, XMP, and frame metadata. A normalized PNG that exceeds the byte limit is rejected; choose a smaller image in that case. Original files are not retained. The returned MIME and size describe normalized bytes.

Every preview and runtime read rechecks ownership and expiry. Cross-user IDs behave as missing resources. Expired attachments fail immediately, even before disk cleanup. A hosted worker sweeps at startup and every five minutes, deleting expired files and metadata in batches. Private expiry-bearing storage keys also let the worker remove orphaned or partial files after interrupted uploads, even when no metadata was saved. Failed sweeps retry; monitor cleanup warnings and disk usage. This is a single-storage deployment implementation, not a distributed cleanup system.

Unsent uploads are deleted when removed, when the composer resets, or when it unmounts. An upload completing after reset is deleted. Sent images remain until expiry so follow-ups and retries can reuse them. Blob preview URLs are revoked on unmount; image bytes and API data never enter Redux or persistent browser history.

## API and runtime request flow

All attachment endpoints require the same `AuditorOrAbove` policy as runtime chat. Bearer authentication is required for previews too; the frontend obtains a Blob through the central API client rather than adding tokens to image URLs.

1. `GET /api/runtime/attachments/options` returns configured public limits and allowed MIME types.
2. `POST /api/runtime/attachments/images` accepts `multipart/form-data` containing exactly one `file` field. Upload multiple images with separate requests.
3. The 201 response is `{ id, fileName, mimeType, width, height, byteLength, expiresUtc }`, with a `Location` header for the preview endpoint. It never includes a storage path/key.
4. `GET /api/runtime/attachments/{id}` returns normalized bytes with `Cache-Control: no-store, private` and `X-Content-Type-Options: nosniff` for the owner.
5. `DELETE /api/runtime/attachments/{id}` deletes the owner's bytes and metadata and returns 204. Owners may delete expired attachments too.
6. `GET /api/runtime/tutors` includes `model.supportsImageInput` from the active snapshot. All supported generation providers can appear in this list.
7. `POST /api/runtime/tutors/{activationSlug}/ask` accepts:

```json
{
  "question": "What happens if Q3 fails?",
  "topK": 5,
  "attachmentIds": [],
  "history": [
    { "role": "user", "content": "Explain this circuit.", "attachmentIds": ["<uploaded-image-uuid>"] },
    { "role": "assistant", "content": "Check the transistor connections." }
  ],
  "conversationId": null
}
```

The UUID placeholder must be replaced with an actual upload ID. `question` may be empty or omitted when current-message attachments exist. With neither text nor current images, the request fails. Assistant history cannot contain attachments. Persistent conversation IDs remain unsupported.

The runtime resolves tutor membership, active revision, and immutable snapshot, then enforces ImageInput **before loading images**. It validates ownership/expiry and loads images in history order followed by current-message order, deduplicating IDs by first occurrence. Prompt labels (`Image 1`, `Image 2`, etc.) match the order of `LlmGenerationRequest.Images` exactly; storage IDs/paths are not included in those labels. Replaying the same bounded input produces the same labels and payload order.

The frontend bounds history to 20 messages / 30,000 content characters and drops oldest messages when needed to meet the total image budget. It retains attachment IDs on user messages. Failed-message retries retain the exact question, attachment IDs, retrieval depth, and bounded history. Expired/missing history images require reattachment or a new chat.

## RAG and providers

RAG remains text-only: question text → existing query embedding model → pgvector course context. The ordinary tutor instructions, bounded history, retrieved context, question, and ordered images then go to the interaction model. Image-only requests skip embedding/retrieval and use the internal prompt `Please analyze the attached image(s).` No captions, multimodal embeddings, or new vector stores are introduced.

| Provider | Multimodal request |
| --- | --- |
| Ollama | Existing `/api/generate` request plus ordered Base64 `images`; omitted for text-only calls |
| OpenAI-compatible (including OpenAI, vLLM, llama.cpp) | User content parts: prompt text followed by ordered `image_url` data URLs |
| Anthropic | Ordered Base64 image source blocks followed by the prompt text block |

Model, endpoint, key handling, generation settings, text-only compatibility methods, and existing response/error handling remain intact. A provider implementing only the legacy text interface explicitly rejects image requests rather than silently losing images. Providers may have stricter format, image-count, resolution, or token limits than Bitcraft.

## Admin diagnostic

Edit a saved interaction model and use **Test image input** to select a local image. The UI uploads it through the same attachment service, calls `POST /api/admin/interaction-models/{id}/test-image` with `{ "attachmentId": "..." }`, and deletes the temporary diagnostic image afterward. The response is `{ "answer": "..." }`.

This Admin-only test uses the exact saved model, runtime endpoint, and credentials with a short descriptive prompt. It can run before enabling Vision and never changes capability metadata. A successful HTTP request is not proof that the model understood the image; the administrator must inspect the answer and explicitly configure Vision.

## Errors and security

| Error code | HTTP status |
| --- | --- |
| `ChatAttachment.FileTooLarge` | 413 |
| `ChatAttachment.UnsupportedType` | 415 |
| `ChatAttachment.InvalidImage`, `InvalidUpload`, `TooManyImages` | 400 |
| `ChatAttachment.NotFound` | 404, including another user's IDs |
| `ChatAttachment.Expired` | 410 |
| `ChatAttachment.StorageUnavailable` | 503 |
| `TutorRuntime.ImageInputNotSupported` | 409 |
| `TutorRuntime.ImageRejected`, `InteractionModel.ImageTestFailed` | 502 |

Upload parsing and stream copying are bounded, including requests without Content-Length. The existing global rate limiter remains active. Restricted codecs, pixel/memory limits, and still-image checks constrain decoder work. The filesystem implementation accepts only generated storage keys, writes atomically, and creates private Unix files/directories. Storage must be dedicated to this application and writable only by the API service account; existing mounted-directory permissions remain the operator's responsibility.

The server never fetches arbitrary image URLs. Chat JSON contains IDs, never Base64. Only provider serialization constructs Base64/data URLs. Logs contain safe identifiers/counts/statuses, not bytes, Base64, storage paths, secret refs, or raw upstream error bodies. Images are sent to the configured LLM provider and should be covered by the deployment's normal provider/privacy policy.

Normalization uses ImageSharp **3.1.12** in Infrastructure. This version avoids the v4 build-time license-key requirement. PNG decoding checks all chunk CRCs and limits decompressed ancillary chunks to 1 MiB. Container completion checks reject truncated PNG/JPEG/WebP files before full decoding. PNG/WebP container animation markers, frame metadata, and decoded frame counts enforce still images, including animations whose identical frames are coalesced. A process-wide cancellation-aware semaphore allows one normalization at a time, alongside the 256 MiB per-allocation limit, 64 MiB retained allocator pool, and existing file/pixel/dimension limits; v3 has no aggregate allocator-limit option. These are bounded-work safeguards, not a hard cap on total process memory. Release publishing and the Docker backend build must be included in dependency-upgrade validation.

## Docker and local configuration

The base `docker-compose.yml`, inherited by dev/prod and Jetson AI overlays, mounts the named `chat_attachments` volume at `/var/lib/bitcraft/chat-attachments` and sets `ChatAttachments__RootPath`. `.env.example` documents optional size/count/dimension/retention overrides. No machine-specific host path is required. For a standalone API, set `ChatAttachments__RootPath` to a dedicated directory outside the web root if images must survive OS temporary-directory cleanup.

The frontend Nginx CSP allows `blob:` image sources for authenticated previews. A separate reverse proxy must allow multipart uploads at least as large as `MaxFileBytes` plus 64 KiB framing overhead. Multi-instance API deployments must share both metadata and attachment storage; independent local disks are not sufficient.

## Migration and local test

Generated migration: `20260909172416_AddMultimodalImageInput`. It adds `Models.Capabilities` (nullable in the existing TPH table, default 0), `TutorConfigurationSnapshots.ModelCapabilities` (non-null, default 0), and `ChatAttachments` with audit foreign keys and ownership/expiry/storage indexes. It does not infer capabilities or rewrite existing snapshots.

1. Apply the migration to your local development database:
   `dotnet ef database update --project src/Bitcraft.Infrastructure --startup-project src/Bitcraft.Api`.
2. Start the API and Tutor Studio using the existing [local setup guide](local-setup.md). Ensure the API attachment directory is writable and your Ollama runtime is reachable.
3. As Admin, create/enable an Ollama runtime engine. Create an interaction model with your actually installed vision model's exact provider model name and enable **Image input / Vision**. Use **Test image input** on the saved model and inspect its answer.
4. Select that model in a tutor draft revision with a prompt; publish and activate it. For a RAG check, attach/vectorize course documents and configure the existing query embedding model before activation.
5. As a tutor member with runtime access, open AI Tutor, select the tutor, attach a JPEG/PNG/WebP, and ask `Why isn't this LED turning on?`. Verify the upload returns an ID and `/ask` sends IDs without Base64.
6. Ask a follow-up about the image, then try image-only input. Verify a failed-message retry preserves the image and history.
7. Select a text-only tutor: the attachment button must be disabled. A forged image request to it returns 409.
8. Disable Vision on the live model: the existing active snapshot remains vision-capable. Activate a new revision to obtain the changed capability.

Automated checks:

```bash
dotnet build bitcraft.slnx
dotnet test tests/Bitcraft.UnitTests/Bitcraft.UnitTests.csproj
dotnet test tests/Bitcraft.ApiIntegrationTests/Bitcraft.ApiIntegrationTests.csproj
dotnet test tests/Bitcraft.InfrastructureIntegrationTests/Bitcraft.InfrastructureIntegrationTests.csproj
cd tutor-studio
npm ci
npm run build
npm run lint
```

Unit tests cover flags/snapshots, text compatibility, image validation/normalization, expiry/cleanup, runtime ordering/RAG, and provider serialization using tiny fixture bytes. API tests use the existing isolated in-memory EF host with real normalization/filesystem storage and a fake generation provider. Actual image understanding requires the manual test with an installed vision model.

## Known limitations

Attachments and local conversation history are temporary; configuration snapshots do not archive user image bytes. Reloading the page clears the local conversation. There is no image-caption retrieval, multimodal embedding, animation, remote-image ingestion, audio, or document chat input. Normalized PNG byte limits and provider-specific image/context limits still apply. Local filesystem storage requires a shared mount for multiple API instances. The diagnostic confirms a request completed; human review establishes whether its answer reflects the image.
