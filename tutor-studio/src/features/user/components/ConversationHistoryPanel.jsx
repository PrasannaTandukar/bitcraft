import { MoreOutlined, PlusOutlined, SearchOutlined } from "@ant-design/icons";
import { Button, Dropdown, Empty, Input, Spin, Typography } from "antd";
import { useState } from "react";

const sections = ["Today", "Yesterday", "Previous 7 Days", "Older"];

function parseUpdatedAt(value) {
  const date = value ? new Date(value) : null;
  return date && !Number.isNaN(date.getTime()) ? date : null;
}

function startOfDay(date) {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

function conversationSection(value, now) {
  const updatedAt = parseUpdatedAt(value.updatedUtc);
  if (!updatedAt) return "Older";
  const daysAgo = Math.floor((startOfDay(now) - startOfDay(updatedAt)) / 86400000);
  if (daysAgo <= 0) return "Today";
  if (daysAgo === 1) return "Yesterday";
  return daysAgo <= 7 ? "Previous 7 Days" : "Older";
}

function formatConversationTime(value, now) {
  const updatedAt = parseUpdatedAt(value.updatedUtc);
  if (!updatedAt) return "Earlier";
  const sameDay = startOfDay(now).getTime() === startOfDay(updatedAt).getTime();
  return new Intl.DateTimeFormat(undefined, sameDay
    ? { hour: "numeric", minute: "2-digit" }
    : { month: "short", day: "numeric" }).format(updatedAt);
}

function sortByUpdatedAt(values) {
  return [...values].sort((left, right) => {
    const leftTime = parseUpdatedAt(left.updatedUtc)?.getTime() ?? 0;
    const rightTime = parseUpdatedAt(right.updatedUtc)?.getTime() ?? 0;
    return rightTime - leftTime;
  });
}

export default function ConversationHistoryPanel({ conversations, selectedConversationId, loading,
  disabled, deletingConversationId, onNewChat, onSelectConversation, onDeleteConversation, drawer = false }) {
  const [query, setQuery] = useState("");
  const now = new Date();
  const normalizedQuery = query.trim().toLocaleLowerCase();
  const filtered = sortByUpdatedAt(conversations).filter((conversation) =>
    !normalizedQuery || (conversation.title || "New conversation").toLocaleLowerCase().includes(normalizedQuery));
  const grouped = sections.map((section) => ({
    section,
    conversations: filtered.filter((conversation) => conversationSection(conversation, now) === section),
  })).filter((group) => group.conversations.length);

  return (
    <aside className={`runtime-history-panel${drawer ? " runtime-history-panel--drawer" : ""}`} aria-label="Conversation history">
      <div className="runtime-history-heading">
        <Typography.Text strong>Chats</Typography.Text>
        <Button type="primary" icon={<PlusOutlined />} onClick={onNewChat} disabled={disabled} block>New chat</Button>
      </div>
      <Input
        className="runtime-history-search"
        allowClear
        aria-label="Search conversations"
        disabled={disabled}
        placeholder="Search conversations"
        prefix={<SearchOutlined />}
        value={query}
        onChange={(event) => setQuery(event.target.value)}
      />
      <div className="runtime-history-list" aria-busy={loading}>
        {loading ? <div className="runtime-history-loading"><Spin size="small" /> <Typography.Text type="secondary">Loading chats</Typography.Text></div> : null}
        {!loading && !conversations.length ? <Empty className="runtime-history-empty" image={Empty.PRESENTED_IMAGE_SIMPLE}
          description={<><Typography.Text strong>No conversations yet</Typography.Text><br /><Typography.Text type="secondary">Start a new chat with this tutor.</Typography.Text></>} /> : null}
        {!loading && conversations.length && !filtered.length ? <Empty className="runtime-history-empty" image={Empty.PRESENTED_IMAGE_SIMPLE} description="No conversations found" /> : null}
        {!loading && grouped.map((group) => (
          <section className="runtime-history-group" key={group.section} aria-label={group.section}>
            <Typography.Text className="runtime-history-group-label">{group.section}</Typography.Text>
            {group.conversations.map((conversation) => {
              const selected = conversation.id === selectedConversationId;
              const title = conversation.title || "New conversation";
              return <div className={`runtime-history-item${selected ? " is-selected" : ""}`} key={conversation.id}>
                <button className="runtime-history-select" type="button" onClick={() => onSelectConversation(conversation.id)}
                  disabled={disabled} aria-current={selected ? "page" : undefined}>
                  <Typography.Text className="runtime-history-title" title={title}>{title}</Typography.Text>
                  <Typography.Text className="runtime-history-time" type="secondary">{formatConversationTime(conversation, now)}</Typography.Text>
                </button>
                <Dropdown
                  trigger={["click"]}
                  menu={{
                    items: [{ key: "delete", danger: true, label: "Delete conversation" }],
                    onClick: ({ key, domEvent }) => {
                      domEvent.stopPropagation();
                      if (key === "delete") onDeleteConversation(conversation.id);
                    },
                  }}
                >
                  <Button className="runtime-history-actions" type="text" size="small" icon={<MoreOutlined />}
                    aria-label={`Actions for ${title}`} disabled={disabled || deletingConversationId === conversation.id}
                    loading={deletingConversationId === conversation.id} onClick={(event) => event.stopPropagation()} />
                </Dropdown>
              </div>;
            })}
          </section>
        ))}
      </div>
    </aside>
  );
}
