// Checks the known-pattern panel lists campaigns and cited records once each, and says when there are none.
import { render, screen } from "@testing-library/react";
import { collectCitedRecords, collectUncitedMatches, KnownPatternPanel } from "./KnownPatternPanel";
import type { AskResponse, CampaignMatch } from "../api/client";

const matches: CampaignMatch[] = [
  {
    documentId: 1,
    kind: "investigation",
    externalId: "EA17002",
    title: "Exhaust Odor in Passenger Cab",
    component: "ENGINE AND ENGINE COOLING",
    filedOn: "2017-01-01",
  },
  {
    documentId: 9,
    kind: "recall",
    externalId: "20V123000",
    title: "Rear camera image may not display",
    component: "BACK OVER PREVENTION",
    filedOn: "2020-03-04",
  },
];

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
  campaignMatches: [],
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

  it("lists a matched record the answer never quoted, kept apart from the cited ones", () => {
    render(<KnownPatternPanel response={{ ...response, campaignMatches: matches }} />);

    const uncited = screen.getByTestId("uncited-matches");
    expect(uncited.textContent).toContain("20V123000");
    expect(uncited.textContent).toContain("Rear camera image may not display");
    // EA17002 was cited, so it belongs in the verified list and nowhere else.
    expect(uncited.textContent).not.toContain("EA17002");
    expect(screen.getByTestId("cited-records").textContent).toContain("EA17002");
  });

  it("says plainly that an unquoted match was not verified", () => {
    // Without this, a retrieved record would look like evidence the answer stood behind.
    render(<KnownPatternPanel response={{ ...response, campaignMatches: matches }} />);

    expect(screen.getByTestId("uncited-caption").textContent).toContain("none of it has been verified");
  });

  it("shows the panel rather than the empty state when only unquoted matches exist", () => {
    render(
      <KnownPatternPanel
        response={{ ...response, citations: [], linkedCampaigns: [], campaignMatches: matches }}
      />,
    );

    expect(screen.queryByTestId("known-pattern-empty")).toBeNull();
    expect(screen.getByTestId("uncited-matches").querySelectorAll("li")).toHaveLength(2);
  });

  it("treats a response from an older server without the field as having no matches", () => {
    const { campaignMatches: _omitted, ...legacy } = response;

    expect(collectUncitedMatches(legacy as AskResponse)).toEqual([]);
  });
});
