import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { App } from "antd";
import { configureStore } from "@reduxjs/toolkit";
import { Provider } from "react-redux";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import AiTutorPage from "../src/pages/user/AiTutorPage.jsx";
import * as api from "../src/features/user/utils/aiTutorApi.js";
import { messageProvenance } from "./fixtures/messageProvenance";

vi.mock("../src/features/user/utils/aiTutorApi.js", () => ({
  listRuntimeTutors: vi.fn(), listRuntimeTutorReadiness: vi.fn(), listTutorConversations: vi.fn(),
  createTutorConversation: vi.fn(), getTutorConversation: vi.fn(), deleteTutorConversation: vi.fn(),
  streamRuntimeTutor: vi.fn(), getChatAttachmentOptions: vi.fn(),
  uploadChatImage: vi.fn(), getChatImage: vi.fn(), deleteChatImage: vi.fn(),
}));

const tutor = { tutorId: "tutor-1", tutorName: "RoboTutor", activationSlug: "robotutor", revisionNumber: 1,
  model: { name: "Qwen", supportsImageInput: false }, knowledgeDocumentCount: 3 };
const summary = { id: "conversation-1", title: "PID control", revisionNumber: 1, updatedUtc: "2026-01-01T00:00:00Z" };
const history = { ...summary, messages: [
  { id: "user-1", role: "user", content: "Explain PID" },
  { id: "assistant-1", role: "assistant", content: "PID uses feedback.", provenance: messageProvenance },
] };
const completed = { conversationId: summary.id, answer: "PID uses feedback.", provenance: messageProvenance,
  sources: [], toolActivities: [] };

function mountChat() {
  const store = configureStore({ reducer: { auth: () => ({ roles: ["Auditor"] }) } });
  return render(<Provider store={store}><MemoryRouter><App><AiTutorPage /></App></MemoryRouter></Provider>);
}

async function sendQuestion() {
  const user = userEvent.setup();
  await screen.findByText("Hello! What would you like to learn with RoboTutor?");
  await user.type(screen.getByRole("textbox", { name: "Message to tutor" }), "Explain PID");
  await user.click(screen.getByRole("button", { name: "Send message" }));
  await waitFor(() => expect(api.streamRuntimeTutor).toHaveBeenCalledOnce());
  return user;
}

