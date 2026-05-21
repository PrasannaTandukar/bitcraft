import React, { useEffect } from 'react';
import BaseManager from './BaseManager.jsx';
import Container from 'react-bootstrap/Container';
import Nav from 'react-bootstrap/Nav';
import Navbar from 'react-bootstrap/Navbar';
import 'bootstrap/dist/css/bootstrap.min.css';
import Settings from './Settings.jsx';
import Benchmarking from './Benchmarking.jsx';
import LoginForm from './Login.jsx';
import { useDispatch, useSelector } from 'react-redux';
import { BrowserRouter, Routes, Route, useNavigate, Navigate } from 'react-router-dom';
import { logout } from './store/authSlice';
import UsersPage from './Users.jsx';
import FilesPage from './FilesPage.jsx';
import TutorsPage from './TutorsPage.jsx';
import EmbeddingModels from './EmbeddingModels.jsx';
import InteractionModelsPage from './InteractionModels.jsx';
import RuntimeEnginesPage from './RuntimeEngines.jsx';
import BenchmarkTemplatesPage from './BenchmarkTemplatesPage.jsx';

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

function EditorRoute({ children }) {
  const { token, roles } = useSelector((state) => state.auth);
  if (!token) return <Navigate to="/login" />;
  if (roles.includes("Admin")) return <Navigate to="/model-catalog" />;  // 👈 changed
  if (!roles.includes("Editor")) return <Navigate to="/" />;
  return children;
}

function AppContent() {
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const { token, user, roles } = useSelector((state) => state.auth);
  const isAdmin = roles?.includes("Admin");
  const isEditor = roles?.includes("Editor") && !isAdmin;


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
                {isEditor && <>
                  <Nav.Link onClick={() => navigate('/')}>Files</Nav.Link>
                                  <Nav.Link onClick={() => navigate('/tutors')}>Tutors</Nav.Link>
                                  <Nav.Link onClick={() => navigate('/benchmark-templates')}>Benchmark Templates</Nav.Link>
                </>}
                {isAdmin && <>
                  <Nav.Link onClick={() => navigate('/role-management')}>Role Management</Nav.Link>
                                  <Nav.Link onClick={() => navigate('/model-catalog')}>Embedding models</Nav.Link>
                                  <Nav.Link onClick={() => navigate('/interaction-model')}>Interaction models</Nav.Link>
                                  <Nav.Link onClick={() => navigate('/runtime-engines')}>Runtime engines</Nav.Link>
                </>}
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

      <Routes>
        <Route path="/login" element={<LoginForm />} />
        <Route path="/" element={
          <EditorRoute><FilesPage /></EditorRoute>
        } />
        <Route path="/settings" element={
          <ProtectedRoute><Settings /></ProtectedRoute>
        } />
        <Route path="/tutors" element={
          <EditorRoute><TutorsPage /></EditorRoute>
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
        <Route path="/benchmark-templates" element={
  <EditorRoute><BenchmarkTemplatesPage /></EditorRoute>
} />
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
