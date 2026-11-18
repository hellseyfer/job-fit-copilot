import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { JobFitApiService } from './job-fit-api.service';
import {
  AnalyzeJobResponse,
  ApiError,
  DocumentKind,
  KnowledgeDocumentResponse,
} from './models';

@Component({
  selector: 'app-root',
  imports: [CommonModule, FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  private readonly api = inject(JobFitApiService);

  readonly apiBaseUrl = signal('');

  readonly docId = signal('mi-cv');
  readonly docKind = signal<DocumentKind>('cv');
  readonly docTitle = signal('My CV');
  readonly docContent = signal('');
  readonly ingesting = signal(false);
  readonly ingestionResult = signal<KnowledgeDocumentResponse | null>(null);

  readonly analysisKind = signal<'' | DocumentKind>('cv');
  readonly jobPosting = signal('');
  readonly analyzing = signal(false);
  readonly analysisResult = signal<AnalyzeJobResponse | null>(null);

  readonly feedback = signal('');
  readonly deciding = signal(false);

  readonly error = signal<ApiError | null>(null);

  onBaseUrlChange(value: string): void {
    this.apiBaseUrl.set(value);
    this.api.setBaseUrl(value);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      this.docContent.set(typeof reader.result === 'string' ? reader.result : '');
      if (!this.docTitle().trim() || this.docTitle() === 'My CV') {
        this.docTitle.set(file.name.replace(/\.[^.]+$/, ''));
      }
    };
    reader.readAsText(file);
  }

  ingest(): void {
    this.error.set(null);
    this.ingesting.set(true);
    this.api
      .ingestDocument(this.docId().trim(), {
        kind: this.docKind(),
        title: this.docTitle().trim(),
        content: this.docContent(),
      })
      .pipe(finalize(() => this.ingesting.set(false)))
      .subscribe({
        next: (res) => this.ingestionResult.set(res),
        error: (err: ApiError) => this.error.set(err),
      });
  }

  analyze(): void {
    this.error.set(null);
    this.analyzing.set(true);
    const kind = this.analysisKind();
    this.api
      .analyze({
        jobPosting: this.jobPosting(),
        ...(kind ? { kind } : {}),
      })
      .pipe(finalize(() => this.analyzing.set(false)))
      .subscribe({
        next: (res) => {
          this.analysisResult.set(res);
          this.feedback.set('');
        },
        error: (err: ApiError) => this.error.set(err),
      });
  }

  decide(approved: boolean, withFeedback: boolean): void {
    const run = this.analysisResult();
    if (!run) {
      return;
    }

    this.error.set(null);
    this.deciding.set(true);
    const comment = withFeedback ? this.feedback().trim() : undefined;
    this.api
      .decideApproval(run.runId, {
        approved,
        ...(comment ? { feedback: comment } : {}),
      })
      .pipe(finalize(() => this.deciding.set(false)))
      .subscribe({
        next: (res) => {
          this.analysisResult.set(res);
          if (res.status !== 'awaiting-approval') {
            this.feedback.set('');
          }
        },
        error: (err: ApiError) => this.error.set(err),
      });
  }
}
