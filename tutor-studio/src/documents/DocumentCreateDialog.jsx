import { Alert, Form, Input, Modal, Select } from "antd";
import { useState } from "react";

export default function DocumentCreateDialog({ folders, onClose, onCreated }) {
    const [name, setName] = useState("");
    const [rawText, setRawText] = useState("");
    const [folderId, setFolderId] = useState();
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState("");

    const handleSubmit = async () => {
        if (!name.trim() || !rawText.trim()) {
            setError("A name and document text are required.");
            return;
        }

        setSaving(true);
        setError("");
        try {
            await onCreated(name.trim(), rawText, folderId || null);
            onClose();
        } catch (requestError) {
            setError(requestError?.message || "The document could not be created.");
        } finally {
            setSaving(false);
        }
    };

    return (
        <Modal
            title="Create document"
            open
            onCancel={onClose}
            onOk={handleSubmit}
            okText="Create document"
            confirmLoading={saving}
            okButtonProps={{ disabled: !name.trim() || !rawText.trim() }}
            closable={!saving}
            mask={{ closable: !saving }}
        >
            {error ? <Alert type="error" title={error} showIcon style={{ marginBottom: 16 }} /> : null}
            <Form layout="vertical">
                <Form.Item label="Name" required>
                    <Input value={name} disabled={saving} autoFocus onChange={(event) => setName(event.target.value)} placeholder="My document" />
                </Form.Item>
                <Form.Item label="Text" required extra="This text is stored as the document's raw content and is available for vectorization.">
                    <Input.TextArea rows={10} value={rawText} disabled={saving} onChange={(event) => setRawText(event.target.value)} placeholder="Paste or type the document text here" />
                </Form.Item>
                <Form.Item label="Folder">
                    <Select
                        allowClear
                        value={folderId}
                        disabled={saving}
                        placeholder="Root (no folder)"
                        onChange={setFolderId}
                        options={folders.map((folder) => ({ value: folder.id, label: folder.name }))}
                    />
                </Form.Item>
            </Form>
        </Modal>
    );
}
