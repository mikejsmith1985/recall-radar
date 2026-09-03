// Checks the search box only submits trimmed text of a usable length.
import { fireEvent, render, screen } from "@testing-library/react";
import { SearchBox } from "./SearchBox";

function renderBox(isDisabled = false) {
  const submitted: string[] = [];
  render(
    <SearchBox label="Symptom" placeholder="Describe it" submitLabel="Search" isDisabled={isDisabled} onSubmit={(text) => submitted.push(text)} />,
  );
  return submitted;
}

describe("SearchBox", () => {
  it("submits the trimmed text", () => {
    const submitted = renderBox();

    fireEvent.change(screen.getByLabelText("Symptom"), { target: { value: "  exhaust smell  " } });
    fireEvent.submit(screen.getByTestId("search-box"));

    expect(submitted).toEqual(["exhaust smell"]);
  });

  it("keeps the button disabled until the text is long enough", () => {
    renderBox();
    const button = screen.getByRole("button", { name: "Search" }) as HTMLButtonElement;

    expect(button.disabled).toBe(true);
    fireEvent.change(screen.getByLabelText("Symptom"), { target: { value: "e" } });
    expect(button.disabled).toBe(true);
    fireEvent.change(screen.getByLabelText("Symptom"), { target: { value: "ex" } });
    expect(button.disabled).toBe(false);
  });

  it("does not submit while disabled, even with text present", () => {
    const submitted = renderBox(true);

    fireEvent.change(screen.getByLabelText("Symptom"), { target: { value: "exhaust" } });
    fireEvent.submit(screen.getByTestId("search-box"));

    expect(submitted).toEqual([]);
  });
});
