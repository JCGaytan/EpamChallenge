import React from 'react';
import { ProgressBar as BootstrapProgressBar } from 'react-bootstrap';

interface ProgressBarProps {
  progress: number;
  isProcessing: boolean;
  isComplete: boolean;
  isCancelled: boolean;
  isFailed: boolean;
}

const ProgressBar: React.FC<ProgressBarProps> = ({
  progress,
  isProcessing,
  isComplete,
  isCancelled,
  isFailed
}) => {
  const getVariant = () => {
    if (isFailed) return 'danger';
    if (isCancelled) return 'warning';
    if (isComplete) return 'success';
    return 'primary';
  };

  const getLabel = () => {
    if (isFailed) return 'Failed';
    if (isCancelled) return 'Cancelled';
    if (isComplete) return 'Completed';
    if (isProcessing) return `Processing... ${Math.round(progress)}%`;
    return 'Ready';
  };

  const isAnimated = isProcessing && !isCancelled && !isFailed;

  return (
    <div className="app-progress">
      <div className="app-progress__meta">
        <span className="app-progress__label">Progress</span>
        <span className="app-progress__status">{getLabel()}</span>
      </div>
      <BootstrapProgressBar
        now={progress}
        variant={getVariant()}
        animated={isAnimated}
        striped={isAnimated}
        className="app-progress__bar"
      />
    </div>
  );
};

export default ProgressBar;