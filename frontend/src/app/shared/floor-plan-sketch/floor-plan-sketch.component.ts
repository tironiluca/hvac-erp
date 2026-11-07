import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  effect,
  forwardRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TranslocoModule } from '@jsverse/transloco';
import { FloorPlanSketch, FloorPlanStroke } from '../../core/models/floor-plan-sketch.model';

/**
 * Resolution-independent logical drawing space. Every stroke point and width is stored in these
 * units regardless of the on-screen canvas size or zoom level, so a sketch renders identically in
 * the form, in the PDF and after a window resize. 3:2 landscape, matching the SCSS `aspect-ratio`.
 */
const LOGICAL_W = 1000;
const LOGICAL_H = 667;

const MAX_SHEETS = 8;
const MIN_ZOOM = 1;
const MAX_ZOOM = 6;
const ZOOM_STEP = 1.35;

type Mode = 'pen' | 'highlighter' | 'eraser' | 'pan';
type SizeKey = 'thin' | 'medium' | 'thick';

const PEN_WIDTHS: Record<SizeKey, number> = { thin: 2.5, medium: 5, thick: 10 };
const HIGHLIGHTER_WIDTH = 24;
const HIGHLIGHTER_OPACITY = 0.35;
const ERASE_RADIUS = 12;

// The legacy `disPlan` palette (MSINKAUT named colours), in draw order.
const PALETTE = ['#1a1c1d', '#c0392b', '#e67e22', '#f1c40f', '#7dcea0', '#27ae60', '#5dade2', '#2471a3'];

interface Snapshot {
  sheets: FloorPlanStroke[][];
  active: number;
}

@Component({
  selector: 'hvac-floor-plan-sketch',
  imports: [MatButtonModule, MatButtonToggleModule, MatIconModule, MatTooltipModule, TranslocoModule],
  templateUrl: './floor-plan-sketch.component.html',
  styleUrl: './floor-plan-sketch.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => FloorPlanSketchComponent), multi: true },
  ],
})
export class FloorPlanSketchComponent implements ControlValueAccessor {
  private canvasRef = viewChild<ElementRef<HTMLCanvasElement>>('canvas');

  protected readonly palette = PALETTE;
  protected readonly color = signal(PALETTE[0]);
  protected readonly size = signal<SizeKey>('medium');
  protected readonly mode = signal<Mode>('pen');
  protected readonly disabled = signal(false);

  protected readonly tabs = signal<number[]>([0]);
  protected readonly activeIndex = signal(0);
  protected readonly currentEmpty = signal(true);
  protected readonly canAddSheet = signal(true);
  protected readonly canRemoveSheet = signal(false);
  protected readonly canUndo = signal(false);
  protected readonly canRedo = signal(false);
  protected readonly zoomPercent = signal(100);
  protected readonly canResetView = signal(false);

  /** One stroke list per sheet; always at least one sheet. */
  private sheets: FloorPlanStroke[][] = [[]];
  private past: Snapshot[] = [];
  private future: Snapshot[] = [];

  private drawing = false;
  private panning = false;
  private panStart = { clientX: 0, clientY: 0, panX: 0, panY: 0 };
  private current: FloorPlanStroke | null = null;
  private redrawQueued = false;

  /** View transform: `zoom` ≥ 1, `panX/panY` in logical units (how far the sheet origin is shifted). */
  private zoom = 1;
  private panX = 0;
  private panY = 0;

  private resizeObserver?: ResizeObserver;
  private wired = false;

  private onChange: (value: FloorPlanSketch | null) => void = () => {};
  private onTouched: () => void = () => {};

  constructor() {
    const destroyRef = inject(DestroyRef);

    effect(() => {
      const el = this.canvasRef()?.nativeElement;
      if (!el || this.wired) {
        return;
      }
      this.wired = true;
      if (typeof ResizeObserver !== 'undefined') {
        this.resizeObserver = new ResizeObserver(() => this.syncBackingStore());
        this.resizeObserver.observe(el);
      }
      el.addEventListener('wheel', this.onWheel, { passive: false });
      this.syncBackingStore();
    });

    destroyRef.onDestroy(() => {
      this.resizeObserver?.disconnect();
      this.canvasRef()?.nativeElement.removeEventListener('wheel', this.onWheel);
    });
  }

