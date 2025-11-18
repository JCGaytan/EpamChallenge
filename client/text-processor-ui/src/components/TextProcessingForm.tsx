import React, { useState, useCallback } from 'react';
import { 
  Container, 
  Row, 
  Col, 
  Card, 
  Form, 
  Button, 
  Alert, 
  Spinner
} from 'react-bootstrap';
import { useTextProcessing } from '../hooks/useTextProcessing';
import { JobStatus } from '../models/types';
import ProgressBar from './ProgressBar';
import ConnectionStatus from './ConnectionStatus';

const TextProcessingForm: React.FC = () => {
  const [inputText, setInputText] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  
  const {
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
  } = useTextProcessing();

  const handleSubmit = useCallback(async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!inputText.trim()) {
      return;
    }

    setIsSubmitting(true);
    
    try {
      await startProcessing(inputText.trim());
    } catch (error: any) {
      console.error('Failed to start processing:', error);
    } finally {
      setIsSubmitting(false);
    }
  }, [inputText, startProcessing]);

  const handleCancel = useCallback(async () => {
    try {
      await cancelProcessing();
    } catch (error: any) {
      console.error('Failed to cancel processing:', error);
    }
  }, [cancelProcessing]);

  const handleClear = useCallback(() => {
    clearJob();
    setInputText('');
  }, [clearJob]);

  const canStartProcessing = !isProcessing && !isSubmitting && inputText.trim().length > 0;
  const canCancelProcessing = Boolean(
    isProcessing && currentJob && [JobStatus.Pending, JobStatus.Running].includes(currentJob.status)
  );

  // Calculate job status indicators
  const isComplete = currentJob?.status === JobStatus.Completed;
  const isCancelled = currentJob?.status === JobStatus.Cancelled;
  const isFailed = currentJob?.status === JobStatus.Failed;

  return (
    <Container className="text-processing-container py-4">
      <Row className="justify-content-center">
        <Col md={8} lg={6}>
          <Card className="app-card">
            <Card.Header className="app-card__header">
              <div className="d-flex justify-content-between align-items-center">
                <h5 className="mb-0">Text Processor</h5>
                <ConnectionStatus
                  isConnected={isConnected}
                  isConnecting={isConnecting}
                  connectionError={connectionError}
                />
              </div>
            </Card.Header>
            
            <Card.Body className="app-card__body">
              <Form onSubmit={handleSubmit}>
                <Form.Group className="mb-3">
                  <Form.Label>Enter text to process:</Form.Label>
                  <Form.Control
                    as="textarea"
                    rows={4}
                    value={inputText}
                    onChange={(e) => setInputText(e.target.value)}
                    placeholder="Type your text here..."
                    disabled={isProcessing}
                    maxLength={10000}
                    style={{ resize: 'vertical' }}
                  />
                  <Form.Text className="text-muted">
                    {inputText.length}/10,000 characters
                  </Form.Text>
                </Form.Group>

                <div className="d-grid gap-2 d-md-flex justify-content-md-start mb-3">
                  <Button
                    variant="primary"
                    type="submit"
                    disabled={!canStartProcessing}
                    className="me-md-2"
                  >
                    {isSubmitting ? (
                      <>
                        <Spinner
                          as="span"
                          animation="border"
                          size="sm"
                          role="status"
                          aria-hidden="true"
                          className="me-2"
                        />
                        Starting...
                      </>
                    ) : (
                      'Process Text'
                    )}
                  </Button>

                  <Button
                    variant="warning"
                    onClick={handleCancel}
                    disabled={!canCancelProcessing}
                    className="me-md-2"
                  >
                    Cancel
                  </Button>

                  <Button
                    variant="outline-secondary"
                    onClick={handleClear}
                    disabled={isProcessing}
                  >
                    Clear
                  </Button>
                </div>

                {/* Progress Bar */}
                {(isProcessing || isComplete || isCancelled || isFailed) && (
                  <ProgressBar
                    progress={progress}
                    isProcessing={isProcessing}
                    isComplete={isComplete}
                    isCancelled={isCancelled}
                    isFailed={isFailed}
                  />
                )}

                {/* Error Display */}
                {error && (
                  <Alert variant="danger" className="mb-3">
                    <Alert.Heading>Error</Alert.Heading>
                    <p className="mb-0">{error}</p>
                  </Alert>
                )}

                {/* Job Information */}
                {currentJob && (
                  <Alert variant="info" className="mb-3">
                    <div className="d-flex justify-content-between align-items-start">
                      <div>
                        <strong>Job ID:</strong> {currentJob.id}<br/>
                        <strong>Status:</strong> {currentJob.status}<br/>
                        <strong>Created:</strong> {new Date(currentJob.createdAt).toLocaleString()}
                      </div>
                      <div className="text-end">
                        {currentJob.startedAt && (
                          <>
                            <strong>Started:</strong> {new Date(currentJob.startedAt).toLocaleString()}<br/>
                          </>
                        )}
                        {currentJob.completedAt && (
                          <>
                            <strong>Completed:</strong> {new Date(currentJob.completedAt).toLocaleString()}<br/>
                          </>
                        )}
                      </div>
                    </div>
                  </Alert>
                )}

                {/* Result Display */}
                {processedResult && (
                  <Form.Group className="mb-3">
                    <Form.Label>
                      Processed Result:
                      {isProcessing && (
                        <span className="text-primary ms-2">
                          <Spinner
                            as="span"
                            animation="grow"
                            size="sm"
                            role="status"
                            aria-hidden="true"
                          />
                          Receiving characters...
                        </span>
                      )}
                    </Form.Label>
                    <Form.Control
                      as="textarea"
                      rows={6}
                      value={processedResult}
                      readOnly
                      style={{
                        fontFamily: 'monospace',
                        fontSize: '14px',
                        wordBreak: 'break-all',
                        resize: 'vertical'
                      }}
                    />
                    <Form.Text className="text-muted">
                      {processedResult.length} characters received
                      {isProcessing && ' (updating in real-time)'}
                    </Form.Text>
                  </Form.Group>
                )}
              </Form>
            </Card.Body>
          </Card>

          {/* Example Section */}
          <Card className="app-card app-card--secondary mt-4">
            <Card.Header className="app-card__header app-card__header--subtle">
              <h6 className="mb-0">How it works</h6>
            </Card.Header>
            <Card.Body className="app-card__body">
              <p className="mb-2">
                <strong>Example input:</strong> "Hello, World!"
              </p>
              <p className="mb-2">
                <strong>Generated output:</strong> " 1!1,1H1W1d1e1l3o2r1/SGVsbG8sIFdvcmxkIQ=="
              </p>
              <p className="small text-muted mb-0">
                The system analyzes character frequency, sorts them, and appends a Base64 encoding.
                Each character is streamed to you in real-time with random delays (1-5 seconds).
              </p>
            </Card.Body>
          </Card>
        </Col>
      </Row>
    </Container>
  );
};

export default TextProcessingForm;