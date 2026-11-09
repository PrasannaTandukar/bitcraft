import { FolderOpenOutlined, InboxOutlined } from "@ant-design/icons";
import { Alert, Form, List, Modal, Progress, Segmented, Select, Spin, Typography, Upload } from "antd";
import { useEffect, useMemo, useRef, useState } from "react";
import { getApiErrorDescription } from "../../../api/client.js";
import { acceptedDocumentExtensions, defaultMaximumUploadLabel, MAX_BATCH_FILES, replaceDocumentContent, uploadDocument, uploadDocumentsBatch } from "../utils/documentApi.js";

export default function DocumentUploadDialog({ document, folders, onClose, onSaved, onRefresh }) {
  const [form] = Form.useForm();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [batchResults, setBatchResults] = useState(null);
  const [selectMode, setSelectMode] = useState("files");
  const [progress, setProgress] = useState(null);
  const requestRef = useRef(null);
  const watchedFileList = Form.useWatch("file", form);

  const selectedCount = watchedFileList?.length ?? 0;
  const selectedBytes = useMemo(
    () => (watchedFileList ?? []).reduce((total, entry) => total + (entry?.size ?? entry?.originFileObj?.size ?? 0), 0),
    [watchedFileList],
  );

  useEffect(() => () => requestRef.current?.abort(), []);

  const handleSubmit = async (values) => {
    const selectedFiles = (values.file || [])
      .map((entry) => entry?.originFileObj || entry)
      .filter(Boolean);
    if (!selectedFiles.length || busy) return;
    setBusy(true);
    setError("");
    setBatchResults(null);
    const controller = new AbortController();
    requestRef.current = controller;
    let saved = null;
    let batch = null;
    try {
      if (document) {
        saved = await replaceDocumentContent(document.id, selectedFiles[0], controller.signal);
        if (!saved || typeof saved !== "object" || !saved.id) throw new Error("Invalid document response.");
      } else if (selectedFiles.length === 1) {
        saved = await uploadDocument(selectedFiles[0], values.folderId || null, controller.signal);
        if (!saved || typeof saved !== "object" || !saved.id) throw new Error("Invalid document response.");
      } else {
        batch = [];
        const groupCount = Math.ceil(selectedFiles.length / MAX_BATCH_FILES);
        for (let index = 0; index < selectedFiles.length; index += MAX_BATCH_FILES) {
          const groupNumber = Math.floor(index / MAX_BATCH_FILES) + 1;
          setProgress({ uploaded: batch.length, total: selectedFiles.length, groupNumber, groupCount });
          const group = selectedFiles.slice(index, index + MAX_BATCH_FILES);
          const groupResults = await uploadDocumentsBatch(group, values.folderId || null, controller.signal);
          batch.push(...(Array.isArray(groupResults?.results) ? groupResults.results : []));
        }
      }
    } catch (requestError) {
      if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "The file could not be uploaded or its text could not be extracted."));
    } finally {
      if (!controller.signal.aborted) {
        requestRef.current = null;
        setBusy(false);
      }
    }
    if (batch) {
      setProgress(null);
      setBatchResults(batch);
      const succeeded = batch.filter((entry) => entry?.documentId).length;
      if (succeeded > 0) onRefresh?.();
    }
    if (saved) onSaved(saved, Boolean(document));
  };

  const isReplace = Boolean(document);
  const batchDone = Boolean(batchResults);
  const folderMode = !isReplace && selectMode === "folder";

  const handleSelectModeChange = (mode) => {
    setSelectMode(mode);
    form.setFieldValue("file", []);
  };

  return (
    <Modal
      title={isReplace ? "Add or replace file content" : "Add files"}
      open
      onCancel={onClose}
      onOk={batchDone ? onClose : () => form.submit()}
      okText={isReplace ? "Replace content" : batchDone ? "Close" : "Upload files"}
      confirmLoading={busy}
      closable={!busy}
      mask={{ closable: !busy }}
      destroyOnHidden
      width={640}
    >
      {isReplace ? <Alert type="warning" title="Replacing content removes existing knowledge chunks because they no longer match the source text." showIcon style={{ marginBottom: 16 }} /> : null}
      {error ? <Alert type="error" title={error} showIcon style={{ marginBottom: 16 }} /> : null}
      {batchResults ? (
        <Alert
          type={batchResults.some((entry) => !entry?.documentId) ? "warning" : "success"}
          title={`${batchResults.filter((entry) => entry?.documentId).length} of ${batchResults.length} files uploaded.`}
          showIcon
          style={{ marginBottom: 16 }}
        />
      ) : null}
      <Form form={form} layout="vertical" onFinish={handleSubmit} initialValues={{ folderId: document?.folderId }} requiredMark={false}>
        {!isReplace ? (
          <Form.Item label="Select">
            <Segmented
              value={selectMode}
              onChange={handleSelectModeChange}
              disabled={busy}
              options={[
                { value: "files", label: "Files", icon: <InboxOutlined /> },
                { value: "folder", label: "Folder", icon: <FolderOpenOutlined /> },
              ]}
            />
          </Form.Item>
        ) : null}
        <Form.Item
          label="Files"
          name="file"
          valuePropName="fileList"
          getValueFromEvent={(event) => Array.isArray(event) ? event : event?.fileList}
          rules={[{ required: true, message: "Select at least one supported file." }]}
          extra={`Supported: TXT, Markdown, CSV, JSON, PDF, and DOCX. Maximum size per file: ${defaultMaximumUploadLabel}. OCR is not supported.${isReplace ? "" : ` Multiple files are uploaded in groups of ${MAX_BATCH_FILES}. Subfolders are flattened; same-name files from different subfolders get the subfolder name prefixed.`}`}
        >
          <Upload.Dragger
            accept={acceptedDocumentExtensions}
            beforeUpload={() => false}
            multiple={!isReplace}
            directory={folderMode}
            maxCount={isReplace ? 1 : undefined}
            showUploadList={false}
            disabled={busy}
          >
            <p className="ant-upload-drag-icon">{folderMode ? <FolderOpenOutlined /> : <InboxOutlined />}</p>
            <p className="ant-upload-text">{isReplace ? "Select or drop a file here" : folderMode ? "Select or drop a folder here. All supported files inside are uploaded." : "Select or drop one or more files here"}</p>
            {!isReplace && selectedCount > 0 ? (
              <p className="ant-upload-hint">{selectedCount} file{selectedCount === 1 ? "" : "s"} selected ({(selectedBytes / (1024 * 1024)).toFixed(1)} MB)</p>
            ) : null}
          </Upload.Dragger>
        </Form.Item>

        {!isReplace ? (
          <Form.Item label="Folder" name="folderId">
            <Select allowClear placeholder="Root (no folder)" options={folders.map((folder) => ({ value: folder.id, label: folder.name }))} />
          </Form.Item>
        ) : null}
      </Form>
      {busy && progress ? (
        <div style={{ marginBottom: 16 }}>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 4 }}>
            <Spin size="small" /> Uploading group {progress.groupNumber} of {progress.groupCount} — {progress.uploaded} of {progress.total} files done…
          </Typography.Paragraph>
          <Progress percent={Math.round(((progress.uploaded + (progress.groupNumber - 1) * MAX_BATCH_FILES) / progress.total) * 100)} size="small" />
        </div>
      ) : null}
      {busy && !progress ? <Typography.Paragraph type="secondary"><Spin size="small" /> Uploading and extracting text…</Typography.Paragraph> : null}
      {batchResults ? (
        <List
          size="small"
          style={{ maxHeight: 240, overflowY: "auto" }}
          dataSource={batchResults.filter((entry) => !entry?.documentId)}
          locale={{ emptyText: "All files uploaded." }}
          renderItem={(entry) => (
            <List.Item>
              <Typography.Text type="danger">{entry?.fileName || "Unknown file"}</Typography.Text>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {entry?.errorDescription || "Failed"}
              </Typography.Text>
            </List.Item>
          )}
        />
      ) : null}
    </Modal>
  );
}
