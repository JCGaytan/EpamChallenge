import React from 'react';
import { act, render, waitFor } from '@testing-library/react';
import type { SignalRCallbacks } from '../services/signalr';
import { JobStatus } from '../models/types';
import { useTextProcessing, type UseTextProcessingResult } from './useTextProcessing';

type SignalRMockModule = {
  __mock: {
    connect: jest.Mock<Promise<void>, [SignalRCallbacks?]>;
    disconnect: jest.Mock<Promise<void>, []>;
    joinJobGroup: jest.Mock<Promise<void>, [string]>;
    leaveJobGroup: jest.Mock<Promise<void>, [string]>;
    cancelJob: jest.Mock<Promise<void>, [string]>;
    getConnectionId: jest.Mock<string | null, []>;
    waitForConnectionId: jest.Mock<Promise<string | null>, [number?]>;
  };
};

type ApiMockModule = {
  __mock: {
    processText: jest.Mock<Promise<any>, [{ text: string }]>;
  };
};

jest.mock('../services/signalr', () => {
  const connect = jest.fn();
  const disconnect = jest.fn();
  const joinJobGroup = jest.fn();
  const leaveJobGroup = jest.fn();
  const cancelJob = jest.fn();
  const getConnectionId = jest.fn();
  const waitForConnectionId = jest.fn();

  return {
    __esModule: true,
    default: {
      connect,
      disconnect,
      joinJobGroup,
      leaveJobGroup,
      cancelJob,
      getConnectionId,
      waitForConnectionId
    },
    __mock: {
      connect,
      disconnect,
      joinJobGroup,
      leaveJobGroup,
      cancelJob,
      getConnectionId,
      waitForConnectionId
    }
  };
});

jest.mock('../services/api', () => {
  const processText = jest.fn();

  return {
    __esModule: true,
    default: {
      processText
    },
    __mock: {
      processText
    }
  };
});

const signalRModule = jest.requireMock('../services/signalr') as SignalRMockModule;
const apiModule = jest.requireMock('../services/api') as ApiMockModule;

const mockConnect = signalRModule.__mock.connect;
const mockDisconnect = signalRModule.__mock.disconnect;
const mockJoinJobGroup = signalRModule.__mock.joinJobGroup;
const mockLeaveJobGroup = signalRModule.__mock.leaveJobGroup;
const mockCancelJob = signalRModule.__mock.cancelJob;
const mockGetConnectionId = signalRModule.__mock.getConnectionId;
const mockWaitForConnectionId = signalRModule.__mock.waitForConnectionId;
const mockProcessText = apiModule.__mock.processText;

const TestHarness: React.FC<{ onChange: (value: UseTextProcessingResult) => void }> = ({ onChange }) => {
  const value = useTextProcessing();

  React.useEffect(() => {
    onChange(value);
  }, [value, onChange]);

  return null;
};

