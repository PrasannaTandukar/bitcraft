# Assistant answer provenance foundation

## Existing execution and storage

Authenticated `TutorRuntimeController` asks and SSE streams pass through the existing
MediatR `TutorRuntimeFeatures` handlers to `ITutorRuntime` / `TutorRuntime`.
Runtime checks membership, conversation ownership, revision eligibility, the immutable
configuration snapshot, enabled models/providers, and attachments before calling
`ITutorAgentExecutor`. Existing conversations supply their pinned revision/snapshot,
not the tutor's current activation. The interactive handlers always request conversation persistence.

Infrastructure's `TutorAgentExecutor` constructs Microsoft Agent Framework's
`ChatClientAgent` with the provider-specific `IChatClient` from `RuntimeChatClientFactory`.
`TutorKnowledgeContextProvider` and `TutorMemoryContextProvider` provide untrusted
context through the framework lifecycle. `TutorToolRegistry` supplies snapshot-bound
functions; `FunctionInvokingChatClient` executes their actual calls through registered,
bounded handlers and records completion activity. No additional chat path is introduced.

`Document` stores name/raw text; `KnowledgeChunk` is the repository's document-chunk
entity and contains index, text, embedding and model/dimension bindings. The pgvector
retriever returns nearest chunks scoped to snapshot document IDs and embedding bindings.
The knowledge context provider rechecks those bindings, computes cosine similarity,
filters/orders candidates and bounds text/context. `TutorRuntimeSource` carries the
document name, chunk identity/index, score and bounded excerpt. `TutorKnowledgeResult`
contains retrieval candidates and reference material. `ContextSources` identifies only
the subset included within the reference-material budget.

`TutorMemoryResult` carries retrieved memory sources and save status. Its
`ContextMemoryCount` counts entries with at least one content character supplied within
the context budget; labels alone do not count. Raw memory is not persisted in provenance.
`TutorToolCallActivity` already carries invocation name/status/timing, sanitized summary
and fixed error categories. Provenance omits even the sanitized result summary.

`TutorConversation` owns ordered `TutorConversationMessage` entities and the serialized
agent session. Runtime completes a turn only after successful generation. The existing
repository saves session, user message, assistant message and provenance together,
using the conversation version for concurrency and a transaction on relational databases.
SSE deltas are progress; `message.completed` follows this same completed-turn save.
Cancellation, generation failure and save conflict do not commit partial provenance.
Runtime also checks cancellation immediately before completing a durable turn.

## Production field origins

Both ordinary asks and SSE execute `TutorRuntime.ExecuteCoreAsync` and
`TutorAgentExecutor.ExecuteCoreAsync`. The following values are captured from that
execution, without another retrieval or inspection of generated answer text:

| Stored provenance | Origin |
| --- | --- |
| Tutor/revision/snapshot IDs | The authorized, snapshot-pinned `TutorAgentRequest` built by runtime |
| Interaction model ID / provider type | The resolved `TutorAgentRequest.Model` and successful executor response |
| Provider model name | The framework response's provider-reported model name; selected snapshot model name if unreported |
| Document/chunk IDs, index and name | `KnowledgeChunk` identity and document display name resolved by the knowledge context provider for this execution |
| Retrieval rank | One-based position in the provider's filtered, ordered `TutorKnowledgeResult.Sources` |
| Cosine similarity | The exact `TutorRuntimeSource.Score` computed by the current RAG context provider; provenance does not recompute or convert it |
| Content excerpt / embedding metadata | The same bounded `ContextSources` used to build reference context and their execution-time embedding metadata |
| Memory boolean/count | `TutorMemoryContextProvider` counts entries with content surviving the context budget; runtime stores only the boolean/count |
| Tool name/status/timestamps/duration/category | Completion activity accumulated by `TutorToolRegistry`'s actual invocation wrapper around registered handlers |
| Capture timestamp | UTC time when the completed assistant message's evidence is created |

`TutorMessageProvenanceCapture` consumes the successful executor response once, and
`TutorConversation.CompleteTurn` attaches it to that turn's assistant message. The
repository commits it with the answer/session. An SSE `message.completed` event is
emitted after that save succeeds. Partial deltas, configured-but-uninvoked tools,
retrieval candidates excluded by the context budget, and failed/cancelled generations
do not become completed answer evidence. Existing legacy `KnowledgeUsed` / `MemoryUsed`
flags now mean context actually supplied; they do not assert model reliance.

