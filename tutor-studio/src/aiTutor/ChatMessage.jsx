import Alert from "react-bootstrap/Alert";
import Button from "react-bootstrap/Button";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import SourceList from "./SourceList.jsx";

export default function ChatMessage({ message, tutorName, showSources, onRetry, retryDisabled }) {
  if (message.error) {
    return (
      <div className="runtime-message-row user">
        <div className="runtime-message-stack">
          <div className="runtime-message user"><span>You</span><p>{message.content}</p></div>
          <Alert variant="danger" className="runtime-message-error">
            {message.error}
            <Button size="sm" variant="outline-danger" onClick={() => onRetry(message)} disabled={retryDisabled}>Retry</Button>
          </Alert>
        </div>
      </div>
    );
  }

  return (
    <div className={`runtime-message-row ${message.role}`}>
      <div className={`runtime-message ${message.role}`}>
        <span>{message.role === "user" ? "You" : tutorName}</span>
          {message.role === "assistant" ? (
              <div className="runtime-markdown">
                  <ReactMarkdown remarkPlugins={[remarkGfm]}>
                      {message.content}
                  </ReactMarkdown>
              </div>
          ) : (
              <p>{message.content}</p>
          )}
        {message.role === "assistant" && showSources && <SourceList sources={message.sources} />}
      </div>
    </div>
  );
}
