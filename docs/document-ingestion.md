---
created_at: 2026-08-07
edited_at: 2026-08-07
creator: codex
editor: codex
ai_gen: true
---
# Document ingestion and vectorization

Tutor Studio uploads source files to `POST /api/documents/upload` as multipart form data. Bitcraft reads a bounded byte payload, extracts text on the API host, and stores only the extracted text and safe file metadata. Browser file paths and uploaded binary content are not persisted.

Supported formats are TXT, Markdown, CSV, JSON, PDF, and DOCX. Plain text supports UTF-8, UTF-8 BOM, and BOM-marked UTF-16 LE/BE. PDF extraction preserves nonblank pages in order; DOCX extraction reads document-order paragraphs and table cells. Images embedded in documents are ignored, and OCR is not supported.

Default safety limits are configured under `Documents`:

- `MaxUploadBytes`: 10,485,760 bytes (10 MiB)
- `MaxExtractedCharacters`: 2,000,000 characters
- `MaxVectorizationChunks`: 500 chunks

Metadata-only documents created by older clients can be repaired through `PUT /api/documents/{documentId}/content`. Extraction must succeed before any existing content changes. A successful content replacement updates the document and removes its now-stale chunks in one transaction; failed extraction preserves both the previous text and vectors.

Vectorization remains an explicit action at `POST /api/documents/{documentId}/vectorize`. New clients select an enabled `EmbeddingModelId` from `GET /api/model-catalog/embedding-models`; legacy provider/model fields remain supported. The configured provider model name must exactly match an installed Ollama embedding model. Bitcraft generates and validates every new embedding before atomically replacing prior chunks, so a failed re-vectorization does not destroy a valid index.

Chunks prefer paragraph, sentence, and whitespace boundaries, with a configurable character overlap. `GET /api/documents/{documentId}/vectorization-status` reports readiness, chunk count, last ingestion time, and safe model metadata without loading embedding arrays.
