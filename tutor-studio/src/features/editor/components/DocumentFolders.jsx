import { DeleteOutlined, EditOutlined, FolderFilled, FolderOpenOutlined, PlusOutlined, SaveOutlined } from "@ant-design/icons";
import { Alert, Badge, Button, Card, Empty, Flex, Input, List, Popconfirm, Space, Tag, Typography } from "antd";
import { useMemo, useState } from "react";
import { getApiErrorDescription } from "../../../api/client.js";
import { createDocumentFolder, deleteDocumentFolder, renameDocumentFolder } from "../utils/documentApi.js";

export default function DocumentFolders({
    folders,
    selectedFolderId,
    onSelectFolder,
    documentCounts = {},
    totalCount = 0,
    canManage,
    canDelete,
    onFoldersChanged,
}) {
    const [error, setError] = useState("");
    const [creating, setCreating] = useState(false);
    const [newFolderName, setNewFolderName] = useState("");
    const [editingFolderId, setEditingFolderId] = useState();
    const [editingName, setEditingName] = useState("");
    const [savingFolderId, setSavingFolderId] = useState();

    const rootCount = useMemo(() => {
        const inFolders = Object.values(documentCounts).reduce((acc, count) => acc + count, 0);
        return Math.max(0, totalCount - inFolders);
    }, [documentCounts, totalCount]);

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
            if (selectedFolderId === folderId) {
                onSelectFolder?.("all");
            }
            await refresh();
        } catch (requestError) {
            setError(getApiErrorDescription(requestError, "The folder could not be deleted."));
        } finally {
            setSavingFolderId(undefined);
        }
    };

    return (
        <Card className="document-folders-card" title="Folders & Filters" size="small" style={{ marginBottom: 16 }}>
            {error ? <Alert type="error" title={error} closable onClose={() => setError("")} showIcon style={{ marginBottom: 12 }} /> : null}

            <Flex gap={8} wrap style={{ marginBottom: 16 }}>
                <Tag.CheckableTag
                    checked={selectedFolderId === "all" || !selectedFolderId}
                    onChange={() => onSelectFolder?.("all")}
                    style={{ fontSize: 13, padding: "4px 10px" }}
                >
                    <Space size={6}>
                        <FolderOpenOutlined />
                        All Documents
                        <Badge count={totalCount} showZero overflowCount={999} style={{ backgroundColor: selectedFolderId === "all" || !selectedFolderId ? "#1677ff" : "#d9d9d9" }} />
                    </Space>
                </Tag.CheckableTag>

                <Tag.CheckableTag
                    checked={selectedFolderId === "root"}
                    onChange={() => onSelectFolder?.("root")}
                    style={{ fontSize: 13, padding: "4px 10px" }}
                >
                    <Space size={6}>
                        <FolderFilled style={{ color: "#fa8c16" }} />
                        Root / Unassigned
                        <Badge count={rootCount} showZero overflowCount={999} style={{ backgroundColor: selectedFolderId === "root" ? "#1677ff" : "#d9d9d9" }} />
                    </Space>
                </Tag.CheckableTag>

                {folders.map((folder) => {
                    const isSelected = selectedFolderId === folder.id;
                    const count = documentCounts[folder.id] ?? 0;
                    return (
                        <Tag.CheckableTag
                            key={folder.id}
                            checked={isSelected}
                            onChange={() => onSelectFolder?.(isSelected ? "all" : folder.id)}
                            style={{ fontSize: 13, padding: "4px 10px" }}
                        >
                            <Space size={6}>
                                <FolderFilled style={{ color: "#1677ff" }} />
                                {folder.name}
                                <Badge count={count} showZero overflowCount={999} style={{ backgroundColor: isSelected ? "#1677ff" : "#d9d9d9" }} />
                            </Space>
                        </Tag.CheckableTag>
                    );
                })}
            </Flex>

            {canManage && folders.length > 0 ? (
                <List
                    size="small"
                    dataSource={folders}
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
                            ) : (
                                <Space>
                                    <Typography.Text strong={selectedFolderId === folder.id}>{folder.name}</Typography.Text>
                                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>({documentCounts[folder.id] ?? 0} files)</Typography.Text>
                                </Space>
                            )}
                        </List.Item>
                    )}
                />
            ) : null}

            {canManage ? (
                <Space.Compact style={{ display: "flex", marginTop: 12 }}>
                    <Input value={newFolderName} disabled={creating} onChange={(event) => setNewFolderName(event.target.value)} onPressEnter={handleCreate} placeholder="New folder name" />
                    <Button type="primary" icon={<PlusOutlined />} loading={creating} disabled={!newFolderName.trim()} onClick={handleCreate}>Create folder</Button>
                </Space.Compact>
            ) : null}
        </Card>
    );
}
