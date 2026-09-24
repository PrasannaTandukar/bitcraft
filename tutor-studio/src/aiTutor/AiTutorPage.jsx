import { MessageOutlined, PlusOutlined } from "@ant-design/icons";
import { Alert, App, Button, Empty, Select, Spin, Typography } from "antd";
import { useEffect, useMemo, useRef, useState } from "react";
import { useSelector } from "react-redux";
import { useNavigate, useSearchParams } from "react-router-dom";
import PageHeader from "../components/PageHeader.jsx";
import { streamRuntimeTutor, listRuntimeTutors, listTutorConversations, createTutorConversation, getTutorConversation, deleteTutorConversation } from "./aiTutorApi.js";
import { getTutorRuntimeError } from "./aiTutorUtils.js";
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
  const [conversations, setConversations] = useState([]);
  const [conversationId, setConversationId] = useState(null);
  const [conversationConfiguration, setConversationConfiguration] = useState(null);
  const [loadingConversation, setLoadingConversation] = useState(false);
  const [messages, setMessages] = useState([]);
  const [draft, setDraft] = useState("");
  const [conversationKey, setConversationKey] = useState(0);
  const [topK, setTopK] = useState(5);
  const [showSources, setShowSources] = useState(true);
  const [useMemory, setUseMemory] = useState(false);
  const [saveToMemory, setSaveToMemory] = useState(false);
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

  useEffect(() => {
    if (!selectedTutor) return;
    const controller = new AbortController();
    listTutorConversations(selectedTutor.activationSlug, controller.signal).then(setConversations)
      .catch((error) => { if (!controller.signal.aborted) setLoadError(getTutorRuntimeError(error)); });
    return () => controller.abort();
  }, [selectedTutor]);

  const displayConversation = (value) => {
    setConversationId(value.id);
    setConversationConfiguration(value);
    setMessages(value.messages.map((message) => ({ ...message, local: false,
      attachments: message.attachmentIds.map((id) => ({ id })) })));
    setDraft("");
    setConversationKey((key) => key + 1);
  };

  const loadConversation = async (id) => {
    setLoadingConversation(true);
    try { displayConversation(await getTutorConversation(selectedTutor.activationSlug, id)); }
    catch (error) { setLoadError(getTutorRuntimeError(error)); }
    finally { setLoadingConversation(false); }
  };

  const removeConversation = () => modal.confirm({
    title: "Delete this conversation?", content: "This permanently removes its stored messages.",
    okText: "Delete", okButtonProps: { danger: true },
    onOk: async () => {
      await deleteTutorConversation(selectedTutor.activationSlug, conversationId);
      setConversations((values) => values.filter((value) => value.id !== conversationId));
      resetConversation();
    },
  });

  const resetConversation = (tutor = selectedTutor) => {
    setConversationId(null);
    setConversationConfiguration(null);
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
      content: "Your conversation is saved. You can return to it later.",
      okText: "Change tutor",
      onOk: () => applyTutor(tutorId),
    });
  };

  const sendQuestion = ({ question: questionOverride, attachments = [], retryTopK } = {}) => {
    const question = (questionOverride ?? draft).trim();
    if ((!question && !attachments.length) || !selectedTutor || requestRef.current) return false;
    const attachmentIds = attachments.map((attachment) => attachment.id);
    const retrievalTopK = retryTopK ?? topK;
    const userMessage = { id: crypto.randomUUID(), role: "user", content: question, attachmentIds, attachments, local: false };
    const assistantMessage = { id: crypto.randomUUID(), role: "assistant", content: "", local: true,
      streaming: true, sources: [], toolActivities: [] };
    const controller = new AbortController();
    requestRef.current = controller;
    setMessages((current) => [...current.filter((message) => !message.error), userMessage, assistantMessage]);
    setDraft("");
    setSending(true);
    setLoadError("");
    const slug = selectedTutor.activationSlug;
    let activeConversationId = conversationId;
    let completedResponse = null;
    const ensureConversation = conversationId ? Promise.resolve({ id: conversationId }) : createTutorConversation(slug, controller.signal);
    ensureConversation.then((conversation) => {
      activeConversationId = conversation.id;
      setConversationId(conversation.id);
      if (!conversationId) setConversationConfiguration(conversation);
      return streamRuntimeTutor(slug, { question, attachmentIds, topK: retrievalTopK,
        conversationId: conversation.id, useMemory, saveToMemory }, controller.signal, (type, event) => {
        if (type === "conversation.started" && event.conversationId) {
          activeConversationId = event.conversationId;
          setConversationId(event.conversationId);
        } else if (type === "message.delta" && event.delta) {
          setMessages((current) => current.map((message) => message.id === assistantMessage.id
            ? { ...message, content: message.content + event.delta } : message));
        } else if (type === "source.retrieved" && event.source) {
          setMessages((current) => current.map((message) => message.id === assistantMessage.id
            ? { ...message, sources: message.sources.some((source) => source.chunkId === event.source.chunkId)
              ? message.sources : [...message.sources, event.source] } : message));
        } else if (type === "tool.started" && event.tool) {
          setMessages((current) => current.map((message) => message.id === assistantMessage.id
            ? { ...message, toolActivities: [...message.toolActivities, event.tool] } : message));
        } else if (type === "tool.completed" && event.tool) {
          setMessages((current) => current.map((message) => {
            if (message.id !== assistantMessage.id) return message;
            const index = message.toolActivities.findLastIndex((tool) =>
              tool.toolName === event.tool.toolName && tool.status === "Running");
            return { ...message, toolActivities: index < 0
              ? [...message.toolActivities, event.tool]
              : message.toolActivities.map((tool, toolIndex) => toolIndex === index ? event.tool : tool) };
          }));
        } else if (type === "message.completed") {
          completedResponse = event.message;
        }
      });
    })
      .then(async () => {
        if (!completedResponse?.conversationId) throw new Error("Tutor stream ended before completion.");
        const stored = await getTutorConversation(slug, completedResponse.conversationId, controller.signal);
        const listed = await listTutorConversations(slug, controller.signal);
        if (!controller.signal.aborted) {
          displayConversation(stored);
          setMessages((current) => current.map((message, index) => index === current.length - 1
            ? { ...message, sources: completedResponse.sources, toolActivities: completedResponse.toolActivities }
            : message));
          setConversations(listed);
        }
      }).catch(async (error) => {
        if (controller.signal.aborted) {
          setMessages((current) => [...current.filter((message) => message.id !== userMessage.id && message.id !== assistantMessage.id),
            { ...userMessage, error: "Generation cancelled.", retryTopK: retrievalTopK }]);
          return;
        }
        setMessages((current) => current.filter((message) => message.id !== assistantMessage.id));
        if (error.status === 409 || !error.status) {
          // A transport failure may follow a committed answer. Reload before allowing another send.
          if (activeConversationId) {
            try { displayConversation(await getTutorConversation(slug, activeConversationId, controller.signal)); }
            catch { /* Keep the visible draft if reloading is also unavailable. */ }
          }
          if (!controller.signal.aborted) setLoadError(`${getTutorRuntimeError(error)} Select the saved conversation to reload before sending again.`);
        } else {
          setMessages((current) => [...current.filter((message) => message.id !== userMessage.id), { ...userMessage, error: getTutorRuntimeError(error), retryTopK: retrievalTopK }]);
        }
      }).finally(() => {
        if (requestRef.current === controller) {
          requestRef.current = null;
          setSending(false);
        }
      });
    return true;
  };

  return (
    <main className="workspace-page ai-runtime-page">
      <div className="workspace-page-inner ai-runtime-container">
        <PageHeader
          title="AI Tutor"
          description="Chat with the active, versioned configuration of a tutor."
          extra={<Button icon={<PlusOutlined />} onClick={() => resetConversation()} disabled={!selectedTutor || sending || loadingConversation}>New chat</Button>}
        />

        {loadError ? <Alert type="error" title="Tutor chat" description={loadError} showIcon /> : null}
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
            <TutorSelector tutors={tutors} selectedTutorId={selectedTutorId} disabled={sending || loadingConversation} onChange={changeTutor} />
            <TutorRuntimeHeader tutor={selectedTutor} canManage={canManage} onConfigure={() => navigate(`/tutors/${selectedTutor.tutorId}?tab=revisions`)} onBenchmark={() => navigate(`/benchmarking?tab=new&tutorId=${selectedTutor.tutorId}&revisionId=${selectedTutor.activeRevisionId}`)} />
            <div className="runtime-conversation-picker">
              {conversationConfiguration ? <Typography.Text>Conversation pinned to revision {conversationConfiguration.revisionNumber}. </Typography.Text> : null}
              <Select aria-label="Saved conversations" placeholder="Saved conversations" value={conversationId}
                style={{ minWidth: 260 }} disabled={sending || loadingConversation} onChange={loadConversation}
                options={conversations.map((value) => ({ value: value.id, label: value.title || "New conversation" }))} />
              <Button danger onClick={removeConversation} disabled={!conversationId || sending || loadingConversation}>Delete conversation</Button>
            </div>
            <ChatSettings topK={topK} showSources={showSources} useMemory={useMemory}
              saveToMemory={saveToMemory} onTopKChange={setTopK}
              onShowSourcesChange={setShowSources} onUseMemoryChange={setUseMemory}
              onSaveToMemoryChange={setSaveToMemory} />

            <section className="runtime-chat" aria-live="polite">
              <div className="runtime-messages">
                {messages.map((message) => <ChatMessage
                    key={message.id}
                    message={message}
                    tutorName={selectedTutor?.tutorName || "Tutor"}
                    showSources={showSources}
                    retryDisabled={sending}
                    onRetry={(failed) =>
                        sendQuestion({ question: failed.content, attachments: failed.attachments, retryTopK: failed.retryTopK })
                    }
                />)}
                <div ref={endRef} />
              </div>
              <ChatComposer key={`${selectedTutorId}-${conversationKey}`} supportsImageInput={(conversationConfiguration?.supportsImageInput ?? selectedTutor?.model?.supportsImageInput) === true} value={draft} tutorName={selectedTutor?.tutorName} disabled={!selectedTutor || loadingConversation} generating={sending} onCancel={() => requestRef.current?.abort()} onChange={setDraft} onSend={(attachments) => sendQuestion({ attachments })} />
            </section>
          </div>
        ) : null}
      </div>
    </main>
  );
}