The benchmark adapter continues to use the same runtime/executor/context providers.
Benchmark execution does not request a durable conversation, so it does not manufacture
an assistant-message provenance record; its existing output metadata remains functional.

## Production execution validation

`TutorRuntimeProvenanceExecutionTests.cs` drives authenticated API requests through the
real runtime, executor, Microsoft Agent Framework, context providers, Calculator handler
and conversation repository. Only the provider client, vector retrieval/embedding
adapters and database provider are test doubles. It checks both ordinary and SSE asks,
five exact short chunks, budget-excluded chunks, zero-knowledge responses, actual tool
success/failure, memory disabled/empty/supplied/budget-limited states, generation IDs,
one retrieval per request, post-context provider failure, cancellation after a delta,
historical evidence after chunk/catalog changes, and the canonical benchmark adapter.
Production DI registrations and a concurrency conflict leaving only the winning message's
provenance are also covered. These deterministic tests do not claim live-provider or
PostgreSQL verification when optional integration environments are unavailable.

## Generation-time model

An assistant message optionally owns `TutorConversationMessageProvenance`:

- Tutor, revision, immutable configuration snapshot and interaction-model IDs.
- Actual provider-reported model name, falling back to the selected snapshot model;
  provider type and UTC capture time.
- `MemoryContextProvided` and `RetrievedMemoryCount`: retrieved memory entries actually
  supplied as context, including a partially supplied entry; no memory text/IDs are stored.
- Independent `TutorRetrievedKnowledgeEvidence` records: document ID/name snapshot,
  chunk ID/index, bounded content excerpt (maximum 1,201 characters, matching current
  retrieval excerpts), original one-based retrieval rank, nullable cosine similarity,
  and embedding model ID/provider model name/dimensions.
- Independent `TutorToolExecutionEvidence` records for actual completed calls:
  tool name, succeeded/failed/timed-out status, start/end UTC timestamps, duration,
  and only the existing safe `Timeout` / `ExecutionFailed` categories. Merely configured
  tools and running progress events are not execution evidence.

Knowledge evidence means **retrieved and supplied to the tutor as context**. It does
not prove that the model relied on a chunk, and is not an explicit model citation.
The stored excerpt reflects supplied context, not today's chunk text. Its capture time
is the parent provenance's `CapturedUtc`. No full documents, vectors, instructions,
system prompts, hidden reasoning, tool arguments/results, raw memory, credentials,
endpoints or agent-session state are copied into this model.
Capture timestamps use UTC microsecond precision so completion payloads and PostgreSQL
history reloads agree exactly. This normalization applies only to new captures; historical
records are never rewritten.

Domain defines framework-independent immutable evidence. Application maps the existing
executor results; Infrastructure maps optional EF owned entities into the three new
tables. The aggregate enforces that provenance matches the conversation's pinned
configuration. Document/chunk/tool/model/configuration identifiers in provenance are
historical scalar values without live-resource foreign keys. Source deletion or
re-vectorization therefore cannot cascade-delete or rewrite evidence. Existing
conversation snapshot/revision restrictions, ownership and tutor deletion behavior
remain in force; explicitly deleting a conversation deletes its owned evidence.

## Migration and compatibility

`20261004145609_AddTutorMessageProvenance` adds:

- `TutorConversationMessageProvenance`, optionally keyed by the assistant message ID.
- `TutorMessageRetrievedKnowledgeEvidence`, owned by that provenance record.
- `TutorMessageToolExecutionEvidence`, owned by that provenance record.

No existing conversation/message is rewritten or backfilled. Old messages retain null
provenance, meaning **not captured**, rather than fabricated evidence that nothing was
retrieved. Apply the migration through the normal deployment/database-update workflow;
no database reset is needed. The migration is generated but not automatically applied
to a local or production database by this implementation.

## Conversation and completion API

`GET /api/runtime/tutors/{activationSlug}/conversations/{conversationId}` now returns
nullable `provenance` on each stored message. User messages and historical assistant
messages without captured evidence return `provenance: null`. Clients can also deserialize
older payloads that omit the field. The existing conversation list remains a lightweight
summary without loading messages.

`POST /api/runtime/tutors/{activationSlug}/ask` includes the same DTO in `provenance`.
`POST /api/runtime/tutors/{activationSlug}/ask/stream` includes it only in
`message.completed`'s `message.provenance`, after successful persistence. The existing
event names and progress payloads remain compatible; excerpts are not repeated with
token deltas or `source.retrieved` events. Failure, cancellation and save conflict do
not send completed provenance. Non-persisting benchmark calls do not fabricate it.

