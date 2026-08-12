import { useEffect, useMemo, useRef, useState } from "react";
import Alert from "react-bootstrap/Alert";
import Button from "react-bootstrap/Button";
import Container from "react-bootstrap/Container";
import Spinner from "react-bootstrap/Spinner";
import { useSelector } from "react-redux";
import { useNavigate, useSearchParams } from "react-router-dom";
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
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { roles = [] } = useSelector((state) => state.auth);
  const canManage = roles.includes("Editor") || roles.includes("Admin");
  const [tutors, setTutors] = useState([]);
  const [selectedTutorId, setSelectedTutorId] = useState("");
  const [messages, setMessages] = useState([]);
  const [draft, setDraft] = useState("");
  const [topK, setTopK] = useState(5);
  const [showSources, setShowSources] = useState(true);
  const [loadingTutors, setLoadingTutors] = useState(true);
  const [sending, setSending] = useState(false);
  const [loadError, setLoadError] = useState("");
  const [requestedTutorUnavailable, setRequestedTutorUnavailable] = useState("");
  const endRef = useRef(null);

  const selectedTutor = useMemo(
    () => tutors.find((tutor) => tutor.tutorId === selectedTutorId) || null,
    [tutors, selectedTutorId],
  );

  useEffect(() => {
    const controller = new AbortController();
    listRuntimeTutors(controller.signal)
      .then((loaded) => {
        const values = loaded ?? [];
        setTutors(values);
        const requestedTutorId = searchParams.get("tutorId");
        const requested = requestedTutorId
          ? values.find((tutor) => tutor.tutorId === requestedTutorId) || null
          : null;
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
  };

  const changeTutor = (tutorId) => {
    if (messages.some((message) => !message.local) && !window.confirm("Changing tutors will start a new local conversation. Continue?")) return;
    const tutor = tutors.find((value) => value.tutorId === tutorId) || null;
    setRequestedTutorUnavailable("");
    setSelectedTutorId(tutorId);
    resetConversation(tutor);
    const next = new URLSearchParams(searchParams);
    if (tutorId) next.set("tutorId", tutorId); else next.delete("tutorId");
    setSearchParams(next, { replace: true });
  };

  const sendQuestion = async (questionOverride, historyOverride) => {
    const question = (questionOverride ?? draft).trim();
    if (!question || !selectedTutor || sending) return;

    const history = historyOverride ?? buildBoundedHistory(messages);
    const userMessage = { id: crypto.randomUUID(), role: "user", content: question, local: false };
    setMessages((current) => [...current.filter((message) => !message.error), userMessage]);
    setDraft("");
    setSending(true);
    try {
      const response = await askRuntimeTutor(selectedTutor.activationSlug, { question, topK, history });
      setMessages((current) => [...current, {
        id: crypto.randomUUID(),
        role: "assistant",
        content: response.answer,
        local: false,
        sources: response.sources,
      }]);
    } catch (error) {
      setMessages((current) => [
        ...current.filter((message) => message.id !== userMessage.id),
        { ...userMessage, error: getTutorRuntimeError(error), retryHistory: history },
      ]);
    } finally {
      setSending(false);
    }
  };

  return (
    <main className="ai-runtime-page">
      <Container className="ai-runtime-container">
        <header className="ai-runtime-title">
          <div><span>Revision-aware learning</span><h1>AI Tutor</h1><p>Chat with the active, versioned configuration of a tutor.</p></div>
          <Button variant="outline-secondary" onClick={() => resetConversation()} disabled={!selectedTutor || sending}>New Chat</Button>
        </header>

        {loadError && <Alert variant="danger">{loadError}</Alert>}
        {loadingTutors && <div className="runtime-loading"><Spinner /><span>Loading active tutors…</span></div>}
        {!loadingTutors && !loadError && tutors.length === 0 && !requestedTutorUnavailable && (
          <Alert variant="warning">
            <h2>No tutors are ready for chat</h2>
            <p>Activate a revision with a prompt, interaction model, and configuration snapshot first.</p>
            {canManage && <Button size="sm" variant="outline-primary" onClick={() => navigate("/tutors")}>Configure tutors</Button>}
          </Alert>
        )}

        {!loadingTutors && !loadError && requestedTutorUnavailable && (
          <Alert variant="warning">
            <h2>This tutor is not ready for chat</h2>
            <p>Activate a valid revision with its configuration snapshot before trying again.</p>
            {canManage && <Button size="sm" variant="outline-primary" onClick={() => navigate(`/tutors/${requestedTutorUnavailable}?tab=revisions`)}>Configure tutor</Button>}
          </Alert>
        )}

        {!loadingTutors && tutors.length > 0 && <>
          <TutorSelector tutors={tutors} selectedTutorId={selectedTutorId} disabled={sending} onChange={changeTutor} />
          <TutorRuntimeHeader
            tutor={selectedTutor}
            canManage={canManage}
            onConfigure={() => navigate(`/tutors/${selectedTutor.tutorId}?tab=revisions`)}
            onBenchmark={() => navigate(`/benchmarking?tab=new&tutorId=${selectedTutor.tutorId}&revisionId=${selectedTutor.activeRevisionId}`)}
          />
          <ChatSettings topK={topK} showSources={showSources} onTopKChange={setTopK} onShowSourcesChange={setShowSources} />

          <section className="runtime-chat" aria-live="polite">
            <div className="runtime-messages">
              {messages.map((message) => (
                <ChatMessage
                  key={message.id}
                  message={message}
                  tutorName={selectedTutor?.tutorName || "Tutor"}
                  showSources={showSources}
                  retryDisabled={sending}
                  onRetry={(failed) => sendQuestion(failed.content, failed.retryHistory)}
                />
              ))}
              {sending && <div className="runtime-thinking"><Spinner size="sm" />{selectedTutor.tutorName} is thinking…</div>}
              <div ref={endRef} />
            </div>
            <ChatComposer value={draft} tutorName={selectedTutor?.tutorName} disabled={!selectedTutor || sending} onChange={setDraft} onSend={() => sendQuestion()} />
          </section>
        </>}
      </Container>
    </main>
  );
}