  // --- ControlValueAccessor ---------------------------------------------------

  /** `ControlValueAccessor`: loads a sketch from the form model (null clears the canvas). */
  writeValue(value: FloorPlanSketch | null): void {
    this.sheets = this.normalize(value);
    this.past = [];
    this.future = [];
    this.current = null;
    this.drawing = false;
    this.activeIndex.set(0);
    this.resetView();
    this.syncState();
    this.scheduleRedraw();
  }

  /** `ControlValueAccessor`: registers the form-model change callback. */
  registerOnChange(fn: (value: FloorPlanSketch | null) => void): void {
    this.onChange = fn;
  }

  /** `ControlValueAccessor`: registers the touched callback. */
  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  /** `ControlValueAccessor`: toggles read-only mode (e.g. confirmed quotes). */
  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  // --- Toolbar --------------------------------------------------------------

  protected pickColor(value: string): void {
    this.color.set(value);
    if (this.mode() === 'eraser' || this.mode() === 'pan') {
      this.mode.set('pen');
    }
  }

  protected setSize(value: SizeKey): void {
    this.size.set(value);
  }

  protected setMode(value: Mode): void {
    this.mode.set(value);
  }

  protected undo(): void {
    if (this.disabled() || this.past.length === 0) {
      return;
    }
    this.future.push(this.snapshot());
    this.restore(this.past.pop()!);
  }

  protected redo(): void {
    if (this.disabled() || this.future.length === 0) {
      return;
    }
    this.past.push(this.snapshot());
    this.restore(this.future.pop()!);
  }

  protected clearSheet(): void {
    if (this.disabled() || this.activeStrokes().length === 0) {
      return;
    }
    this.pushHistory();
    this.replaceActive([]);
    this.commit();
  }

  // --- Sheets -------------------------------------------------------------

  protected selectSheet(index: number): void {
    if (index < 0 || index >= this.sheets.length) {
      return;
    }
    this.activeIndex.set(index);
    this.resetView();
    this.syncState();
    this.scheduleRedraw();
  }

  protected addSheet(): void {
    if (this.disabled() || this.sheets.length >= MAX_SHEETS) {
      return;
    }
    this.pushHistory();
    this.sheets = [...this.sheets, []];
    this.activeIndex.set(this.sheets.length - 1);
    this.resetView();
    this.commit();
  }

  protected removeSheet(): void {
    if (this.disabled() || this.sheets.length <= 1) {
      return;
    }
    this.pushHistory();
    const removed = this.activeIndex();
    this.sheets = this.sheets.filter((_, i) => i !== removed);
    this.activeIndex.set(Math.min(removed, this.sheets.length - 1));
    this.resetView();
    this.commit();
  }

  // --- Zoom / pan -------------------------------------------------------

  protected zoomIn(): void {
    this.applyZoom(this.zoom * ZOOM_STEP);
  }

  protected zoomOut(): void {
    this.applyZoom(this.zoom / ZOOM_STEP);
  }

  protected resetView(): void {
    this.zoom = 1;
    this.panX = 0;
    this.panY = 0;
    this.zoomPercent.set(100);
    this.canResetView.set(false);
    this.scheduleRedraw();
  }

  // Ctrl/⌘ + wheel zooms toward the cursor; a plain wheel is left to scroll the page (the tool is
  // embedded in long forms). The on-canvas +/− buttons cover the no-modifier case.
  private onWheel = (event: WheelEvent): void => {
    if (this.disabled() || !(event.ctrlKey || event.metaKey)) {
      return;
    }
    event.preventDefault();
    const factor = event.deltaY < 0 ? ZOOM_STEP : 1 / ZOOM_STEP;
    this.applyZoom(this.zoom * factor, event.clientX, event.clientY);
  };

