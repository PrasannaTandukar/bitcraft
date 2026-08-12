import Button from "react-bootstrap/Button";
import Form from "react-bootstrap/Form";

export default function ChatComposer({ value, tutorName, disabled, onChange, onSend }) {
  const submit = (event) => {
    event.preventDefault();
    onSend();
  };
  return (
    <Form className="runtime-composer" onSubmit={submit}>
      <Form.Control
        as="textarea"
        rows={2}
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
      <Button type="submit" disabled={disabled || !value.trim()}>Send</Button>
    </Form>
  );
}
