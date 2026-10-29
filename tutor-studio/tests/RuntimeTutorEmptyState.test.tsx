import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import RuntimeTutorEmptyState from "../src/features/user/components/RuntimeTutorEmptyState";

describe("RuntimeTutorEmptyState", () => {
  it("shows tutor-specific readiness reason and routes to knowledge setup", async () => {
    const user = userEvent.setup();
    const onConfigure = vi.fn();
    render(<RuntimeTutorEmptyState
      canManage
      onConfigure={onConfigure}
      readiness={[{
        tutorId: "tutor-1",
        tutorName: "RoboTutor",
        isChatReady: false,
        reasons: [{ code: "KnowledgeNotVectorized", message: "4 attached documents still require vectorization." }],
      }]}
    />);

    expect(screen.getByText("No tutors are ready for chat")).toBeInTheDocument();
    expect(screen.getByText("RoboTutor")).toBeInTheDocument();
    expect(screen.getByText("4 attached documents still require vectorization.")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Configure tutor" }));
    expect(onConfigure).toHaveBeenCalledWith("tutor-1", "knowledge");
  });

  it("keeps unavailable-tutor details out of view-only users' controls", () => {
    render(<RuntimeTutorEmptyState
      canManage={false}
      onConfigure={vi.fn()}
      readiness={[{
        tutorId: "tutor-1",
        tutorName: "RoboTutor",
        isChatReady: false,
        reasons: [{ code: "MissingEmbeddingModel", message: "Query embedding is missing." }],
      }]}
    />);

    expect(screen.getByText("Query embedding is missing.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Configure tutor" })).not.toBeInTheDocument();
  });
});
