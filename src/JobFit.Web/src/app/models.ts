export type DocumentKind = 'cv' | 'project';

export interface KnowledgeDocumentResponse {
  documentId: string;
  kind: DocumentKind;
  title: string;
  chunkCount: number;
  embeddingModel: string;
  status: 'created' | 'updated' | 'unchanged';
}

export interface EvidenceItem {
  documentId: string;
  kind: DocumentKind;
  title: string;
  section: string;
  chunkIndex: number;
  text: string;
  score: number;
}

export interface RequirementAssessment {
  requirementId: string;
  description: string;
  importance: 'must-have' | 'nice-to-have';
  status: 'met' | 'partial' | 'missing';
  rationale: string;
  evidence: EvidenceItem[];
}

export interface ScoreFitResponse {
  score: number;
  strengths: string[];
  partial: string[];
  gaps: string[];
  assessments: RequirementAssessment[];
  chatModel: string;
  embeddingModel: string;
}

export interface CoverLetterSentence {
  text: string;
  evidence: EvidenceItem[];
}

export interface RemovedSentence {
  text: string;
  reason: string;
}

export interface CoverLetter {
  text: string;
  sentences: CoverLetterSentence[];
  removed: RemovedSentence[];
  attempts: number;
}

export interface StudyItem {
  requirementId: string;
  focus: string;
  steps: string[];
}

export interface RunUsage {
  inputTokens: number;
  outputTokens: number;
  totalTokens: number;
  costUsd: number;
  durationMs: number;
  modelCalls: number;
}

export interface AnalyzeJobResponse {
  runId: string;
  status: 'awaiting-approval' | 'approved' | 'rejected' | 'completed';
  title: string;
  seniority: string;
  fit: ScoreFitResponse;
  route: 'strong-fit' | 'weak-fit';
  path: string[];
  coverLetter?: CoverLetter;
  studyPlan?: StudyItem[];
  usage?: RunUsage;
}

export interface ApiError {
  status: number;
  title: string;
  isPromptInjection: boolean;
  field?: string;
  layer?: string;
  details: string[];
}
