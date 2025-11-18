// Models for the text processing application

export interface ProcessingJob {
  id: string;
  inputText: string;
  processedText?: string;
  status: JobStatus;
  progress: number;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
  errorMessage?: string;
}

export enum JobStatus {
  Pending = 'Pending',
  Running = 'Running', 
  Completed = 'Completed',
  Cancelled = 'Cancelled',
  Failed = 'Failed'
}

export interface ProcessTextRequest {
  text: string;
}

export interface CharacterProcessedEvent {
  jobId: string;
  character: string;
  progress: number;
}

export interface JobCompletedEvent {
  jobId: string;
  result: string;
  completedAt: string;
  duration: string;
}

export interface JobCancelledEvent {
  jobId: string;
  cancelledAt: string;
}

export interface JobFailedEvent {
  jobId: string;
  errorMessage: string;
  failedAt: string;
}

export interface JobCancellationFailedEvent {
  jobId: string;
  error?: string;
}

export interface ApiError {
  error: string;
}