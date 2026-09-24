import { LockOutlined, MailOutlined } from "@ant-design/icons";
import { Alert, Button, Form, Input, Typography } from "antd";
import { useEffect } from "react";
import { useDispatch, useSelector } from "react-redux";
import { Link, useNavigate } from "react-router-dom";
import AuthShell from "../components/AuthShell.jsx";
import { clearError, registerUser } from "../store/authSlice.js";
import "../styles/Login.css";

function RegisterForm() {
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
    dispatch(clearError());
    dispatch(registerUser({ email: email.trim(), password }));
  };

  return (
    <AuthShell title="Create your account" description="Set up access to the Bitcraft Tutor Studio workspace.">
      {error ? <Alert className="auth-alert" type="error" title="Unable to create account" description={error} showIcon /> : null}

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

        <Form.Item
          label="Password"
          name="password"
          rules={[
            { required: true, message: "Create a password." },
            { min: 6, message: "Password must be at least 6 characters." },
          ]}
        >
          <Input.Password prefix={<LockOutlined />} placeholder="At least 6 characters" autoComplete="new-password" />
        </Form.Item>

        <Form.Item
          label="Confirm password"
          name="confirmPassword"
          dependencies={["password"]}
          rules={[
            { required: true, message: "Confirm your password." },
            ({ getFieldValue }) => ({
              validator(_, value) {
                return !value || getFieldValue("password") === value
                  ? Promise.resolve()
                  : Promise.reject(new Error("Passwords do not match."));
              },
            }),
          ]}
        >
          <Input.Password prefix={<LockOutlined />} placeholder="Repeat password" autoComplete="new-password" />
        </Form.Item>

        <Button type="primary" htmlType="submit" loading={loading} block>
          Create account
        </Button>
      </Form>

      <Typography.Paragraph className="auth-switch" type="secondary">
        Already have an account? <Link to="/login">Sign in</Link>
      </Typography.Paragraph>
    </AuthShell>
  );
}

export default RegisterForm;
