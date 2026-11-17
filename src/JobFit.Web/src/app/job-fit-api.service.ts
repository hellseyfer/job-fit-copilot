import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import {
  AnalyzeJobResponse,
  ApiError,
  DocumentKind,
  KnowledgeDocumentResponse,
} from './models';

const PROMPT_INJECTION_TYPE = 'https://job-fit-copilot/problems/prompt-injection';

@Injectable({ providedIn: 'root' })
export class JobFitApiService {
  private readonly http = inject(HttpClient);
  private baseUrl = '';

  setBaseUrl(url: string): void {
    this.baseUrl = url.trim().replace(/\/+$/, '');
  }

  ingestDocument(
    id: string,
    payload: { kind: DocumentKind; title: string; content: string },
  ): Observable<KnowledgeDocumentResponse> {
    return this.http
      .put<KnowledgeDocumentResponse>(
        `${this.baseUrl}/api/knowledge/documents/${encodeURIComponent(id)}`,
        payload,
      )
      .pipe(catchError((err) => throwError(() => parseApiError(err))));
  }

  analyze(payload: {
    jobPosting: string;
    kind?: DocumentKind;
  }): Observable<AnalyzeJobResponse> {
    return this.http
      .post<AnalyzeJobResponse>(`${this.baseUrl}/api/analyses`, payload)
      .pipe(catchError((err) => throwError(() => parseApiError(err))));
  }

  decideApproval(
    runId: string,
    payload: { approved: boolean; feedback?: string },
  ): Observable<AnalyzeJobResponse> {
    return this.http
      .post<AnalyzeJobResponse>(
        `${this.baseUrl}/api/analyses/${encodeURIComponent(runId)}/approval`,
        payload,
      )
      .pipe(catchError((err) => throwError(() => parseApiError(err))));
  }
}

export function parseApiError(error: unknown): ApiError {
  if (!(error instanceof HttpErrorResponse)) {
    return {
      status: 0,
      title: 'Unexpected error',
      isPromptInjection: false,
      details: [String(error)],
    };
  }

  const body = error.error as Record<string, unknown> | null;
  const title =
    (typeof body?.['title'] === 'string' && body['title']) ||
    error.statusText ||
    `Request failed (${error.status})`;

  const isPromptInjection = body?.['type'] === PROMPT_INJECTION_TYPE;
  const field = typeof body?.['field'] === 'string' ? body['field'] : undefined;
  const layer = typeof body?.['layer'] === 'string' ? body['layer'] : undefined;

  const details: string[] = [];
  if (body?.['errors'] && typeof body['errors'] === 'object') {
    for (const [key, value] of Object.entries(
      body['errors'] as Record<string, unknown>,
    )) {
      if (Array.isArray(value)) {
        for (const msg of value) {
          details.push(`${key}: ${String(msg)}`);
        }
      }
    }
  }

  if (error.status === 0 && details.length === 0) {
    details.push('Could not reach the API. Check that JobFit.Api is running and the API URL/proxy is configured.');
  }

  return { status: error.status, title, isPromptInjection, field, layer, details };
}
