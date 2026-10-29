import { Alert, Button, Typography } from "antd";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import SourceList from "./SourceList.jsx";

export default function ChatMessage({ message, tutorName, showSources, onRetry, retryDisabled }) {
  if (message.error) {
    return (
      <div className="runtime-message-row user">
        <div className="runtime-message-stack">
          <div className="runtime-message user"><Typography.Text className="runtime-message-author">You</Typography.Text><p>{message.content}</p></div>
          <Alert type="error" title={message.error} action={<Button size="small" danger onClick={() => onRetry(message)} disabled={retryDisabled}>Retry</Button>} showIcon />
        </div>
      </div>
    );
  }

  return (
    <div className={`runtime-message-row ${message.role}`}>
      <article className={`runtime-message ${message.role}`}>
        <Typography.Text className="runtime-message-author">{message.role === "user" ? "You" : tutorName}</Typography.Text>
        {message.role === "assistant" ? (
          <div className="runtime-markdown">
            <ReactMarkdown remarkPlugins={[remarkGfm]}>{message.content}</ReactMarkdown>
          </div>
        ) : <p>{message.content}</p>}
        {message.role === "assistant" && showSources ? <SourceList sources={message.sources} /> : null}
      </article>
    </div>
  );
}
