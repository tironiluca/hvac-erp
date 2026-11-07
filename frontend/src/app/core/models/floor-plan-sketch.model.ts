/**
 * Free-hand floor-plan sketch produced by the `hvac-floor-plan-sketch` canvas and stored as
 * `jsonb` on the quote / repair / installation. Coordinates are in the logical canvas space
 * (`width` × `height`) captured when the drawing was made; the PDF renderer and the editor both
 * scale from there. A sketch holds one or more {@link FloorPlanSheet}s (e.g. one per storey). The
 * server validates the shape (`FloorPlanSketchValidator`) but the schema lives here.
 */
export interface FloorPlanStroke {
  /** `#RRGGBB`. */
  color: string;
  /** Line width in logical canvas units. */
  width: number;
  /** 0–1. Highlighter strokes are wide + semi-transparent. */
  opacity: number;
  /** Polyline points as `[x, y]` pairs. */
  points: [number, number][];
}

/** One drawing surface within a sketch. */
export interface FloorPlanSheet {
  /** Optional label ("Piano terra", "Primo piano", …). */
  label?: string;
  strokes: FloorPlanStroke[];
}

export interface FloorPlanSketch {
  version: 1;
  /** Shared logical canvas size for every sheet. */
  width: number;
  height: number;
  sheets: FloorPlanSheet[];
}
