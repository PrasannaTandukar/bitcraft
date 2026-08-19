import { DeleteOutlined, EditOutlined, PlusOutlined, SaveOutlined } from "@ant-design/icons";
import { Alert, Button, Card, Empty, Flex, Input, List, Popconfirm, Space, Typography } from "antd";
import { useState } from "react";
import { getApiErrorDescription } from "../api/client.js";
import { createDocumentFolder, deleteDocumentFolder, renameDocumentFolder } from "./documentApi.js";

export default function DocumentFolders({ folders, canManage, canDelete, onFoldersChanged }) {
    const [error, setError] = useState("");
    const [creating, setCreating] = useState(false);
    const [newFolderName, setNewFolderName] = useState("");
    const [editingFolderId, setEditingFolderId] = useState();
    const [editingName, setEditingName] = useState("");
    const [savingFolderId, setSavingFolderId] = useState();

    if (!canManage) return null;

    const refresh = async () => {
        await onFoldersChanged?.();
    };

    const handleCreate = async () => {
        if (!newFolderName.trim()) return;
        setCreating(true);
        setError("");
        try {
            await createDocumentFolder(newFolderName.trim());
            setNewFolderName("");
            await refresh();
        } catch (requestError) {
            setError(getApiErrorDescription(requestError, "The folder could not be created."));
        } finally {
            setCreating(false);
        }
    };

    const handleRename = async (folderId) => {
        if (!editingName.trim()) return;
        setSavingFolderId(folderId);
        setError("");
        try {
            await renameDocumentFolder(folderId, editingName.trim());
            setEditingFolderId(undefined);
            setEditingName("");
            await refresh();
        } catch (requestError) {
            setError(getApiErrorDescription(requestError, "The folder could not be renamed."));
        } finally {
            setSavingFolderId(undefined);
        }
    };

    const handleDelete = async (folderId) => {
        setSavingFolderId(folderId);
        setError("");
        try {
            await deleteDocumentFolder(folderId);
            await refresh();
        } catch (requestError) {
            setError(getApiErrorDescription(requestError, "The folder could not be deleted."));
        } finally {
            setSavingFolderId(undefined);
        }
    };

    return (
        <Card className="document-folders-card" title="Folders" size="small" style={{ marginBottom: 16 }}>
            {error ? <Alert type="error" title={error} closable onClose={() => setError("")} showIcon style={{ marginBottom: 12 }} /> : null}
            <List
                dataSource={folders}
                locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No folders yet" /> }}
                renderItem={(folder) => (
                    <List.Item
                        actions={editingFolderId === folder.id ? [] : [
                            <Button key="rename" type="text" icon={<EditOutlined />} onClick={() => { setEditingFolderId(folder.id); setEditingName(folder.name); }}>Rename</Button>,
                            canDelete ? (
                                <Popconfirm key="delete" title="Delete this folder?" description="Documents inside move to the root." okText="Delete" okButtonProps={{ danger: true }} onConfirm={() => handleDelete(folder.id)}>
                                    <Button type="text" danger icon={<DeleteOutlined />} loading={savingFolderId === folder.id}>Delete</Button>
                                </Popconfirm>
                            ) : null,
                        ].filter(Boolean)}
                    >
                        {editingFolderId === folder.id ? (
                            <Flex gap={8} style={{ width: "100%" }}>
                                <Input value={editingName} autoFocus disabled={savingFolderId === folder.id} onChange={(event) => setEditingName(event.target.value)} onPressEnter={() => handleRename(folder.id)} />
                                <Button type="primary" icon={<SaveOutlined />} loading={savingFolderId === folder.id} onClick={() => handleRename(folder.id)}>Save</Button>
                                <Button onClick={() => { setEditingFolderId(undefined); setEditingName(""); }}>Cancel</Button>
                            </Flex>
                        ) : <Typography.Text>{folder.name}</Typography.Text>}
                    </List.Item>
                )}
            />
            <Space.Compact style={{ display: "flex", marginTop: 12 }}>
                <Input value={newFolderName} disabled={creating} onChange={(event) => setNewFolderName(event.target.value)} onPressEnter={handleCreate} placeholder="New folder name" />
                <Button type="primary" icon={<PlusOutlined />} loading={creating} disabled={!newFolderName.trim()} onClick={handleCreate}>Create</Button>
            </Space.Compact>
        </Card>
    );
}
