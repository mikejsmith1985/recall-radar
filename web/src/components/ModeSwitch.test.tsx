// Checks the mode switch disables embedding-backed modes, and explains why, when embeddings are off.
import { fireEvent, render, screen } from "@testing-library/react";
import { chooseDefaultMode, EmbeddingsUnavailableExplanation, ModeSwitch } from "./ModeSwitch";
import type { RetrievalMode } from "../api/client";

describe("ModeSwitch", () => {
  it("offers all three modes when embeddings are available", () => {
    render(<ModeSwitch mode="hybrid" isEmbeddingsAvailable onChange={() => undefined} />);

    for (const mode of ["dense", "sparse", "hybrid"]) {
      expect((screen.getByTestId(`mode-${mode}`) as HTMLButtonElement).disabled).toBe(false);
    }
    expect(screen.getByTestId("mode-hybrid").getAttribute("aria-checked")).toBe("true");
    expect(screen.queryByTestId("mode-explanation")).toBeNull();
  });

  it("disables meaning and combined modes with an explanation when embeddings are unavailable", () => {
    render(<ModeSwitch mode="sparse" isEmbeddingsAvailable={false} onChange={() => undefined} />);

    expect((screen.getByTestId("mode-dense") as HTMLButtonElement).disabled).toBe(true);
    expect((screen.getByTestId("mode-hybrid") as HTMLButtonElement).disabled).toBe(true);
    expect((screen.getByTestId("mode-sparse") as HTMLButtonElement).disabled).toBe(false);
    expect(screen.getByTestId("mode-explanation").textContent).toBe(EmbeddingsUnavailableExplanation);
  });

  it("reports the clicked mode", () => {
    const chosen: RetrievalMode[] = [];
    render(<ModeSwitch mode="hybrid" isEmbeddingsAvailable onChange={(mode) => chosen.push(mode)} />);

    fireEvent.click(screen.getByTestId("mode-sparse"));

    expect(chosen).toEqual(["sparse"]);
  });

  it("defaults to combined ranking only when embeddings exist", () => {
    expect(chooseDefaultMode(true)).toBe("hybrid");
    expect(chooseDefaultMode(false)).toBe("sparse");
  });
});
