import { SendOutlined } from "@ant-design/icons";
import { Button, Input, Tooltip } from "antd";

export default function ChatComposer({ value, tutorName, disabled, onChange, onSend }) {
  const submit = (event) => {
    event.preventDefault();
    onSend();
  };
  return (
    <form className="runtime-composer" onSubmit={submit}>
      <Input.TextArea
        autoSize={{ minRows: 1, maxRows: 6 }}
        value={value}
        disabled={disabled}
        placeholder={tutorName ? `Ask ${tutorName}…` : "Select a tutor to begin…"}
        onChange={(event) => onChange(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === "Enter" && !event.shiftKey) {
            event.preventDefault();
            onSend();
          }
        }}
        maxLength={20000}
      />
      <Tooltip title="Send message">
        <Button type="primary" htmlType="submit" aria-label="Send message" icon={<SendOutlined />} disabled={disabled || !value.trim()} />
      </Tooltip>
    </form>
  );
}
