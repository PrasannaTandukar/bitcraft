Explainable RAG / Answer Provenance — final integration review

The feature preserves the canonical runtime and Clean Architecture boundaries. This review
fixed a PostgreSQL capture-timestamp precision mismatch, batched runtime document-name
reads, and avoided multiplication of history rows across knowledge/tool collections.
The legacy fallback now says “chunks retrieved,” because its candidates do not prove that
all chunks survived the context budget. Its unused percentage converter was removed.
Test hosts isolate their Npgsql caches across the existing destructive *disposable-database*
migration test. Only the intentional timeout unit test uses a 20 ms tool timeout; success
tests use the production timeout. Neither production tool limits nor rate limits changed.

1. Architecture flow

   Authorized API → TutorRuntimeFeatures handlers → ITutorRuntime/TutorRuntime →
   authorized conversation and pinned revision/snapshot → ITutorAgentExecutor →
   Microsoft ChatClientAgent → knowledge/memory context providers + snapshot-bound
   functions through FunctionInvokingChatClient → provider-specific IChatClient →
   completed answer and provenance → existing conversation repository → API/SSE →
   existing AiTutorPage/ChatMessage. Interactive and benchmark execution share this path.
   Benchmark calls without durable conversations do not manufacture persisted messages.

2. Database/schema

   Migration `20261004145609_AddTutorMessageProvenance` adds three owned tables:
   `TutorConversationMessageProvenance`, `TutorMessageRetrievedKnowledgeEvidence`, and
   `TutorMessageToolExecutionEvidence`. Provenance is optional for historical messages.
   Message ownership cascades only on conversation/message deletion. Source/chunk/model
   identities in evidence are historical values without live-resource foreign keys.
   This review adds no migration and performs no update against the user's database.

   Existing indexes are sufficient for current reads: provenance's `MessageId` primary
   key; unique knowledge `(MessageId, RetrievalRank)`; tool `MessageId`; and the existing
   unique message `(ConversationId, Sequence)`. No additional index is required for this
   feature. Future cross-conversation reporting would need indexes selected from actual
   reporting queries rather than speculative source/model indexes.

3. Runtime capture points

   Knowledge evidence comes from the provider's exact bounded `ContextSources`, with
   original rank, captured cosine similarity, excerpt, document name/identity and chunk
   identity/index. Capture does not search again. Memory uses the count of items with
   content actually supplied, not all retrieved candidates. Tool evidence comes from
   actual completed invocation activities, including failed/timed-out calls; mere tool
   mentions are not invocations. Runtime identities come from the authorized execution
   request; provider-reported model metadata is captured at generation time.

   The existing completed-turn save persists message, session and evidence together.
   Cancelled/failed generation before completion and rejected concurrent saves produce
   no completed-answer evidence. UTC capture times are normalized to microsecond
   precision before persistence, preserving equality with PostgreSQL history responses.

4. API

   Stored messages, ordinary ask results and final streamed ask results carry nullable
   `provenance`. DTOs explicitly allowlist knowledge, memory summary, tool metadata and
   generation metadata. `documentGroupKey` is a message-local ordinal, not a document ID
   or source-opening capability. Identical filenames remain distinct. Scores are labelled
   cosine similarity, never calibrated percentages. Page/section metadata is not invented.

5. SSE

   Token and tool progress events remain unchanged. Excerpts/provenance are sent once in
   `message.completed.message.provenance`, after persistence succeeds. They do not appear
   on each delta. Completed controls appear without replacing streamed answer text;
   saved-history refresh preserves an open disclosure and associates completion metadata
   with its own answer, even if another tab has appended a later turn.

6. Frontend

   `MessageProvenance.jsx` is rendered by `ChatMessage.jsx`, wired through the existing
   `AiTutorPage.jsx`. Sources counts unique document groups, shows bounded plain-text
   previews with Show more, and puts raw scores/rank/index under Retrieval details.
   Why this answer shows supplied knowledge/memory counts, actual tool outcomes, captured
   model, pinned revision and shortened snapshot ID. Old messages have no invented
   explanation. The Show sources preference is respected. Keyboard disclosures and
   wrapping names use the existing Ant Design styling.

