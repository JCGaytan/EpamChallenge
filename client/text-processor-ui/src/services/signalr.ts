import * as signalR from '@microsoft/signalr';
import { 
  CharacterProcessedEvent, 
  JobCompletedEvent, 
  JobCancelledEvent, 
  JobFailedEvent,
  JobCancellationFailedEvent
} from '../models/types';

export interface SignalRCallbacks {
  onCharacterProcessed?: (event: CharacterProcessedEvent) => void;
  onJobCompleted?: (event: JobCompletedEvent) => void;
  onJobCancelled?: (event: JobCancelledEvent) => void;
  onJobFailed?: (event: JobFailedEvent) => void;
  onJobCancellationFailed?: (event: JobCancellationFailedEvent) => void;
  onConnected?: () => void;
  onDisconnected?: (error?: Error) => void;
  onReconnecting?: (error?: Error) => void;
  onReconnected?: () => void;
}

export class SignalRService {
  private connection: signalR.HubConnection | null = null;
  private callbacks: SignalRCallbacks = {};
  private isConnecting = false;
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 5;
  private connectionId: string | null = null;

  constructor(private hubUrl: string) {}

  /**
   * Establishes connection to the SignalR hub
   */
  async connect(callbacks: SignalRCallbacks = {}): Promise<string | null> {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      this.callbacks = callbacks;
      console.log('SignalR already connected');
      return this.getConnectionId();
    }

    if (this.isConnecting) {
      console.log('SignalR connection already in progress');
      return this.waitForConnectionId();
    }

    this.isConnecting = true;
    this.callbacks = callbacks;

