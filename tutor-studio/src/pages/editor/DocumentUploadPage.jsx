import { ArrowLeftOutlined, CloudUploadOutlined, InboxOutlined, PlusOutlined } from "@ant-design/icons";
import { Alert, Button, Card, Col, Divider, Form, Input, Progress, Row, Select, Space, Spin, Typography, Upload } from "antd";
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { getApiErrorDescription } from "../../api/client.js";
import PageHeader from "../../components/PageHeader.jsx";
import { acceptedDocumentExtensions, defaultMaximumUploadLabel, listDocumentFolders, uploadDocument } from "../../features/editor/utils/documentApi.js";
import useRolePermissions from "../../hooks/useRolePermissions.js";

export default function DocumentUploadPage() {
    const navigate = useNavigate();
    const { canWrite } = useRolePermissions();
    const [form] = Form.useForm();
    const [folders, setFolders] = useState([]);
    const [foldersLoading, setFoldersLoading] = useState(true);
    const [uploading, setUploading] = useState(false);
    const [error, setError] = useState("");
    const [successDoc, setSuccessDoc] = useState(null);

    useEffect(() => {
        const controller = new AbortController();
        listDocumentFolders(controller.signal)
            .then((data) => setFolders(Array.isArray(data) ? data : []))
            .catch(() => { })
            .finally(() => setFoldersLoading(false));
        return () => controller.abort();
    }, []);

    const handleUpload = async (values) => {
        const fileList = values.fileList || [];
        const file = fileList[0]?.originFileObj || fileList[0];
        if (!file) return;

        setUploading(true);
        setError("");
        setSuccessDoc(null);

        try {
            const result = await uploadDocument(file, values.folderId || null);
            setSuccessDoc(result);
            form.resetFields();
        } catch (err) {
            setError(getApiErrorDescription(err, "Failed to upload file or extract its text content."));
        } finally {
            setUploading(false);
        }
    };

    return (
        <main className="workspace-page">
            <div className="workspace-page-inner" style={{ maxWidth: 900, margin: "0 auto" }}>
                <PageHeader
                    title="Upload document"
                    description="Upload files for server-side text extraction and vector knowledge ingestion."
                    extra={[
                        <Button key="back" icon={<ArrowLeftOutlined />} onClick={() => navigate("/documents")}>
                            Back to Documents
                        </Button>,
                    ]}
                />

                {successDoc ? (
                    <Alert
                        type="success"
                        showIcon
                        title="Upload and text extraction complete"
                        description={
                            <Space direction="vertical" style={{ width: "100%", marginTop: 8 }}>
                                <Typography.Text orientation="vertical">
                                    <strong>{successDoc.name}</strong> was uploaded and {successDoc.rawTextLength ? `${successDoc.rawTextLength.toLocaleString()} characters` : "text"} extracted.
                                </Typography.Text>
                                <Space style={{ marginTop: 8 }}>
                                    <Button type="primary" onClick={() => navigate("/documents")}>
                                        View in Documents
                                    </Button>
                                    <Button onClick={() => setSuccessDoc(null)}>Upload another file</Button>
                                </Space>
                            </Space>
                        }
                        style={{ marginBottom: 24 }}
                    />
                ) : null}

                {error ? (
                    <Alert
                        type="error"
                        title="Upload failed"
                        description={error}
                        showIcon
                        closable
                        onClose={() => setError("")}
                        style={{ marginBottom: 24 }}
                    />
                ) : null}

                <Card>
                    <Form
                        form={form}
                        layout="vertical"
                        onFinish={handleUpload}
                        requiredMark={false}
                        disabled={!canWrite || uploading}
                    >
                        <Form.Item
                            label="Select file"
                            name="fileList"
                            valuePropName="fileList"
                            getValueFromEvent={(e) => (Array.isArray(e) ? e : e?.fileList)}
                            rules={[{ required: true, message: "Please select a file to upload." }]}
                        >
                            <Upload.Dragger
                                accept={acceptedDocumentExtensions}
                                beforeUpload={() => false}
                                maxCount={1}
                                multiple={false}
                                disabled={uploading}
                                style={{ padding: "32px 16px" }}
                            >
                                <p className="ant-upload-drag-icon">
                                    <InboxOutlined style={{ color: "#4f46e5", fontSize: 48 }} />
                                </p>
                                <p className="ant-upload-text" style={{ fontSize: 16, fontWeight: 500 }}>
                                    Click or drag file to this area to upload
                                </p>
                                <p className="ant-upload-hint" style={{ color: "#667085" }}>
                                    Supported formats: <strong>TXT, Markdown, CSV, JSON, PDF, DOCX</strong>. Maximum size: <strong>{defaultMaximumUploadLabel}</strong>.
                                </p>
                            </Upload.Dragger>
                        </Form.Item>

                        <Row gutter={16}>
                            <Col xs={24} md={12}>
                                <Form.Item label="Destination folder" name="folderId" extra="Organize your documents for easier filtering.">
                                    <Select
                                        allowClear
                                        placeholder="Root / Unassigned"
                                        loading={foldersLoading}
                                        options={folders.map((f) => ({ value: f.id, label: f.name }))}
                                    />
                                </Form.Item>
                            </Col>
                        </Row>

                        <Divider />

                        <div style={{ display: "flex", gap: 16, justifyContent: "space-between", alignItems: "center" }}>
                            <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                                Extracted text will be immediately available to vectorize in the Knowledge layer.
                            </Typography.Text>
                            <Space>
                                <Button onClick={() => navigate("/documents")}>Cancel</Button>
                                <Button
                                    type="primary"
                                    htmlType="submit"
                                    icon={<CloudUploadOutlined />}
                                    loading={uploading}
                                    disabled={!canWrite}
                                >
                                    Upload & Extract Text
                                </Button>
                            </Space>
                        </div>
                    </Form>
                </Card>
            </div>
        </main>
    );
}
