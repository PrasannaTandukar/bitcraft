import { useCallback, useEffect, useRef, useState } from "react";
import { Alert, Button, Form, Spinner } from "react-bootstrap";
import { getApiErrorDescription } from "../api/client.js";
import {
    createDocumentFolder,
    deleteDocumentFolder,
    listDocumentFolders,
    renameDocumentFolder,
} from "./documentApi.js";

export default function DocumentFolders({ folders, loading, canManage, onFoldersChanged }) {
    const [localFolders, setLocalFolders] = useState([]);
    const [localLoading, setLocalLoading] = useState(true);
    const [error, setError] = useState("");
    const [notice, setNotice] = useState("");
    const [creating, setCreating] = useState(false);
    const [newFolderName, setNewFolderName] = useState("");
    const [renamingFolder, setRenamingFolder] = useState(null);
    const [renameName, setRenameName] = useState("");
    const [deletingFolder, setDeletingFolder] = useState(null);
    const requestRef = useRef(null);

    const loadFolders = useCallback(async () => {
        requestRef.current?.abort();
        const controller = new AbortController();
        requestRef.current = controller;
        setLocalLoading(true);
        setError("");
        try {
            const values = await listDocumentFolders(controller.signal);
            if (!Array.isArray(values)) throw new Error("Invalid folders response.");
            setLocalFolders(values.filter((item) => item?.id));
        } catch (requestError) {
            if (requestError.name !== "AbortError") {
                setError(getApiErrorDescription(requestError, "Folders could not be loaded."));
            }
        } finally {
            if (!controller.signal.aborted) setLocalLoading(false);
        }
    }, []);

    useEffect(() => {
        const timer = window.setTimeout(() => loadFolders(), 0);
        return () => {
            window.clearTimeout(timer);
            requestRef.current?.abort();
        };
    }, [loadFolders]);

    // Sync with parent-provided folders when they change
    useEffect(() => {
        if (!loading && folders) {
            setLocalFolders(folders);
            setLocalLoading(false);
        }
    }, [folders, loading]);

    const handleCreate = async () => {
        if (!newFolderName.trim()) return;
        setCreating(true);
        setError("");
        try {
            const created = await createDocumentFolder(newFolderName.trim());
            setLocalFolders((current) => [created, ...current]);
            setNewFolderName("");
            setNotice("Folder created.");
            onFoldersChanged?.();
        } catch (requestError) {
            setError(getApiErrorDescription(requestError, "The folder could not be created."));
        } finally {
            setCreating(false);
        }
    };

    const handleRename = async (folderId) => {
        if (!renameName.trim()) return;
        setError("");
        try {
            await renameDocumentFolder(folderId, renameName.trim());
            setLocalFolders((current) => current.map((folder) =>
                folder.id === folderId ? { ...folder, name: renameName.trim() } : folder
            ));
            setRenamingFolder(null);
            setRenameName("");
            setNotice("Folder renamed.");
            onFoldersChanged?.();
        } catch (requestError) {
            setError(getApiErrorDescription(requestError, "The folder could not be renamed."));
        }
    };

    const handleDelete = async (folderId) => {
        if (!deletingFolder) return;
        setError("");
        try {
            await deleteDocumentFolder(folderId);
            setLocalFolders((current) => current.filter((folder) => folder.id !== folderId));
            setDeletingFolder(null);
            setNotice("Folder deleted.");
            onFoldersChanged?.();
        } catch (requestError) {
            setError(getApiErrorDescription(requestError, "The folder could not be deleted."));
        }
    };

    if (!canManage) return null;

    return (
        <section className="document-folders mb-4">
            <div className="document-folders-header">
                <h2 className="h6 mb-0">Folders</h2>
            </div>

            {error && <Alert variant="danger" dismissible onClose={() => setError("")}>{error}</Alert>}
            {notice && <Alert variant="success" dismissible onClose={() => setNotice("")}>{notice}</Alert>}

            {localLoading ? (
                <div className="document-busy-message" role="status">
                    <Spinner animation="border" size="sm" />
                    <span>Loading folders…</span>
                </div>
            ) : (
                <>
                    <div className="document-folders-list">
                        {localFolders.length === 0 && (
                            <p className="text-muted mb-0 small">No folders yet. Create one to organize your documents.</p>
                        )}
                        {localFolders.map((folder) => (
                            <div key={folder.id} className="document-folder-item">
                                {renamingFolder === folder.id ? (
                                    <Form.Group className="mb-0">
                                        <Form.Control
                                            type="text"
                                            size="sm"
                                            value={renameName}
                                            autoFocus
                                            onChange={(event) => setRenameName(event.target.value)}
                                            onKeyDown={(event) => {
                                                if (event.key === "Enter") handleRename(folder.id);
                                                if (event.key === "Escape") { setRenamingFolder(null); setRenameName(""); }
                                            }}
                                            placeholder="Folder name"
                                        />
                                        <div className="document-folder-actions mt-1">
                                            <Button size="sm" variant="outline-primary" onClick={() => handleRename(folder.id)}>Save</Button>
                                            <Button size="sm" variant="outline-secondary" onClick={() => { setRenamingFolder(null); setRenameName(""); }}>Cancel</Button>
                                        </div>
                                    </Form.Group>
                                ) : (
                                    <>
                                        <span className="document-folder-name">{folder.name}</span>
                                        <div className="document-folder-actions">
                                            <Button size="sm" variant="outline-secondary" onClick={() => { setRenamingFolder(folder.id); setRenameName(folder.name); }}>Rename</Button>
                                            <Button size="sm" variant="outline-danger" onClick={() => setDeletingFolder(folder.id)}>Delete</Button>
                                        </div>
                                    </>
                                )}
                            </div>
                        ))}
                    </div>

                    <Form.Group className="document-folders-create mt-2">
                        <Form.Label className="small">New folder</Form.Label>
                        <div className="d-flex gap-2">
                            <Form.Control
                                type="text"
                                size="sm"
                                value={newFolderName}
                                disabled={creating}
                                onChange={(event) => setNewFolderName(event.target.value)}
                                onKeyDown={(event) => { if (event.key === "Enter") handleCreate(); }}
                                placeholder="Folder name"
                            />
                            <Button size="sm" disabled={creating || !newFolderName.trim()} onClick={handleCreate}>
                                {creating ? <><Spinner size="sm" className="me-1" />Creating…</> : "Create"}
                            </Button>
                        </div>
                    </Form.Group>
                </>
            )}

            {deletingFolder && (
                <div className="document-folder-delete-confirm mt-2 p-2 border rounded bg-light">
                    <p className="mb-2 small">Delete folder <strong>{localFolders.find((f) => f.id === deletingFolder)?.name}</strong>? Documents inside will move to the root.</p>
                    <Button size="sm" variant="danger" onClick={() => handleDelete(deletingFolder)}>Delete</Button>{" "}
                    <Button size="sm" variant="outline-secondary" onClick={() => setDeletingFolder(null)}>Cancel</Button>
                </div>
            )}
        </section>
    );
}
