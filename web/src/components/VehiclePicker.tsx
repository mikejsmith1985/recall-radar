// Lets the owner choose which of their vehicles every search and question is scoped to.
// Rendered as a radio group of buttons rather than a <select> so real pointer clicks can drive it.
import type { Vehicle } from "../api/client";

interface VehiclePickerProps {
  vehicles: Vehicle[];
  selectedVehicleId: number | null;
  onSelect: (vehicleId: number) => void;
}

function formatCounts(vehicle: Vehicle): string {
  const { complaint, recall, investigation } = vehicle.counts;
  return `${complaint} complaints · ${recall} recalls · ${investigation} investigations`;
}

export function VehiclePicker({ vehicles, selectedVehicleId, onSelect }: VehiclePickerProps) {
  if (vehicles.length === 0) {
    return (
      <p className="empty-state" data-testid="vehicle-picker-empty">
        No vehicles yet. Add one below and its NHTSA records load in the background.
      </p>
    );
  }

  return (
    <div className="vehicle-picker" role="radiogroup" aria-label="Vehicle" data-testid="vehicle-picker">
      {vehicles.map((vehicle) => {
        const isSelected = vehicle.id === selectedVehicleId;
        return (
          <button
            key={vehicle.id}
            type="button"
            role="radio"
            aria-checked={isSelected}
            data-testid={`vehicle-option-${vehicle.id}`}
            onClick={() => onSelect(vehicle.id)}
          >
            {vehicle.displayName}
            <small>{formatCounts(vehicle)}</small>
          </button>
        );
      })}
    </div>
  );
}
