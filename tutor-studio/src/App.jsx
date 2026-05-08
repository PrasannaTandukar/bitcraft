import React, { useEffect } from 'react';
import BaseManager from './BaseManager.jsx';
import Container from 'react-bootstrap/Container';
import Nav from 'react-bootstrap/Nav';
import Navbar from 'react-bootstrap/Navbar';
import 'bootstrap/dist/css/bootstrap.min.css';
import Settings from './Settings.jsx';
import Benchmarking from './Benchmarking.jsx';
import LoginForm from './Login.jsx';
import { Provider, useDispatch, useSelector } from 'react-redux';
import { BrowserRouter, Routes, Route, useNavigate, Navigate } from 'react-router-dom';
import store from './store';
import { logout } from './store/authSlice';
import UsersPage from './Users.jsx';
import FilesPage from './FilesPage.jsx';

// ✅ Blocks pages if not logged in
function ProtectedRoute({ children }) {
  const { token } = useSelector((state) => state.auth);
  if (!token) return <Navigate to="/login" />;
  return children;
}

function AppContent() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const { token, user } = useSelector((state) => state.auth);

useEffect(() => {
  if (!token) navigate('/login');
}, [token]);
  useEffect(() => {
  if (user) console.log('User updated:', user);
}, [user]);
  return (
    <div style={{ height: "100%", flexDirection: "column", display: "flex" }}>
      <Navbar expand="lg" className="bg-body-tertiary" style={{ height: "10vh" }}>
        <Container>
          <Navbar.Brand onClick={() => navigate('/')}>
            Agentic Tutor
          </Navbar.Brand>
          <Navbar.Toggle aria-controls="basic-navbar-nav" />
          <Navbar.Collapse id="basic-navbar-nav">
            <Nav className="me-auto">
              {token && <>
                <Nav.Link onClick={() => navigate('/settings')}>Settings</Nav.Link>
                              <Nav.Link onClick={() => navigate('/benchmarking')}>Benchmarking</Nav.Link>
                              <Nav.Link onClick={() => navigate('/role-management')}>Role Management</Nav.Link>
                <Nav.Link onClick={() => dispatch(logout())}>
                  Logout ({user})
                </Nav.Link>
              </>}
              {!token && (
                <Nav.Link onClick={() => navigate('/login')}>Login</Nav.Link>
              )}
            </Nav>
          </Navbar.Collapse>
        </Container>
      </Navbar>

      {/* ✅ All pages defined here */}
      <Routes>
        <Route path="/login" element={<LoginForm />} />
        <Route path="/" element={
          <ProtectedRoute><FilesPage /></ProtectedRoute>
        } />
        <Route path="/settings" element={
          <ProtectedRoute><Settings /></ProtectedRoute>
        } />
              <Route path="/role-management" element={
          <ProtectedRoute><UsersPage /></ProtectedRoute>
        } />
        <Route path="/benchmarking" element={
          <ProtectedRoute><Benchmarking /></ProtectedRoute>
        } />
        {/* Catch all unknown routes */}
        <Route path="*" element={<Navigate to="/" />} />
      </Routes>
    </div>
  );
}

function AppRouter() {
  return (
      <BrowserRouter>
        <AppContent />
      </BrowserRouter>
  );
}

export default AppRouter;