import React from 'react';
import { Badge } from 'react-bootstrap';

interface ConnectionStatusProps {
  isConnected: boolean;
  isConnecting: boolean;
  connectionError: string | null;
}

const ConnectionStatus: React.FC<ConnectionStatusProps> = ({
  isConnected,
  isConnecting,
  connectionError
}) => {
  const getStatusBadge = () => {
    if (connectionError) {
      return (
        <Badge bg="danger" className="connection-status__badge">
          <i className="bi bi-x-circle me-1"></i>
          Connection Failed
        </Badge>
      );
    }
    
    if (isConnecting) {
      return (
        <Badge bg="warning" className="connection-status__badge">
          <i className="bi bi-arrow-clockwise me-1"></i>
          Connecting...
        </Badge>
      );
    }
    
    if (isConnected) {
      return (
        <Badge bg="success" className="connection-status__badge">
          <i className="bi bi-check-circle me-1"></i>
          Connected
        </Badge>
      );
    }
    
    return (
      <Badge bg="secondary" className="connection-status__badge">
        <i className="bi bi-circle me-1"></i>
        Disconnected
      </Badge>
    );
  };

  return (
    <div className="connection-status">
      <span className="connection-status__label">Real-time status</span>
      <div className="connection-status__badge-wrapper">
        {getStatusBadge()}
      </div>
      {connectionError && (
        <div className="connection-status__error">
          {connectionError}
        </div>
      )}
    </div>
  );
};

export default ConnectionStatus;