// Checks the ask page shows a grounded answer and opens a citation with its span highlighted.
import { fireEvent, render, screen } from "@testing-library/react";
import type { ApiClient, AskRequest, AskResponse, SourceDocument } from "../api/client";
import { AskPage } from "./AskPage";

const body = "During the EA17-002 investigation, the agency reviewed and analyzed complaints.";
const quote = "the agency reviewed and analyzed";
const document: SourceDocument = { id: 4123, kind: "investigation", externalId: "EA17002", title: "Exhaust Odor in Passenger Cab", component: "STRUCTURE:BODY", filedOn: "2017-07-27", body, vehicleId: 1 };
const grounded: AskResponse = {
  answerId: 17,
  answer: "Exhaust odor in the cabin is a documented pattern.",
  isKnownPattern: true,
  isGrounded: true,
  citations: [{ documentId: 4123, externalId: "EA17002", kind: "investigation", quote, startOffset: body.indexOf(quote), endOffset: body.indexOf(quote) + quote.length }],
  droppedCitationCount: 1,
  linkedCampaigns: ["17V000000"],
  retrievedDocumentIds: [4123],
  campaignMatches: [],
};

function createClient(ask: (request: AskRequest) => Promise<AskResponse>): ApiClient {
  const unsupported = () => Promise.reject(new Error("not used in this test"));
  return { getHealth: unsupported, listVehicles: unsupported, search: unsupported, ask, getDocument: () => Promise.resolve(document), getEval: unsupported };
}

function askQuestion(text: string) {
  fireEvent.change(screen.getByLabelText("Symptom"), { target: { value: text } });
  fireEvent.submit(screen.getByTestId("search-box"));
}

describe("AskPage", () => {
  it("asks for a vehicle first", () => {
    render(<AskPage client={createClient(() => Promise.reject(new Error()))} vehicleId={null} isEmbeddingsAvailable isAnsweringAvailable />);

    expect(screen.getByTestId("ask-needs-vehicle")).toBeTruthy();
  });

  it("sends the question in the strongest available mode and shows the answer with its links", async () => {
    const requests: AskRequest[] = [];
    const client = createClient((request) => {
      requests.push(request);
      return Promise.resolve(grounded);
    });
    render(<AskPage client={client} vehicleId={1} isEmbeddingsAvailable={false} isAnsweringAvailable />);

    askQuestion("exhaust smell inside the cabin");

    await screen.findByTestId("answer-panel");
    expect(requests).toEqual([{ vehicleId: 1, question: "exhaust smell inside the cabin", mode: "sparse" }]);
    expect(screen.getByTestId("grounded-status").textContent).toBe("Grounded");
    expect(screen.getByTestId("dropped-count").textContent).toContain("1 citation dropped");
    expect(screen.getByTestId("linked-campaigns").textContent).toContain("17V000000");
  });

  it("opens a citation and highlights the quoted span inside the record", async () => {
    render(<AskPage client={createClient(() => Promise.resolve(grounded))} vehicleId={1} isEmbeddingsAvailable isAnsweringAvailable />);
    askQuestion("exhaust smell");
    await screen.findByTestId("answer-panel");

    fireEvent.click(screen.getByTestId("citation-button"));

    expect((await screen.findByTestId("citation-highlight")).textContent).toBe("the agency reviewed and analyzed");
    expect(screen.getByTestId("record-body").textContent).toBe(body);
  });

  it("disables asking and explains when answering is unavailable", () => {
    render(<AskPage client={createClient(() => Promise.resolve(grounded))} vehicleId={1} isEmbeddingsAvailable isAnsweringAvailable={false} />);

    expect(screen.getByTestId("answering-unavailable")).toBeTruthy();
    expect((screen.getByLabelText("Symptom") as HTMLInputElement).disabled).toBe(true);
  });

  it("shows the API's explanation when the question fails", async () => {
    render(<AskPage client={createClient(() => Promise.reject(new Error("Answering is unavailable")))} vehicleId={1} isEmbeddingsAvailable isAnsweringAvailable />);

    askQuestion("exhaust smell");

    expect((await screen.findByTestId("ask-error")).textContent).toContain("Answering is unavailable");
  });
});
