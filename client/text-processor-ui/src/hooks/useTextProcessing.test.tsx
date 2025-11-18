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

  return {
    __esModule: true,
    default: {
      connect,
      disconnect,
      joinJobGroup,
      leaveJobGroup,
      cancelJob
    },
    __mock: {
      connect,
      disconnect,
      joinJobGroup,
      leaveJobGroup,
      cancelJob
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
    expect(mockProcessText).toHaveBeenCalledWith({ text: 'Hello' });
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
});