7. Security/privacy

   Conversation access requires current tutor membership and conversation ownership.
   Selected excerpts are intentionally part of the student's authorized answer, not a
   grant to open the entire document. Integration tests prove the student receives 403
   when opening the owner's source through normal document APIs, while the owner receives
   200; anonymous, another reader and revoked membership cannot read the conversation.
   Source-opening controls are omitted rather than constructing unrestricted URLs.

   Provenance contains no system prompts, hidden reasoning, provider endpoints/secrets,
   API keys, session state, raw memory, tool arguments/results or raw exception traces.
   Framework TextReasoningContent is excluded from deltas and persisted session history;
   telemetry has sensitive data disabled. Tool errors use fixed safe categories. Frontend
   metadata renders an explicit allowlist and excerpts as text, not HTML. This review's
   protection claims concern these fields/channels, not a guarantee about arbitrary text
   a generative provider may produce in its ordinary answer.

8. Performance

   Provenance creates no extra vector search, per-chunk HTTP call or per-message database
   read. Knowledge and optional memory searches remain separate corpus searches; saving
   memory retains its existing embedding/write. Document names are projected together
   as ID/name only, with no RawText. Repository history uses four split reads independent
   of turn count, avoiding a knowledge-by-tool join product. Evidence excludes vectors,
   full documents, raw tool payloads and raw memories. Knowledge is capped at Top K 10,
   each excerpt at 1,201 characters, and total supplied reference context at 6,000
   characters. Historical metadata mapping performs no document/vector lookup.

   Full conversation history remains unpaginated. Conversation-list summary mapping
   retains its pre-existing snapshot metadata read per conversation; provenance does not
   add message/evidence loads there. Paging and batching those summary reads remain
   separate follow-ups for very large histories/catalogs.

9. Scenario/test evidence

   `TutorProvenanceReviewTests.cs` uses a disposable PostgreSQL database, real migrations,
   canonical runtime/executor/Agent Framework, real Calculator, real pgvector memory
   storage, and actual message persistence. Only provider responses, knowledge retrieval
   and embeddings are controlled adapters. These fixtures are not the user's live tutor
   or Qwen model and use a small test embedding dimension rather than assuming 768.

   | Scenario | Verified |
   | --- | --- |
   | A: equal PWM / robot curves | Exact supplied motors excerpt, rank/score, one knowledge search, SSE completion, stored/history metadata and generation IDs |
   | B: 12 V × 5 Ah ÷ 18 W | Actual Calculator invocation and result 3.333… hours, success evidence, one knowledge search despite the agent's tool round trip |
   | C: memory | Saved through the real memory provider/store and supplied in a different conversation; boolean/count match, private text absent from provenance |
   | D: no RAG | Greeting skips search even with knowledge configured; genuine empty search returns no knowledge evidence |
   | E: old history | Null-provenance assistant/user messages persist and load without fabricated metadata |
   | F: Snapshot A → B | B captured through the normal active-revision endpoint; old answer's serialized provenance stays identical on A, history performs no retrieval, fresh chat uses B |

   Existing tests additionally cover five exact supplied chunks, context-budget omissions,
   multiple documents/chunks, disabled/empty memory, tools merely mentioned in text,
   failure/timeout, cancellation, conflicts, edited/deleted sources/catalog metadata,
   authorization, SSE payload frequency, API field allowlists, benchmark reuse and
   dependency boundaries. Frontend tests cover corresponding disclosures, null/zero
   evidence, streaming completion/history refresh, multi-tab association, keyboard use,
   long names, safe text and omitted source navigation. Chrome fixture checks from the
   UI implementation verified no horizontal overflow at 320 px and 375 px.