describe("provenance in production Chat page state", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    Element.prototype.scrollIntoView = vi.fn();
    vi.mocked(api.listRuntimeTutors).mockResolvedValue([tutor]);
    vi.mocked(api.listRuntimeTutorReadiness).mockResolvedValue([]);
    vi.mocked(api.listTutorConversations).mockResolvedValue([]);
    vi.mocked(api.createTutorConversation).mockResolvedValue(summary);
    vi.mocked(api.getTutorConversation).mockResolvedValue(history);
    vi.mocked(api.getChatAttachmentOptions).mockResolvedValue({ maxImagesPerMessage: 4 });
  });

  it("streams tokens, receives final provenance once and keeps the open disclosure through history refresh", async () => {
    let emit: (type: string, value: unknown) => void;
    let finish: () => void;
    let loadHistory: (value: unknown) => void;
    vi.mocked(api.streamRuntimeTutor).mockImplementation((_slug, _payload, _signal, onEvent) => {
      emit = onEvent;
      return new Promise<void>((resolve) => { finish = resolve; });
    });
    vi.mocked(api.getTutorConversation).mockImplementation(() => new Promise((resolve) => { loadHistory = resolve; }));
    mountChat();
    const user = await sendQuestion();
    act(() => emit("message.delta", { delta: "PID uses " }));
    expect(screen.getByText("PID uses")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Why this answer?" })).not.toBeInTheDocument();
    act(() => emit("message.delta", { delta: "feedback." }));
    const answer = screen.getByText(completed.answer);
    act(() => emit("message.completed", { message: completed }));
    expect(screen.getByRole("button", { name: "Sources · 3" })).toBeInTheDocument();
    expect(screen.getByText(completed.answer)).toBe(answer);
    await user.click(screen.getByRole("button", { name: "Sources · 3" }));
    expect(screen.getByRole("heading", { name: "08_control_pid.md" })).toBeInTheDocument();
    await act(async () => finish());
    await waitFor(() => expect(api.getTutorConversation).toHaveBeenCalledOnce());
    await act(async () => loadHistory(history));
    await waitFor(() => expect(screen.getByRole("textbox", { name: "Message to tutor" })).toBeEnabled());
    expect(screen.getByRole("button", { name: "Sources · 3" })).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByText(completed.answer)).toBe(answer);
    expect(screen.getAllByRole("button", { name: "Why this answer?" })).toHaveLength(1);
    expect(api.streamRuntimeTutor).toHaveBeenCalledWith("robotutor", expect.objectContaining({ question: "Explain PID" }),
      expect.any(AbortSignal), expect.any(Function));
  });

  it("reopening a saved conversation renders the same persisted provenance", async () => {
    vi.mocked(api.listTutorConversations).mockResolvedValue([summary]);
    mountChat();
    const user = userEvent.setup();
    await user.click(await screen.findByRole("button", { name: /^PID control/ }));
    await screen.findByRole("button", { name: "Sources · 3" });
    await user.click(screen.getByRole("button", { name: "Why this answer?" }));
    expect(screen.getByText("5 chunks retrieved")).toBeInTheDocument();
    expect(screen.getByText("Provided — 2 memories supplied")).toBeInTheDocument();
    expect(screen.getByText("qwen3.8:27b")).toBeInTheDocument();
    expect(api.getTutorConversation).toHaveBeenCalledWith("robotutor", summary.id, expect.any(AbortSignal));
    expect(api.streamRuntimeTutor).not.toHaveBeenCalled();
  });

  it("keeps final metadata on its own answer if another turn is already present in refreshed history", async () => {
    vi.mocked(api.streamRuntimeTutor).mockImplementation(async (_slug, _payload, _signal, emit) => {
      emit("message.completed", { message: completed });
    });
    vi.mocked(api.getTutorConversation).mockResolvedValue({ ...history, messages: [...history.messages,
      { id: "user-2", role: "user", content: "Another tab's question" },
      { id: "assistant-2", role: "assistant", content: "Another answer", provenance: null },
    ] });
    mountChat();
    await sendQuestion();
    await screen.findByText("Another answer");
    expect(screen.getAllByRole("button", { name: "Sources · 3" })).toHaveLength(1);
    const laterAnswer = screen.getByText("Another answer").closest("article");
    expect(laterAnswer).not.toHaveTextContent("Why this answer?");
  });

  it("loads old messages with null provenance without fabricating an explanation", async () => {
    vi.mocked(api.listTutorConversations).mockResolvedValue([summary]);
    vi.mocked(api.getTutorConversation).mockResolvedValue({ ...history,
      messages: history.messages.map((message) => ({ ...message, provenance: null })) });
    mountChat();
    await userEvent.setup().click(await screen.findByRole("button", { name: /^PID control/ }));
    await screen.findByText(completed.answer);
    expect(screen.queryByRole("button", { name: "Why this answer?" })).not.toBeInTheDocument();
  });

  it("does not show completed provenance for failed generation", async () => {
    vi.mocked(api.streamRuntimeTutor).mockImplementation(async (_slug, _payload, _signal, emit) => {
      emit("message.delta", { delta: "Partial answer" });
      throw Object.assign(new Error("Unavailable"), { status: 503 });
    });
    mountChat();
    await sendQuestion();
    await screen.findByRole("button", { name: "Retry" });
    expect(screen.queryByText("Partial answer")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Why this answer?" })).not.toBeInTheDocument();
    expect(api.getTutorConversation).not.toHaveBeenCalled();
  });

  it("preserves cancellation without claiming completed provenance", async () => {
    vi.mocked(api.streamRuntimeTutor).mockImplementation((_slug, _payload, signal, emit) => {
      emit("message.delta", { delta: "Partial answer" });
      return new Promise((_resolve, reject) => signal.addEventListener("abort", () => reject(new DOMException("Cancelled", "AbortError"))));
    });
    mountChat();
    const user = await sendQuestion();
    await user.click(screen.getByRole("button", { name: "Cancel generation" }));
    await screen.findByText("Generation cancelled.");
    expect(screen.queryByRole("button", { name: "Why this answer?" })).not.toBeInTheDocument();
    expect(api.getTutorConversation).not.toHaveBeenCalled();
  });
});
