import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import ChatMessage from "../src/features/user/components/ChatMessage";
vi.mock("../src/features/user/components/SourceList", () => ({
  default: ({ sources }: { sources: unknown[] }) => (
    <div data-testid="source-list">
      {sources?.length ?? 0} sources
    </div>
  ),
}));

describe("ChatMessage", () => {
  const userMessage = {
    role: "user",
    content: "Hello tutor",
  };

  const assistantMessage = {
    role: "assistant",
    content: "Hello **student**",
    sources: [],
  };

  it("renders a user message", () => {
    render(
      <ChatMessage
        message={userMessage}
        tutorName="Physics Tutor"
        showSources={false}
        onRetry={vi.fn()}
        retryDisabled={false}
      />,
    );

    expect(screen.getByText("You")).toBeInTheDocument();
    expect(screen.getByText("Hello tutor")).toBeInTheDocument();
  });

  it("renders the tutor name for an assistant message", () => {
    render(
      <ChatMessage
        message={assistantMessage}
        tutorName="Physics Tutor"
        showSources={false}
        onRetry={vi.fn()}
        retryDisabled={false}
      />,
    );

    expect(screen.getByText("Physics Tutor")).toBeInTheDocument();
  });

  it("renders assistant content as Markdown", () => {
    render(
      <ChatMessage
        message={assistantMessage}
        tutorName="Physics Tutor"
        showSources={false}
        onRetry={vi.fn()}
        retryDisabled={false}
      />,
    );

    const strongText = screen.getByText("student");

    expect(strongText.tagName).toBe("STRONG");
  });

  it("renders normal user content as plain text", () => {
    const message = {
      role: "user",
      content: "**Hello student**",
    };

    render(
      <ChatMessage
        message={message}
        tutorName="Physics Tutor"
        showSources={false}
        onRetry={vi.fn()}
        retryDisabled={false}
      />,
    );

    expect(screen.getByText("**Hello student**")).toBeInTheDocument();
    expect(screen.queryByText("Hello student")).not.toBeInTheDocument();
  });

  it("renders the assistant message with the assistant CSS classes", () => {
    const { container } = render(
      <ChatMessage
        message={assistantMessage}
        tutorName="Physics Tutor"
        showSources={false}
        onRetry={vi.fn()}
        retryDisabled={false}
      />,
    );

    expect(
      container.querySelector(".runtime-message-row.assistant"),
    ).toBeInTheDocument();

    expect(
      container.querySelector("article.runtime-message.assistant"),
    ).toBeInTheDocument();
  });

  it("renders the user message with the user CSS classes", () => {
    const { container } = render(
      <ChatMessage
        message={userMessage}
        tutorName="Physics Tutor"
        showSources={false}
        onRetry={vi.fn()}
        retryDisabled={false}
      />,
    );

    expect(
      container.querySelector(".runtime-message-row.user"),
    ).toBeInTheDocument();

    expect(
      container.querySelector("article.runtime-message.user"),
    ).toBeInTheDocument();
  });

  it("does not render sources when showSources is false", () => {
    const message = {
      ...assistantMessage,
      sources: [
        { title: "Source 1" },
        { title: "Source 2" },
      ],
    };

    render(
      <ChatMessage
        message={message}
        tutorName="Physics Tutor"
        showSources={false}
        onRetry={vi.fn()}
        retryDisabled={false}
      />,
    );

    expect(screen.queryByTestId("source-list")).not.toBeInTheDocument();
  });

  it("renders sources for an assistant message when showSources is true", () => {
    const message = {
      ...assistantMessage,
      sources: [
        { title: "Source 1" },
        { title: "Source 2" },
      ],
    };

    render(
      <ChatMessage
        message={message}
        tutorName="Physics Tutor"
        showSources={true}
        onRetry={vi.fn()}
        retryDisabled={false}
      />,
    );

    expect(screen.getByTestId("source-list")).toBeInTheDocument();
    expect(screen.getByText("2 sources")).toBeInTheDocument();
  });

  it("does not render sources for a user message even when showSources is true", () => {
    const message = {
      ...userMessage,
      sources: [
        { title: "Source 1" },
      ],
    };

    render(
      <ChatMessage
        message={message}
        tutorName="Physics Tutor"
        showSources={true}
        onRetry={vi.fn()}
        retryDisabled={false}
      />,
    );

    expect(screen.queryByTestId("source-list")).not.toBeInTheDocument();
  });

  it("renders an error alert when the message has an error", () => {
    const message = {
      role: "user",
      content: "What is Newton's second law?",
      error: "Failed to send message",
    };

    render(
      <ChatMessage
        message={message}
        tutorName="Physics Tutor"
        showSources={false}
        onRetry={vi.fn()}
        retryDisabled={false}
      />,
    );

    expect(screen.getByText("Failed to send message")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Retry" })).toBeInTheDocument();
  });

  it("renders the original user message above the error", () => {
    const message = {
      role: "user",
      content: "What is Newton's second law?",
      error: "Failed to send message",
    };

    render(
      <ChatMessage
        message={message}
        tutorName="Physics Tutor"
        showSources={false}
        onRetry={vi.fn()}
        retryDisabled={false}
      />,
    );

    expect(
      screen.getByText("What is Newton's second law?"),
    ).toBeInTheDocument();

    expect(screen.getByText("Failed to send message")).toBeInTheDocument();
  });

  it("calls onRetry with the message when Retry is clicked", async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();

    const message = {
      role: "user",
      content: "Try again",
      error: "Request failed",
    };

    render(
      <ChatMessage
        message={message}
        tutorName="Physics Tutor"
        showSources={false}
        onRetry={onRetry}
        retryDisabled={false}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Retry" }));

    expect(onRetry).toHaveBeenCalledTimes(1);
    expect(onRetry).toHaveBeenCalledWith(message);
  });

  it("disables the Retry button when retryDisabled is true", () => {
    const message = {
      role: "user",
      content: "Try again",
      error: "Request failed",
    };

    render(
      <ChatMessage
        message={message}
        tutorName="Physics Tutor"
        showSources={false}
        onRetry={vi.fn()}
        retryDisabled={true}
      />,
    );

    expect(screen.getByRole("button", { name: "Retry" })).toBeDisabled();
  });

  it("does not call onRetry when the Retry button is disabled", async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();

    const message = {
      role: "user",
      content: "Try again",
      error: "Request failed",
    };

    render(
      <ChatMessage
        message={message}
        tutorName="Physics Tutor"
        showSources={false}
        onRetry={onRetry}
        retryDisabled={true}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Retry" }));

    expect(onRetry).not.toHaveBeenCalled();
  });

  it("does not render a Retry button for a normal message", () => {
    render(
      <ChatMessage
        message={userMessage}
        tutorName="Physics Tutor"
        showSources={false}
        onRetry={vi.fn()}
        retryDisabled={false}
      />,
    );

    expect(
      screen.queryByRole("button", { name: "Retry" }),
    ).not.toBeInTheDocument();
  });

  it("supports GitHub-flavoured Markdown", () => {
    const message = {
      role: "assistant",
      content: "- First item\n- Second item",
      sources: [],
    };

    render(
      <ChatMessage
        message={message}
        tutorName="Physics Tutor"
        showSources={false}
        onRetry={vi.fn()}
        retryDisabled={false}
      />,
    );

    expect(screen.getByText("First item")).toBeInTheDocument();
    expect(screen.getByText("Second item")).toBeInTheDocument();

    expect(screen.getByRole("list")).toBeInTheDocument();
  });

  it("does not render the normal assistant message layout when there is an error", () => {
    const message = {
      role: "assistant",
      content: "Something went wrong",
      error: "Failed to generate response",
      sources: [{ title: "Source" }],
    };

    const { container } = render(
      <ChatMessage
        message={message}
        tutorName="Physics Tutor"
        showSources={true}
        onRetry={vi.fn()}
        retryDisabled={false}
      />,
    );

    expect(
      container.querySelector(".runtime-message-stack"),
    ).toBeInTheDocument();

    expect(
      container.querySelector(".runtime-message-row.user"),
    ).toBeInTheDocument();

    expect(screen.getByRole("button", { name: "Retry" })).toBeInTheDocument();
    expect(screen.queryByTestId("source-list")).not.toBeInTheDocument();
  });
});
