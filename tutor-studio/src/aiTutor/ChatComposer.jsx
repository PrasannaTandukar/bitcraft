import { CloseOutlined, PaperClipOutlined, SendOutlined, StopOutlined } from "@ant-design/icons";
import { Alert, App, Button, Input, Spin, Tooltip } from "antd";
import { useEffect, useRef, useState } from "react";
import AttachmentImage from "./AttachmentImage.jsx";
import { deleteChatImage, getChatAttachmentOptions, uploadChatImage } from "./aiTutorApi.js";
import { getTutorRuntimeError } from "./aiTutorUtils.js";

const defaultLimits = { maxFileBytes: 5242880, maxImagesPerMessage: 4, maxImagesPerRequest: 20, allowedMimeTypes: ["image/jpeg", "image/png", "image/webp"] };

export default function ChatComposer({ value, tutorName, supportsImageInput, disabled, generating, onCancel, onChange, onSend }) {
  const { message } = App.useApp();
  const [pending, setPending] = useState([]);
  const [limits, setLimits] = useState(defaultLimits);
  const [error, setError] = useState("");
  const fileInput = useRef(null);
  const pendingRef = useRef([]);
  const lifecycle = useRef(null);
  const updatePending = (next) => { pendingRef.current = next; setPending(next); };
  const uploading = pending.some((attachment) => attachment.uploading);

  useEffect(() => {
    const current = { active: true };
    lifecycle.current = current;
    const controller = new AbortController();
    getChatAttachmentOptions(controller.signal).then(setLimits).catch(() => { /* Defaults still allow server-authoritative validation. */ });
    return () => {
      current.active = false;
      controller.abort();
      pendingRef.current.filter((attachment) => !attachment.uploading).forEach((attachment) => {
        void deleteChatImage(attachment.id).catch(() => { /* Retention cleanup retries abandoned uploads. */ });
      });
    };
  }, []);

  const selectFiles = async (event) => {
    const files = Array.from(event.target.files ?? []);
    event.target.value = "";
    if (disabled || generating || !supportsImageInput) return;
    setError("");
    if (pendingRef.current.length + files.length > limits.maxImagesPerMessage) {
      setError(`Attach up to ${limits.maxImagesPerMessage} images per message.`);
      return;
    }
    const invalid = files.find((file) => !limits.allowedMimeTypes.includes(file.type) || !file.size || file.size > limits.maxFileBytes);
    if (invalid) {
      setError(!limits.allowedMimeTypes.includes(invalid.type) ? "Choose a JPEG, PNG, or WebP image." : !invalid.size ? "The selected image is empty." : `Images must be at most ${(limits.maxFileBytes / 1048576).toFixed(1)} MB.`);
      return;
    }
    const current = lifecycle.current;
    const uploads = files.map((file) => ({ id: crypto.randomUUID(), fileName: file.name, uploading: true, file }));
    updatePending([...pendingRef.current, ...uploads]);
    await Promise.all(uploads.map(async (upload) => {
      try {
        const attachment = await uploadChatImage(upload.file);
        if (!current.active || !pendingRef.current.some((item) => item.id === upload.id)) {
          await deleteChatImage(attachment.id).catch(() => {});
          return;
        }
        updatePending(pendingRef.current.map((item) => item.id === upload.id ? attachment : item));
      } catch (failure) {
        if (current.active) {
          updatePending(pendingRef.current.filter((item) => item.id !== upload.id));
          setError(getTutorRuntimeError(failure));
        }
      }
    }));
  };

  const remove = (attachment) => {
    updatePending(pendingRef.current.filter((item) => item.id !== attachment.id));
    if (!attachment.uploading) void deleteChatImage(attachment.id).catch(() => message.warning("Image removed from this message. Temporary storage will expire automatically."));
  };

  const submit = (event) => {
    event?.preventDefault();
    if (disabled || generating || uploading || (!value.trim() && !pending.length)) return;
    if (onSend(pendingRef.current, limits)) {
      // Ownership transfers to sent local messages; never delete their attachments during composer cleanup.
      updatePending([]);
      setError("");
    }
  };

  return (
    <form className="runtime-composer" onSubmit={submit}>
      {pending.length ? <div className="runtime-attachments runtime-composer-attachments">
        {pending.map((attachment) => <div className="runtime-pending-image" key={attachment.id}>
          {attachment.uploading ? <Spin size="small" aria-label={`Uploading ${attachment.fileName}`} /> : <AttachmentImage attachment={attachment} />}
          <Button size="small" type="text" aria-label={`Remove ${attachment.fileName}`} icon={<CloseOutlined />} onClick={() => remove(attachment)} disabled={disabled || generating} />
        </div>)}
      </div> : null}
      {error ? <Alert className="runtime-composer-error" type="error" title={error} showIcon closable onClose={() => setError("")} /> : null}
      <input ref={fileInput} type="file" accept="image/jpeg,image/png,image/webp" multiple hidden onChange={selectFiles} aria-label="Choose images" />
      <Tooltip title={!supportsImageInput ? "This tutor's current model does not support image input." : `Attach images (up to ${limits.maxImagesPerMessage})`}>
        <Button aria-label="Attach images" icon={<PaperClipOutlined />} disabled={disabled || generating || !supportsImageInput || pending.length >= limits.maxImagesPerMessage} onClick={() => fileInput.current?.click()} />
      </Tooltip>
      <Input.TextArea
        autoSize={{ minRows: 1, maxRows: 6 }}
        value={value}
        aria-label="Message to tutor"
        disabled={disabled || generating}
        placeholder={tutorName ? `Ask ${tutorName}…` : "Select a tutor to begin…"}
        onChange={(event) => onChange(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === "Enter" && !event.shiftKey && !event.nativeEvent.isComposing) submit(event);
        }}
        maxLength={20000}
      />
      <Tooltip title={generating ? "Cancel generation" : uploading ? "Waiting for image uploads" : "Send message"}>
        {generating
          ? <Button danger aria-label="Cancel generation" icon={<StopOutlined />} onClick={onCancel} />
          : <Button type="primary" htmlType="submit" aria-label="Send message" icon={<SendOutlined />} disabled={disabled || uploading || (!value.trim() && !pending.length)} />}
      </Tooltip>
    </form>
  );
}