The API DTO allowlists knowledge excerpts and captured document names/chunk indexes/ranks,
memory boolean/count, tool name/status/success/duration/safe category, capture time, and
runtime tutor/revision/snapshot/model identifiers plus captured provider/model strings.
Each chunk also has a message-scoped `documentGroupKey` such as `document-1`, derived
from persisted document identities in retrieval-rank order. This enables accurate unique
document counts even for identical filenames, without disclosing document IDs or granting
source access. Grouping needs no live catalog lookup or additional migration.
`knowledge.retrievedChunkCount` counts only chunks retrieved **and supplied** as context.
`similarityScore` is the captured cosine similarity, accompanied by
`similarityMetric: "cosineSimilarity"`; it is not a percentage. No page/section is invented.
Revision number is taken from the pinned revision, never the current activation or a
snapshot counter. Missing revision metadata produces a nullable provenance revision
number rather than an invented value. Model display metadata uses the stored provider
model name; it is not looked up from today's catalog.

### Authorization boundary

Conversation reads require current tutor membership **and** ownership of the conversation.
Even a tutor owner cannot read another user's conversation through these endpoints.
Selected, bounded knowledge excerpts are intentionally treated as part of that authorized
message: the tutor made this snapshot's material available to members as teaching context.
This does not grant access to the full original file. Revoking membership blocks further
message/provenance reads. Source deletion does not rewrite the stored excerpt.

Documents have a stricter, separate ownership rule: `DocumentFeatures.GetDocumentById`
and status endpoints require the current user to own the document. The new provenance
DTO exposes no document/chunk IDs, source paths, file URLs or opening capabilities,
and no new source-serving endpoint is introduced. Existing legacy runtime source IDs
remain opaque metadata and confer no permission. Any future source-opening link must
go through the existing authorized document API. Tests exercise a student with tutor
membership who can read their own message excerpt while receiving 403 for the owner's
document; anonymous, other-reader and revoked-membership reads are also denied.

No prompts, hidden reasoning, session serialization, provider endpoints/credentials,
tool arguments/results or raw memory enter this DTO. Domain entities are projected to
Application response records and then explicit API contracts; no Domain entity is
serialized through these conversation/chat responses.

[answer-provenance.example.json](answer-provenance.example.json) contains the actual API
contract serialized by the mapping test, with invented IDs/content/provider metadata.

## Chat presentation

Completed assistant messages with provenance show compact `Sources · N` and
`Why this answer?` disclosures below the answer, using the existing Ant Design UI.
N counts unique historical documents using the message-scoped grouping keys. If an older
API payload lacks keys, the UI labels name-based groups without asserting a unique-document
count. The existing Show sources preference hides Sources but leaves Why this answer available.
Historical messages without provenance retain their previous rendering.

Sources groups excerpts by document, truncates each preview to 320 characters with an
accessible Show more control, and makes raw cosine similarity/rank/index available only
under Retrieval details. No page/section, calibrated relevance label or percentage is
fabricated. Excerpts render as text. Why this answer shows actual supplied knowledge/memory
counts, safe tool success/failure/timeout, captured model, pinned revision, and a shortened
snapshot identifier. It never renders arbitrary provenance properties, prompts, raw tool
errors/arguments, provider configuration or private memory.

The final SSE completion reveals controls on the existing streamed answer. The subsequent
authorized history refresh keeps its rendering key, so an open disclosure stays open.
Reopening a saved conversation uses the same stored provenance with no extra retrieval or
per-source HTTP requests. Source-opening controls are intentionally omitted: the current
provenance API grants no source-opening capability or original document identifiers.
Precise source/page highlighting is likewise deferred.

Runtime document display names are fetched in one lightweight batch for the selected,
authorized snapshot document IDs, without loading full document text. Conversation reads
eagerly load messages and both owned evidence collections with split queries; PostgreSQL
regression tests verify four repository reads for both one and twenty turns, instead of
joins multiplying knowledge excerpts by tool calls. Summaries do not load message evidence.

## Deferred work

Model-authored citations, citation verification, source-link
capabilities, raw memory disclosure, richer tool identifiers and benchmark-specific
message persistence remain deferred. This presentation step needs no additional database migration;
the provenance persistence migration above still must be applied through normal deployment.

## Changed files

Domain:

- `src/Bitcraft.Domain/Tutoring/TutorConversation.cs`
- `src/Bitcraft.Domain/Tutoring/TutorConversationMessageProvenance.cs`

