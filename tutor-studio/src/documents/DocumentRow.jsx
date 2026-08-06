import { Badge, Button } from "react-bootstrap";

function mimeIcon(mimeType, name) {
  if (mimeType === "application/pdf" || name?.toLowerCase().endsWith(".pdf")) return "📕";
  if (mimeType?.includes("wordprocessingml") || name?.toLowerCase().endsWith(".docx")) return "📝";
  return "📄";
}

function formatDate(value) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "Unknown date" : date.toLocaleDateString();
}

function statusLabel(document, status) {
  if (!document.hasRawText) return { variant: "warning", text: "No extracted text" };
  if (status?.unavailable) return { variant: "secondary", text: "Vectorization status unavailable" };
  if (status?.isStale) return { variant: "warning", text: "Needs re-vectorization" };
  if (status?.isVectorized) return { variant: "success", text: `Vectorized · ${status.chunkCount} chunks` };
  if (status === undefined) return { variant: "secondary", text: "Checking vectorization…" };
  return { variant: "info", text: "Ready for vectorization" };
}

export default function DocumentRow({ document, folderName, status, canManage, onPreview, onReplace, onVectorize, onDelete }) {
  const state = statusLabel(document, status);
  return (
    <div className="document-row">
      <div className="document-row-main">
        <span className="document-icon" aria-hidden="true">{mimeIcon(document.mimeType, document.name)}</span>
        <div className="document-row-copy">
          <div className="document-name">{document.name || "Unnamed document"}</div>
          <div className="document-meta">
            {document.mimeType || "Unknown type"} · {formatDate(document.lastModifiedUtc || document.createdUtc)}
            {folderName ? ` · ${folderName}` : ""}
          </div>
        </div>
      </div>
      <div className="document-row-status">
        <Badge bg={document.hasRawText ? "success" : "warning"} text={document.hasRawText ? undefined : "dark"}>
          {document.hasRawText ? `${Number(document.rawTextLength || 0).toLocaleString()} extracted characters` : "No extracted text"}
        </Badge>
        <Badge bg={state.variant} text={state.variant === "warning" || state.variant === "info" ? "dark" : undefined}>{state.text}</Badge>
      </div>
      <div className="document-row-actions">
        <Button size="sm" variant="outline-secondary" onClick={() => onPreview(document)}>Preview text</Button>
        {canManage && !document.hasRawText && (
          <Button size="sm" variant="outline-primary" onClick={() => onReplace(document)}>Add file content</Button>
        )}
        {canManage && document.hasRawText && (
          <Button size="sm" variant="outline-primary" onClick={() => onVectorize(document)}>
            {status?.isVectorized ? "Re-vectorize" : "Vectorize"}
          </Button>
        )}
        {canManage && <Button size="sm" variant="outline-danger" onClick={() => onDelete(document)}>Delete</Button>}
      </div>
    </div>
  );
}
