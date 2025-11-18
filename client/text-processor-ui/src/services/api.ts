import axios, { AxiosResponse } from 'axios';
import { ProcessingJob, ProcessTextRequest } from '../models/types';

// Configuration
const resolveApiBaseUrl = (): string => {
  const rawValue = process.env.REACT_APP_API_URL?.trim();

  if (rawValue) {
    if (rawValue.toLowerCase() === 'origin') {
      if (typeof window !== 'undefined' && window.location?.origin) {
        return window.location.origin.replace(/\/$/, '');
      }
      return '';
    }

    if (rawValue.startsWith('/')) {
      if (typeof window !== 'undefined' && window.location?.origin) {
        return `${window.location.origin.replace(/\/$/, '')}${rawValue}`;
      }
      return rawValue;
    }

    return rawValue.replace(/\/$/, '');
  }

  if (typeof window !== 'undefined' && window.location?.origin) {
    return window.location.origin.replace(/\/$/, '');
  }

  return 'https://localhost:7180';
};

const normalizeApiBaseUrl = (value: string): string => {
  const trimmed = value.replace(/\/$/, '');
  if (trimmed.endsWith('/api')) {
    return trimmed;
  }
  return `${trimmed}/api`;
};

const API_BASE_URL = normalizeApiBaseUrl(resolveApiBaseUrl());
const API_TIMEOUT = 30000; // 30 seconds

// Create axios instance with default configuration
const apiClient = axios.create({
  baseURL: API_BASE_URL,
  timeout: API_TIMEOUT,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor for logging
apiClient.interceptors.request.use(
  (config) => {
    console.log(`Making ${config.method?.toUpperCase()} request to ${config.url}`);
    return config;
  },
  (error) => {
    console.error('Request error:', error);
    return Promise.reject(error);
  }
);

// Response interceptor for error handling
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    console.error('Response error:', error.response?.data || error.message);
    return Promise.reject(error);
  }
);

export class TextProcessingApi {
  /**
   * Starts a new text processing job
   */
  static async processText(
    request: ProcessTextRequest,
    signalRConnectionId?: string
  ): Promise<ProcessingJob> {
    try {
      const response: AxiosResponse<ProcessingJob> = await apiClient.post(
        '/textprocessing/process',
        request,
        signalRConnectionId
          ? {
              headers: {
                'X-SignalR-ConnectionId': signalRConnectionId,
              },
            }
          : undefined
      );
      return response.data;
    } catch (error: any) {
      throw new Error(
        error.response?.data?.error || 
        error.response?.data?.message || 
        'Failed to start text processing'
      );
    }
  }

  /**
   * Gets the status and details of a processing job
   */
  static async getJob(jobId: string): Promise<ProcessingJob> {
    try {
      const response: AxiosResponse<ProcessingJob> = await apiClient.get(
        `/textprocessing/jobs/${jobId}`
      );
      return response.data;
    } catch (error: any) {
      if (error.response?.status === 404) {
        throw new Error('Job not found');
      }
      throw new Error(
        error.response?.data?.error || 
        error.response?.data?.message || 
        'Failed to get job details'
      );
    }
  }

  /**
   * Cancels a running processing job
   */
  static async cancelJob(jobId: string): Promise<void> {
    try {
      await apiClient.post(`/textprocessing/jobs/${jobId}/cancel`);
    } catch (error: any) {
      if (error.response?.status === 404) {
        throw new Error('Job not found');
      }
      if (error.response?.status === 400) {
        throw new Error(
          error.response.data?.error || 'Job cannot be cancelled in its current state'
        );
      }
      throw new Error(
        error.response?.data?.error || 
        error.response?.data?.message || 
        'Failed to cancel job'
      );
    }
  }

  /**
   * Gets all jobs for the current client
   */
  static async getJobs(): Promise<ProcessingJob[]> {
    try {
      const response: AxiosResponse<ProcessingJob[]> = await apiClient.get(
        '/textprocessing/jobs'
      );
      return response.data;
    } catch (error: any) {
      throw new Error(
        error.response?.data?.error || 
        error.response?.data?.message || 
        'Failed to get jobs'
      );
    }
  }

  /**
   * Checks if the API is available
   */
  static async ping(): Promise<boolean> {
    try {
      await axios.get(`${API_BASE_URL}/ping`, { timeout: 5000 });
      return true;
    } catch (error) {
      console.error('API ping failed:', error);
      return false;
    }
  }
}

export default TextProcessingApi;