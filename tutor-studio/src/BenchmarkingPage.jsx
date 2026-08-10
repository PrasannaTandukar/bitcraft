import { useCallback, useEffect, useState } from "react";
import Container from "react-bootstrap/Container";
import Nav from "react-bootstrap/Nav";
import { useSelector } from "react-redux";
import { useNavigate, useSearchParams } from "react-router-dom";
import { getApiErrorDescription } from "./api/client.js";
import BenchmarkRunHistory from "./benchmarking/BenchmarkRunHistory.jsx";
import BenchmarkRunWizard from "./benchmarking/BenchmarkRunWizard.jsx";
import BenchmarkTemplatesPanel from "./benchmarking/BenchmarkTemplatesPanel.jsx";
import { listTutors } from "./benchmarking/benchmarkingApi.js";
import "./benchmarking/Benchmarking.css";

export default function BenchmarkingPage() {
  const { roles = [] } = useSelector((state) => state.auth);
  const navigate = useNavigate();
  const canManage = roles.includes("Editor") || roles.includes("Admin");
  const [searchParams, setSearchParams] = useSearchParams();
  const requestedTab = searchParams.get("tab") || (canManage ? "new" : "history");
  const tab = !canManage && requestedTab !== "history" ? "history" : requestedTab;
  const validTab = ["new", "history", "templates"].includes(tab) ? tab : canManage ? "new" : "history";
  const selectedTutorId = searchParams.get("tutor") || "";
  const selectedRevisionId = searchParams.get("revision") || "";
  const selectedRunId = searchParams.get("run") || "";
  const [tutors, setTutors] = useState([]);
  const [tutorsLoading, setTutorsLoading] = useState(true);
  const [tutorsError, setTutorsError] = useState("");

  useEffect(() => {
    if (requestedTab === validTab) return;
    const next = new URLSearchParams(searchParams);
    next.set("tab", validTab);
    setSearchParams(next, { replace: true });
  }, [requestedTab, validTab, searchParams, setSearchParams]);

  useEffect(() => {
    const controller = new AbortController();
    listTutors(controller.signal)
      .then((data) => {
        setTutors(data ?? []);
        setTutorsError("");
      })
      .catch((error) => {
        if (error.name !== "AbortError") setTutorsError(getApiErrorDescription(error, "Could not load tutors."));
      })
      .finally(() => {
        if (!controller.signal.aborted) setTutorsLoading(false);
      });
    return () => controller.abort();
  }, []);

  const setTab = (nextTab) => {
    const next = new URLSearchParams(searchParams);
    next.set("tab", nextTab);
    next.delete("run");
    setSearchParams(next);
  };

  const updateHistoryLocation = (tutorId, runId = "") => {
    const next = new URLSearchParams(searchParams);
    next.set("tab", "history");
    if (tutorId) next.set("tutor", tutorId); else next.delete("tutor");
    if (runId) next.set("run", runId); else next.delete("run");
    setSearchParams(next);
  };

  const updateNewRunSelection = useCallback((tutorId, revisionId = "") => {
    setSearchParams((current) => {
      const next = new URLSearchParams(current);
      next.set("tab", "new");
      if (tutorId) next.set("tutor", tutorId); else next.delete("tutor");
      if (revisionId) next.set("revision", revisionId); else next.delete("revision");
      return next;
    }, { replace: true });
  }, [setSearchParams]);

  return (
    <main className="benchmarking-page">
      <Container className="benchmarking-container">
        <header className="benchmarking-header">
          <div><span className="benchmarking-eyebrow">Tutor quality workspace</span><h1>Benchmarking</h1><p>Generate tutor answers, evaluate approximate quality indicators, and review auditable results.</p></div>
        </header>
        <Nav variant="tabs" activeKey={validTab} onSelect={(key) => key && setTab(key)} className="benchmarking-tabs" aria-label="Benchmarking views">
          {canManage && <Nav.Item><Nav.Link eventKey="new">New Run</Nav.Link></Nav.Item>}
          <Nav.Item><Nav.Link eventKey="history">Run History</Nav.Link></Nav.Item>
          {canManage && <Nav.Item><Nav.Link eventKey="templates">Templates</Nav.Link></Nav.Item>}
        </Nav>
        <section className="benchmarking-content">
          {validTab === "new" && canManage && <BenchmarkRunWizard tutors={tutors} tutorsLoading={tutorsLoading} tutorsError={tutorsError} initialTutorId={selectedTutorId} initialRevisionId={selectedRevisionId} onSelectionChange={updateNewRunSelection} onConfigureTutor={(tutorId) => navigate(`/tutors/${tutorId}?tab=revisions`)} onOpenTemplates={() => setTab("templates")} onCreated={updateHistoryLocation} />}
          {validTab === "history" && <BenchmarkRunHistory tutors={tutors} tutorsLoading={tutorsLoading} tutorsError={tutorsError} selectedTutorId={selectedTutorId} selectedRunId={selectedRunId} onSelectTutor={(tutorId) => updateHistoryLocation(tutorId)} onOpenRun={(runId) => updateHistoryLocation(selectedTutorId, runId)} onCloseRun={() => updateHistoryLocation(selectedTutorId)} />}
          {validTab === "templates" && canManage && <BenchmarkTemplatesPanel />}
        </section>
      </Container>
    </main>
  );
}
