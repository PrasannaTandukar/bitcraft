import { CheckCircleOutlined, MinusCircleOutlined, WarningOutlined } from "@ant-design/icons";
import { Button, Typography } from "antd";
import { useId, useState } from "react";

const excerptLimit = 320;

function ContextExcerpt({ chunk, documentName, similarityMetric }) {
  const [expanded, setExpanded] = useState(false);
  const excerptId = useId();
  const excerpt = chunk.excerpt || "";
  const hasMore = excerpt.length > excerptLimit;
  return <div className="runtime-evidence-chunk">
    <p id={excerptId} className="runtime-evidence-excerpt">
      {excerpt ? (hasMore && !expanded ? `${excerpt.slice(0, excerptLimit)}…` : excerpt) : "Excerpt not available."}
    </p>
    {hasMore ? <Button type="link" size="small" aria-expanded={expanded} aria-controls={excerptId}
      aria-label={`${expanded ? "Show less" : "Show more"} of ${documentName}, retrieval rank ${chunk.retrievalRank}`}
      onClick={() => setExpanded((value) => !value)}>{expanded ? "Show less" : "Show more"}</Button> : null}
    <details className="runtime-evidence-details">
      <summary>Retrieval details</summary>
      <span>Retrieval rank {chunk.retrievalRank} · Chunk index {chunk.chunkIndex}</span>
      {similarityMetric === "cosineSimilarity" && Number.isFinite(chunk.similarityScore)
        ? <span>Cosine similarity {chunk.similarityScore.toFixed(3)}</span> : null}
    </details>
  </div>;
}

function EvidenceStatus({ provided, children, failed = false }) {
  const Icon = failed ? WarningOutlined : provided ? CheckCircleOutlined : MinusCircleOutlined;
  return <div className={`runtime-evidence-status${failed ? " is-failed" : ""}`}>
    <Icon aria-hidden="true" /><span>{children}</span>
  </div>;
}

export default function MessageProvenance({ provenance, showSources = true }) {
  const [panel, setPanel] = useState(null);
  const disclosureId = useId();
  const chunks = provenance.knowledge?.retrievedChunks ?? [];
  const groups = new Map();
  chunks.forEach((chunk, index) => {
    // Current APIs supply a message-local key. Older payloads can still display name groups,
    // but cannot truthfully claim a unique-document count without that key.
    const key = chunk.documentGroupKey || chunk.documentName || `unknown-${index}`;
    if (!groups.has(key)) groups.set(key, { key, name: chunk.documentName || "Tutor document", chunks: [] });
    groups.get(key).chunks.push(chunk);
  });
  const hasDocumentCount = chunks.every((chunk) => Boolean(chunk.documentGroupKey));
  const chunkCount = provenance.knowledge?.retrievedChunkCount ?? chunks.length;
  const memory = provenance.memory;
  const tools = provenance.tools ?? [];
  const runtime = provenance.runtime;
  const sourcesOpen = panel === "sources" && showSources && chunks.length > 0;
  const whyOpen = panel === "why";

  return <div className="runtime-provenance">
    <div className="runtime-provenance-controls">
      {showSources && chunks.length > 0 ? <Button type="text" size="small"
        aria-expanded={sourcesOpen} aria-controls={`${disclosureId}-sources`}
        onClick={() => setPanel(sourcesOpen ? null : "sources")}>
        {hasDocumentCount ? `Sources · ${groups.size}` : "Sources"}
      </Button> : null}
      <Button type="text" size="small" aria-expanded={whyOpen} aria-controls={`${disclosureId}-why`}
        onClick={() => setPanel(whyOpen ? null : "why")}>Why this answer?</Button>
    </div>
    <section id={`${disclosureId}-sources`} hidden={!sourcesOpen} aria-label="Sources provided as context"
      className="runtime-provenance-panel">
      {sourcesOpen ? <>
        <Typography.Text strong>Sources provided as context</Typography.Text>
        <p className="runtime-provenance-note">These excerpts were retrieved and supplied to the tutor as context.</p>
        {[...groups.values()].map((group) => <section key={group.key} className="runtime-evidence-document" aria-label={group.name}>
          <h4>{group.name}</h4>
          {group.chunks.map((chunk, index) => <ContextExcerpt key={`${chunk.retrievalRank}-${index}`}
            chunk={chunk} documentName={group.name} similarityMetric={provenance.knowledge?.similarityMetric} />)}
        </section>)}
      </> : null}
    </section>
    <section id={`${disclosureId}-why`} hidden={!whyOpen} aria-label="Answer provenance" className="runtime-provenance-panel">
      {whyOpen ? <>
        <p className="runtime-provenance-note">Context and configuration supplied for this response.</p>
        <div className="runtime-provenance-summary">
          <section><h4>Knowledge</h4>
            {chunkCount > 0 ? <>
              <EvidenceStatus provided>{chunkCount} {chunkCount === 1 ? "chunk" : "chunks"} retrieved</EvidenceStatus>
              <EvidenceStatus provided>{groups.size} {hasDocumentCount
                ? (groups.size === 1 ? "document" : "documents") : (groups.size === 1 ? "document group" : "document groups")} supplied as context</EvidenceStatus>
            </> : <EvidenceStatus>No tutor knowledge retrieved</EvidenceStatus>}
          </section>
          <section><h4>Tutor memory</h4>
            <EvidenceStatus provided={memory?.contextProvided}>{memory?.contextProvided
              ? `Provided — ${memory.retrievedItemCount} ${memory.retrievedItemCount === 1 ? "memory" : "memories"} supplied` : "Not used"}</EvidenceStatus>
          </section>
          <section><h4>Tools</h4>
            {tools.length ? tools.map((tool, index) => <EvidenceStatus key={index} provided={tool.succeeded} failed={!tool.succeeded}>
              {tool.toolName} — {tool.succeeded ? "successful" : tool.status === "TimedOut" ? "timed out" : "failed"}
            </EvidenceStatus>) : <EvidenceStatus>No tools invoked</EvidenceStatus>}
          </section>
          {runtime ? <section><h4>Runtime</h4><dl className="runtime-provenance-runtime">
            <dt>Model</dt><dd>{runtime.providerModelName || "Not recorded"}</dd>
            <dt>Revision</dt><dd>{runtime.revisionNumber ?? "Not recorded"}</dd>
            <dt>Snapshot</dt><dd>{runtime.snapshotId ? `${runtime.snapshotId.slice(0, 8)}…` : "Not recorded"}</dd>
          </dl></section> : null}
        </div>
      </> : null}
    </section>
  </div>;
}
