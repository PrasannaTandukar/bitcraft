import React, { useEffect } from 'react';
import 'bootstrap/dist/css/bootstrap.min.css';
import './App.css';
import Settings from './Settings.jsx';
import LoginForm from './Login.jsx';
import RegisterForm from './Register.jsx';
import { useSelector } from 'react-redux';
import { BrowserRouter, Routes, Route, useLocation, useNavigate, Navigate } from 'react-router-dom';
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
import AiTutorPage from "./aiTutor/AiTutorPage.jsx";
import ProfilePage from './ProfilePage.jsx';
import Navigation from './Navigation.jsx';

function ProtectedRoute({ children }) {
  const { token } = useSelector((state) => state.auth);
  if (!token) return <Navigate to="/login" replace />;
  return children;
}

function AppContent() {
  const navigate = useNavigate();
  const location = useLocation();
  const { token } = useSelector((state) => state.auth);

  useEffect(() => {
    const isPublicAuthRoute = location.pathname === '/login' || location.pathname === '/register';
    if (!token && !isPublicAuthRoute) navigate('/login');
  }, [token, location.pathname, navigate]);

  return (
    <div className="app-shell">
      <Navigation />

      <Routes>
        <Route path="/login" element={<LoginForm />} />
        <Route path="/register" element={<RegisterForm />} />
        <Route path="/" element={
          <ProtectedRoute><Dashboard /></ProtectedRoute>
        } />
        <Route path="/chat" element={
          <ProtectedRoute><AiTutorPage /></ProtectedRoute>
        } />
        <Route path="/documents" element={
          <RoleRoute allowedRoles={["Auditor", "Editor", "Admin"]}><FilesPage /></RoleRoute>
        } />
        <Route path="/documents/upload" element={
          <RoleRoute allowedRoles={["Editor", "Admin"]}><FilesPage defaultView="upload" /></RoleRoute>
        } />
        <Route path="tutors/:tutorId" element={
          <RoleRoute allowedRoles={["Auditor", "Editor", "Admin"]}>
            <TutorDetailsPage />
          </RoleRoute>
        } />
        <Route path="benchmarking" element={
          <RoleRoute allowedRoles={["Auditor", "Editor", "Admin"]}>
            <BenchmarkingPage />
          </RoleRoute>
        } />
        <Route path="/benchmarking" element={
          <RoleRoute allowedRoles={["Auditor", "Editor", "Admin"]}><BenchmarkingPage /></RoleRoute>
        } />
        <Route path="/tutor-setup" element={
          <RoleRoute allowedRoles={["Editor", "Admin"]}><div className="placeholder-page"><h2 className="h4">Tutor Setup</h2><p className="text-muted">Coming soon — tutor configuration and setup surfaces.</p></div></RoleRoute>
        } />
        <Route path="/admin/roles" element={
          <AdminRoute><UsersPage /></AdminRoute>
        } />
        <Route path="/admin/embedding-models" element={
          <AdminRoute><EmbeddingModels /></AdminRoute>
        } />
        <Route path="/admin/ai-models" element={
          <AdminRoute><InteractionModelsPage /></AdminRoute>
        } />
        <Route path="/admin/ai-providers" element={
          <AdminRoute><RuntimeEnginesPage /></AdminRoute>
        } />
        <Route path="/profile" element={
          <ProtectedRoute><ProfilePage /></ProtectedRoute>
        } />
        <Route path="/settings" element={
          <ProtectedRoute><Settings /></ProtectedRoute>
        } />
        <Route path="benchmark-templates" element={
          <Navigate to="/benchmarking?tab=templates" replace />
        } />
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
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
