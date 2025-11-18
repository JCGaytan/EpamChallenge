import { useState, useEffect, useCallback, useRef } from 'react';
import signalRService, { SignalRCallbacks } from '../services/signalr';
import { 
  ProcessingJob, 
  CharacterProcessedEvent, 
  JobCompletedEvent, 
  JobCancelledEvent, 
  JobFailedEvent,
  JobStatus
} from '../models/types';

export interface UseSignalRResult {
  isConnected: boolean;
  isConnecting: boolean;
  connectionError: string | null;
  connectionId: string | null;
  connect: () => Promise<void>;
  disconnect: () => Promise<void>;
  joinJobGroup: (jobId: string) => Promise<void>;
  leaveJobGroup: (jobId: string) => Promise<void>;
  cancelJob: (jobId: string) => Promise<void>;
}

/**
 * Custom hook for managing SignalR connection and events
 */
export function useSignalR(callbacks: SignalRCallbacks = {}): UseSignalRResult {
  const [isConnected, setIsConnected] = useState(false);
  const [isConnecting, setIsConnecting] = useState(false);
  const [connectionError, setConnectionError] = useState<string | null>(null);
  const [connectionId, setConnectionId] = useState<string | null>(signalRService.getConnectionId());

  const connect = useCallback(async () => {
    if (isConnected || isConnecting) return;

    setIsConnecting(true);
    setConnectionError(null);

    try {
      await signalRService.connect({
        ...callbacks,
        onConnected: () => {
          setIsConnected(true);
          setIsConnecting(false);
          setConnectionError(null);
           setConnectionId(signalRService.getConnectionId());
          callbacks.onConnected?.();
        },
        onDisconnected: (error) => {
          setIsConnected(false);
          setIsConnecting(false);
          setConnectionId(null);
          if (error) {
            setConnectionError(error.message);
          }
          callbacks.onDisconnected?.(error);
        },
        onReconnecting: (error) => {
          setIsConnected(false);
          setConnectionId(null);
          callbacks.onReconnecting?.(error);
        },
        onReconnected: () => {
          setIsConnected(true);
          setConnectionError(null);
          setConnectionId(signalRService.getConnectionId());
          callbacks.onReconnected?.();
        }
      });
    } catch (error: any) {
      setIsConnecting(false);
      setConnectionError(error.message || 'Failed to connect to real-time service');
    }
  }, [isConnected, isConnecting, callbacks]);

  const disconnect = useCallback(async () => {
    try {
      await signalRService.disconnect();
      setIsConnected(false);
      setIsConnecting(false);
      setConnectionError(null);
      setConnectionId(null);
    } catch (error: any) {
      console.error('Failed to disconnect:', error);
    }
  }, []);

  const joinJobGroup = useCallback(async (jobId: string) => {
    try {
      await signalRService.joinJobGroup(jobId);
    } catch (error: any) {
      console.error('Failed to join job group:', error);
      throw error;
    }
  }, []);

  const leaveJobGroup = useCallback(async (jobId: string) => {
    try {
      await signalRService.leaveJobGroup(jobId);
    } catch (error: any) {
      console.error('Failed to leave job group:', error);
    }
  }, []);

  const cancelJob = useCallback(async (jobId: string) => {
    try {
      await signalRService.cancelJob(jobId);
    } catch (error: any) {
      console.error('Failed to cancel job via SignalR:', error);
      throw error;
    }
  }, []);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      signalRService.disconnect().catch(console.error);
    };
  }, []);

  return {
    isConnected,
    isConnecting,
    connectionError,
    connectionId,
    connect,
    disconnect,
    joinJobGroup,
    leaveJobGroup,
    cancelJob
  };
}

export interface UseTextProcessingResult {
  currentJob: ProcessingJob | null;
  processedResult: string;
  isProcessing: boolean;
  progress: number;
  error: string | null;
  startProcessing: (text: string) => Promise<void>;
  cancelProcessing: () => Promise<void>;
  clearJob: () => void;
  isConnected: boolean;
  isConnecting: boolean;
  connectionError: string | null;
}

/**
 * Custom hook for managing text processing jobs and real-time updates
 */
