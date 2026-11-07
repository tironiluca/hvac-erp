/** Mirrors the backend `PdfDocumentType` enum (wire value is the name). */
export type PdfDocumentType = 'Quote' | 'Repair' | 'Installation';

export const PDF_DOCUMENT_TYPES: readonly PdfDocumentType[] = ['Quote', 'Repair', 'Installation'];

/** Mirrors `HvacErp.Application.Pdf.PdfTemplateDefinition`. */
export interface PdfTemplateDefinition {
  page: PdfPageConfig;
  header: PdfHeaderConfig;
  sections: PdfSection[];
  footer: PdfFooterConfig;
}

export interface PdfPageConfig {
  size: 'A4' | 'Letter';
  margin: number;
  fontFamily: string;
  baseFontSize: number;
  accentColor: string;
  /** Language of the fixed captions the server prints (table headers, totals, page numbers). */
  language: PdfLanguage;
}

export type PdfLanguage = 'en' | 'it';

export interface PdfHeaderConfig {
  showLogo: boolean;
  showTitle: boolean;
  titleOverride?: string | null;
  showDocumentMeta: boolean;
  showAgent: boolean;
  showClientBlock: boolean;
  clientFields: string[];
}

export interface PdfSection {
  type: string;
  enabled: boolean;
  titleOverride?: string | null;
  columns: string[];
  options: Record<string, boolean>;
  body?: string | null;
}

export interface PdfFooterConfig {
  lines: string[];
  showPageNumbers: boolean;
}

/** `GET /api/pdf-templates` row. */
export interface PdfTemplateListItem {
  id: number;
  documentType: PdfDocumentType;
  name: string;
  isActive: boolean;
  isSystemDefault: boolean;
  updatedAt: string;
  updatedBy?: string | null;
}

/** `GET /api/pdf-templates/{id}` — a template with its full definition. */
export interface PdfTemplate extends PdfTemplateListItem {
  definition: PdfTemplateDefinition;
}

export interface UpsertPdfTemplateRequest {
  documentType: PdfDocumentType;
  name: string;
  definition: PdfTemplateDefinition;
}

// --- Catalog (drives the editor UI) ----------------------------------------------------------

export interface PdfTemplateCatalog {
  documentType: PdfDocumentType;
  sections: PdfSectionCatalogEntry[];
  clientFields: PdfFieldOption[];
  fontFamilies: string[];
}

export interface PdfSectionCatalogEntry {
  type: string;
  labelKey: string;
  repeatable: boolean;
  supportsBody: boolean;
  columns: PdfColumnOption[];
  options: PdfOptionToggle[];
}

export interface PdfColumnOption {
  key: string;
  labelKey: string;
  defaultOn: boolean;
}

export interface PdfOptionToggle {
  key: string;
  labelKey: string;
  defaultOn: boolean;
}

export interface PdfFieldOption {
  key: string;
  labelKey: string;
}
