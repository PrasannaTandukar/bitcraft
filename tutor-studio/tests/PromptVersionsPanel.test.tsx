import { describe, expect, it, vi, beforeEach } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import PromptVersionsPanel from "../src/features/editor/components/PromptVersionsPanel";
import { createPromptVersion } from "../src/features/editor/hooks/tutorApi";

// Mock API
vi.mock("../src/features/editor/hooks/tutorApi", () => ({
    createPromptVersion: vi.fn(),
}));

// Mock utilities
vi.mock("../src/features/editor/hooks/tutorUtils", () => ({
    formatDateTime: vi.fn((date) => `Formatted ${date}`),
}));

// Mock API error helper
vi.mock("../src/api/client.js", () => ({
    getApiErrorDescription: vi.fn(
        (_error, fallback) => fallback
    ),
}));

describe("PromptVersionsPanel", () => {
    const prompts = [
        {
            id: "prompt-1",
            versionNumber: 1,
            name: "Initial Tutor Prompt",
            createdUtc: "2026-09-01T10:00:00Z",
            notes: "Initial version",
            content: "You are an AI tutor.",
        },
        {
            id: "prompt-2",
            versionNumber: 2,
            name: "Updated Tutor Prompt",
            createdUtc: "2026-09-02T10:00:00Z",
            notes: "Improved teaching style",
            content: "You are a helpful robotics tutor.",
        },
    ];

    const defaultProps = {
        tutorId: "tutor-123",
        prompts,
        activePromptVersionId: "prompt-2",
        canManage: true,
        onChanged: vi.fn(),
    };

    beforeEach(() => {
        vi.clearAllMocks();
        vi.mocked(createPromptVersion).mockResolvedValue(undefined);
    });

    it("renders the Prompt versions heading", () => {
        render(<PromptVersionsPanel {...defaultProps} />);

        expect(
            screen.getByRole("heading", { name: /prompt versions/i })
        ).toBeInTheDocument();
    });

    it("renders the description", () => {
        render(<PromptVersionsPanel {...defaultProps} />);

        expect(
            screen.getByText(
                /immutable tutor instruction history/i
            )
        ).toBeInTheDocument();
    });

    it("renders the immutable prompt information alert", () => {
        render(<PromptVersionsPanel {...defaultProps} />);

        expect(
            screen.getByText(/prompt versions are immutable/i)
        ).toBeInTheDocument();

        expect(
            screen.getByText(
                /create a new version to make changes/i
            )
        ).toBeInTheDocument();
    });

    it("renders the Create prompt version button when canManage is true", () => {
        render(<PromptVersionsPanel {...defaultProps} />);

        expect(
            screen.getByRole("button", {
                name: /create prompt version/i,
            })
        ).toBeInTheDocument();
    });

    it("does not render the create button when canManage is false", () => {
        render(
            <PromptVersionsPanel
                {...defaultProps}
                canManage={false}
            />
        );

        expect(
            screen.queryByRole("button", {
                name: /create prompt version/i,
            })
        ).not.toBeInTheDocument();
    });

    it("renders all prompt versions", () => {
        render(<PromptVersionsPanel {...defaultProps} />);

        expect(
            screen.getByText(/Prompt v1 · Initial Tutor Prompt/)
        ).toBeInTheDocument();

        expect(
            screen.getByText(/Prompt v2 · Updated Tutor Prompt/)
        ).toBeInTheDocument();
    });

    it("marks the active prompt version as Legacy active", () => {
        render(<PromptVersionsPanel {...defaultProps} />);

        expect(
            screen.getByText("Legacy active")
        ).toBeInTheDocument();
    });

    it("does not mark inactive prompt versions as active", () => {
        render(
            <PromptVersionsPanel
                {...defaultProps}
                activePromptVersionId="prompt-1"
            />
        );

        const activeTags = screen.getAllByText("Legacy active");

        expect(activeTags).toHaveLength(1);
    });

    it("shows prompt details when a prompt version is expanded", async () => {
        render(<PromptVersionsPanel {...defaultProps} />);

        const prompt = screen.getByText(
            /Prompt v1 · Initial Tutor Prompt/
        );

        fireEvent.click(prompt);

        expect(
            await screen.findByText("Prompt content")
        ).toBeInTheDocument();

        expect(
            screen.getByText("Notes: Initial version")
        ).toBeInTheDocument();

        expect(
            screen.getByText(/Created 9\/1\/2026, 6:00:00 PM/)
        ).toBeInTheDocument();

        expect(
            screen.getByText("You are an AI tutor.")
        ).toBeInTheDocument();
    });

    it("renders Empty when there are no prompt versions", () => {
        render(
            <PromptVersionsPanel
                {...defaultProps}
                prompts={[]}
            />
        );

        expect(
            screen.getByText("No prompt versions")
        ).toBeInTheDocument();
    });

    it("renders Create first prompt when there are no prompts and user can manage", () => {
        render(
            <PromptVersionsPanel
                {...defaultProps}
                prompts={[]}
                canManage={true}
            />
        );

        expect(
            screen.getByRole("button", {
                name: /create first prompt/i,
            })
        ).toBeInTheDocument();
    });

    it("does not render Create first prompt when user cannot manage", () => {
        render(
            <PromptVersionsPanel
                {...defaultProps}
                prompts={[]}
                canManage={false}
            />
        );

        expect(
            screen.queryByRole("button", {
                name: /create first prompt/i,
            })
        ).not.toBeInTheDocument();
    });

    it("opens the create prompt drawer", async () => {
        render(<PromptVersionsPanel {...defaultProps} />);

        fireEvent.click(
            screen.getByRole("button", {
                name: /create prompt version/i,
            })
        );

        expect(
            await screen.getByRole('button', { name: /create prompt version/i })
        ).toBeInTheDocument();

        expect(
            screen.getByText("Structured Builder")
        ).toBeInTheDocument();

        expect(
            screen.getByText("Raw Prompt Editor")
        ).toBeInTheDocument();
    });

    it("renders structured builder fields by default", async () => {
        render(<PromptVersionsPanel {...defaultProps} />);

        fireEvent.click(
            screen.getByRole("button", {
                name: /create prompt version/i,
            })
        );

        expect(
            await screen.findByText("System Steering & Persona")
        ).toBeInTheDocument();

        expect(
            screen.getByText("Tone & Teaching Style")
        ).toBeInTheDocument();

        expect(
            screen.getByText(
                "Pedagogical Guardrails & Boundaries"
            )
        ).toBeInTheDocument();

        expect(
            screen.getByText(
                "Response Structure & Output Format"
            )
        ).toBeInTheDocument();

        expect(
            screen.getByText("Assembled Prompt Preview")
        ).toBeInTheDocument();
    });

    it("generates assembled prompt content from structured fields", async () => {
        render(<PromptVersionsPanel {...defaultProps} />);

        fireEvent.click(
            screen.getByRole("button", {
                name: /create prompt version/i,
            })
        );

        const steeringInput = screen.getByPlaceholderText(
            /You are an expert AI tutor specializing in Robotics/i
        );

        fireEvent.change(steeringInput, {
            target: {
                value: "You are a robotics tutor.",
            },
        });

        const preview = screen.getByRole("textbox", {
            name: /assembled prompt preview/i,
        });

        await waitFor(() => {
            expect(preview).toHaveValue(
                "## Role & Steering\nYou are a robotics tutor."
            );
        });
    });

    it("generates all structured prompt sections", async () => {
        render(<PromptVersionsPanel {...defaultProps} />);

        fireEvent.click(
            screen.getByRole("button", {
                name: /create prompt version/i,
            })
        );

        fireEvent.change(
            screen.getByPlaceholderText(
                /You are an expert AI tutor specializing in Robotics/i
            ),
            {
                target: {
                    value: "You are a robotics tutor.",
                },
            }
        );

        fireEvent.change(
            screen.getByPlaceholderText(
                /Adopt a supportive, Socratic teaching style/i
            ),
            {
                target: {
                    value: "Use a Socratic teaching style.",
                },
            }
        );

        fireEvent.change(
            screen.getByPlaceholderText(
                /Never solve graded homework problems directly/i
            ),
            {
                target: {
                    value: "Do not directly solve graded work.",
                },
            }
        );

        fireEvent.change(
            screen.getByPlaceholderText(
                /Format equations using LaTeX/i
            ),
            {
                target: {
                    value: "Use Markdown formatting.",
                },
            }
        );

        const preview = screen.getByRole("textbox", {
            name: /assembled prompt preview/i,
        });

        await waitFor(() => {
            expect(preview).toHaveValue(
                "## Role & Steering\nYou are a robotics tutor.\n\n" +
                "## Tone & Style Guidelines\nUse a Socratic teaching style.\n\n" +
                "## Pedagogical Guardrails & Boundaries\nDo not directly solve graded work.\n\n" +
                "## Response Format & Output Structure\nUse Markdown formatting."
            );
        });
    });

    it("switches to raw prompt editor", async () => {
        render(<PromptVersionsPanel {...defaultProps} />);

        fireEvent.click(
            screen.getByRole("button", {
                name: /create prompt version/i,
            })
        );

        fireEvent.click(
            screen.getByText("Raw Prompt Editor")
        );

        expect(
            await screen.findByText("Prompt content")
        ).toBeInTheDocument();

        expect(
            screen.getByPlaceholderText(
                "You are an expert AI tutor..."
            )
        ).toBeInTheDocument();

        expect(
            screen.queryByText("System Steering & Persona")
        ).not.toBeInTheDocument();
    });

    it("shows raw prompt description when raw mode is selected", async () => {
        render(<PromptVersionsPanel {...defaultProps} />);

        fireEvent.click(
            screen.getByRole("button", {
                name: /create prompt version/i,
            })
        );

        fireEvent.click(
            screen.getByText("Raw Prompt Editor")
        );

        expect(
            await screen.findByText(
                "Direct markdown/template editing"
            )
        ).toBeInTheDocument();
    });

    it("creates a prompt version using raw prompt mode", async () => {
        const onChanged = vi.fn();

        render(
            <PromptVersionsPanel
                {...defaultProps}
                onChanged={onChanged}
            />
        );

        fireEvent.click(
            screen.getByRole("button", {
                name: /create prompt version/i,
            })
        );

        fireEvent.click(
            screen.getByText("Raw Prompt Editor")
        );

        fireEvent.change(
            screen.getByPlaceholderText(
                "e.g. Socratic Robotics Guide v2"
            ),
            {
                target: {
                    value: "Test Prompt v3",
                },
            }
        );

        fireEvent.change(
            screen.getByPlaceholderText(
                "You are an expert AI tutor..."
            ),
            {
                target: {
                    value: "You are a test AI tutor.",
                },
            }
        );

        fireEvent.click(
            screen.getByRole("button", {
                name: /create immutable version/i,
            })
        );

        await waitFor(() => {
            expect(createPromptVersion).toHaveBeenCalledWith(
                "tutor-123",
                {
                    name: "Test Prompt v3",
                    content: "You are a test AI tutor.",
                    notes: null,
                    setAsActive: false,
                }
            );
        });

        expect(onChanged).toHaveBeenCalledTimes(1);
    });

    it("creates a prompt version with notes", async () => {
        render(<PromptVersionsPanel {...defaultProps} />);

        fireEvent.click(
            screen.getByRole("button", {
                name: /create prompt version/i,
            })
        );

        fireEvent.click(
            screen.getByText("Raw Prompt Editor")
        );

        fireEvent.change(
            screen.getByPlaceholderText(
                "e.g. Socratic Robotics Guide v2"
            ),
            {
                target: {
                    value: "Version 3",
                },
            }
        );

        fireEvent.change(
            screen.getByPlaceholderText(
                "You are an expert AI tutor..."
            ),
            {
                target: {
                    value: "Test prompt content.",
                },
            }
        );

        fireEvent.change(
            screen.getByPlaceholderText(
                "What changed in this prompt revision?"
            ),
            {
                target: {
                    value: "Updated teaching instructions.",
                },
            }
        );

        fireEvent.click(
            screen.getByRole("button", {
                name: /create immutable version/i,
            })
        );

        await waitFor(() => {
            expect(createPromptVersion).toHaveBeenCalledWith(
                "tutor-123",
                {
                    name: "Version 3",
                    content: "Test prompt content.",
                    notes: "Updated teaching instructions.",
                    setAsActive: false,
                }
            );
        });
    });

    it("creates a prompt version with setAsActive enabled", async () => {
        render(<PromptVersionsPanel {...defaultProps} />);

        fireEvent.click(
            screen.getByRole("button", {
                name: /create prompt version/i,
            })
        );

        fireEvent.click(
            screen.getByText("Raw Prompt Editor")
        );

        fireEvent.change(
            screen.getByPlaceholderText(
                "e.g. Socratic Robotics Guide v2"
            ),
            {
                target: {
                    value: "Active Prompt",
                },
            }
        );

        fireEvent.change(
            screen.getByPlaceholderText(
                "You are an expert AI tutor..."
            ),
            {
                target: {
                    value: "Active prompt content.",
                },
            }
        );

        fireEvent.click(
            screen.getByRole("checkbox", {
                name: /set as legacy active prompt/i,
            })
        );

        fireEvent.click(
            screen.getByRole("button", {
                name: /create immutable version/i,
            })
        );

        await waitFor(() => {
            expect(createPromptVersion).toHaveBeenCalledWith(
                "tutor-123",
                {
                    name: "Active Prompt",
                    content: "Active prompt content.",
                    notes: null,
                    setAsActive: true,
                }
            );
        });
    });

    it("trims the prompt name, content, and notes", async () => {
        render(<PromptVersionsPanel {...defaultProps} />);

        fireEvent.click(
            screen.getByRole("button", {
                name: /create prompt version/i,
            })
        );

        fireEvent.click(
            screen.getByText("Raw Prompt Editor")
        );

        fireEvent.change(
            screen.getByPlaceholderText(
                "e.g. Socratic Robotics Guide v2"
            ),
            {
                target: {
                    value: "   Test Prompt   ",
                },
            }
        );

        fireEvent.change(
            screen.getByPlaceholderText(
                "You are an expert AI tutor..."
            ),
            {
                target: {
                    value: "   Test content   ",
                },
            }
        );

        fireEvent.change(
            screen.getByPlaceholderText(
                "What changed in this prompt revision?"
            ),
            {
                target: {
                    value: "   Updated prompt   ",
                },
            }
        );

        fireEvent.click(
            screen.getByRole("button", {
                name: /create immutable version/i,
            })
        );

        await waitFor(() => {
            expect(createPromptVersion).toHaveBeenCalledWith(
                "tutor-123",
                {
                    name: "Test Prompt",
                    content: "Test content",
                    notes: "Updated prompt",
                    setAsActive: false,
                }
            );
        });
    });

    it("displays an error when prompt creation fails", async () => {
        vi.mocked(createPromptVersion).mockRejectedValue(
            new Error("API request failed")
        );

        render(<PromptVersionsPanel {...defaultProps} />);

        fireEvent.click(
            screen.getByRole("button", {
                name: /create prompt version/i,
            })
        );

        fireEvent.click(
            screen.getByText("Raw Prompt Editor")
        );

        fireEvent.change(
            screen.getByPlaceholderText(
                "e.g. Socratic Robotics Guide v2"
            ),
            {
                target: {
                    value: "Failed Prompt",
                },
            }
        );

        fireEvent.change(
            screen.getByPlaceholderText(
                "You are an expert AI tutor..."
            ),
            {
                target: {
                    value: "Prompt content.",
                },
            }
        );

        fireEvent.click(
            screen.getByRole("button", {
                name: /create immutable version/i,
            })
        );

        expect(
            await screen.findByText(
                "Could not create the prompt version."
            )
        ).toBeInTheDocument();
    });

    it("closes the drawer when Cancel is clicked", async () => {
        render(<PromptVersionsPanel {...defaultProps} />);

        fireEvent.click(
            screen.getByRole("button", {
                name: /create prompt version/i,
            })
        );

        expect(
            await screen.getByRole('button', { name: /create prompt version/i })
        ).toBeInTheDocument();

        fireEvent.click(
            screen.getByRole("button", {
                name: /cancel/i,
            })
        );

        await waitFor(() => {
            expect(
                screen.queryByPlaceholderText(
                    "e.g. Socratic Robotics Guide v2"
                )
            ).not.toBeInTheDocument();
        });
    });

    it("does not allow the drawer to open when canManage is false", () => {
        render(
            <PromptVersionsPanel
                {...defaultProps}
                canManage={false}
            />
        );

        expect(
            screen.queryByText("Create prompt version")
        ).not.toBeInTheDocument();
    });

    it("uses the tutor ID when creating a prompt version", async () => {
        render(
            <PromptVersionsPanel
                {...defaultProps}
                tutorId="different-tutor-id"
            />
        );

        fireEvent.click(
            screen.getByRole("button", {
                name: /create prompt version/i,
            })
        );

        fireEvent.click(
            screen.getByText("Raw Prompt Editor")
        );

        fireEvent.change(
            screen.getByPlaceholderText(
                "e.g. Socratic Robotics Guide v2"
            ),
            {
                target: {
                    value: "Test Version",
                },
            }
        );

        fireEvent.change(
            screen.getByPlaceholderText(
                "You are an expert AI tutor..."
            ),
            {
                target: {
                    value: "Test content.",
                },
            }
        );

        fireEvent.click(
            screen.getByRole("button", {
                name: /create immutable version/i,
            })
        );

        await waitFor(() => {
            expect(createPromptVersion).toHaveBeenCalledWith(
                "different-tutor-id",
                expect.any(Object)
            );
        });
    });
});
