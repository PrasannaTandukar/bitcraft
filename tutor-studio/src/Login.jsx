import Button from 'react-bootstrap/Button';
import Form from 'react-bootstrap/Form';
import 'bootstrap/dist/css/bootstrap.min.css';
import { useState,useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { loginUser, clearError } from './store/authSlice';
import { BrowserRouter, Routes, Route, useNavigate, Navigate } from 'react-router-dom';
//TODO make this page better looking and connect to backend
function LoginForm() {
  const dispatch = useDispatch();
  const { loading, error, user,token } = useSelector((state) => state.auth);
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const handleSubmit = (e) => {
    e.preventDefault();
    dispatch(loginUser({ email, password }));
  };
  useEffect(() => {
    if (token) navigate('/');
  }, [token]);
  //if (token!==null) return <p>Welcome, {user}!</p>;
  return (
      <Form onSubmit={handleSubmit}>
      <div style={{ height: 300, width: 300, padding: 15 }}>
        {error && (
  <p style={{ color: 'red', fontSize: '0.9rem' }}>
    {error}
  </p>
)}
      <Form.Group className="mb-3" controlId="formBasicEmail">
        <Form.Label>Email address</Form.Label>
        <Form.Control type="email"
          placeholder="Email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          required />
      </Form.Group>

      <Form.Group className="mb-3" controlId="formBasicPassword">
        <Form.Label>Password</Form.Label>
        <Form.Control type="password"
          placeholder="Password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required />
      </Form.Group>
      <Form.Group className="mb-3" controlId="formBasicCheckbox">
        <Form.Check type="checkbox" label="Keep me logged in" />
      </Form.Group>
      <Button variant="primary" type="submit">
        Submit
        </Button>
        
              </div>
    </Form>
  );
}

export default LoginForm;