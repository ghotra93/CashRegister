/** An uploaded-file entry, as returned by GET /api/files (AC-027). */
export interface UploadedFileSummary {
  id: string;
  fileName: string;
  uploadedAt: string;
  lineCount: number;
  errorLineCount: number;
}

/** GET/PUT /api/settings/divisor. */
export interface DivisorResponse {
  divisor: number;
}

/** RFC 9457 problem details, the API's error shape (AC-015). */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
}