    try {
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(this.hubUrl, {
          skipNegotiation: false,
          transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling
        })
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            if (retryContext.previousRetryCount < 3) {
              return 1000 * Math.pow(2, retryContext.previousRetryCount); // 1s, 2s, 4s
            }
            return 10000; // 10s for subsequent attempts
          }
        })
        .configureLogging(signalR.LogLevel.Information)
        .build();

      this.setupEventHandlers();
      
      await this.connection.start();
      this.connectionId = this.connection.connectionId ?? null;
      console.log('SignalR Connected successfully');
      this.isConnecting = false;
      this.reconnectAttempts = 0;
      
      if (this.callbacks.onConnected) {
        this.callbacks.onConnected();
      }

      return this.connectionId;
    } catch (error) {
      this.isConnecting = false;
      console.error('SignalR Connection failed:', error);
      throw new Error(`Failed to connect to SignalR hub: ${error}`);
    }
  }

  /**
   * Disconnects from the SignalR hub
   */
  async disconnect(): Promise<void> {
    if (this.connection) {
      try {
        await this.connection.stop();
        console.log('SignalR Disconnected');
      } catch (error) {
        console.error('Error disconnecting SignalR:', error);
      } finally {
        this.connection = null;
        this.connectionId = null;
      }
    }
  }

  /**
   * Joins a job group to receive updates for a specific job
   */
  async joinJobGroup(jobId: string): Promise<void> {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      try {
        await this.connection.invoke('JoinJobGroup', jobId);
        console.log(`Joined job group: ${jobId}`);
      } catch (error) {
        console.error(`Failed to join job group ${jobId}:`, error);
        throw error;
      }
    } else {
      throw new Error('SignalR connection is not established');
    }
  }

  /**
   * Leaves a job group
   */
  async leaveJobGroup(jobId: string): Promise<void> {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      try {
        await this.connection.invoke('LeaveJobGroup', jobId);
        console.log(`Left job group: ${jobId}`);
      } catch (error) {
        console.error(`Failed to leave job group ${jobId}:`, error);
      }
    }
  }

  /**
   * Cancels a job via SignalR
   */
  async cancelJob(jobId: string): Promise<void> {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      try {
        await this.connection.invoke('CancelJob', jobId);
        console.log(`Cancelled job: ${jobId}`);
      } catch (error) {
        console.error(`Failed to cancel job ${jobId}:`, error);
        throw error;
      }
    } else {
      throw new Error('SignalR connection is not established');
    }
  }

  /**
   * Gets the current connection state
   */
  getConnectionState(): signalR.HubConnectionState {
    return this.connection?.state ?? signalR.HubConnectionState.Disconnected;
  }

  /**
   * Checks if the connection is established
   */
  isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }

  /**
   * Returns the current SignalR connection id, if available
   */
  getConnectionId(): string | null {
    return this.connectionId ?? this.connection?.connectionId ?? null;
  }

  /**
   * Waits until a connection id is available or times out
   */
  async waitForConnectionId(timeoutMs = 5000): Promise<string | null> {
    const existing = this.getConnectionId();
    if (existing) {
      return existing;
    }

    return new Promise<string | null>((resolve, reject) => {
      const start = Date.now();
      const poll = setInterval(() => {
        const id = this.getConnectionId();
        if (id) {
          clearInterval(poll);
          clearTimeout(timeoutHandle);
          resolve(id);
        } else if (Date.now() - start >= timeoutMs) {
          clearInterval(poll);
          clearTimeout(timeoutHandle);
          reject(new Error('Timed out waiting for SignalR connection id'));
        }
      }, 50);

      const timeoutHandle = setTimeout(() => {
        clearInterval(poll);
        reject(new Error('Timed out waiting for SignalR connection id'));
      }, timeoutMs);
    }).catch(error => {
      console.error(error);
      return null;
    });
  }

  /**
   * Sets up event handlers for SignalR events
   */
  private setupEventHandlers(): void {
    if (!this.connection) return;

    // Handle character processed events
    this.connection.on('CharacterProcessed', (event: CharacterProcessedEvent) => {
      console.log('Character processed:', event);
      if (this.callbacks.onCharacterProcessed) {
        this.callbacks.onCharacterProcessed(event);
      }
    });

    // Handle job completion events
    this.connection.on('JobCompleted', (event: JobCompletedEvent) => {
      console.log('Job completed:', event);
      if (this.callbacks.onJobCompleted) {
        this.callbacks.onJobCompleted(event);
      }
    });

    // Handle job cancellation events
    this.connection.on('JobCancelled', (event: JobCancelledEvent) => {
      console.log('Job cancelled:', event);
      if (this.callbacks.onJobCancelled) {
        this.callbacks.onJobCancelled(event);
      }
    });

    // Handle job failure events
    this.connection.on('JobFailed', (event: JobFailedEvent) => {
      console.log('Job failed:', event);
      if (this.callbacks.onJobFailed) {
        this.callbacks.onJobFailed(event);
      }
    });

    // Handle connection events
    this.connection.onclose((error) => {
      console.log('SignalR connection closed:', error);
      this.connectionId = null;
      if (this.callbacks.onDisconnected) {
        this.callbacks.onDisconnected(error);
      }
    });

    this.connection.onreconnecting((error) => {
      console.log('SignalR reconnecting:', error);
      this.connectionId = null;
      if (this.callbacks.onReconnecting) {
        this.callbacks.onReconnecting(error);
      }
    });

    this.connection.onreconnected(() => {
      console.log('SignalR reconnected');
      this.reconnectAttempts = 0;
      this.connectionId = this.connection?.connectionId ?? null;
      if (this.callbacks.onReconnected) {
        this.callbacks.onReconnected();
      }
    });

    this.connection.on('JobCancellationFailed', (event: JobCancellationFailedEvent | string) => {
      const payload: JobCancellationFailedEvent =
        typeof event === 'string'
          ? { jobId: event, error: 'Cancellation failed' }
          : event;
      console.log('Job cancellation failed:', payload);
      if (this.callbacks.onJobCancellationFailed) {
        this.callbacks.onJobCancellationFailed(payload);
      }
    });
  }
}

// Singleton instance
const resolveSignalRUrl = (): string => {
  const rawValue = process.env.REACT_APP_SIGNALR_URL?.trim();

  if (rawValue) {
    if (rawValue.toLowerCase() === 'origin') {
      if (typeof window !== 'undefined' && window.location?.origin) {
        return `${window.location.origin.replace(/\/$/, '')}/hubs/processing`;
      }
      return '/hubs/processing';
    }

    if (rawValue.startsWith('/')) {
      if (typeof window !== 'undefined' && window.location?.origin) {
        return `${window.location.origin.replace(/\/$/, '')}${rawValue}`;
      }
      return rawValue;
    }

    return rawValue;
  }

  if (typeof window !== 'undefined' && window.location?.origin) {
    return `${window.location.origin.replace(/\/$/, '')}/hubs/processing`;
  }

  return 'http://localhost:5133/hubs/processing';
};

const signalRService = new SignalRService(resolveSignalRUrl());

export default signalRService;