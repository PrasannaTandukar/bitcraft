import "./App.css";
import { useSelector } from "react-redux";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import AiTutorPage from "./aiTutor/AiTutorPage.jsx";
import BenchmarkingPage from "./BenchmarkingPage.jsx";
import Dashboard from "./Dashboard.jsx";
import EmbeddingModels from "./EmbeddingModels.jsx";
import FilesPage from "./FilesPage.jsx";
import InteractionModelsPage from "./InteractionModels.jsx";
import AppLayout from "./layout/AppLayout.jsx";
import LoginForm from "./Login.jsx";
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
        <Route path="settings" element={<Settings />} />
        <Route path="ai-tutor" element={<AiTutorPage />} />
        <Route path="files" element={
          <RoleRoute allowedRoles={["Auditor", "Editor", "Admin"]}>
            <FilesPage />
          </RoleRoute>
        } />
        <Route path="tutors" element={
          <RoleRoute allowedRoles={["Auditor", "Editor", "Admin"]}>
            <TutorsPage />
          </RoleRoute>
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
        <Route path="role-management" element={
          <RoleRoute allowedRoles={["Admin"]}>
            <UsersPage />
          </RoleRoute>
        } />
        <Route path="model-catalog" element={
          <RoleRoute allowedRoles={["Admin"]}>
            <EmbeddingModels />
          </RoleRoute>
        } />
        <Route path="interaction-model" element={
          <RoleRoute allowedRoles={["Admin"]}>
            <InteractionModelsPage />
          </RoleRoute>
        } />
        <Route path="runtime-engines" element={
          <RoleRoute allowedRoles={["Admin"]}>
            <RuntimeEnginesPage />
          </RoleRoute>
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
