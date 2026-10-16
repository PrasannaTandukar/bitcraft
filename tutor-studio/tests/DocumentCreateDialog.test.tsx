import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import DocumentCreateDialog from "../src/features/editor/components/DocumentCreateDialog";
import { afterEach } from "vitest";
import { cleanup } from "@testing-library/react";

describe("DocumentCreateDialog", () => {

    afterEach(async () => {
    cleanup();
    // let antd's delayed state updates (Select focus/open) flush before teardown
    await new Promise((resolve) => setTimeout(resolve, 50));
    });
    const folders = [
        {
            id: "folder-1",
            name: "Folder One",
        },
        {
            id: "folder-2",
            name: "Folder Two",
        },
    ];

    const defaultProps = {
        folders,
        onClose: vi.fn(),
        onCreated: vi.fn(),
    };

    it("renders the dialog and form fields", () => {
        render(<DocumentCreateDialog {...defaultProps} />);

        expect(screen.getByRole("dialog")).toBeInTheDocument();
        expect(
            screen.getByRole("dialog", { name: /create document/i })
        ).toBeInTheDocument();

        expect(
            screen.getByPlaceholderText("My document")
        ).toBeInTheDocument();

        expect(
            screen.getByPlaceholderText(
                "Paste or type the document text here"
            )
        ).toBeInTheDocument();

        // Verify the placeholder text is visible to the user
        expect(screen.getByText('Root (no folder)')).toBeInTheDocument();

        expect(
            screen.getByRole("button", { name: /Create document/i })
        ).toBeInTheDocument();

        expect(
            screen.getByRole("button", { name: /cancel/i })
        ).toBeInTheDocument();
    });

    it("disables the Create button when the required fields are empty", () => {
        render(<DocumentCreateDialog {...defaultProps} />);

        const createButton = screen.getByRole("button", {
            name: /create document/i,
        });

        expect(createButton).toBeDisabled();
    });

    it("enables the Create button when name and text are provided", () => {
        render(<DocumentCreateDialog {...defaultProps} />);

        const nameInput = screen.getByPlaceholderText("My document");
        const textInput = screen.getByPlaceholderText(
            "Paste or type the document text here"
        );

        const createButton = screen.getByRole("button", {
            name: /create document/i,
        });

        fireEvent.change(nameInput, {
            target: { value: "Test Document" },
        });

        fireEvent.change(textInput, {
            target: { value: "This is test document content." },
        });

        expect(createButton).toBeEnabled();
    });

    it("calls onCreated with the document name, text, and null folder", async () => {
        const onCreated = vi.fn().mockResolvedValue(undefined);

        render(
            <DocumentCreateDialog
                {...defaultProps}
                onCreated={onCreated}
            />
        );

        fireEvent.change(screen.getByPlaceholderText("My document"), {
            target: { value: "Test Document" },
        });

        fireEvent.change(
            screen.getByPlaceholderText(
                "Paste or type the document text here"
            ),
            {
                target: { value: "Test document content." },
            }
        );

        fireEvent.click(
            screen.getByRole("button", { name: /create document/i })
        );

        await waitFor(() => {
            expect(onCreated).toHaveBeenCalledWith(
                "Test Document",
                "Test document content.",
                null
            );
        });
    });

    it("calls onClose after successfully creating a document", async () => {
        const onCreated = vi.fn().mockResolvedValue(undefined);
        const onClose = vi.fn();

        render(
            <DocumentCreateDialog
                {...defaultProps}
                onCreated={onCreated}
                onClose={onClose}
            />
        );

        fireEvent.change(screen.getByPlaceholderText("My document"), {
            target: { value: "Test Document" },
        });

        fireEvent.change(
            screen.getByPlaceholderText(
                "Paste or type the document text here"
            ),
            {
                target: { value: "Test document content." },
            }
        );

        fireEvent.click(
            screen.getByRole("button", { name: /create document/i })
        );

        await waitFor(() => {
            expect(onClose).toHaveBeenCalledTimes(1);
        });
    });

    it("trims whitespace from the document name", async () => {
        const onCreated = vi.fn().mockResolvedValue(undefined);

        render(
            <DocumentCreateDialog
                {...defaultProps}
                onCreated={onCreated}
            />
        );

        fireEvent.change(screen.getByPlaceholderText("My document"), {
            target: { value: "   Test Document   " },
        });

        fireEvent.change(
            screen.getByPlaceholderText(
                "Paste or type the document text here"
            ),
            {
                target: { value: "Document content." },
            }
        );

        fireEvent.click(
            screen.getByRole("button", { name: /create document/i })
        );

        await waitFor(() => {
            expect(onCreated).toHaveBeenCalledWith(
                "Test Document",
                "Document content.",
                null
            );
        });
    });

    it("allows selecting a folder", async () => {
        const onCreated = vi.fn().mockResolvedValue(undefined);

        render(
            <DocumentCreateDialog
                {...defaultProps}
                onCreated={onCreated}
            />
        );

        fireEvent.change(screen.getByPlaceholderText("My document"), {
            target: { value: "Test Document" },
        });

        fireEvent.change(
            screen.getByPlaceholderText(
                "Paste or type the document text here"
            ),
            {
                target: { value: "Document content." },
            }
        );

        const folderSelect = screen.getByRole("combobox");

        fireEvent.mouseDown(folderSelect);

        const folderOption = await screen.findByText("Folder One");

        fireEvent.click(folderOption);

        fireEvent.click(
            screen.getByRole("button", { name: /create document/i })
        );

        await waitFor(() => {
            expect(onCreated).toHaveBeenCalledWith(
                "Test Document",
                "Document content.",
                "folder-1"
            );
        });
    });

    it("displays an error when document creation fails", async () => {
        const onCreated = vi
            .fn()
            .mockRejectedValue(new Error("Failed to create document"));

        const onClose = vi.fn();

        render(
            <DocumentCreateDialog
                {...defaultProps}
                onCreated={onCreated}
                onClose={onClose}
            />
        );

        fireEvent.change(screen.getByPlaceholderText("My document"), {
            target: { value: "Test Document" },
        });

        fireEvent.change(
            screen.getByPlaceholderText(
                "Paste or type the document text here"
            ),
            {
                target: { value: "Document content." },
            }
        );

        fireEvent.click(
            screen.getByRole("button", { name: /create document/i })
        );

        expect(
            await screen.findByText("Failed to create document")
        ).toBeInTheDocument();

        expect(onClose).not.toHaveBeenCalled();
    });

    it("displays the default error message when creation fails without a message", async () => {
        const onCreated = vi.fn().mockRejectedValue({});

        render(
            <DocumentCreateDialog
                {...defaultProps}
                onCreated={onCreated}
            />
        );

        fireEvent.change(screen.getByPlaceholderText("My document"), {
            target: { value: "Test Document" },
        });

        fireEvent.change(
            screen.getByPlaceholderText(
                "Paste or type the document text here"
            ),
            {
                target: { value: "Document content." },
            }
        );

        fireEvent.click(
            screen.getByRole("button", { name: /create document/i })
        );

        expect(
            await screen.findByText(
                "The document could not be created."
            )
        ).toBeInTheDocument();
    });

    it("calls onClose when Cancel is clicked", () => {
        const onClose = vi.fn();

        render(
            <DocumentCreateDialog
                {...defaultProps}
                onClose={onClose}
            />
        );

        fireEvent.click(
            screen.getByRole("button", { name: /cancel/i })
        );

        expect(onClose).toHaveBeenCalledTimes(1);
    });

    it("disables the form while saving", async () => {
        let resolveCreation!: () => void;

        const onCreated = vi.fn(
            () =>
                new Promise<void>((resolve) => {
                    resolveCreation = resolve;
                })
        );

        render(
            <DocumentCreateDialog
                {...defaultProps}
                onCreated={onCreated}
            />
        );

        const nameInput = screen.getByPlaceholderText("My document");
        const textInput = screen.getByPlaceholderText(
            "Paste or type the document text here"
        );
        const folderSelect = screen.getByRole("combobox");

        fireEvent.change(nameInput, {
            target: { value: "Test Document" },
        });

        fireEvent.change(textInput, {
            target: { value: "Document content." },
        });

        fireEvent.click(
            screen.getByRole("button", { name: /create document/i })
        );

        await waitFor(() => {
            expect(nameInput).toBeDisabled();
            expect(textInput).toBeDisabled();
            expect(folderSelect).toBeDisabled();
        });

        resolveCreation();

        await waitFor(() => {
            expect(nameInput).not.toBeDisabled();
            expect(textInput).not.toBeDisabled();
        });
    });

    it("renders an empty folder list correctly", () => {
        render(
            <DocumentCreateDialog
                {...defaultProps}
                folders={[]}
            />
        );

        expect(screen.getByText('Root (no folder)')).toBeInTheDocument();
    });
});
