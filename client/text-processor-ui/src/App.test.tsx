import React from 'react';
import { render, screen } from '@testing-library/react';
import App from './App';

const mockHook = {
  currentJob: null,
  processedResult: '',
  isProcessing: false,
  progress: 0,
  error: null,
  startProcessing: jest.fn(),
  cancelProcessing: jest.fn(),
  clearJob: jest.fn(),
  isConnected: true,
  isConnecting: false,
  connectionError: null
};

jest.mock('./hooks/useTextProcessing', () => ({
  useTextProcessing: () => mockHook
}));

describe('App', () => {
  it('renders the text processing form controls', () => {
    render(<App />);

    expect(screen.getByText(/text processor/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /process text/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
  });
});
