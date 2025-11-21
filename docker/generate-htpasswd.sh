#!/bin/bash

# Script to generate htpasswd file for Nginx basic authentication
# Usage: ./generate-htpasswd.sh [username] [password]

USERNAME=${1:-admin}
PASSWORD=${2:-textprocessor2025}

echo "Generating htpasswd file for user: $USERNAME"

# Generate htpasswd file
htpasswd -cb ./htpasswd "$USERNAME" "$PASSWORD"

if [ $? -eq 0 ]; then
    echo "✅ htpasswd file created successfully!"
    echo "Username: $USERNAME"
    echo "Password: $PASSWORD"
    echo ""
    echo "File contents:"
    cat ./htpasswd
else
    echo "❌ Failed to create htpasswd file. Make sure 'htpasswd' command is available."
    echo "On Ubuntu/Debian: sudo apt-get install apache2-utils"
    echo "On CentOS/RHEL: sudo yum install httpd-tools"
    echo "On macOS: brew install httpd"
fi