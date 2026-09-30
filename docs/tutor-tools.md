---
created_at: 2026-09-18
edited_at: 2026-09-18
creator: codex
editor: codex
ai_gen: true
---
# Tutor tools in Tutor Studio

Tutor tools use the existing Microsoft Agent Framework runtime. Calculator is the currently registered built-in implementation. MCP and Approved HTTP are not offered, and tool configuration cannot supply executable URLs.

## Demonstration

1. Open **AI Models** under Admin. Edit a supported interaction model and enable **Function calling**. This records a capability; it does not add tool support to a model. The local Ollama installation reports tool support for `qwen3-vl:4b` and `llama3.2:latest`.
2. Open **Tutors**, create or open a tutor, and create a prompt version. A suitable prompt tells the tutor to use Calculator for arithmetic questions.
3. Open **Revisions**, create a draft, and select the prompt and interaction model.
4. In **Tools**, choose **Add Tool**, select **Calculator**, keep **Automatic** and **Enabled**, and choose **Save tool**. The input schema is supplied by the server; no JSON editing is required.
5. Keep the tool's **This revision** switch on, then choose **Save draft**.
6. Choose **Publish**, confirm, then **Activate** and confirm. The revision should show **Snapshot: Ready**.
7. Choose **Chat** and start a new conversation with this tutor. Existing conversations retain their original snapshot.
8. Send: `Calculate (845 × 37) / 12. Do not calculate it mentally; use the calculator tool.`
9. The expected result is approximately `2605.4166666666665`. Tool activity is driven by actual `tool.started` and `tool.completed` SSE events, not by recognizing calculation text in the browser.

## Ownership, selection, and history

- A tutor owns available `ToolDefinition` records. The definition's real execution policy determines whether it is disabled, automatic, or requires approval; there is no second persisted Enabled flag.
- A draft selects tools using existing `TutorRevisionTool` links. The editor saves `toolDefinitionIds` together with the rest of the draft. An omitted list preserves existing links; an empty list removes them. Cross-tutor IDs and more than ten selected tools are rejected.
- Available tools are saved immediately by the tool modal. Revision switches take effect when **Save draft** is chosen.
- Definitions can be edited while referenced only by drafts. Once any non-draft revision references a definition, it cannot be edited. Add a differently named definition for changed behavior in a future draft.
- Deletion is a hard delete and is allowed only when no revision references the definition. Remove a tool from drafts and save before deleting it. Immutable revisions retain their references.
- Activation serializes the selected definitions' IDs, tutor ownership, names, descriptions, schemas, implementation keys, kinds, and policies into the existing immutable snapshot. Runtime resolution continues to use that snapshot, preserving old conversation behavior.

## Validation and execution policies

**Automatic** maps to `ReadOnlyAutomatic` and is allowed only for a registered read-only handler. **Disabled** prevents execution. **Requires Approval** is stored and displayed accurately, but publication and activation reject that policy until a chat approval flow exists. It is never automatically approved.

The revision editor warns when selected enabled tools need Function Calling and the selected model lacks that capability. Both publication and activation enforce the rule. Text-only configurations and disabled tools do not require Function Calling. Provider rejections of tool requests produce a safe, actionable runtime error without exposing upstream error bodies.

## API

All tool endpoints require the existing Editor-or-Admin API policy plus Editor or Owner membership of the requested tutor. API responses use contracts rather than Domain entities.

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/tutors/{tutorId}/tools` | List available definitions |
| GET | `/api/tutors/{tutorId}/tools/{toolId}` | Read a tutor-scoped definition |
| POST | `/api/tutors/{tutorId}/tools` | Register a known implementation; schema defaults automatically |
| PUT | `/api/tutors/{tutorId}/tools/{toolId}` | Edit a mutable definition |
| DELETE | `/api/tutors/{tutorId}/tools/{toolId}` | Delete an unreferenced definition |
| GET | `/api/tutors/{tutorId}/tools/catalog` | Safe metadata for registered built-ins |
| PUT | `/api/tutors/{tutorId}/revisions/{revisionId}` | Save draft configuration including optional `toolDefinitionIds` |

Revision responses include `toolDefinitionIds`. The tutor interaction-model catalog includes `supportsFunctionCalling`. Tool names must meet the existing runtime function-name rules: 1–64 letters, digits, underscores or hyphens, starting with a letter or underscore.

## Validation coverage

API integration tests cover CRUD, catalog metadata and derived schemas, cross-user and cross-tutor access, unknown implementation keys, unsupported kinds and policies, saved revision link removal/re-addition, immutable-definition protection, disabled-tool publication, approval-policy rejection, Function Calling validation, and snapshot capture. Existing runtime tests cover snapshot isolation, handler execution, and streamed activity. Provider error tests verify useful errors without leaking upstream details.

Frontend validation uses lint, production build, and a real browser against the Docker development environment; this frontend currently has no component-test runner installed.
