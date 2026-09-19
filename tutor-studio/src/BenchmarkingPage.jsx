import { Card, Tabs } from "antd";
import { useCallback, useEffect, useState } from "react";
import { useSelector } from "react-redux";
import { useNavigate, useSearchParams } from "react-router-dom";
import { getApiErrorDescription } from "./api/client.js";
import BenchmarkRunHistory from "./benchmarking/BenchmarkRunHistory.jsx";
import BenchmarkRunWizard from "./benchmarking/BenchmarkRunWizard.jsx";
import BenchmarkTemplatesPanel from "./benchmarking/BenchmarkTemplatesPanel.jsx";
import StandardBenchmarkHistory from "./benchmarking/StandardBenchmarkHistory.jsx";
import { listTutors } from "./benchmarking/benchmarkingApi.js";
import PageHeader from "./components/PageHeader.jsx";
import "./benchmarking/Benchmarking.css";

export default function BenchmarkingPage() {
  const { roles = [] } = useSelector((state) => state.auth);
  const navigate = useNavigate();
  const canManage = roles.includes("Editor") || roles.includes("Admin");
  const [searchParams, setSearchParams] = useSearchParams();
  const requestedTab = searchParams.get("tab") || (canManage ? "new" : "history");
  const tab = !canManage && requestedTab !== "history" ? "history" : requestedTab;
  const validTab = ["new", "history", "standard", "templates"].includes(tab) ? tab : canManage ? "new" : "history";
  const selectedTutorId = searchParams.get("tutorId") || searchParams.get("tutor") || "";
  const selectedRevisionId = searchParams.get("revisionId") || searchParams.get("revision") || "";
  const selectedRunId = searchParams.get("run") || "";
  const [tutors, setTutors] = useState([]);
  const [tutorsLoading, setTutorsLoading] = useState(true);
  const [tutorsError, setTutorsError] = useState("");

  useEffect(() => {
    if (requestedTab === validTab) return;
    const next = new URLSearchParams(searchParams);
    next.set("tab", validTab);
    setSearchParams(next, { replace: true });
  }, [requestedTab, searchParams, setSearchParams, validTab]);

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

  const updateHistoryLocation = (tutorId, runId = "", kind = "tutor") => {
    const next = new URLSearchParams(searchParams);
    next.set("tab", kind === "standard" ? "standard" : "history");
    if (kind !== "standard") {
      if (tutorId) next.set("tutorId", tutorId); else next.delete("tutorId");
    } else {
      next.delete("tutorId");
    }
    if (runId) next.set("run", runId); else next.delete("run");
    setSearchParams(next);
  };

  const updateNewRunSelection = useCallback((tutorId, revisionId = "") => {
    setSearchParams((current) => {
      const next = new URLSearchParams(current);
      next.set("tab", "new");
      if (tutorId) next.set("tutorId", tutorId); else next.delete("tutorId");
      if (revisionId) next.set("revisionId", revisionId); else next.delete("revisionId");
      return next;
    }, { replace: true });
  }, [setSearchParams]);

  const items = [
    canManage ? {
      key: "new",
      label: "New run",
      children: <BenchmarkRunWizard tutors={tutors} tutorsLoading={tutorsLoading} tutorsError={tutorsError} initialTutorId={selectedTutorId} initialRevisionId={selectedRevisionId} onSelectionChange={updateNewRunSelection} onConfigureTutor={(tutorId) => navigate(`/tutors/${tutorId}?tab=revisions`)} onOpenTemplates={() => setTab("templates")} onCreated={updateHistoryLocation} />,
    } : null,
    {
      key: "history",
      label: "Run history",
      children: <BenchmarkRunHistory tutors={tutors} tutorsLoading={tutorsLoading} tutorsError={tutorsError} selectedTutorId={selectedTutorId} selectedRunId={selectedRunId} onSelectTutor={(tutorId) => updateHistoryLocation(tutorId)} onOpenRun={(runId) => updateHistoryLocation(selectedTutorId, runId)} onCloseRun={() => updateHistoryLocation(selectedTutorId)} />,
    },
    {
      key: "standard",
      label: "Standard benchmarks",
      children: <StandardBenchmarkHistory selectedRunId={selectedRunId} onOpenRun={(runId) => {
        const next = new URLSearchParams(searchParams);
        next.set("tab", "standard");
        next.set("run", runId);
        setSearchParams(next);
      }} onCloseRun={() => {
        const next = new URLSearchParams(searchParams);
        next.delete("run");
        setSearchParams(next);
      }} />,
    },
    canManage ? { key: "templates", label: "Templates", children: <BenchmarkTemplatesPanel /> } : null,
  ].filter(Boolean);

  return (
    <main className="workspace-page benchmarking-page">
      <div className="workspace-page-inner">
        <PageHeader title="Benchmarking" description="Generate tutor answers, evaluate quality indicators, and review auditable results." />
        <Card className="benchmark-workspace-card" styles={{ body: { paddingTop: 0 } }}>
          <Tabs activeKey={validTab} onChange={setTab} items={items} />
        </Card>
      </div>
    </main>
  );
}
