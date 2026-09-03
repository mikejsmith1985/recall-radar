// Checks a result card shows the record identity and all three parts of its rank explanation.
import { render, screen } from "@testing-library/react";
import { describeRank, ResultCard } from "./ResultCard";
import type { SearchHit } from "../api/client";

const hit: SearchHit = {
  documentId: 4123,
  chunkId: 4123,
  kind: "investigation",
  externalId: "EA17002",
  title: "Exhaust Odor in Passenger Cab",
  component: "ENGINE AND ENGINE COOLING:EXHAUST SYSTEM",
  filedOn: "2017-07-27",
  snippet: "During the EA17-002 investigation, the agency reviewed …",
  denseRank: 1,
  sparseRank: 3,
  fusedScore: 0.03226,
};

describe("ResultCard", () => {
  it("shows kind, title, identifier, component, date and snippet", () => {
    render(<ul><ResultCard hit={hit} position={1} /></ul>);

    const card = screen.getByTestId("result-card");
    expect(card.textContent).toContain("investigation");
    expect(card.textContent).toContain("Exhaust Odor in Passenger Cab");
    expect(card.textContent).toContain("EA17002");
    expect(card.textContent).toContain("2017-07-27");
    expect(card.textContent).toContain("agency reviewed");
  });

  it("shows meaning rank, keyword rank and fused score", () => {
    render(<ul><ResultCard hit={hit} position={1} /></ul>);

    expect(screen.getByTestId("dense-rank").textContent).toBe("Meaning rank 1");
    expect(screen.getByTestId("sparse-rank").textContent).toBe("Keyword rank 3");
    expect(screen.getByTestId("fused-score").textContent).toBe("Fused score 0.0323");
  });

  it("says when a method did not surface the record", () => {
    render(<ul><ResultCard hit={{ ...hit, denseRank: null, filedOn: null, title: "" }} position={2} /></ul>);

    expect(screen.getByTestId("dense-rank").textContent).toBe("Meaning: not in top results");
    expect(screen.getByTestId("dense-rank").className).toContain("absent");
    expect(screen.getByTestId("result-card").textContent).toContain("date unknown");
    expect(describeRank("Keyword", null)).toBe("Keyword: not in top results");
  });
});
