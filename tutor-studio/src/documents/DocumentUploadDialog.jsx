import { InboxOutlined } from "@ant-design/icons";
import { Alert, Form, Modal, Select, Spin, Typography, Upload } from "antd";
import { useEffect, useRef, useState } from "react";
import { getApiErrorDescription } from "../api/client.js";
import { acceptedDocumentExtensions, defaultMaximumUploadLabel, replaceDocumentContent, uploadDocument } from "./documentApi.js";

export default function DocumentUploadDialog({ document, folders, onClose, onSaved }) {
  const [form] = Form.useForm();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const requestRef = useRef(null);

  useEffect(() => () => requestRef.current?.abort(), []);

  const handleSubmit = async (values) => {
    const selected = values.file?.[0]?.originFileObj || values.file?.[0];
    if (!selected || busy) return;
    setBusy(true);
    setError("");
    const controller = new AbortController();
    requestRef.current = controller;
    let saved = null;
    try {
      saved = document
        ? await replaceDocumentContent(document.id, selected, controller.signal)
        : await uploadDocument(selected, values.folderId || null, controller.signal);
      if (!saved || typeof saved !== "object" || !saved.id) throw new Error("Invalid document response.");
    } catch (requestError) {
      if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "The file could not be uploaded or its text could not be extracted."));
    } finally {
      if (!controller.signal.aborted) {
        requestRef.current = null;
        setBusy(false);
      }
    }
    if (saved) onSaved(saved, Boolean(document));
  };

  return (
    <Modal
      title={document ? "Add or replace file content" : "Add file"}
      open
      onCancel={onClose}
      onOk={() => form.submit()}
      okText={document ? "Replace content" : "Upload file"}
      confirmLoading={busy}
      closable={!busy}
      mask={{ closable: !busy }}
      destroyOnHidden
    >
      {document ? <Alert type="warning" title="Replacing content removes existing knowledge chunks because they no longer match the source text." showIcon style={{ marginBottom: 16 }} /> : null}
      {error ? <Alert type="error" title={error} showIcon style={{ marginBottom: 16 }} /> : null}
      <Form form={form} layout="vertical" onFinish={handleSubmit} initialValues={{ folderId: document?.folderId }} requiredMark={false}>
        <Form.Item
          label="File"
          name="file"
          valuePropName="fileList"
          getValueFromEvent={(event) => Array.isArray(event) ? event : event?.fileList}
          rules={[{ required: true, message: "Select a supported file." }]}
          extra={`Supported: TXT, Markdown, CSV, JSON, PDF, and DOCX. Maximum size: ${defaultMaximumUploadLabel}. OCR is not supported.`}
        >
          <Upload.Dragger accept={acceptedDocumentExtensions} beforeUpload={() => false} maxCount={1} disabled={busy}>
            <p className="ant-upload-drag-icon"><InboxOutlined /></p>
            <p className="ant-upload-text">Select or drop a file here</p>
          </Upload.Dragger>
        </Form.Item>

        {!document ? (
          <Form.Item label="Folder" name="folderId">
            <Select allowClear placeholder="Root (no folder)" options={folders.map((folder) => ({ value: folder.id, label: folder.name }))} />
          </Form.Item>
        ) : null}
      </Form>
      {busy ? <Typography.Paragraph type="secondary"><Spin size="small" /> Uploading and extracting text…</Typography.Paragraph> : null}
    </Modal>
  );
}
