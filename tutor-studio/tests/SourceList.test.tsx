import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import SourceList from "../src/features/user/components/SourceList.jsx";

vi.mock("../src/features/user/utils/aiTutorUtils.js", () => ({
  sourcePercent: vi.fn((score: number) => `${Math.round(score * 100)}%`),
}));

describe("SourceList", () => {
  const sources = [
    {
      chunkId: "chunk-1",
      documentName: "Introduction to React",
      chunkIndex: 3,
      score: 0.92,
    },
    {
      chunkId: "chunk-2",
      documentName: "TypeScript Guide",
      chunkIndex: 7,
      score: 0.81,
    },
  ];

  async function openSources() {
    const user = userEvent.setup();

    await user.click(
      screen.getByText("2 sources used"),
    );
  }

  it("renders nothing when sources are undefined", () => {
    const { container } = render(
      <SourceList sources={undefined} />,
    );

    expect(container).toBeEmptyDOMElement();
  });

  it("renders nothing when sources are empty", () => {
    const { container } = render(
      <SourceList sources={[]} />,
    );

    expect(container).toBeEmptyDOMElement();
  });

  it("renders the singular source label", () => {
    render(
      <SourceList
        sources={[
          {
            chunkId: "chunk-1",
            documentName: "React Guide",
            chunkIndex: 2,
            score: 0.95,
          },
        ]}
      />,
    );

    expect(
      screen.getByText("1 source used"),
    ).toBeInTheDocument();
  });

  it("renders the plural source label", () => {
    render(
      <SourceList sources={sources} />,
    );

    expect(
      screen.getByText("2 sources used"),
    ).toBeInTheDocument();
  });

  it("renders source details when the list is opened", async () => {
    render(
      <SourceList sources={sources} />,
    );

    await openSources();

    expect(
      screen.getByText("Introduction to React"),
    ).toBeInTheDocument();

    expect(
      screen.getByText("TypeScript Guide"),
    ).toBeInTheDocument();

    expect(
      screen.getByText("Chunk 3"),
    ).toBeInTheDocument();

    expect(
      screen.getByText("Chunk 7"),
    ).toBeInTheDocument();
  });

  it("uses the fallback document name when documentName is missing", async () => {
    render(
      <SourceList
        sources={[
          {
            chunkId: "chunk-1",
            documentName: "",
            chunkIndex: 4,
            score: 0.88,
          },
        ]}
      />,
    );

    const user = userEvent.setup();

    await user.click(
      screen.getByText("1 source used"),
    );

    expect(
      screen.getByText("Tutor document"),
    ).toBeInTheDocument();
  });

  it("uses the fallback document name when documentName is undefined", async () => {
    render(
      <SourceList
        sources={[
          {
            chunkId: "chunk-1",
            chunkIndex: 4,
            score: 0.88,
          },
        ]}
      />,
    );

    const user = userEvent.setup();

    await user.click(
      screen.getByText("1 source used"),
    );

    expect(
      screen.getByText("Tutor document"),
    ).toBeInTheDocument();
  });

  it("renders the similarity percentage for each source", async () => {
    render(
      <SourceList sources={sources} />,
    );

    await openSources();

    expect(
      screen.getByText("Similarity 92%"),
    ).toBeInTheDocument();

    expect(
      screen.getByText("Similarity 81%"),
    ).toBeInTheDocument();
  });

  it("calls sourcePercent with each source score", async () => {
    const { sourcePercent } = await import(
      "../src/features/user/utils/aiTutorUtils.js"
    );

    render(
      <SourceList sources={sources} />,
    );

    await openSources();

    expect(sourcePercent).toHaveBeenCalledWith(0.92);
    expect(sourcePercent).toHaveBeenCalledWith(0.81);
  });

  it("renders all provided sources", async () => {
    const manySources = [
      {
        chunkId: "chunk-1",
        documentName: "Document One",
        chunkIndex: 1,
        score: 0.9,
      },
      {
        chunkId: "chunk-2",
        documentName: "Document Two",
        chunkIndex: 2,
        score: 0.8,
      },
      {
        chunkId: "chunk-3",
        documentName: "Document Three",
        chunkIndex: 3,
        score: 0.7,
      },
    ];

    render(
      <SourceList sources={manySources} />,
    );

    const user = userEvent.setup();

    await user.click(
      screen.getByText("3 sources used"),
    );

    expect(
      screen.getByText("Document One"),
    ).toBeInTheDocument();

    expect(
      screen.getByText("Document Two"),
    ).toBeInTheDocument();

    expect(
      screen.getByText("Document Three"),
    ).toBeInTheDocument();
  });

  it("renders the source list with the expected CSS classes", async () => {
    const { container } = render(
      <SourceList sources={sources} />,
    );

    expect(
      container.querySelector(".runtime-sources"),
    ).toBeInTheDocument();

    const user = userEvent.setup();

    await user.click(
      screen.getByText("2 sources used"),
    );

    expect(
      container.querySelector(".runtime-source-list"),
    ).toBeInTheDocument();

    expect(
      container.querySelectorAll(".runtime-source-row"),
    ).toHaveLength(2);
  });
});
