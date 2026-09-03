// Checks the answer panel makes grounding and dropped citations visible, never hidden.
import { fireEvent, render, screen } from "@testing-library/react";
import { AnswerPanel, describeDroppedCitations } from "./AnswerPanel";
import type { AskResponse, Citation } from "../api/client";

const citation: Citation = { documentId: 4123, externalId: "EA17002", kind: "investigation", quote: "the agency reviewed and analyzed", startOffset: 41, endOffset: 73 };

const grounded: AskResponse = {
  answerId: 17,
  answer: "Exhaust odor entering the passenger compartment is a documented pattern.",
  isKnownPattern: true,
  isGrounded: true,
  citations: [citation],
  droppedCitationCount: 1,
  linkedCampaigns: ["17V000000"],
  retrievedDocumentIds: [4123],
};

describe("AnswerPanel", () => {
  it("shows a grounded answer with its citations and the dropped count", () => {
    const opened: Citation[] = [];
    render(<AnswerPanel response={grounded} onOpenCitation={(chosen) => opened.push(chosen)} />);

    expect(screen.getByTestId("grounded-status").textContent).toBe("Grounded");
    expect(screen.getByTestId("known-pattern-status").textContent).toBe("Known pattern");
    expect(screen.getByTestId("answer-text").textContent).toContain("documented pattern");
    expect(screen.getByTestId("dropped-count").textContent).toContain("1 citation dropped");
    fireEvent.click(screen.getByTestId("citation-button"));
    expect(opened).toEqual([citation]);
  });

  it("labels an answer with no surviving citations as not grounded", () => {
    render(<AnswerPanel response={{ ...grounded, isGrounded: false, isKnownPattern: false, citations: [], droppedCitationCount: 2 }} onOpenCitation={() => undefined} />);

    expect(screen.getByTestId("grounded-status").textContent).toBe("Not grounded");
    expect(screen.getByTestId("known-pattern-status").textContent).toBe("No known pattern");
    expect(screen.queryByTestId("citation-list")).toBeNull();
    expect(screen.getByTestId("dropped-count").textContent).toContain("2 citations dropped");
  });

  it("states explicitly when nothing was dropped", () => {
    expect(describeDroppedCitations(0)).toContain("Every citation");
  });
});
