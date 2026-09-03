// Checks the known-pattern panel lists campaigns and cited records once each, and says when there are none.
import { render, screen } from "@testing-library/react";
import { collectCitedRecords, KnownPatternPanel } from "./KnownPatternPanel";
import type { AskResponse } from "../api/client";

const response: AskResponse = {
  answerId: 1,
  answer: "Known pattern.",
  isKnownPattern: true,
  isGrounded: true,
  citations: [
    { documentId: 1, externalId: "EA17002", kind: "investigation", quote: "a", startOffset: 0, endOffset: 1 },
    { documentId: 2, externalId: "11760888", kind: "complaint", quote: "b", startOffset: 0, endOffset: 1 },
    { documentId: 1, externalId: "EA17002", kind: "investigation", quote: "c", startOffset: 0, endOffset: 1 },
    { documentId: 3, externalId: "19V435000", kind: "recall", quote: "d", startOffset: 0, endOffset: 1 },
  ],
  droppedCitationCount: 0,
  linkedCampaigns: ["17V000000"],
  retrievedDocumentIds: [1, 2, 3],
};

describe("KnownPatternPanel", () => {
  it("lists linked campaigns and each cited recall or investigation once, skipping complaints", () => {
    render(<KnownPatternPanel response={response} />);

    expect(screen.getByTestId("linked-campaigns").textContent).toContain("17V000000");
    const cited = screen.getByTestId("cited-records");
    expect(cited.textContent).toContain("EA17002");
    expect(cited.textContent).toContain("19V435000");
    expect(cited.textContent).not.toContain("11760888");
    expect(cited.querySelectorAll("li")).toHaveLength(2);
  });

  it("says so when nothing was linked", () => {
    render(<KnownPatternPanel response={{ ...response, citations: [], linkedCampaigns: [] }} />);

    expect(screen.getByTestId("known-pattern-empty")).toBeTruthy();
  });

  it("keeps citation order when collecting records", () => {
    expect(collectCitedRecords(response).map((record) => record.externalId)).toEqual(["EA17002", "19V435000"]);
  });
});
