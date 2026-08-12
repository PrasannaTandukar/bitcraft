import { PlusOutlined, ReloadOutlined } from "@ant-design/icons";
import { Alert, Button, Empty, Form, Input, Modal, Space, Table, Typography } from "antd";
import { useEffect, useState } from "react";
import { useSelector } from "react-redux";
import { useNavigate } from "react-router-dom";
import { getApiErrorDescription } from "./api/client.js";
import PageHeader from "./components/PageHeader.jsx";
import StatusTag from "./components/StatusTag.jsx";
import { createTutor, listTutors } from "./tutors/tutorApi.js";
import { formatDateTime, revisionStatus, tutorStatus } from "./tutors/tutorUtils.js";
import "./tutors/TutorDetails.css";

function AddTutorModal({ open, onClose, onCreated }) {
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const submit = async (values) => {
    setSaving(true);
    setError("");
    try {
      const created = await createTutor({
        name: values.name.trim(),
        description: values.description?.trim() || null,
      });
      form.resetFields();
      onCreated(created);
    } catch (requestError) {
      setError(getApiErrorDescription(requestError, "Could not create the tutor."));
    } finally {
      setSaving(false);
    }
  };

  const close = () => {
    if (saving) return;
    setError("");
    form.resetFields();
    onClose();
  };

  return (
    <Modal
      title="Create tutor"
      open={open}
      onCancel={close}
      okText="Create tutor"
      confirmLoading={saving}
      onOk={() => form.submit()}
      destroyOnHidden
    >
      {error ? <Alert type="error" title={error} showIcon style={{ marginBottom: 16 }} /> : null}
      <Form form={form} layout="vertical" onFinish={submit} requiredMark={false}>
        <Form.Item label="Name" name="name" rules={[{ required: true, whitespace: true, message: "Enter a tutor name." }]}>
          <Input autoFocus maxLength={200} showCount />
        </Form.Item>
        <Form.Item label="Description" name="description">
          <Input.TextArea rows={4} maxLength={2000} showCount placeholder="Explain this tutor's purpose and audience." />
        </Form.Item>
      </Form>
    </Modal>
  );
}

export default function TutorsPage() {
  const navigate = useNavigate();
  const { roles = [] } = useSelector((state) => state.auth);
  const canManage = roles.includes("Editor") || roles.includes("Admin");
  const [tutors, setTutors] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [showCreate, setShowCreate] = useState(false);
  const [reload, setReload] = useState(0);

  useEffect(() => {
    const controller = new AbortController();
    listTutors(controller.signal)
      .then((result) => {
        setTutors(result ?? []);
        setError("");
      })
      .catch((requestError) => {
        if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "Could not load tutors."));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [reload]);

  const refresh = () => {
    setLoading(true);
    setReload((value) => value + 1);
  };

  const columns = [
    {
      title: "Tutor",
      key: "tutor",
      sorter: (a, b) => a.name.localeCompare(b.name),
      render: (_, tutor) => (
        <div className="tutor-name-cell">
          <Typography.Text strong>{tutor.name}</Typography.Text>
          <Typography.Text type="secondary" ellipsis={{ tooltip: tutor.description || "No description provided." }}>
            {tutor.description || "No description provided."}
          </Typography.Text>
        </div>
      ),
    },
    {
      title: "Status",
      dataIndex: "status",
      width: 120,
      filters: ["Draft", "Active", "Inactive", "Disabled"].map((value) => ({ text: value, value })),
      onFilter: (value, record) => tutorStatus(record.status) === value,
      render: (value) => <StatusTag value={tutorStatus(value)} />,
    },
    {
      title: "Active revision",
      key: "revision",
      width: 190,
      render: (_, tutor) => tutor.activeRevisionVersionNumber
        ? <Space size={6}>Revision {tutor.activeRevisionVersionNumber}<StatusTag value={revisionStatus(tutor.activeRevisionStatus)} /></Space>
        : <Typography.Text type="secondary">Not activated</Typography.Text>,
    },
    {
      title: "Active prompt",
      key: "prompt",
      width: 190,
      render: (_, tutor) => tutor.activePromptVersionName
        ? `v${tutor.activePromptVersionNumber} · ${tutor.activePromptVersionName}`
        : <Typography.Text type="secondary">Not selected</Typography.Text>,
    },
    {
      title: "Updated",
      key: "updated",
      width: 180,
      sorter: (a, b) => new Date(a.modifiedUtc || a.createdUtc) - new Date(b.modifiedUtc || b.createdUtc),
      render: (_, tutor) => formatDateTime(tutor.modifiedUtc || tutor.createdUtc),
    },
    {
      title: "",
      key: "action",
      width: 100,
      fixed: "right",
      render: (_, tutor) => <Button type="link" onClick={() => navigate(`/tutors/${tutor.id}`)}>{canManage ? "Configure" : "Open"}</Button>,
    },
  ];

  return (
    <main className="workspace-page">
      <div className="workspace-page-inner">
        <PageHeader
          title="Tutors"
          description="Build versioned prompts, runtime configurations, connected knowledge, and benchmark-ready tutor revisions."
          extra={[
            <Button key="refresh" icon={<ReloadOutlined />} onClick={refresh} loading={loading}>Refresh</Button>,
            canManage ? <Button key="create" type="primary" icon={<PlusOutlined />} onClick={() => setShowCreate(true)}>Create tutor</Button> : null,
          ].filter(Boolean)}
        />

        {error ? <Alert type="error" title="Tutors could not be loaded" description={error} showIcon style={{ marginBottom: 16 }} /> : null}

        <Table
          className="workspace-table"
          rowKey="id"
          columns={columns}
          dataSource={tutors}
          loading={loading}
          pagination={{ pageSize: 10, showSizeChanger: true }}
          scroll={{ x: 950 }}
          locale={{
            emptyText: (
              <Empty description="No tutors yet">
                {canManage ? <Button type="primary" onClick={() => setShowCreate(true)}>Create your first tutor</Button> : null}
              </Empty>
            ),
          }}
          onRow={(tutor) => ({
            onDoubleClick: () => navigate(`/tutors/${tutor.id}`),
          })}
        />
      </div>

      <AddTutorModal open={showCreate} onClose={() => setShowCreate(false)} onCreated={(created) => navigate(`/tutors/${created.id}`)} />
    </main>
  );
}
