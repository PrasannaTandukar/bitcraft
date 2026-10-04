import { ArrowLeftOutlined, ExperimentOutlined, MessageOutlined, ReloadOutlined } from "@ant-design/icons";
import { Alert, Button, Card, Flex, Result, Spin, Tabs, Tag, Typography } from "antd";
import { useEffect, useState } from "react";
import { useSelector } from "react-redux";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import { getApiErrorDescription } from "../../api/client.js";
import StatusTag from "../../components/StatusTag.jsx";
import PromptVersionsPanel from "../../features/editor/components/PromptVersionsPanel.jsx";
import TutorKnowledgePanel from "../../features/editor/components/TutorKnowledgePanel.jsx";
import TutorOverviewPanel from "../../features/editor/components/TutorOverviewPanel.jsx";
import TutorRevisionsPanel from "../../features/editor/components/TutorRevisionsPanel.jsx";
import {
  getTutor,
  listEmbeddingModelCatalog,
  listInteractionModelCatalog,
  listPromptVersions,
  listTutorRevisions,
  listTutorSnapshots,
  listRuntimeTutorReadiness,
} from "../../features/editor/hooks/tutorApi.js";
import { tutorStatus } from "../../features/editor/utils/tutorUtils.js";
import "../../styles/TutorDetails.css";

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
  const [runtimeReadiness, setRuntimeReadiness] = useState(null);
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
  }, [requestedTab, searchParams, setSearchParams, tab]);

  useEffect(() => {
    const controller = new AbortController();
    Promise.all([
      getTutor(tutorId, controller.signal),
      listPromptVersions(tutorId, controller.signal),
      listTutorRevisions(tutorId, controller.signal),
      listTutorSnapshots(tutorId, controller.signal),
      listRuntimeTutorReadiness(controller.signal),
    ])
      .then(([loadedTutor, loadedPrompts, loadedRevisions, loadedSnapshots, readinessValues]) => {
        setTutor(loadedTutor);
        setPrompts(loadedPrompts ?? []);
        setRevisions(loadedRevisions ?? []);
        setSnapshots(loadedSnapshots ?? []);
        setRuntimeReadiness((readinessValues ?? []).find((value) => value.tutorId === tutorId) ?? null);
        setError("");
      })
      .catch((requestError) => {
        if (requestError.name !== "AbortError") setError(getApiErrorDescription(requestError, "Could not load the tutor workspace."));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [reload, tutorId]);

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
  }, [canManage, reload, tutorId]);

  const setTab = (nextTab) => {
    const next = new URLSearchParams(searchParams);
    next.set("tab", nextTab);
    setSearchParams(next);
  };

  const refresh = () => {
    setLoading(true);
    setReload((value) => value + 1);
  };

  if (loading && !tutor) return <div className="workspace-loading"><Spin size="large" description="Loading tutor workspace" /></div>;
  if (!tutor && error) return <Result status="error" title="Tutor workspace unavailable" subTitle={error} extra={<Button icon={<ReloadOutlined />} onClick={refresh}>Retry</Button>} />;

  const items = [
    { key: "overview", label: "Overview", children: <TutorOverviewPanel tutor={tutor} prompts={prompts} revisions={revisions} runtimeReadiness={runtimeReadiness} canManage={canManage} onOpenTab={setTab} /> },
    { key: "prompts", label: `Prompt versions (${prompts.length})`, children: <PromptVersionsPanel tutorId={tutorId} prompts={prompts} activePromptVersionId={tutor.activePromptVersionId} canManage={canManage} onChanged={refresh} /> },
    { key: "revisions", label: `Revisions (${revisions.length})`, children: <><>{catalogError ? <Alert type="warning" title={catalogError} showIcon style={{ marginBottom: 16 }} /> : null}</><TutorRevisionsPanel tutorId={tutorId} revisions={revisions} prompts={prompts} interactionModels={interactionModels} embeddingModels={embeddingModels} canManage={canManage} onChanged={refresh} onCreatePrompt={() => setTab("prompts")} /></> },
    { key: "knowledge", label: "Knowledge", children: <TutorKnowledgePanel tutorId={tutorId} canManage={canManage} /> },
  ];

  return (
    <main className="workspace-page tutor-details-page">
      <div className="workspace-page-inner">
        <Button type="text" icon={<ArrowLeftOutlined />} onClick={() => navigate("/tutors")} className="tutor-back-button">Tutors</Button>

        {error ? <Alert type="error" title="Some tutor data could not be refreshed" description={error} showIcon style={{ marginBottom: 16 }} /> : null}

        <Flex className="tutor-detail-header" justify="space-between" align="flex-start" gap={20} wrap>
          <div className="tutor-detail-title">
            <Flex align="center" gap={8} wrap>
              <Typography.Title level={2}>{tutor.name}</Typography.Title>
              <StatusTag value={tutorStatus(tutor.status)} />
            </Flex>
            <Typography.Paragraph type="secondary">{tutor.description || "Configure this tutor's prompts, models, knowledge, and runtime revisions."}</Typography.Paragraph>
            <Flex gap={8} wrap>
              <Tag>{revisions.length} revisions</Tag>
              <Tag>{snapshots.length} snapshots</Tag>
            </Flex>
          </div>
          {tutor.activeRevisionId ? (
            <Flex gap={8} wrap>
              <Button icon={<MessageOutlined />} onClick={() => navigate(`/ai-tutor?tutorId=${tutor.id}`)}>Chat</Button>
              {canManage ? <Button type="primary" icon={<ExperimentOutlined />} onClick={() => navigate(`/benchmarking?tab=new&tutorId=${tutor.id}&revisionId=${tutor.activeRevisionId}`)}>Benchmark</Button> : null}
            </Flex>
          ) : null}
        </Flex>

        <Card className="tutor-tabs-card" styles={{ body: { paddingTop: 0 } }}>
          <Tabs activeKey={tab} onChange={setTab} items={items} />
        </Card>
      </div>
    </main>
  );
}
