import { LockOutlined, MailOutlined } from "@ant-design/icons";
import { Alert, Button, Form, Input, Typography } from "antd";
import { useEffect } from "react";
import { useDispatch, useSelector } from "react-redux";
import { Link, useNavigate } from "react-router-dom";
import AuthShell from "./components/AuthShell.jsx";
import { clearError, loginUser } from "./store/authSlice";
import "./Login.css";

function LoginForm() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const { loading, error, token } = useSelector((state) => state.auth);

  useEffect(() => {
    dispatch(clearError());
  }, [dispatch]);

  useEffect(() => {
    if (token) navigate("/");
  }, [navigate, token]);

  const handleSubmit = ({ email, password }) => {
    dispatch(loginUser({ email: email.trim(), password }));
  };

  return (
    <AuthShell title="Welcome back" description="Sign in to manage your Tutor Studio workspace.">
      {error ? <Alert className="auth-alert" type="error" title="Unable to sign in" description={error} showIcon /> : null}

      <Form layout="vertical" size="large" onFinish={handleSubmit} requiredMark={false} autoComplete="on">
        <Form.Item
          label="Email address"
          name="email"
          rules={[
            { required: true, message: "Enter your email address." },
            { type: "email", message: "Enter a valid email address." },
          ]}
        >
          <Input prefix={<MailOutlined />} placeholder="name@company.com" autoComplete="email" />
        </Form.Item>

        <Form.Item label="Password" name="password" rules={[{ required: true, message: "Enter your password." }]}>
          <Input.Password prefix={<LockOutlined />} placeholder="Password" autoComplete="current-password" />
        </Form.Item>

        <Button type="primary" htmlType="submit" loading={loading} block>
          Sign in
        </Button>
      </Form>

      <Typography.Paragraph className="auth-switch" type="secondary">
        Need an account? <Link to="/register">Create one</Link>
      </Typography.Paragraph>
    </AuthShell>
  );
}

export default LoginForm;
