import { Alert, Button, Spin, Tag, Typography } from "antd";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import AttachmentImage from "./AttachmentImage.jsx";
import SourceList from "./SourceList.jsx";

export default function ChatMessage({ message, tutorName, showSources, onRetry, retryDisabled }) {
  const images = message.attachments?.length ? <div className="runtime-attachments">
    {message.attachments.map((attachment) => <AttachmentImage key={attachment.id} attachment={attachment} />)}
  </div> : null;
  if (message.error) {
    return (
      <div className="runtime-message-row user">
        <div className="runtime-message-stack">
          <div className="runtime-message user"><Typography.Text className="runtime-message-author">You</Typography.Text>{images}{message.content ? <p>{message.content}</p> : null}</div>
          <Alert type="error" title={message.error} action={<Button size="small" danger onClick={() => onRetry(message)} disabled={retryDisabled}>Retry</Button>} showIcon />
        </div>
      </div>
    );
  }

  return (
    <div className={`runtime-message-row ${message.role}`}>
      <article className={`runtime-message ${message.role}`}>
        <Typography.Text className="runtime-message-author">{message.role === "user" ? "You" : tutorName}</Typography.Text>
        {images}
        {message.role === "assistant" ? (
          <div className="runtime-markdown">
            {message.content ? <ReactMarkdown remarkPlugins={[remarkGfm]}>{message.content}</ReactMarkdown>
              : message.streaming ? <div className="runtime-thinking"><Spin size="small" /> <span>Thinking…</span></div> : null}
          </div>
        ) : <p>{message.content}</p>}
        {message.role === "assistant" && message.toolActivities?.length ? <div className="runtime-tool-activity" role="status" aria-live="polite">
          {message.toolActivities.map((tool, index) => <Tag key={`${tool.toolName}-${index}`}
            icon={tool.status === "Running" ? <Spin size="small" /> : null}
            color={tool.status === "Succeeded" ? "success" : tool.status === "Running" ? "processing" : "error"}>
            {tool.toolName}: {tool.status === "Running" ? "Running…" : tool.status === "Succeeded" ? "Completed" : tool.status === "TimedOut" ? "Timed out" : "Failed"}
          </Tag>)}
        </div> : null}
        {message.role === "assistant" && showSources ? <SourceList sources={message.sources} /> : null}
      </article>
    </div>
  );
}
