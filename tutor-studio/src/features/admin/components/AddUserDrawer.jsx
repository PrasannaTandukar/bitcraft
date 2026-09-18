import { Alert, Button, Drawer, Form, Input, Space } from "antd";

export default function AddUserDrawer({
  open,
  saving,
  registerError,
  onSubmit,
  onClose,
}) {
  const [form] = Form.useForm();

  const close = () => {
    if (saving) return;
    onClose();
  };

  const save = async (values) => {
    const success = await onSubmit(values);
    if (success) onClose();
  };

  return (
    <Drawer
      title="Add user"
      open={open}
      onClose={close}
      size={420}
      destroyOnHidden
      footer={
        <Space style={{ width: "100%", justifyContent: "flex-end" }}>
          <Button onClick={close} disabled={saving}>
            Cancel
          </Button>
          <Button type="primary" onClick={() => form.submit()} loading={saving}>
            Add user
          </Button>
        </Space>
      }
    >
      {registerError ? (
        <Alert
          type="error"
          title={registerError}
          showIcon
          style={{ marginBottom: 16 }}
        />
      ) : null}
      <Form form={form} layout="vertical" onFinish={save} requiredMark={false}>
        <Form.Item
          label="Email"
          name="email"
          rules={[
            {
              required: true,
              whitespace: true,
              message: "Enter an email address.",
            },
            { type: "email", message: "Enter a valid email address." },
          ]}
        >
          <Input placeholder="user@bitcraft.com" autoComplete="off" />
        </Form.Item>
        <Form.Item
          label="Password"
          name="password"
          rules={[
            { required: true, message: "Enter a password." },
            { min: 8, message: "Password must be at least 8 characters." },
            {
              pattern: /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).+$/,
              message:
                "Password needs an uppercase letter, lowercase letter, digit, and symbol.",
            },
          ]}
        >
          <Input.Password
            placeholder="Admin1234!"
            autoComplete="new-password"
          />
        </Form.Item>
      </Form>
    </Drawer>
  );
}
