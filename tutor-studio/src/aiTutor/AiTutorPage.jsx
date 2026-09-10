import { MessageOutlined, PlusOutlined } from "@ant-design/icons";
import { Alert, App, Button, Empty, Spin, Typography } from "antd";
import { useEffect, useMemo, useRef, useState } from "react";
import { useSelector } from "react-redux";
import { useNavigate, useSearchParams } from "react-router-dom";
import PageHeader from "../components/PageHeader.jsx";
import { askRuntimeTutor, listRuntimeTutors } from "./aiTutorApi.js";
import { buildBoundedHistory, getTutorRuntimeError } from "./aiTutorUtils.js";
import ChatComposer from "./ChatComposer.jsx";
import ChatMessage from "./ChatMessage.jsx";
import ChatSettings from "./ChatSettings.jsx";
import TutorRuntimeHeader from "./TutorRuntimeHeader.jsx";
import TutorSelector from "./TutorSelector.jsx";
import "./AiTutor.css";

const welcomeMessage = (tutor) => tutor ? [{
  id: crypto.randomUUID(),
  role: "assistant",
  content: `Hello! What would you like to learn with ${tutor.tutorName}?`,
  local: true,
  sources: [],
}] : [];

export default function AiTutorPage() {
  const { modal } = App.useApp();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { roles = [] } = useSelector((state) => state.auth);
  const canManage = roles.includes("Editor") || roles.includes("Admin");
  const [tutors, setTutors] = useState([]);
  const [selectedTutorId, setSelectedTutorId] = useState("");
  const [messages, setMessages] = useState([]);
  const [draft, setDraft] = useState("");
  const [conversationKey, setConversationKey] = useState(0);
  const [topK, setTopK] = useState(5);
  const [showSources, setShowSources] = useState(true);
  const [loadingTutors, setLoadingTutors] = useState(true);
  const [sending, setSending] = useState(false);
  const [loadError, setLoadError] = useState("");
  const [requestedTutorUnavailable, setRequestedTutorUnavailable] = useState("");
  const endRef = useRef(null);
  const requestRef = useRef(null);
  useEffect(() => () => requestRef.current?.abort(), []);

  const selectedTutor = useMemo(() => tutors.find((tutor) => tutor.tutorId === selectedTutorId) || null, [selectedTutorId, tutors]);

  useEffect(() => {
    const controller = new AbortController();
    listRuntimeTutors(controller.signal)
      .then((loaded) => {
        const values = loaded ?? [];
        setTutors(values);
        const requestedTutorId = searchParams.get("tutorId");
        const requested = requestedTutorId ? values.find((tutor) => tutor.tutorId === requestedTutorId) || null : null;
        const selected = requestedTutorId ? requested : values[0] || null;
        setRequestedTutorUnavailable(requestedTutorId && !requested ? requestedTutorId : "");
        setSelectedTutorId(selected?.tutorId || "");
        setMessages(welcomeMessage(selected));
        setLoadError("");
      })
      .catch((error) => {
        if (error.name !== "AbortError") setLoadError(getTutorRuntimeError(error));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoadingTutors(false);
      });
    return () => controller.abort();
    // Query selection is intentionally applied only when the catalog first loads.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: "smooth", block: "nearest" });
  }, [messages, sending]);

  const resetConversation = (tutor = selectedTutor) => {
    setMessages(welcomeMessage(tutor));
    setDraft("");
    setConversationKey((key) => key + 1);
  };

  const applyTutor = (tutorId) => {
    const tutor = tutors.find((value) => value.tutorId === tutorId) || null;
    setRequestedTutorUnavailable("");
    setSelectedTutorId(tutorId);
    resetConversation(tutor);
    const next = new URLSearchParams(searchParams);
    if (tutorId) next.set("tutorId", tutorId); else next.delete("tutorId");
    setSearchParams(next, { replace: true });
  };

  const changeTutor = (tutorId) => {
    if (!messages.some((message) => !message.local)) {
      applyTutor(tutorId);
      return;
    }
    modal.confirm({
      title: "Start a new conversation?",
      content: "Changing tutors clears the local conversation shown on this page.",
      okText: "Change tutor",
      onOk: () => applyTutor(tutorId),
    });
  };

  const sendQuestion = ({ question: questionOverride, history: historyOverride, attachments = [], limits, retryTopK } = {}) => {
    const question = (questionOverride ?? draft).trim();
    if ((!question && !attachments.length) || !selectedTutor || requestRef.current) return false;
    const attachmentIds = attachments.map((attachment) => attachment.id);
    const history = historyOverride ?? buildBoundedHistory(messages, attachmentIds, limits?.maxImagesPerRequest);
    const retrievalTopK = retryTopK ?? topK;
    const userMessage = { id: crypto.randomUUID(), role: "user", content: question, attachmentIds, attachments, local: false };
    const controller = new AbortController();
    requestRef.current = controller;
    setMessages((current) => [...current.filter((message) => !message.error), userMessage]);
    setDraft("");
    setSending(true);
    askRuntimeTutor(selectedTutor.activationSlug, { question, attachmentIds, topK: retrievalTopK, history }, controller.signal)
      .then((response) => {
        if (!controller.signal.aborted) setMessages((current) => [...current, { id: crypto.randomUUID(), role: "assistant", content: response.answer, local: false, sources: response.sources }]);
      }).catch((error) => {
        if (!controller.signal.aborted) setMessages((current) => [...current.filter((message) => message.id !== userMessage.id), { ...userMessage, error: getTutorRuntimeError(error), retryHistory: history, retryTopK: retrievalTopK }]);
      }).finally(() => {
        if (requestRef.current === controller) requestRef.current = null;
        if (!controller.signal.aborted) setSending(false);
      });
    return true;
  };

  return (
    <main className="workspace-page ai-runtime-page">
      <div className="workspace-page-inner ai-runtime-container">
        <PageHeader
          title="AI Tutor"
          description="Chat with the active, versioned configuration of a tutor."
          extra={<Button icon={<PlusOutlined />} onClick={() => resetConversation()} disabled={!selectedTutor || sending}>New chat</Button>}
        />

        {loadError ? <Alert type="error" title="Active tutors could not be loaded" description={loadError} showIcon /> : null}
        {loadingTutors ? <div className="workspace-loading"><Spin size="large" description="Loading active tutors" /></div> : null}
        {!loadingTutors && !loadError && !tutors.length && !requestedTutorUnavailable ? (
          <Empty image={<MessageOutlined className="runtime-empty-icon" />} description="No tutors are ready for chat">
            <Typography.Paragraph type="secondary">Activate a revision with a prompt, interaction model, and configuration snapshot first.</Typography.Paragraph>
            {canManage ? <Button type="primary" onClick={() => navigate("/tutors")}>Configure tutors</Button> : null}
          </Empty>
        ) : null}
        {!loadingTutors && !loadError && requestedTutorUnavailable ? (
          <Alert type="warning" title="This tutor is not ready for chat" description="Activate a valid revision with its configuration snapshot before trying again." action={canManage ? <Button size="small" onClick={() => navigate(`/tutors/${requestedTutorUnavailable}?tab=revisions`)}>Configure tutor</Button> : null} showIcon />
        ) : null}

        {!loadingTutors && tutors.length ? (
          <div className="runtime-workspace">
            <TutorSelector tutors={tutors} selectedTutorId={selectedTutorId} disabled={sending} onChange={changeTutor} />
            <TutorRuntimeHeader tutor={selectedTutor} canManage={canManage} onConfigure={() => navigate(`/tutors/${selectedTutor.tutorId}?tab=revisions`)} onBenchmark={() => navigate(`/benchmarking?tab=new&tutorId=${selectedTutor.tutorId}&revisionId=${selectedTutor.activeRevisionId}`)} />
            <ChatSettings topK={topK} showSources={showSources} onTopKChange={setTopK} onShowSourcesChange={setShowSources} />

            <section className="runtime-chat" aria-live="polite">
              <div className="runtime-messages">
                {messages.map((message) => <ChatMessage
                    key={message.id}
                    message={message}
                    tutorName={selectedTutor?.tutorName || "Tutor"}
                    showSources={showSources}
                    retryDisabled={sending}
                    onRetry={(failed) =>
                        sendQuestion({ question: failed.content, history: failed.retryHistory, attachments: failed.attachments, retryTopK: failed.retryTopK })
                    }
                />)}
                {sending ? <div className="runtime-thinking"><Spin size="small" /> <span>{selectedTutor.tutorName} is thinking…</span></div> : null}
                <div ref={endRef} />
              </div>
              <ChatComposer key={`${selectedTutorId}-${conversationKey}`} supportsImageInput={selectedTutor?.model?.supportsImageInput === true} value={draft} tutorName={selectedTutor?.tutorName} disabled={!selectedTutor || sending} onChange={setDraft} onSend={(attachments, limits) => sendQuestion({ attachments, limits })} />
            </section>
          </div>
        ) : null}
      </div>
    </main>
  );
}
