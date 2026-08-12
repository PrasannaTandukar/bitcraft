import { useEffect, useState } from "react";
import Alert from "react-bootstrap/Alert";
import Button from "react-bootstrap/Button";
import Container from "react-bootstrap/Container";
import Nav from "react-bootstrap/Nav";
import Spinner from "react-bootstrap/Spinner";
import { useSelector } from "react-redux";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import { getApiErrorDescription } from "../api/client.js";
import PromptVersionsPanel from "./PromptVersionsPanel.jsx";
import TutorKnowledgePanel from "./TutorKnowledgePanel.jsx";
import TutorOverviewPanel from "./TutorOverviewPanel.jsx";
import TutorRevisionsPanel from "./TutorRevisionsPanel.jsx";
import {
  getTutor,
  listEmbeddingModelCatalog,
  listInteractionModelCatalog,
  listPromptVersions,
  listTutorRevisions,
  listTutorSnapshots,
} from "./tutorApi.js";
import "./TutorDetails.css";

const VALID_TABS = ["overview", "prompts", "revisions", "knowledge"];

export default function TutorDetailsPage() {
  const { tutorId } = useParams();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { roles = [] } = useSelector((state) => state.auth);
  const canManage = roles.includes("Editor") || roles.includes("Admin");
  const requestedTab = searchParams.get("tab") || "overview";
  const tab = VALID_TABS.includes(requestedTab) ? requestedTab : "overview";
  const [tutor, setTutor] = useState(null);
  const [prompts, setPrompts] = useState([]);
  const [revisions, setRevisions] = useState([]);
  const [snapshots, setSnapshots] = useState([]);
  const [interactionModels, setInteractionModels] = useState([]);
  const [embeddingModels, setEmbeddingModels] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [catalogError, setCatalogError] = useState("");
  const [reload, setReload] = useState(0);

  useEffect(() => {
    if (requestedTab === tab) return;
    const next = new URLSearchParams(searchParams);
    next.set("tab", tab);
    setSearchParams(next, { replace: true });
  }, [requestedTab, tab, searchParams, setSearchParams]);

  useEffect(() => {
    const controller = new AbortController();
    Promise.all([
      getTutor(tutorId, controller.signal),
      listPromptVersions(tutorId, controller.signal),
      listTutorRevisions(tutorId, controller.signal),
      listTutorSnapshots(tutorId, controller.signal),
    ])
      .then(([loadedTutor, loadedPrompts, loadedRevisions, loadedSnapshots]) => {
        setTutor(loadedTutor);
        setPrompts(loadedPrompts ?? []);
        setRevisions(loadedRevisions ?? []);
        setSnapshots(loadedSnapshots ?? []);
        setError("");
      })
      .catch((requestError) => {
        if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "Could not load the tutor workspace."));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [tutorId, reload]);

  useEffect(() => {
    if (!canManage) return undefined;
    const controller = new AbortController();
    Promise.all([
      listInteractionModelCatalog(tutorId, controller.signal),
      listEmbeddingModelCatalog(tutorId, controller.signal),
    ])
      .then(([loadedInteractionModels, loadedEmbeddingModels]) => {
        setInteractionModels(loadedInteractionModels ?? []);
        setEmbeddingModels(loadedEmbeddingModels ?? []);
        setCatalogError("");
      })
      .catch((requestError) => {
        if (requestError.name !== "AbortError") setCatalogError(getApiErrorDescription(requestError, "Could not load the model catalog."));
      });
    return () => controller.abort();
  }, [tutorId, canManage, reload]);

  const setTab = (nextTab) => {
    const next = new URLSearchParams(searchParams);
    next.set("tab", nextTab);
    setSearchParams(next);
  };

  const refresh = () => {
    setLoading(true);
    setReload((value) => value + 1);
  };

  if (loading && !tutor) return <div className="tutor-loading"><Spinner /><span>Loading tutor workspace…</span></div>;

  return (
    <main className="tutor-workspace-page">
      <Container className="tutor-workspace-container">
        <Button variant="link" className="tutor-back-link" onClick={() => navigate("/tutors")}>← Back to tutors</Button>
        {error && <Alert variant="danger">{error}<div className="mt-2"><Button size="sm" variant="outline-danger" onClick={refresh}>Retry</Button></div></Alert>}
        {tutor && <>
          <header className="tutor-page-header">
            <div>
              <span className="tutor-eyebrow">Tutor workspace</span>
              <h1>{tutor.name}</h1>
              <p>{tutor.description || "Configure this tutor's prompt, models, knowledge, and runtime revision."}</p>
            </div>
            <div className="tutor-header-stat"><strong>{revisions.length}</strong><span>Revisions</span></div>
            <div className="tutor-header-stat"><strong>{snapshots.length}</strong><span>Snapshots</span></div>
            {tutor.activeRevisionId && <div className="d-flex flex-column gap-2">
              <Button size="sm" onClick={() => navigate(`/ai-tutor?tutorId=${tutor.id}`)}>Chat with Tutor</Button>
              {canManage && <Button size="sm" variant="outline-primary" onClick={() => navigate(`/benchmarking?tab=new&tutorId=${tutor.id}&revisionId=${tutor.activeRevisionId}`)}>Benchmark Tutor</Button>}
            </div>}
          </header>

          <Nav variant="tabs" activeKey={tab} onSelect={(key) => key && setTab(key)} className="tutor-tabs" aria-label="Tutor configuration views">
            <Nav.Item><Nav.Link eventKey="overview">Overview</Nav.Link></Nav.Item>
            <Nav.Item><Nav.Link eventKey="prompts">Prompt Versions</Nav.Link></Nav.Item>
            <Nav.Item><Nav.Link eventKey="revisions">Revisions</Nav.Link></Nav.Item>
            <Nav.Item><Nav.Link eventKey="knowledge">Knowledge</Nav.Link></Nav.Item>
          </Nav>
          <section className="tutor-tab-content">
            {catalogError && tab === "revisions" && <Alert variant="warning">{catalogError} Revision history is still available, but model selectors cannot be loaded.</Alert>}
            {tab === "overview" && <TutorOverviewPanel tutor={tutor} prompts={prompts} revisions={revisions} canManage={canManage} onOpenTab={setTab} />}
            {tab === "prompts" && <PromptVersionsPanel tutorId={tutorId} prompts={prompts} activePromptVersionId={tutor.activePromptVersionId} canManage={canManage} onChanged={refresh} />}
            {tab === "revisions" && <TutorRevisionsPanel tutorId={tutorId} revisions={revisions} prompts={prompts} interactionModels={interactionModels} embeddingModels={embeddingModels} canManage={canManage} onChanged={refresh} onCreatePrompt={() => setTab("prompts")} />}
            {tab === "knowledge" && <TutorKnowledgePanel tutorId={tutorId} canManage={canManage} />}
          </section>
        </>}
      </Container>
    </main>
  );
}
