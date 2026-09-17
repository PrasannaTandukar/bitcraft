import { ExperimentOutlined } from "@ant-design/icons";
import { Alert, Button, Space, Typography } from "antd";
import { useEffect, useRef, useState } from "react";
import { apiFetch, getApiErrorDescription } from "../api/client.js";
import { deleteChatImage, getChatAttachmentOptions, uploadChatImage } from "../aiTutor/aiTutorApi.js";

export default function ModelImageInputTest({ modelId, disabled }) {
  const input = useRef(null);
  const active = useRef(false);
  const [testing, setTesting] = useState(false);
  const [result, setResult] = useState(null);
  useEffect(() => { active.current = true; return () => { active.current = false; }; }, []);

  const testImage = async (event) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file || testing) return;
    let attachment;
    setTesting(true);
    setResult(null);
    try {
      const limits = await getChatAttachmentOptions();
      if (!limits.allowedMimeTypes.includes(file.type) || !file.size || file.size > limits.maxFileBytes) {
        setResult({ type: "error", text: `Choose a JPEG, PNG, or WebP image up to ${(limits.maxFileBytes / 1048576).toFixed(1)} MB.` });
        return;
      }
      attachment = await uploadChatImage(file);
      if (!active.current) return;
      const response = await apiFetch(`/api/admin/interaction-models/${encodeURIComponent(modelId)}/test-image`, { method: "POST", json: { attachmentId: attachment.id } });
      if (active.current) setResult({ type: "success", text: response.answer });
    } catch (error) {
      if (active.current) setResult({ type: "error", text: getApiErrorDescription(error, "Image input test failed.") });
    } finally {
      if (attachment) await deleteChatImage(attachment.id).catch(() => { /* Expiry cleanup removes abandoned diagnostics. */ });
      if (active.current) setTesting(false);
    }
  };

  return <Space orientation="vertical" style={{ marginBottom: 20 }}>
    <input ref={input} type="file" accept="image/jpeg,image/png,image/webp" hidden onChange={testImage} aria-label="Choose image to test model" />
    <Button icon={<ExperimentOutlined />} loading={testing} disabled={disabled} onClick={() => input.current?.click()}>Test image input</Button>
    <Typography.Text type="secondary">Tests the last saved model configuration. Review the answer; this test does not change the Vision switch.</Typography.Text>
    {result ? <Alert type={result.type} title={result.type === "success" ? "Image request completed" : "Image test failed"} description={result.text} showIcon /> : null}
  </Space>;
}
