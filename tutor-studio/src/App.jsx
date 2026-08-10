import React, { useEffect } from 'react';
import Container from 'react-bootstrap/Container';
import Nav from 'react-bootstrap/Nav';
import Navbar from 'react-bootstrap/Navbar';
import 'bootstrap/dist/css/bootstrap.min.css';
import './App.css';
import Settings from './Settings.jsx';
import LoginForm from './Login.jsx';
import RegisterForm from './Register.jsx';
import { useDispatch, useSelector } from 'react-redux';
import { BrowserRouter, Routes, Route, NavLink, useLocation, useNavigate, Navigate } from 'react-router-dom';
import { logout } from './store/authSlice';
import UsersPage from './Users.jsx';
import FilesPage from './FilesPage.jsx';
import TutorsPage from './TutorsPage.jsx';
import TutorDetailsPage from './tutors/TutorDetailsPage.jsx';
import EmbeddingModels from './EmbeddingModels.jsx';
import InteractionModelsPage from './InteractionModels.jsx';
import RuntimeEnginesPage from './RuntimeEngines.jsx';
import BenchmarkingPage from './BenchmarkingPage.jsx';
import RoleRoute from './RoleRoute.jsx';
import Dashboard from './Dashboard.jsx';
import OllamaChat from "./OllamaChat.jsx";

function ProtectedRoute({ children }) {
  const { token } = useSelector((state) => state.auth);
  if (!token) return <Navigate to="/login" />;
  return children;
}

function AdminRoute({ children }) {
  const { token, roles } = useSelector((state) => state.auth);
  if (!token) return <Navigate to="/login" />;
  if (!roles.includes("Admin")) return <Navigate to="/" />;
  return children;
}

function AppContent() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const location = useLocation();
  const { token, user, roles } = useSelector((state) => state.auth);
  const isAdmin = roles?.includes("Admin");
  const canAccessBenchmarking = roles?.some((role) => ["Auditor", "Editor", "Admin"].includes(role));
  const canAccessTutors = roles?.some((role) => ["Auditor", "Editor", "Admin"].includes(role));
  const canAccessFiles = roles?.some((role) => ["Auditor", "Editor", "Admin"].includes(role));


  useEffect(() => {
    const isPublicAuthRoute = location.pathname === '/login' || location.pathname === '/register';
    if (!token && !isPublicAuthRoute) navigate('/login');
  }, [token, location.pathname, navigate]);

  useEffect(() => {
    if (user) console.log('User updated:', user);
  }, [user]);

  return (
    <div className="app-shell">
      <Navbar expand="lg" className="app-navbar">
        <Container className="app-navbar-container">
          <Navbar.Brand className="app-brand" onClick={() => navigate('/')}>
            Agentic Tutor
          </Navbar.Brand>
          <Navbar.Toggle aria-controls="app-navbar-nav" />
          <Navbar.Collapse id="app-navbar-nav">
            <Nav className="app-nav-links me-auto">
              {token && <>
                <Nav.Link as={NavLink} to="/" end>Dashboard</Nav.Link>
                <Nav.Link as={NavLink} to="/settings">Settings</Nav.Link>
                <Nav.Link as={NavLink} to="/ai-tutor">AI Tutor</Nav.Link>
                {canAccessBenchmarking && <Nav.Link as={NavLink} to="/benchmarking">Benchmarking</Nav.Link>}
                {canAccessFiles && <Nav.Link as={NavLink} to="/files">Files</Nav.Link>}
                {canAccessTutors && <Nav.Link as={NavLink} to="/tutors">Tutors</Nav.Link>}
                {isAdmin && <>
                  <Nav.Link as={NavLink} to="/role-management">Role Management</Nav.Link>
                  <Nav.Link as={NavLink} to="/model-catalog">Model Catalog</Nav.Link>
                  <Nav.Link as={NavLink} to="/interaction-model">Interaction models</Nav.Link>
                  <Nav.Link as={NavLink} to="/runtime-engines">Runtime engines</Nav.Link>
                </>}
              </>}
              {!token && (
                <Nav.Link as={NavLink} to="/login">Login</Nav.Link>
              )}
            </Nav>
            {token && (
              <div className="app-account">
                {user && <span className="app-user-email">{user}</span>}
                <button
                  className="app-logout-button"
                  type="button"
                  onClick={() => dispatch(logout())}
                >
                  Logout
                </button>
              </div>
            )}
          </Navbar.Collapse>
        </Container>
      </Navbar>

      <Routes>
        <Route path="/login" element={<LoginForm />} />
        <Route path="/register" element={<RegisterForm />} />
        <Route path="/" element={
          <ProtectedRoute><Dashboard /></ProtectedRoute>
        } />
        <Route path="/settings" element={
          <ProtectedRoute><Settings /></ProtectedRoute>
        } />
        <Route path="/files" element={
          <RoleRoute allowedRoles={["Auditor", "Editor", "Admin"]}><FilesPage /></RoleRoute>
        } />
        <Route path="/ai-tutor" element={
          <ProtectedRoute><OllamaChat /></ProtectedRoute>
        } />
        <Route path="/tutors" element={
          <RoleRoute allowedRoles={["Auditor", "Editor", "Admin"]}><TutorsPage /></RoleRoute>
        } />
        <Route path="/tutors/:tutorId" element={
          <RoleRoute allowedRoles={["Auditor", "Editor", "Admin"]}><TutorDetailsPage /></RoleRoute>
        } />
        <Route path="/model-catalog" element={
          <AdminRoute><EmbeddingModels /></AdminRoute>
        } />
                      <Route path="/interaction-model" element={
          <AdminRoute><InteractionModelsPage /></AdminRoute>
        } />
                      <Route path="/runtime-engines" element={
          <AdminRoute><RuntimeEnginesPage /></AdminRoute>
        } />
        <Route path="/role-management" element={
          <AdminRoute><UsersPage /></AdminRoute>
        } />
        <Route path="/benchmarking" element={
          <RoleRoute allowedRoles={["Auditor", "Editor", "Admin"]}><BenchmarkingPage /></RoleRoute>
        } />
        <Route path="/benchmark-templates" element={<Navigate to="/benchmarking?tab=templates" replace />} />
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
