import Button from 'react-bootstrap/Button';
import Form from 'react-bootstrap/Form';
import 'bootstrap/dist/css/bootstrap.min.css';
import { useEffect, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { Link, useNavigate } from 'react-router-dom';
import { clearError, registerUser } from './store/authSlice';
import './Login.css';
import './Register.css';

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function RegisterForm() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const { loading, error, token } = useSelector((state) => state.auth);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [validationErrors, setValidationErrors] = useState({});

  useEffect(() => {
    dispatch(clearError());
  }, [dispatch]);

  useEffect(() => {
    if (token) navigate('/');
  }, [token, navigate]);

  useEffect(() => {
    document.body.classList.add('register-route');

    return () => {
      document.body.classList.remove('register-route');
    };
  }, []);

  const clearFieldError = (field) => {
    setValidationErrors((currentErrors) => {
      if (!currentErrors[field]) return currentErrors;

      const nextErrors = { ...currentErrors };
      delete nextErrors[field];
      return nextErrors;
    });
  };

  const validateForm = () => {
    const nextErrors = {};

    if (!email.trim()) {
      nextErrors.email = 'Email is required.';
    } else if (!EMAIL_PATTERN.test(email.trim())) {
      nextErrors.email = 'Please enter a valid email address.';
    }

    if (!password) {
      nextErrors.password = 'Password is required.';
    } else if (password.length < 6) {
      nextErrors.password = 'Password must be at least 6 characters.';
    }

    if (!confirmPassword) {
      nextErrors.confirmPassword = 'Please confirm your password.';
    } else if (password !== confirmPassword) {
      nextErrors.confirmPassword = 'Passwords do not match.';
    }

    return nextErrors;
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    if (loading) return;

    const nextErrors = validateForm();
    setValidationErrors(nextErrors);

    if (Object.keys(nextErrors).length > 0) return;

    dispatch(clearError());
    dispatch(
        registerUser({
          email: email.trim(),
          password,
        }),
    );
  };

  return (
    <main className="login-page register-page">
      <section className="auth-card" aria-label="Create account">
        <div className="auth-form-panel">
          <div className="auth-copy">
            <h1>Create Account</h1>
            <p>Start your 30 days trial today</p>
          </div>

          <Form className="auth-form" onSubmit={handleSubmit} noValidate>
            {error && <p className="auth-error">{error}</p>}

            <Form.Group className="auth-field" controlId="registerEmail">
              <Form.Label className="visually-hidden">Email address</Form.Label>
              <Form.Control
                type="email"
                placeholder="Email"
                value={email}
                onChange={(event) => {
                  setEmail(event.target.value);
                  clearFieldError('email');
                }}
                isInvalid={Boolean(validationErrors.email)}
              />
              <Form.Control.Feedback type="invalid">
                {validationErrors.email}
              </Form.Control.Feedback>
            </Form.Group>

            <Form.Group className="auth-field" controlId="registerPassword">
              <Form.Label className="visually-hidden">Password</Form.Label>
              <div className="password-field-wrap">
                <Form.Control
                  type={showPassword ? 'text' : 'password'}
                  placeholder="Password"
                  value={password}
                  onChange={(event) => {
                    setPassword(event.target.value);
                    clearFieldError('password');
                    clearFieldError('confirmPassword');
                  }}
                  isInvalid={Boolean(validationErrors.password)}
                />
                <button
                  className="password-toggle"
                  type="button"
                  onClick={() => setShowPassword((visible) => !visible)}
                  aria-label={showPassword ? 'Hide password' : 'Show password'}
                >
                  {showPassword ? (
                    <svg viewBox="0 0 24 24" aria-hidden="true">
                      <path d="M12 5c5.2 0 9.3 4.6 10.7 6.5a.84.84 0 0 1 0 1C21.3 14.4 17.2 19 12 19S2.7 14.4 1.3 12.5a.84.84 0 0 1 0-1C2.7 9.6 6.8 5 12 5Zm0 2C8.3 7 5.1 9.8 3.4 12c1.7 2.2 4.9 5 8.6 5s6.9-2.8 8.6-5C18.9 9.8 15.7 7 12 7Zm0 2.25A2.75 2.75 0 1 1 9.25 12 2.75 2.75 0 0 1 12 9.25Zm0 2A.75.75 0 1 0 12.75 12a.75.75 0 0 0-.75-.75Z" />
                    </svg>
                  ) : (
                    <svg viewBox="0 0 24 24" aria-hidden="true">
                      <path d="m3.7 2.3 18 18-1.4 1.4-3.1-3.1A11.9 11.9 0 0 1 12 19C6.8 19 2.7 14.4 1.3 12.5a.84.84 0 0 1 0-1 19 19 0 0 1 4.2-4.2L2.3 3.7l1.4-1.4Zm3.2 6.4A15.4 15.4 0 0 0 3.4 12c1.7 2.2 4.9 5 8.6 5a9.7 9.7 0 0 0 3.6-.7l-1.7-1.7a3.75 3.75 0 0 1-4.5-4.5L6.9 8.7ZM12 5c5.2 0 9.3 4.6 10.7 6.5a.84.84 0 0 1 0 1 18.5 18.5 0 0 1-3 3.3l-1.4-1.4A15.9 15.9 0 0 0 20.6 12C18.9 9.8 15.7 7 12 7a9.9 9.9 0 0 0-2.2.2L8.1 5.5A12 12 0 0 1 12 5Zm-.6 4.3 3.3 3.3a2.75 2.75 0 0 0-3.3-3.3Z" />
                    </svg>
                  )}
                </button>
              </div>
              <Form.Control.Feedback
                className={validationErrors.password ? 'd-block' : ''}
                type="invalid"
              >
                {validationErrors.password}
              </Form.Control.Feedback>
            </Form.Group>

            <Form.Group className="auth-field" controlId="registerConfirmPassword">
              <Form.Label className="visually-hidden">Confirm Password</Form.Label>
              <div className="password-field-wrap">
                <Form.Control
                  type={showConfirmPassword ? 'text' : 'password'}
                  placeholder="Confirm Password"
                  value={confirmPassword}
                  onChange={(event) => {
                    setConfirmPassword(event.target.value);
                    clearFieldError('confirmPassword');
                  }}
                  isInvalid={Boolean(validationErrors.confirmPassword)}
                />
                <button
                  className="password-toggle"
                  type="button"
                  onClick={() =>
                    setShowConfirmPassword((visible) => !visible)
                  }
                  aria-label={
                    showConfirmPassword
                      ? 'Hide confirm password'
                      : 'Show confirm password'
                  }
                >
                  {showConfirmPassword ? (
                    <svg viewBox="0 0 24 24" aria-hidden="true">
                      <path d="M12 5c5.2 0 9.3 4.6 10.7 6.5a.84.84 0 0 1 0 1C21.3 14.4 17.2 19 12 19S2.7 14.4 1.3 12.5a.84.84 0 0 1 0-1C2.7 9.6 6.8 5 12 5Zm0 2C8.3 7 5.1 9.8 3.4 12c1.7 2.2 4.9 5 8.6 5s6.9-2.8 8.6-5C18.9 9.8 15.7 7 12 7Zm0 2.25A2.75 2.75 0 1 1 9.25 12 2.75 2.75 0 0 1 12 9.25Zm0 2A.75.75 0 1 0 12.75 12a.75.75 0 0 0-.75-.75Z" />
                    </svg>
                  ) : (
                    <svg viewBox="0 0 24 24" aria-hidden="true">
                      <path d="m3.7 2.3 18 18-1.4 1.4-3.1-3.1A11.9 11.9 0 0 1 12 19C6.8 19 2.7 14.4 1.3 12.5a.84.84 0 0 1 0-1 19 19 0 0 1 4.2-4.2L2.3 3.7l1.4-1.4Zm3.2 6.4A15.4 15.4 0 0 0 3.4 12c1.7 2.2 4.9 5 8.6 5a9.7 9.7 0 0 0 3.6-.7l-1.7-1.7a3.75 3.75 0 0 1-4.5-4.5L6.9 8.7ZM12 5c5.2 0 9.3 4.6 10.7 6.5a.84.84 0 0 1 0 1 18.5 18.5 0 0 1-3 3.3l-1.4-1.4A15.9 15.9 0 0 0 20.6 12C18.9 9.8 15.7 7 12 7a9.9 9.9 0 0 0-2.2.2L8.1 5.5A12 12 0 0 1 12 5Zm-.6 4.3 3.3 3.3a2.75 2.75 0 0 0-3.3-3.3Z" />
                    </svg>
                  )}
                </button>
              </div>
              <Form.Control.Feedback
                className={validationErrors.confirmPassword ? 'd-block' : ''}
                type="invalid"
              >
                {validationErrors.confirmPassword}
              </Form.Control.Feedback>
            </Form.Group>

            <Button className="sign-in-button" type="submit" disabled={loading}>
              {loading ? 'Creating Account...' : 'Create Account'}
            </Button>
          </Form>

          {/*<div className="auth-divider" aria-hidden="true">*/}
          {/*  <span>Or sign up with</span>*/}
          {/*</div>*/}

          {/*<div className="social-buttons" aria-label="Social sign up options">*/}
          {/*  <button type="button" aria-label="Continue with Google">*/}
          {/*    <span className="google-mark">G</span>*/}
          {/*  </button>*/}
          {/*  <button type="button" aria-label="Continue with Apple">*/}
          {/*    <svg viewBox="0 0 24 24" aria-hidden="true">*/}
          {/*      <path d="M15.7 12.7c0-2.1 1.7-3.1 1.8-3.2-1-1.4-2.5-1.6-3-1.6-1.3-.1-2.5.8-3.2.8-.7 0-1.8-.8-3-.8-1.6 0-3 .9-3.8 2.3-1.6 2.8-.4 7 1.1 9.3.8 1.1 1.7 2.4 2.9 2.4 1.2 0 1.6-.7 3.1-.7 1.5 0 1.9.7 3.1.7 1.3 0 2.1-1.2 2.8-2.3.9-1.3 1.2-2.6 1.2-2.7-.1 0-3-.1-3-4.2ZM13.7 6.5c.6-.8 1-1.8.9-2.9-.9 0-2 .6-2.7 1.4-.6.7-1.1 1.8-.9 2.8 1 .1 2-.5 2.7-1.3Z" />*/}
          {/*    </svg>*/}
          {/*  </button>*/}
          {/*  <button type="button" aria-label="Continue with Facebook">*/}
          {/*    <span className="facebook-mark">f</span>*/}
          {/*  </button>*/}
          {/*</div>*/}

          <p className="auth-switch-copy">
            Already have an account? <Link to="/login">Sign in</Link>
          </p>
        </div>

        <aside className="visual-panel" aria-label="Decorative artwork">
          <div className="sun" />
          <div className="ridge ridge-back" />
          <div className="ridge ridge-front" />
          {/*<div className="visual-footer">*/}
          {/*  <p>Manage learning, tutors, and documents in one place.</p>*/}
          {/*  <div className="visual-controls" aria-hidden="true">*/}
          {/*    <span>←</span>*/}
          {/*    <span>→</span>*/}
          {/*  </div>*/}
          {/*</div>*/}
        </aside>
      </section>
    </main>
  );
}

export default RegisterForm;
