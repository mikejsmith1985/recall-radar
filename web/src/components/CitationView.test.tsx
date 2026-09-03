// Checks the citation highlight covers exactly the [start, end) range the API verified.
import { render, screen } from "@testing-library/react";
import { CitationView, splitForHighlight } from "./CitationView";
import type { Citation, SourceDocument } from "../api/client";

const document: SourceDocument = {
  id: 4123,
  kind: "investigation",
  externalId: "EA17002",
  title: "Exhaust Odor in Passenger Cab",
  component: "STRUCTURE:BODY",
  filedOn: "2017-07-27",
  body: "During the EA17-002 investigation, the agency reviewed and analyzed complaints.",
  vehicleId: 1,
};

const quote = "the agency reviewed and analyzed";
const citation: Citation = {
  documentId: 4123,
  externalId: "EA17002",
  kind: "investigation",
  quote,
  startOffset: document.body.indexOf(quote),
  endOffset: document.body.indexOf(quote) + quote.length,
};

describe("splitForHighlight", () => {
  it("splits the body into before, highlighted and after segments", () => {
    const segments = splitForHighlight("abcdef", 2, 4);

    expect(segments).toEqual({ before: "ab", highlighted: "cd", after: "ef" });
  });

  it("highlights nothing when the range is outside the body", () => {
    expect(splitForHighlight("abc", 2, 10)).toEqual({ before: "abc", highlighted: "", after: "" });
    expect(splitForHighlight("abc", 2, 2)).toEqual({ before: "abc", highlighted: "", after: "" });
  });
});

describe("CitationView", () => {
  it("marks exactly the cited span inside the verbatim body", () => {
    render(<CitationView document={document} citation={citation} />);

    expect(screen.getByTestId("citation-highlight").textContent).toBe(citation.quote);
    expect(screen.getByTestId("record-body").textContent).toBe(document.body);
    expect(screen.getByTestId("citation-view").textContent).toContain("EA17002");
  });
});
