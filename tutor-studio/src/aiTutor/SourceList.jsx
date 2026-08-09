import Badge from "react-bootstrap/Badge";
import { sourcePercent } from "./aiTutorUtils.js";

export default function SourceList({ sources }) {
  if (!sources?.length) return null;
  return (
    <details className="runtime-sources">
      <summary>{sources.length} source{sources.length === 1 ? "" : "s"} used</summary>
      <div className="runtime-source-list">
        {sources.map((source) => (
          <div className="runtime-source" key={source.chunkId}>
            <span>
              <strong>{source.documentName || "Tutor document"}</strong>
              <small>Chunk {source.chunkIndex}</small>
            </span>
            <Badge bg="light" text="dark">Similarity {sourcePercent(source.score)}</Badge>
          </div>
        ))}
      </div>
    </details>
  );
}
