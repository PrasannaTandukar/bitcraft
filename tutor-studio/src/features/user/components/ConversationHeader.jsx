import { MessageOutlined, SettingOutlined } from "@ant-design/icons";
import { Button, Tooltip, Typography } from "antd";

export default function ConversationHeader({ title, revisionNumber, onOpenHistory, onOpenSettings }) {
  return (
    <header className="runtime-conversation-header">
      <div className="runtime-conversation-heading">
        <Typography.Text strong className="runtime-conversation-title">{title || "New conversation"}</Typography.Text>
        {revisionNumber !== null && revisionNumber !== undefined ? <Typography.Text type="secondary">Revision {revisionNumber}</Typography.Text> : null}
      </div>
      <div className="runtime-conversation-header-actions">
        <Button className="runtime-open-history" icon={<MessageOutlined />} onClick={onOpenHistory}>Chats</Button>
        <Tooltip title="Conversation settings"><Button type="text" icon={<SettingOutlined />} aria-label="Open conversation settings" onClick={onOpenSettings} /></Tooltip>
      </div>
    </header>
  );
}