  private applyZoom(target: number, clientX?: number, clientY?: number): void {
    const canvas = this.canvasRef()?.nativeElement;
    if (!canvas) {
      return;
    }
    const next = clamp(target, MIN_ZOOM, MAX_ZOOM);
    if (next === this.zoom) {
      return;
    }

    if (clientX != null && clientY != null) {
      // Keep the logical point under the cursor fixed while zooming.
      const rect = canvas.getBoundingClientRect();
      const bx = canvas.width / rect.width / (canvas.width / LOGICAL_W);
      const by = canvas.height / rect.height / (canvas.height / LOGICAL_H);
      const cursorX = (clientX - rect.left) * bx;
      const cursorY = (clientY - rect.top) * by;
      this.panX += cursorX * (1 / next - 1 / this.zoom);
      this.panY += cursorY * (1 / next - 1 / this.zoom);
    }

    this.zoom = next;
    this.clampView();
    this.zoomPercent.set(Math.round(this.zoom * 100));
    this.canResetView.set(this.zoom !== 1 || this.panX !== 0 || this.panY !== 0);
    this.scheduleRedraw();
  }

  private clampView(): void {
    if (this.zoom <= 1) {
      this.panX = 0;
      this.panY = 0;
      return;
    }
    const margin = 120;
    const visW = LOGICAL_W / this.zoom;
    const visH = LOGICAL_H / this.zoom;
    this.panX = clamp(this.panX, -(LOGICAL_W - margin), visW - margin);
    this.panY = clamp(this.panY, -(LOGICAL_H - margin), visH - margin);
  }

  // --- Pointer input ----------------------------------------------------

  protected onPointerDown(event: PointerEvent): void {
    if (this.disabled() || event.button !== 0) {
      return;
    }
    const canvas = this.canvasRef()?.nativeElement;
    if (!canvas) {
      return;
    }
    event.preventDefault();
    canvas.setPointerCapture(event.pointerId);
    this.onTouched();

    if (this.mode() === 'pan') {
      this.panning = true;
      this.panStart = { clientX: event.clientX, clientY: event.clientY, panX: this.panX, panY: this.panY };
      return;
    }

    const point = this.toLogical(event);

    if (this.mode() === 'eraser') {
      this.pushHistory();
      this.eraseAt(point);
      return;
    }

    const isHighlighter = this.mode() === 'highlighter';
    this.pushHistory();
    this.current = {
      color: this.color(),
      width: isHighlighter ? HIGHLIGHTER_WIDTH : PEN_WIDTHS[this.size()],
      opacity: isHighlighter ? HIGHLIGHTER_OPACITY : 1,
      points: [point],
    };
    this.drawing = true;
  }

  protected onPointerMove(event: PointerEvent): void {
    if (this.disabled()) {
      return;
    }

    if (this.panning) {
      this.updatePan(event);
      return;
    }

    if (this.mode() === 'eraser') {
      if (event.buttons === 1) {
        this.eraseAt(this.toLogical(event));
      }
      return;
    }

    if (!this.drawing || !this.current) {
      return;
    }
    const point = this.toLogical(event);
    const last = this.current.points[this.current.points.length - 1];
    if (Math.hypot(point[0] - last[0], point[1] - last[1]) < 1) {
      return;
    }
    this.current.points.push(point);
    this.scheduleRedraw();
  }

  protected onPointerUp(event: PointerEvent): void {
    const canvas = this.canvasRef()?.nativeElement;
    if (canvas?.hasPointerCapture(event.pointerId)) {
      canvas.releasePointerCapture(event.pointerId);
    }

    if (this.panning) {
      this.panning = false;
      return;
    }

    if (this.mode() === 'eraser') {
      // History was pushed on down; a click that erased nothing leaves a redundant entry — trim it.
      this.dedupeHistoryHead();
      this.syncState();
      return;
    }

    if (!this.drawing) {
      return;
    }
    this.drawing = false;

    if (this.current && this.current.points.length >= 1) {
      if (this.current.points.length === 1) {
        // A dot: duplicate the point so the polyline renders a round cap.
        this.current.points.push([this.current.points[0][0] + 0.01, this.current.points[0][1]]);
      }
      this.replaceActive([...this.activeStrokes(), this.current]);
    } else {
      this.past.pop();
    }
    this.current = null;
    this.commit();
  }

  // --- Internals ------------------------------------------------------