describe('useTextProcessing', () => {
  let latest: { current: UseTextProcessingResult | null };
  let registeredCallbacks: SignalRCallbacks | undefined;

  beforeEach(() => {
    latest = { current: null };
    registeredCallbacks = undefined;

    jest.clearAllMocks();

    mockConnect.mockImplementation(async (callbacks: SignalRCallbacks = {}) => {
      registeredCallbacks = callbacks;
      callbacks.onConnected?.();
    });

    mockDisconnect.mockResolvedValue(undefined);
    mockJoinJobGroup.mockResolvedValue(undefined);
    mockLeaveJobGroup.mockResolvedValue(undefined);
    mockCancelJob.mockResolvedValue(undefined);

    mockGetConnectionId.mockReturnValue('test-connection-id');
    mockWaitForConnectionId.mockResolvedValue('test-connection-id');

    mockProcessText.mockResolvedValue({
      id: 'job-1',
      inputText: 'Hello',
      status: JobStatus.Running,
      progress: 0,
      createdAt: new Date().toISOString()
    });
  });

  const mountHook = async () => {
    render(<TestHarness onChange={(value) => { latest.current = value; }} />);
    await waitFor(() => expect(latest.current).not.toBeNull());
  };

  it('connects, starts a job, and joins the SignalR group', async () => {
    await mountHook();

    await act(async () => {
      await latest.current!.startProcessing('Hello');
    });

    expect(mockConnect).toHaveBeenCalledTimes(1);
    expect(mockProcessText).toHaveBeenCalledWith({ text: 'Hello' }, 'test-connection-id');
    await waitFor(() => expect(latest.current?.currentJob?.id).toBe('job-1'));
    expect(mockJoinJobGroup).toHaveBeenCalledWith('job-1');
    expect(latest.current?.isProcessing).toBe(true);
  });

  it('updates processed result and status when SignalR events arrive', async () => {
    await mountHook();

    await act(async () => {
      await latest.current!.startProcessing('Hello');
    });

    await waitFor(() => expect(registeredCallbacks).toBeDefined());

    act(() => {
      registeredCallbacks?.onCharacterProcessed?.({
        jobId: 'job-1',
        character: 'A',
        progress: 42
      });
    });

    await waitFor(() => expect(latest.current?.processedResult).toBe('A'));
    expect(latest.current?.progress).toBe(42);

    const completionPayload = {
      jobId: 'job-1',
      result: 'A/QQ==',
      completedAt: new Date().toISOString(),
      duration: '00:00:01'
    };

    act(() => {
      registeredCallbacks?.onJobCompleted?.(completionPayload);
    });

    await waitFor(() => expect(latest.current?.currentJob?.status).toBe(JobStatus.Completed));
    expect(latest.current?.processedResult).toBe('A/QQ==');
    expect(latest.current?.isProcessing).toBe(false);
  });

  it('clears the active job and leaves the SignalR group', async () => {
    await mountHook();

    await act(async () => {
      await latest.current!.startProcessing('Hello');
    });

    await waitFor(() => expect(latest.current?.currentJob).not.toBeNull());

    act(() => {
      latest.current!.clearJob();
    });

    await waitFor(() => expect(latest.current?.currentJob).toBeNull());
    expect(latest.current?.processedResult).toBe('');
    expect(latest.current?.isProcessing).toBe(false);
    expect(mockLeaveJobGroup).toHaveBeenCalledWith('job-1');
  });

  it('prevents multiple concurrent processes in the same tab', async () => {
    await mountHook();

    // Start first process
    await act(async () => {
      await latest.current!.startProcessing('Hello');
    });

    await waitFor(() => expect(latest.current?.isProcessing).toBe(true));

    // Try to start second process while first is running
    await expect(async () => {
      await act(async () => {
        await latest.current!.startProcessing('World');
      });
    }).rejects.toThrow('A processing job is already running in this tab. You can start another process in a new tab or window.');

    expect(mockProcessText).toHaveBeenCalledTimes(1); // Only called once
  });

  it('allows new process after previous one is completed', async () => {
    await mountHook();

    // Start first process
    await act(async () => {
      await latest.current!.startProcessing('Hello');
    });

    await waitFor(() => expect(latest.current?.isProcessing).toBe(true));

    // Complete the job
    act(() => {
      registeredCallbacks?.onJobCompleted?.({
        jobId: 'job-1',
        result: 'Hello/SGVsbG8=',
        completedAt: new Date().toISOString(),
        duration: '00:00:01'
      });
    });

    await waitFor(() => expect(latest.current?.isProcessing).toBe(false));

    // Now should be able to start a new process
    mockProcessText.mockResolvedValue({
      id: 'job-2',
      inputText: 'World',
      status: JobStatus.Running,
      progress: 0,
      createdAt: new Date().toISOString()
    });

    await act(async () => {
      await latest.current!.startProcessing('World');
    });

    await waitFor(() => expect(latest.current?.currentJob?.id).toBe('job-2'));
    expect(mockProcessText).toHaveBeenCalledTimes(2);
  });

  it('allows new process after previous one is cancelled', async () => {
    await mountHook();

    // Start first process
    await act(async () => {
      await latest.current!.startProcessing('Hello');
    });

    await waitFor(() => expect(latest.current?.isProcessing).toBe(true));

    // Cancel the job
    act(() => {
      registeredCallbacks?.onJobCancelled?.({
        jobId: 'job-1',
        cancelledAt: new Date().toISOString()
      });
    });

    await waitFor(() => expect(latest.current?.isProcessing).toBe(false));

    // Now should be able to start a new process
    mockProcessText.mockResolvedValue({
      id: 'job-2',
      inputText: 'World',
      status: JobStatus.Running,
      progress: 0,
      createdAt: new Date().toISOString()
    });

    await act(async () => {
      await latest.current!.startProcessing('World');
    });

    await waitFor(() => expect(latest.current?.currentJob?.id).toBe('job-2'));
    expect(mockProcessText).toHaveBeenCalledTimes(2);
  });
});
