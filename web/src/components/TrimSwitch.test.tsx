// Checks the trim switch only offers a choice it can actually honour, and says what it leaves out.
import { fireEvent, render, screen } from "@testing-library/react";
import { describeOtherTrims, NoVinExplanation, TrimSwitch } from "./TrimSwitch";

describe("describeOtherTrims", () => {
  it("counts in words a person reads, singular included", () => {
    expect(describeOtherTrims(0)).toContain("Every record on file");
    expect(describeOtherTrims(1)).toBe("1 record on file is from another version of this model.");
    expect(describeOtherTrims(385)).toContain("385 records");
  });
});

describe("TrimSwitch", () => {
  it("offers no choice without a VIN, because there is nothing to narrow to", () => {
    render(<TrimSwitch trims="allTrims" vehicleTrim={null} otherTrims={0} onChange={() => undefined} />);

    expect(screen.queryByTestId("trim-switch")).toBeNull();
    expect(screen.getByTestId("trim-needs-vin").textContent).toBe(NoVinExplanation);
  });

  it("names the trim on the narrow option, rather than saying \"this trim\"", () => {
    // Somebody has to recognise their own truck in it.
    render(
      <TrimSwitch trims="thisTrim" vehicleTrim="SuperCrew-Raptor · 5.2L · 8 cyl" otherTrims={385} onChange={() => undefined} />,
    );

    expect(screen.getByTestId("trims-thisTrim").textContent).toBe("SuperCrew-Raptor · 5.2L · 8 cyl");
    expect(screen.getByTestId("trims-thisTrim").getAttribute("aria-checked")).toBe("true");
  });

  it("claims nothing before a search has counted", () => {
    // Zero would be a statement, and nothing has said it yet.
    render(<TrimSwitch trims="thisTrim" vehicleTrim="SuperCrew-Raptor" otherTrims={null} onChange={() => undefined} />);

    expect(screen.getByTestId("trim-switch")).toBeTruthy();
    expect(screen.queryByTestId("trim-explanation")).toBeNull();
  });

  it("says how many records the narrow search leaves out", () => {
    render(<TrimSwitch trims="thisTrim" vehicleTrim="SuperCrew-Raptor" otherTrims={385} onChange={() => undefined} />);

    expect(screen.getByTestId("trim-explanation").textContent).toContain("385 records");
  });

  it("reports the scope somebody picked", () => {
    const chosen: string[] = [];
    render(
      <TrimSwitch trims="thisTrim" vehicleTrim="SuperCrew-Raptor" otherTrims={385} onChange={(next) => chosen.push(next)} />,
    );

    fireEvent.click(screen.getByTestId("trims-allTrims"));

    expect(chosen).toEqual(["allTrims"]);
  });
});