  private updatePan(event: PointerEvent): void {
    const canvas = this.canvasRef()?.nativeElement;
    if (!canvas) {
      return;
    }
    const rect = canvas.getBoundingClientRect();
    const bx = canvas.width / rect.width / (canvas.width / LOGICAL_W);
    const by = canvas.height / rect.height / (canvas.height / LOGICAL_H);
    this.panX = this.panStart.panX + ((event.clientX - this.panStart.clientX) * bx) / this.zoom;
    this.panY = this.panStart.panY + ((event.clientY - this.panStart.clientY) * by) / this.zoom;
    this.clampView();
    this.canResetView.set(this.zoom !== 1 || this.panX !== 0 || this.panY !== 0);
    this.scheduleRedraw();
  }

  private activeStrokes(): FloorPlanStroke[] {
    return this.sheets[this.activeIndex()] ?? [];
  }

  private replaceActive(strokes: FloorPlanStroke[]): void {
    const active = this.activeIndex();
    this.sheets = this.sheets.map((s, i) => (i === active ? strokes : s));
  }

  private eraseAt(point: [number, number]): void {
    const strokes = this.activeStrokes();
    let hit = -1;
    for (let i = strokes.length - 1; i >= 0; i--) {
      const s = strokes[i];
      if (s.points.some((p) => Math.hypot(p[0] - point[0], p[1] - point[1]) <= ERASE_RADIUS + s.width / 2)) {
        hit = i;
        break;
      }
    }
    if (hit === -1) {
      return;
    }
    this.replaceActive(strokes.filter((_, i) => i !== hit));
    this.commit();
  }

  private pushHistory(): void {
    this.past.push(this.snapshot());
    if (this.past.length > 100) {
      this.past.shift();
    }
    this.future = [];
  }

  private snapshot(): Snapshot {
    return { sheets: this.sheets, active: this.activeIndex() };
  }

  private restore(snapshot: Snapshot): void {
    this.sheets = snapshot.sheets;
    this.activeIndex.set(clamp(snapshot.active, 0, this.sheets.length - 1));
    this.resetView();
    this.commit();
  }

  private dedupeHistoryHead(): void {
    if (this.past.length > 0 && this.past[this.past.length - 1].sheets === this.sheets) {
      this.past.pop();
    }
  }

  private commit(): void {
    this.syncState();
    this.scheduleRedraw();

    const meaningful = this.sheets.some((s) => s.length > 0);
    this.onChange(
      meaningful
        ? {
            version: 1,
            width: LOGICAL_W,
            height: LOGICAL_H,
            sheets: this.sheets.map((strokes) => ({ strokes })),
          }
        : null,
    );
  }

  private syncState(): void {
    this.tabs.set(this.sheets.map((_, i) => i));
    this.currentEmpty.set(this.activeStrokes().length === 0);
    this.canAddSheet.set(this.sheets.length < MAX_SHEETS);
    this.canRemoveSheet.set(this.sheets.length > 1);
    this.canUndo.set(this.past.length > 0);
    this.canRedo.set(this.future.length > 0);
  }

  private normalize(value: FloorPlanSketch | null | undefined): FloorPlanStroke[][] {
    if (!value) {
      return [[]];
    }

    const w = value.width > 0 ? value.width : LOGICAL_W;
    const h = value.height > 0 ? value.height : LOGICAL_H;
    const sx = LOGICAL_W / w;
    const sy = LOGICAL_H / h;
    const rescale = Math.abs(sx - 1) > 1e-6 || Math.abs(sy - 1) > 1e-6;

    const rawSheets: { strokes?: FloorPlanStroke[] }[] = Array.isArray(value.sheets)
      ? value.sheets
      : Array.isArray((value as { strokes?: FloorPlanStroke[] }).strokes)
        ? [{ strokes: (value as { strokes?: FloorPlanStroke[] }).strokes }]
        : [];

    const sheets = rawSheets.slice(0, MAX_SHEETS).map((sheet) =>
      (Array.isArray(sheet.strokes) ? sheet.strokes : []).slice(0, 2000).map((stroke) => ({
        color: stroke.color,
        width: stroke.width,
        opacity: stroke.opacity,
        points: (stroke.points ?? []).map((p): [number, number] =>
          rescale ? [p[0] * sx, p[1] * sy] : [p[0], p[1]],
        ),
      })),
    );

    return sheets.length ? sheets : [[]];
  }

