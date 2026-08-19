import { useState } from "react";
import { Alert, Button, Form, Modal } from "react-bootstrap";

export default function DocumentCreateDialog({ folders, onClose, onCreated }) {
    const [name, setName] = useState("");
    const [rawText, setRawText] = useState("");
    const [folderId, setFolderId] = useState("");
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState("");

    const handleSubmit = async (event) => {
        event.preventDefault();
        if (!name.trim()) { setError("Name is required."); return; }
        if (!rawText.trim()) { setError("Document text is required."); return; }
        setSaving(true);
        setError("");
        try {
            await onCreated(name.trim(), rawText, folderId || null);
        } catch (requestError) {
            setError(requestError?.message || "The document could not be created.");
        } finally {
            setSaving(false);
        }
    };

    return (
        <Modal show onHide={saving ? undefined : onClose} backdrop={saving ? "static" : true} keyboard={!saving} centered>
            <Form onSubmit={handleSubmit}>
                <Modal.Header closeButton={!saving}>
                    <Modal.Title>Create document</Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    {error && <Alert variant="danger">{error}</Alert>}
                    <Form.Group controlId="create-document-name" className="mb-3">
                        <Form.Label>Name</Form.Label>
                        <Form.Control
                            type="text"
                            value={name}
                            disabled={saving}
                            required
                            autoFocus
                            onChange={(event) => setName(event.target.value)}
                            placeholder="My document"
                        />
                    </Form.Group>
                    <Form.Group controlId="create-document-text" className="mb-3">
                        <Form.Label>Text</Form.Label>
                        <Form.Control
                            as="textarea"
                            rows={10}
                            value={rawText}
                            disabled={saving}
                            required
                            onChange={(event) => setRawText(event.target.value)}
                            placeholder="Paste or type the document text here…"
                        />
                        <Form.Text>
                            The raw text content of this document. This text will be used for vectorization.
                        </Form.Text>
                    </Form.Group>
                    <Form.Group controlId="create-document-folder">
                        <Form.Label>Folder</Form.Label>
                        <Form.Select value={folderId} disabled={saving} onChange={(event) => setFolderId(event.target.value)}>
                            <option value="">Root (no folder)</option>
                            {folders.map((folder) => (
                                <option key={folder.id} value={folder.id}>{folder.name}</option>
                            ))}
                        </Form.Select>
                    </Form.Group>
                </Modal.Body>
                <Modal.Footer>
                    <Button variant="outline-secondary" onClick={onClose} disabled={saving}>Cancel</Button>
                    <Button type="submit" disabled={saving || !name.trim() || !rawText.trim()}>Create</Button>
                </Modal.Footer>
            </Form>
        </Modal>
    );
}
