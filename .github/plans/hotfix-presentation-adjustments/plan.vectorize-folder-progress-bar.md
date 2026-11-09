---
created_at: 2026-11-09
edited_at: 2026-11-09
creator: planner
editor: planner
ai_gen: true
branch: hotfix-presentation-adjustments
css: 3
dis: 3
cil: low
status: complete
---

# Plan: Vectorize Folder Progress Bar

Sub-plan of the `hotfix-presentation-adjustments` branch. The main batch-upload plan is complete.

## Goal

Show a real per-document progress bar while the Vectorize Folder dialog runs.

## Background

`VectorizeFolderDialog.jsx` calls `vectorizeDocumentsBatch` once. The server returns all results at the end. The frontend cannot show progress during the wait. The user chose Route A: the dialog calls the existing single-document endpoint `POST /api/documents/{id}/vectorize` once per document in a sequential loop. Each call updates the progress bar. Request overhead is negligible next to embedding compute on a local model.

## Non-Goals

- No backend streaming, SSE, or job-status endpoint.
- No parallel document vectorization in the frontend loop.
- No changes to `vectorize-batch` or `upload-batch` endpoints.
- No changes to the single-document Vectorize dialog.

## Assumptions

- `FilesPage` holds the folder-scoped document list in `documents`. It can pass it to the dialog.
- `vectorizeDocument(documentId, payload, signal)` in `documentApi.js` already exists and works for one document.
- The per-document endpoint checks ownership. The loop runs as the same authenticated user.
- Backend `ListByFolderAsync` and the folder button count direct folder members only. The frontend filter must match that predicate.

## Risks

- Sequential requests are slower than one batch request. Mitigation: per-document progress makes the wait visible. Embedding compute dominates total time.
- Closing the tab mid-loop stops remaining documents. Already vectorized documents keep their chunks. This matches batch endpoint behavior.

## Work Steps

### Step 1 — Pass the folder document list to the dialog

Skill: `bitcraft-frontend-feature`

1. Edit `tutor-studio/src/pages/editor/FilesPage.jsx`. Next to `vectorizeFolderCount` (line 58), add a `vectorizeFolderDocuments` constant. For `selectedFolderId === "root"` use `documents.filter((document) => !document.folderId)`. Otherwise use `documents.filter((document) => document.folderId === vectorizeFolderId)`.
2. In the `VectorizeFolderDialog` render (line 129), add the prop `documents={vectorizeFolderDocuments}`. Remove the `documentCount={vectorizeFolderCount}` prop. The dialog derives the count from the list.

Validation: from `tutor-studio/` run `npm run lint`. Expected result: zero errors.

### Step 2 — Replace the batch call with a sequential per-document loop

Skill: `bitcraft-frontend-feature`

1. Edit `tutor-studio/src/features/editor/components/VectorizeFolderDialog.jsx`. Change the component signature to accept a `documents` prop. Treat it as the array of `{ id, name }` for the folder.
2. Add state `progress` holding `{ done, total }` or `null`.
3. In `handleSubmit`, replace the single `vectorizeDocumentsBatch` call with a `for` loop over `documents`. For each document call `vectorizeDocument(document.id, { embeddingModelId: modelId, maxChunkLength: chunkSize, chunkOverlap: overlap }, controller.signal)`.
4. Wrap each call in `try`/`catch`. On success increment `done`. On failure push `{ name: document.name, errorDescription: getApiErrorDescription(requestError, "Vectorization failed.") }` into a local failures array and still increment `done`. A per-document failure does not stop the loop.
5. Call `setProgress({ done, total: documents.length })` after each document. Use `setProgress(null)` after the loop.
6. After the loop set the results summary state to `{ succeeded, failed: failures }`. Call `onVectorized?.()` once when `succeeded > 0`.
7. Replace the `saving` paragraph with an antd `Progress` bar plus the text `Vectorizing document {done + 1} of {total}` while the loop runs. Keep the existing spinner text as a fallback when `progress` is `null`.
8. Update the results Alert to read from the new summary shape. Update the failures `List` `dataSource` to the failures array. Render each item as the document name plus the error description.
9. Keep the existing `AbortController`, `requestRef`, abort-on-unmount effect, and disabled-close behavior unchanged.

Validation: from `tutor-studio/` run `npm run lint`. Expected result: zero errors.

### Step 3 — Validate in the browser

Skill: `bitcraft-frontend-feature`

1. From `tutor-studio/` run `npm run dev`.
2. Open Documents. Select a folder with at least 3 documents. Click Vectorize folder. Confirm the progress bar advances per document and the failure list renders.
3. Stop the dev server after the check.

Validation: manual observation. Expected result: the bar advances and the summary matches the document count.

## Acceptance Criteria

- The Vectorize Folder dialog shows a progress bar that advances after each document.
- The progress text names the current document number and the total.
- A per-document failure does not stop the loop.
- The dialog lists each failed document name with its error description.
- The summary states how many documents succeeded.
- Existing abort and disabled-close behavior stays unchanged.

## Open Human Decisions

None. The human selected Route A (frontend sequential per-document calls).

## Delegation Route

- Steps 1-3: Lead Dev with `bitcraft-frontend-feature`.
- Review: `bitcraft-review` at CIL low. Stage A drift check plus Stage B style review of the two files.

## Phase 2 Prompt

Run the `bitcraft-develop` skill for `.github/plans/hotfix-presentation-adjustments/plan.vectorize-folder-progress-bar.md`. Execute work steps in order. Validate each step before the next.