10. Validation and environment

   | Command | Final result |
   | --- | --- |
   | `dotnet test` | 633 passed: 503 unit, 35 infrastructure, 95 API; zero failed/skipped |
   | Architecture test filter | 5 passed |
   | `dotnet build bitcraft.slnx --no-restore` | Passed; zero warnings/errors |
   | `npm test -- --run` | 480 passed across 26 files |
   | `npm run lint` | Passed |
   | `npm run build` | Passed; existing bundle-size/dynamic-import warnings |
   | `git diff --check` | Passed |

   PostgreSQL opt-in tests run with `BITCRAFT_SESSION_TEST_POSTGRES` pointing only to the disposable
   container. It is removed after validation. No real Ollama/Qwen service is exercised by
   these controlled-provider tests. The running user API is SSH-forwarded and returns
   HTTP 401 for the tutor catalog without an authenticated session; the frontend returns
   200. Live RoboTutor configuration, existing conversations and current migration state
   were therefore not inspected or modified. Live model/UI verification remains pending.

11. Remaining limits

   Retrieval evidence means retrieved and supplied as context, not proof of model reliance.
   There are no explicit model-authored inline citations, full-source links, page/section
   fabrication, precise highlighting or private memory disclosure. A valid configured
   Calculator does not force every model to invoke it. Snapshot A retains configuration
   bindings; historic answer excerpts are preserved by provenance even if live documents
   or vectors change. Historical null provenance means not captured, not zero retrieval.

12. RoboTutor demo

   - Deploy/restart both current backend and frontend, applying the existing provenance
     migration through the normal database-update workflow if it is pending.
   - Sign in with appropriate tutor membership. In RoboTutor Workspace, confirm Chat
     readiness, vectorized compatible knowledge, actual catalog model selections and
     current revision/snapshot. If the intended catalog entries are configured, these
     should be `qwen3.8:27b` and `nomic-embed-text:latest` with 768 dimensions; this review
     does not claim to have checked those live entries. Enable Show sources in Chat.
   - Ensure the snapshot includes Calculator with Automatic execution and the selected
     model supports function calling. Missing immutable revision configuration requires
     a new draft, publish and activation. Knowledge-only snapshot refresh does not.
   - Start New chat and ask: “Why can equal PWM on two motors still cause a robot to
     curve?” Observe deltas, then Sources and Why this answer. Inspect motors excerpts,
     retrieval details, chunk/document counts, model/revision and Snapshot A. Reopen the
     saved conversation and confirm the same data.
   - Start New chat and ask: “A 12 V 5 Ah battery powers a robot consuming 18 W. Calculate
     ideal runtime.” Ideal arithmetic is 60 Wh / 18 W = 3⅓ hours, or 3 h 20 min. Confirm
     Calculator — successful in Why this answer. A correct number alone proves no tool
     use. If no call occurred, check the active configuration and explicitly ask the
     tutor to use Calculator; do not manufacture evidence from answer text.
   - For memory, enable Save this conversation to memory and say: “My robot has a smaller
     left wheel. Remember this calibration detail for future help.” Wait for completion.
     Start a DIFFERENT New chat, disable Save, enable Use Tutor Memory, and ask about that
     wheel setup and drift. Same-conversation memory is intentionally excluded. Confirm
     supplied boolean/count without private memory text in the explanation.
   - For no RAG, start New chat with memory off and ask “Hello.” Why this answer should
     show no tutor knowledge retrieved and there should be no Sources control. Unrelated
     substantive questions may still retrieve chunks; trust the actual evidence.
   - Open an existing pre-feature conversation: it should load normally without explanation
     controls. Creating a new zero-context message does not simulate historical null data.
   - For Snapshot B, attach an additional already-vectorized compatible document. In
     Knowledge, use Capture new snapshot when the overview reports that current knowledge
     differs from the active snapshot. Start a NEW chat to verify B. Reopen the saved PWM
     conversation: it must still display A and the same captured excerpts. Changing model,
     prompt, embedding or tool configuration instead requires a new revision.

Suggested captures: collapsed controls, grouped Sources, expanded retrieval details,
Calculator success, memory supplied vs absent, zero-knowledge explanation, old history,
the A/B snapshot comparison, and mobile disclosures. Do not show source opening as an
implemented feature.