  private toLogical(event: PointerEvent): [number, number] {
    const canvas = this.canvasRef()!.nativeElement;
    const rect = canvas.getBoundingClientRect();
    const bx = canvas.width / rect.width / (canvas.width / LOGICAL_W);
    const by = canvas.height / rect.height / (canvas.height / LOGICAL_H);
    const lx = ((event.clientX - rect.left) * bx) / this.zoom - this.panX;
    const ly = ((event.clientY - rect.top) * by) / this.zoom - this.panY;
    return [clamp(lx, 0, LOGICAL_W), clamp(ly, 0, LOGICAL_H)];
  }

  private syncBackingStore(): void {
    const canvas = this.canvasRef()?.nativeElement;
    if (!canvas) {
      return;
    }
    const dpr = window.devicePixelRatio || 1;
    const cssWidth = canvas.clientWidth || 1;
    const cssHeight = canvas.clientHeight || Math.round((cssWidth * LOGICAL_H) / LOGICAL_W);
    const nextWidth = Math.round(cssWidth * dpr);
    const nextHeight = Math.round(cssHeight * dpr);
    if (canvas.width !== nextWidth || canvas.height !== nextHeight) {
      canvas.width = nextWidth;
      canvas.height = nextHeight;
    }
    this.redraw();
  }

  private scheduleRedraw(): void {
    if (this.redrawQueued) {
      return;
    }
    this.redrawQueued = true;
    requestAnimationFrame(() => {
      this.redrawQueued = false;
      this.redraw();
    });
  }

  private redraw(): void {
    const canvas = this.canvasRef()?.nativeElement;
    const ctx = canvas?.getContext('2d');
    if (!canvas || !ctx) {
      return;
    }

    const bx = canvas.width / LOGICAL_W;
    const by = canvas.height / LOGICAL_H;
    const z = this.zoom;
    const hairline = 1 / (z * ((bx + by) / 2));

    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.fillStyle = '#e9ebee';
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    ctx.setTransform(z * bx, 0, 0, z * by, this.panX * z * bx, this.panY * z * by);

    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, LOGICAL_W, LOGICAL_H);

    ctx.save();
    ctx.beginPath();
    ctx.rect(0, 0, LOGICAL_W, LOGICAL_H);
    ctx.clip();

    ctx.strokeStyle = 'rgba(0, 0, 0, 0.06)';
    ctx.lineWidth = hairline;
    const step = LOGICAL_W / 20;
    ctx.beginPath();
    for (let x = step; x < LOGICAL_W; x += step) {
      ctx.moveTo(x, 0);
      ctx.lineTo(x, LOGICAL_H);
    }
    for (let y = step; y < LOGICAL_H; y += step) {
      ctx.moveTo(0, y);
      ctx.lineTo(LOGICAL_W, y);
    }
    ctx.stroke();

    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    const strokes = this.current ? [...this.activeStrokes(), this.current] : this.activeStrokes();
    for (const stroke of strokes) {
      if (stroke.points.length === 0) {
        continue;
      }
      ctx.globalAlpha = stroke.opacity;
      ctx.strokeStyle = stroke.color;
      ctx.lineWidth = stroke.width;
      ctx.beginPath();
      ctx.moveTo(stroke.points[0][0], stroke.points[0][1]);
      for (let i = 1; i < stroke.points.length; i++) {
        ctx.lineTo(stroke.points[i][0], stroke.points[i][1]);
      }
      ctx.stroke();
    }
    ctx.globalAlpha = 1;
    ctx.restore();

    ctx.strokeStyle = 'rgba(0, 0, 0, 0.28)';
    ctx.lineWidth = hairline;
    ctx.strokeRect(0, 0, LOGICAL_W, LOGICAL_H);
  }
}

function clamp(value: number, min: number, max: number): number {
  return value < min ? min : value > max ? max : value;
}
