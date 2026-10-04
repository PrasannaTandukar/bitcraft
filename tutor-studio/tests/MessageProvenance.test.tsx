import { describe, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import ChatMessage from "../src/features/user/components/ChatMessage.jsx";
import { messageProvenance } from "./fixtures/messageProvenance";

const baseMessage = { id: "message-1", role: "assistant", content: "Here is the answer." };
const props = { tutorName: "RoboTutor", showSources: true, onRetry: vi.fn(), retryDisabled: false };
const renderMessage = (provenance = messageProvenance) => render(<ChatMessage {...props} message={{ ...baseMessage, provenance }} />);
const withoutKnowledge = { ...messageProvenance, knowledge: { ...messageProvenance.knowledge, retrievedChunkCount: 0, retrievedChunks: [] } };

describe("assistant answer provenance", () => {
  it("starts collapsed, counts unique documents and groups independent excerpts", async () => {
    const user = userEvent.setup();
    renderMessage();
    const sources = screen.getByRole("button", { name: "Sources · 3" });
    expect(sources).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByText("08_control_pid.md")).not.toBeInTheDocument();
    await user.click(sources);
    expect(sources).toHaveAttribute("aria-expanded", "true");
    const panel = screen.getByRole("region", { name: "Sources provided as context" });
    expect(panel.id).toBe(sources.getAttribute("aria-controls"));
    expect(within(panel).getAllByRole("heading")).toHaveLength(3);
    const pid = within(panel).getByRole("region", { name: "08_control_pid.md" });
    expect(within(pid).getByText(messageProvenance.knowledge.retrievedChunks[0].excerpt)).toBeInTheDocument();
    expect(within(pid).getByText(messageProvenance.knowledge.retrievedChunks[1].excerpt)).toBeInTheDocument();
    expect(panel).not.toHaveTextContent(/used by the model|sources used|86%|page|section/i);
  });

  it("counts same-name distinct documents independently using message-local keys", () => {
    const value = { ...messageProvenance, knowledge: { ...messageProvenance.knowledge,
      retrievedChunks: messageProvenance.knowledge.retrievedChunks.map((chunk) => ({ ...chunk, documentName: "notes.md" })) } };
    renderMessage(value);
    expect(screen.getByRole("button", { name: "Sources · 3" })).toBeInTheDocument();
  });

  it("does not invent a unique-document count for older payloads without grouping keys", async () => {
    const user = userEvent.setup();
    const value = { ...messageProvenance, knowledge: { ...messageProvenance.knowledge,
      retrievedChunks: messageProvenance.knowledge.retrievedChunks.map((chunk) => ({ ...chunk, documentGroupKey: undefined })) } };
    renderMessage(value);
    expect(screen.getByRole("button", { name: "Sources" })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Why this answer?" }));
    expect(screen.getByText("3 document groups supplied as context")).toBeInTheDocument();
  });

  it("has no Sources control for zero retrieval and truthfully explains a plain response", async () => {
    const user = userEvent.setup();
    renderMessage({ ...withoutKnowledge, memory: { contextProvided: false, retrievedItemCount: 0 }, tools: [] });
    expect(screen.queryByRole("button", { name: /Sources/ })).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Why this answer?" }));
    expect(screen.getByText("No tutor knowledge retrieved")).toBeInTheDocument();
    expect(screen.getByText("Not used")).toBeInTheDocument();
    expect(screen.getByText("No tools invoked")).toBeInTheDocument();
  });

  it("shows captured knowledge, memory, actual tool success and historical runtime", async () => {
    const user = userEvent.setup();
    renderMessage();
    await user.click(screen.getByRole("button", { name: "Why this answer?" }));
    expect(screen.getByText("5 chunks retrieved")).toBeInTheDocument();
    expect(screen.getByText("3 documents supplied as context")).toBeInTheDocument();
    expect(screen.getByText("Provided — 2 memories supplied")).toBeInTheDocument();
    expect(screen.getByText("Calculator — successful")).toBeInTheDocument();
    expect(screen.getByText("qwen3.8:27b")).toBeInTheDocument();
    const panel = screen.getByRole("region", { name: "Answer provenance" });
    expect(panel).toHaveTextContent("Revision1");
    expect(panel).toHaveTextContent("Snapshot12345678…");
    expect(panel).not.toHaveTextContent(messageProvenance.runtime.snapshotId);
  });

  it("records failed and timed-out tools without raw error details", async () => {
    const user = userEvent.setup();
    renderMessage({ ...withoutKnowledge, tools: [
      { toolName: "Calculator", status: "Failed", succeeded: false, durationMs: 1, errorCategory: "private stack trace" },
      { toolName: "Lookup", status: "TimedOut", succeeded: false, durationMs: 1, errorCategory: "Timeout" },
    ] });
    await user.click(screen.getByRole("button", { name: "Why this answer?" }));
    expect(screen.getByText("Calculator — failed")).toBeInTheDocument();
    expect(screen.getByText("Lookup — timed out")).toBeInTheDocument();
    expect(screen.queryByText("private stack trace")).not.toBeInTheDocument();
  });

  it("opens disclosures with the keyboard and switches panels without losing button focus", async () => {
    const user = userEvent.setup();
    renderMessage();
    await user.tab();
    const sources = screen.getByRole("button", { name: "Sources · 3" });
    expect(sources).toHaveFocus();
    await user.keyboard("{Enter}");
    expect(sources).toHaveAttribute("aria-expanded", "true");
    await user.tab();
    const why = screen.getByRole("button", { name: "Why this answer?" });
    expect(why).toHaveFocus();
    await user.keyboard(" ");
    expect(why).toHaveAttribute("aria-expanded", "true");
    expect(sources).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByRole("region", { name: "Sources provided as context" })).not.toBeInTheDocument();
  });

  it("clamps long excerpts and allows a deliberate expansion", async () => {
    const user = userEvent.setup();
    const excerpt = `${"Context text ".repeat(60)}unique end of excerpt`;
    const value = { ...messageProvenance, knowledge: { ...messageProvenance.knowledge,
      retrievedChunks: [{ ...messageProvenance.knowledge.retrievedChunks[0], excerpt }] } };
    renderMessage(value);
    await user.click(screen.getByRole("button", { name: "Sources · 1" }));
    expect(screen.queryByText(excerpt)).not.toBeInTheDocument();
    const expand = screen.getByRole("button", { name: /Show more of/ });
    expect(expand).toHaveAttribute("aria-expanded", "false");
    await user.click(expand);
    expect(screen.getByText(excerpt)).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: /Show less of/ }));
    expect(screen.queryByText(excerpt)).not.toBeInTheDocument();
  });

  it("shows raw cosine similarity only under retrieval details, never percentages or calibrated labels", async () => {
    const user = userEvent.setup();
    renderMessage();
    await user.click(screen.getByRole("button", { name: "Sources · 3" }));
    expect(screen.getByText("Cosine similarity 0.860")).not.toBeVisible();
    await user.click(screen.getAllByText("Retrieval details")[0]);
    expect(screen.getByText("Cosine similarity 0.860")).toBeVisible();
    expect(screen.getByText("Retrieval rank 1 · Chunk index 0")).toBeVisible();
    expect(screen.queryByText(/86%|High relevance/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Cosine similarity null/)).not.toBeInTheDocument();
  });

  it("omits source navigation because this API grants no document-opening capability", async () => {
    const user = userEvent.setup();
    renderMessage();
    await user.click(screen.getByRole("button", { name: "Sources · 3" }));
    expect(screen.queryByRole("link")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "View source" })).not.toBeInTheDocument();
  });

  it("renders long names as wrapping text and treats excerpts as text rather than HTML", async () => {
    const user = userEvent.setup();
    const name = `${"very_long_filename_".repeat(30)}.md`;
    const value = { ...messageProvenance, knowledge: { ...messageProvenance.knowledge,
      retrievedChunks: [{ ...messageProvenance.knowledge.retrievedChunks[0], documentName: name, excerpt: '<script>alert("unsafe")</script>' }] } };
    const { container } = renderMessage(value);
    await user.click(screen.getByRole("button", { name: "Sources · 1" }));
    expect(screen.getByRole("heading", { name })).toBeInTheDocument();
    expect(screen.getByText('<script>alert("unsafe")</script>')).toBeInTheDocument();
    expect(container.querySelector("script")).toBeNull();
    expect(container.querySelector(".runtime-provenance-panel")).toBeInTheDocument();
  });

  it("never renders additional hidden prompt, memory, reasoning or provider configuration fields", async () => {
    const user = userEvent.setup();
    const unsafe = { ...messageProvenance, systemPrompt: "SECRET PROMPT", chainOfThought: "PRIVATE REASONING",
      memory: { ...messageProvenance.memory, content: "PRIVATE MEMORY" },
      runtime: { ...messageProvenance.runtime, endpoint: "PRIVATE ENDPOINT", apiKey: "PRIVATE KEY" } };
    const { container } = renderMessage(unsafe);
    await user.click(screen.getByRole("button", { name: "Sources · 3" }));
    await user.click(screen.getByRole("button", { name: "Why this answer?" }));
    expect(container).not.toHaveTextContent(/SECRET PROMPT|PRIVATE REASONING|PRIVATE MEMORY|PRIVATE ENDPOINT|PRIVATE KEY/);
  });

  it("keeps legacy, user and failed messages free of provenance controls", () => {
    const { rerender } = render(<ChatMessage {...props} message={baseMessage} />);
    expect(screen.queryByRole("button", { name: "Why this answer?" })).not.toBeInTheDocument();
    rerender(<ChatMessage {...props} message={{ ...baseMessage, role: "user", provenance: messageProvenance }} />);
    expect(screen.queryByRole("button", { name: "Why this answer?" })).not.toBeInTheDocument();
    rerender(<ChatMessage {...props} message={{ ...baseMessage, error: "Failed", provenance: messageProvenance }} />);
    expect(screen.queryByRole("button", { name: "Why this answer?" })).not.toBeInTheDocument();
  });

  it("reveals controls after streaming completes without replacing the answer", () => {
    const { rerender } = render(<ChatMessage {...props} message={{ ...baseMessage, streaming: true, provenance: messageProvenance }} />);
    const answer = screen.getByText(baseMessage.content);
    expect(screen.queryByRole("button", { name: "Sources · 3" })).not.toBeInTheDocument();
    rerender(<ChatMessage {...props} message={{ ...baseMessage, streaming: false, provenance: messageProvenance }} />);
    expect(screen.getByRole("button", { name: "Sources · 3" })).toBeInTheDocument();
    expect(screen.getByText(baseMessage.content)).toBe(answer);
  });

  it("honors the existing Show sources preference while keeping the explanation available", async () => {
    const user = userEvent.setup();
    render(<ChatMessage {...props} showSources={false} message={{ ...baseMessage, provenance: messageProvenance }} />);
    expect(screen.queryByRole("button", { name: /Sources/ })).not.toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Why this answer?" }));
    expect(screen.getByText("3 documents supplied as context")).toBeInTheDocument();
  });
});
