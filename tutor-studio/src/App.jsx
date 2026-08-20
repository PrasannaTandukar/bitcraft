import "./App.css";
import { useSelector } from "react-redux";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import AiTutorPage from "./aiTutor/AiTutorPage.jsx";
import BenchmarkingPage from "./BenchmarkingPage.jsx";
import Dashboard from "./Dashboard.jsx";
import DocumentUploadPage from "./DocumentUploadPage.jsx";
import EmbeddingModels from "./EmbeddingModels.jsx";
import FilesPage from "./FilesPage.jsx";
import InteractionModelsPage from "./InteractionModels.jsx";
import AppLayout from "./layout/AppLayout.jsx";
import LoginForm from "./Login.jsx";
import ProfilePage from "./ProfilePage.jsx";
import RegisterForm from "./Register.jsx";
import RoleRoute from "./RoleRoute.jsx";
import RuntimeEnginesPage from "./RuntimeEngines.jsx";
import Settings from "./Settings.jsx";
import TutorsPage from "./TutorsPage.jsx";
import TutorDetailsPage from "./tutors/TutorDetailsPage.jsx";
import UsersPage from "./Users.jsx";

function ProtectedRoute({ children }) {
  const { token } = useSelector((state) => state.auth);
  if (!token) return <Navigate to="/login" replace />;
  return children;
}

function AppContent() {
  return (
    <Routes>
      <Route path="/login" element={<LoginForm />} />
      <Route path="/register" element={<RegisterForm />} />
      <Route element={<ProtectedRoute><AppLayout /></ProtectedRoute>}>
        <Route index element={<Dashboard />} />
        <Route path="chat" element={<AiTutorPage />} />
        <Route path="documents" element={<RoleRoute allowedRoles={["Auditor", "Editor", "Admin"]}><FilesPage /></RoleRoute>} />
        <Route path="documents/upload" element={<RoleRoute allowedRoles={["Editor", "Admin"]}><DocumentUploadPage /></RoleRoute>} />
        <Route path="tutors" element={<RoleRoute allowedRoles={["Auditor", "Editor", "Admin"]}><TutorsPage /></RoleRoute>} />
        <Route path="tutors/:tutorId" element={<RoleRoute allowedRoles={["Auditor", "Editor", "Admin"]}><TutorDetailsPage /></RoleRoute>} />
        <Route path="benchmarking" element={<RoleRoute allowedRoles={["Auditor", "Editor", "Admin"]}><BenchmarkingPage /></RoleRoute>} />
        <Route path="tutor-setup" element={<Navigate to="/tutors" replace />} />
        <Route path="admin/roles" element={<RoleRoute allowedRoles={["Admin"]}><UsersPage /></RoleRoute>} />
        <Route path="admin/embedding-models" element={<RoleRoute allowedRoles={["Auditor", "Editor", "Admin"]}><EmbeddingModels /></RoleRoute>} />
        <Route path="admin/ai-models" element={<RoleRoute allowedRoles={["Auditor", "Editor", "Admin"]}><InteractionModelsPage /></RoleRoute>} />
        <Route path="admin/ai-providers" element={<RoleRoute allowedRoles={["Auditor", "Editor", "Admin"]}><RuntimeEnginesPage /></RoleRoute>} />
        <Route path="profile" element={<ProfilePage />} />
        <Route path="settings" element={<Settings />} />
        <Route path="ai-tutor" element={<Navigate to="/chat" replace />} />
        <Route path="files" element={<Navigate to="/documents" replace />} />
        <Route path="role-management" element={<Navigate to="/admin/roles" replace />} />
        <Route path="model-catalog" element={<Navigate to="/admin/embedding-models" replace />} />
        <Route path="interaction-model" element={<Navigate to="/admin/ai-models" replace />} />
        <Route path="runtime-engines" element={<Navigate to="/admin/ai-providers" replace />} />
        <Route path="benchmark-templates" element={<Navigate to="/benchmarking?tab=templates" replace />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

export default function AppRouter() {
  return <BrowserRouter><AppContent /></BrowserRouter>;
}