export function useTextProcessing(): UseTextProcessingResult {
  const [currentJob, setCurrentJob] = useState<ProcessingJob | null>(null);
  const [processedResult, setProcessedResult] = useState<string>('');
  const [isProcessing, setIsProcessing] = useState(false);
  const [progress, setProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const currentJobRef = useRef<ProcessingJob | null>(null);

  useEffect(() => {
    currentJobRef.current = currentJob;
  }, [currentJob]);

  const {
    connect,
    joinJobGroup,
    leaveJobGroup,
    cancelJob,
    isConnected,
    isConnecting,
    connectionError,
    connectionId
  } = useSignalR({
    onCharacterProcessed: (event: CharacterProcessedEvent) => {
      const activeJob = currentJobRef.current;
      if (activeJob?.id === event.jobId) {
        setProcessedResult(prev => prev + event.character);
        setProgress(event.progress);
      }
    },
    onJobCompleted: (event: JobCompletedEvent) => {
      const activeJob = currentJobRef.current;
      if (activeJob?.id === event.jobId) {
        setCurrentJob(prev => prev ? {
          ...prev,
          status: JobStatus.Completed,
          processedText: event.result,
          completedAt: event.completedAt
        } : null);
        setIsProcessing(false);
        setProgress(100);
        setProcessedResult(event.result);
      }
    },
    onJobCancelled: (event: JobCancelledEvent) => {
      const activeJob = currentJobRef.current;
      if (activeJob?.id === event.jobId) {
        setCurrentJob(prev => prev ? {
          ...prev,
          status: JobStatus.Cancelled,
          completedAt: event.cancelledAt
        } : null);
        setIsProcessing(false);
      }
    },
    onJobFailed: (event: JobFailedEvent) => {
      const activeJob = currentJobRef.current;
      if (activeJob?.id === event.jobId) {
        setCurrentJob(prev => prev ? {
          ...prev,
          status: JobStatus.Failed,
          errorMessage: event.errorMessage,
          completedAt: event.failedAt
        } : null);
        setIsProcessing(false);
        setError(event.errorMessage);
      }
    },
    onJobCancellationFailed: (event) => {
      const activeJob = currentJobRef.current;
      if (!activeJob || activeJob.id !== event.jobId) {
        return;
      }
      setError(event.error || 'Failed to cancel processing');
    }
  });

  const startProcessing = useCallback(async (text: string) => {
    if (isProcessing) {
      throw new Error('A processing job is already running');
    }

    setError(null);
    setProcessedResult('');
    setProgress(0);

    try {
      // Ensure SignalR connection
      if (!isConnected) {
        await connect();
      }

      let effectiveConnectionId = connectionId ?? signalRService.getConnectionId();
      if (!effectiveConnectionId) {
        effectiveConnectionId = await signalRService.waitForConnectionId();
      }
      if (!effectiveConnectionId) {
        throw new Error('Real-time connection is not ready');
      }

      // Import API service dynamically to avoid circular dependencies
      const { default: TextProcessingApi } = await import('../services/api');
      
      const job = await TextProcessingApi.processText({ text }, effectiveConnectionId);
      
      setCurrentJob(job);
      setIsProcessing(true);
      
      // Join the job group for real-time updates
      await joinJobGroup(job.id);
      
    } catch (error: any) {
      setError(error.message || 'Failed to start processing');
      setIsProcessing(false);
      throw error;
    }
  }, [isProcessing, isConnected, connect, joinJobGroup, connectionId]);

  const cancelProcessing = useCallback(async () => {
    if (!currentJob || !isProcessing) {
      return;
    }

    try {
      await cancelJob(currentJob.id);
      // The job cancellation will be handled by the SignalR event
    } catch (error: any) {
      console.error('Failed to cancel job:', error);
      setError('Failed to cancel processing');
      throw error;
    }
  }, [currentJob, isProcessing, cancelJob]);

  const clearJob = useCallback(() => {
    if (currentJob && isProcessing) {
      leaveJobGroup(currentJob.id).catch(console.error);
    }
    setCurrentJob(null);
    setProcessedResult('');
    setProgress(0);
    setIsProcessing(false);
    setError(null);
  }, [currentJob, isProcessing, leaveJobGroup]);

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      if (currentJob && isProcessing) {
        leaveJobGroup(currentJob.id).catch(console.error);
      }
    };
  }, [currentJob, isProcessing, leaveJobGroup]);

  return {
    currentJob,
    processedResult,
    isProcessing,
    progress,
    error,
    startProcessing,
    cancelProcessing,
    clearJob,
    isConnected,
    isConnecting,
    connectionError
  };
}