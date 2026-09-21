import type { DivisorResponse, ProblemDetails, UploadedFileSummary } from './types';

const filesPath = '/api/files';
const divisorPath = '/api/settings/divisor';

/** A failed API call. Carries the server's ProblemDetails title and detail when present (AC-015). */
export class ApiError extends Error {
  readonly status: number;
  readonly title: string;
  readonly detail: string | undefined;

  constructor(status: number, title: string, detail?: string) {
    super(detail === undefined ? title : `${title}: ${detail}`);
    this.name = 'ApiError';
    this.status = status;
    this.title = title;
    this.detail = detail;
  }
}

async function toApiError(response: Response): Promise<ApiError> {
  const fallbackTitle = `Request failed (${String(response.status)})`;
  const isProblem = response.headers.get('Content-Type')?.includes('json') ?? false;
  if (!isProblem) {
    return new ApiError(response.status, fallbackTitle);
  }

  const problem = (await response.json()) as ProblemDetails;
  return new ApiError(response.status, problem.title ?? fallbackTitle, problem.detail);
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, init);
  if (!response.ok) {
    throw await toApiError(response);
  }
  return (await response.json()) as T;
}

/** Uploaded files, newest first (AC-027). */
export function listFiles(): Promise<UploadedFileSummary[]> {
  return request<UploadedFileSummary[]>(filesPath);
}

/** Uploads a transaction file for processing; the server expects the field name "file". */
export function uploadFile(file: File): Promise<UploadedFileSummary> {
  const form = new FormData();
  form.append('file', file);
  return request<UploadedFileSummary>(filesPath, { method: 'POST', body: form });
}

/** Where the browser downloads a file's change output (AC-028). */
export function outputUrl(id: string): string {
  return `${filesPath}/${encodeURIComponent(id)}/output`;
}

export function getDivisor(): Promise<DivisorResponse> {
  return request<DivisorResponse>(divisorPath);
}

/** Changes the special-case divisor; the server rejects values below 1 (AC-025, AC-026). */
export function setDivisor(divisor: number): Promise<DivisorResponse> {
  return request<DivisorResponse>(divisorPath, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ divisor }),
  });
}
