# Research Report: Agentic Documentation, Inbox Efficiency, and Markdown Metadata

> Provenance: AI-Generated Report by Researcher. Date: 2026-05-19.

## 1. Documentation MCP vs. Local Document Base

**Question:** Should we introduce a GitHub Copilot Documentation MCP to aggregate online info, or create a local document base for AI Ops guidelines?

*   **Option A: External Documentation MCP (Web/API Aggregation)**
    *   **Pros:** Always accesses the latest GitHub Copilot specs, zero storage overhead, eliminates local synchronization drift.
    *   **Cons:** Higher latency, prone to rate limits, susceptible to upstream URL structure changes, and increases the context window size with uncurated web noise. 
*   **Option B: Local Document Base (RAG / Vector DB)**
    *   **Pros:** Highly curated context boundaries. Since Bitcraft is an Agentic AI Tutor platform, we can **dogfood our own product** by loading AI Ops / generic agent documentation into the `pgvector` / `Documents` bounded context.
    *   **Cons:** Requires manual ingestion / scheduled sync steps to keep agentic framework guidelines fresh.
*   **Recommendation:** **Hybrid Approach.** Use the local Markdown governance docs (`.github/agentic-ai/`) and our Read-Only MCP server as the primary, high-trust baseline for project-specific rules. For broad upstream capabilities (like specific Copilot SDK quirks), connect an external Documentation MCP, but configure it as *per-request approval* to prevent contextual dilution.

## 2. Inbox System: Context-Efficiency Review

**Question:** Is there a more context-efficient solution for the Markdown-based inboxing system?

*   **Analysis of Current State:** Currently, `.github/inboxes/<agent>/inbox.md` files hold tasks. Loading these raw files consumes significant tokens because of markdown formatting and conversational cruft.
*   **Alternatives Evaluated:**
    1.  **JSON / SQLite Backed Inbox + MCP:** Storing inbox items as structured data and serving them strictly via the `mcp-server` endpoints (`list_inboxes`, `read_inbox`).
    2.  **Jira / Bitbucket Integration:** Offloading the backlog to an external service. (From our parked research task: Jira REST APIs and Bitbucket webhooks are free-tier compatible, though it violates the "no active integration config in repository" rule without an external credential setup).
*   **Recommendation:** Keep the inbox system conceptually the same, but enforce its consumption strictly through the **Bitcraft MCP Server**. Instead of agents calling `read_file` on `inbox.md`, they should use the `list_inboxes` and `read_inbox` tools. To improve density, we can migrate the markdown bullet points into standard YAML/JSON arrays inside the `.md` or `.json` files, which the MCP parses and returns cleanly without markdown token bloat.

## 3. Markdown Metadata and Enforcement (AI vs Human boundaries)

**Question:** What is the industry standard for metadata indicating human vs. AI modification boundaries?

*   **Standard Practice:** **YAML Frontmatter**. It is universally supported by static analysis tools, Copilot context parsers, and VS Code extensions.
*   **Proposed Schema:**
    ```yaml
    ---
    maintainer: human | ai | co-authored
    ai_allowed: true | false
    last_editor: m.smith | bitcraft-planner
    created_at: 2026-05-19
    ---
    ```
*   **Enforcement Mechanisms:**
    1.  **Agentic Rule:** Inform agents via `.github/copilot-instructions.md` to always parse the frontmatter. If `ai_allowed: false`, agents must refuse code generation/modification requests for that file.
    2.  **CI Validation:** Integrate a custom rule into our existing `.markdownlint.jsonc` or a `husky` pre-commit script to verify metadata presence.
    3.  **Read-Only MCP Server:** The `verify_architecture_readonly` MCP tool can be extended to quickly validate that no AI agent modified an `ai_allowed: false` file (by checking `last_editor` claims, or using `git` blame MCP to match commits).

## Open Decisions for AI Ops / Planner

1.  **Approve YAML Schema:** Decide if the 4 proposed frontmatter fields are sufficient.
2.  **Inbox Transition:** Decide if we want to change inbox syntax from Markdown bullets to YAML.
3.  **Dogfooding:** Decide if we should ingest upstream Copilot guidelines into our own PostgreSQL/pgvector instance via the Python sidecar.