Application:

- `src/Bitcraft.Application/Common/Models/TutorAgentModels.cs`
- `src/Bitcraft.Application/Common/Models/TutorRuntimeModels.cs`
- `src/Bitcraft.Application/Common/Models/TutorMessageProvenanceModels.cs`
- `src/Bitcraft.Application/Features/Tutoring/TutorConversationService.cs`
- `src/Bitcraft.Application/Features/Tutoring/TutorRuntime.cs`
- `src/Bitcraft.Application/Features/Tutoring/TutorMessageProvenanceCapture.cs`
- `src/Bitcraft.Application/Features/Tutoring/TutorMessageProvenancePresentation.cs`

API:

- `src/Bitcraft.Api/Contracts/Tutoring/TutorConversations.cs`
- `src/Bitcraft.Api/Contracts/Tutoring/TutorRuntime.cs`
- `src/Bitcraft.Api/Contracts/Tutoring/TutorMessageProvenance.cs`
- `src/Bitcraft.Api/Controllers/Tutoring/TutorConversationsController.cs`
- `src/Bitcraft.Api/Mapping/TutoringMapping.cs`

Infrastructure:

- `src/Bitcraft.Infrastructure/ExternalServices/Agents/TutorKnowledgeContextProvider.cs`
- `src/Bitcraft.Infrastructure/ExternalServices/Agents/TutorMemoryContextProvider.cs`
- `src/Bitcraft.Infrastructure/Persistence/Configurations/Tutoring/TutorConversationConfiguration.cs`
- `src/Bitcraft.Infrastructure/Persistence/Migrations/20261004145609_AddTutorMessageProvenance.cs`
- `src/Bitcraft.Infrastructure/Persistence/Migrations/20261004145609_AddTutorMessageProvenance.Designer.cs`
- `src/Bitcraft.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`

Tests:

- `tests/Bitcraft.UnitTests/Application/Tutoring/TutorMessageProvenanceTests.cs`
- `tests/Bitcraft.UnitTests/Infrastructure/Agents/TutorAgentExecutorTests.cs`
- `tests/Bitcraft.UnitTests/Infrastructure/Agents/TutorKnowledgeContextProviderTests.cs`
- `tests/Bitcraft.UnitTests/Infrastructure/Agents/TutorMemoryContextProviderTests.cs`
- `tests/Bitcraft.InfrastructureIntegrationTests/Persistence/TutorMessageProvenancePersistenceTests.cs`
- `tests/Bitcraft.ApiIntegrationTests/TutorRuntimeEndpointTests.cs`
- `tests/Bitcraft.ApiIntegrationTests/TutorRuntimeProvenanceExecutionTests.cs`
- `tests/Bitcraft.ApiIntegrationTests/ProvenanceTestChatClient.cs`
- `tests/Bitcraft.ApiIntegrationTests/BitcraftApiFactory.cs`
- `tests/Bitcraft.UnitTests/Architecture/CleanArchitectureTests.cs`
- `tests/Bitcraft.UnitTests/Api/Tutoring/TutorMessageProvenanceMappingTests.cs`
- `tests/Bitcraft.ApiIntegrationTests/TutorProvenanceApiTests.cs`

Documentation: `docs/answer-provenance.md`, `docs/answer-provenance.example.json`.

Frontend presentation:

- `tutor-studio/src/features/user/components/MessageProvenance.jsx`
- `tutor-studio/src/features/user/components/ChatMessage.jsx`
- `tutor-studio/src/features/user/components/SourceList.jsx`
- `tutor-studio/src/pages/user/AiTutorPage.jsx`
- `tutor-studio/src/styles/AiTutor.css`
- `tutor-studio/tests/MessageProvenance.test.tsx`
- `tutor-studio/tests/AiTutorProvenanceIntegration.test.tsx`
- `tutor-studio/tests/SourceList.test.tsx`
- `tutor-studio/tests/fixtures/messageProvenance.ts`

Suggested presentation captures: a completed answer with both controls collapsed;
grouped Sources with an expanded excerpt; Why this answer showing memory and Calculator;
a plain answer with no knowledge/memory/tools; the same persisted answer after reopening;
and either disclosure at a mobile viewport. Live source navigation and precise highlighting
should not be depicted as available features.

The final integration/architecture review, scenario coverage, indexes, environment limits,
and RoboTutor demonstration steps are recorded in [answer-provenance-review.md](answer-provenance-review.md).
