#!/usr/bin/env bash
# Requires: Docker, AWS CLI, .NET 8 SDK

set -euo pipefail

BASE_URL="http://localhost:5000"
LOCALSTACK_URL="http://localhost:4566"
REGION="us-east-1"

# ---------------------------------------------------------------------------
# 1. Check Docker is running
# ---------------------------------------------------------------------------
if ! docker info > /dev/null 2>&1; then
  echo "ERROR: Docker is not running. Please start Docker Desktop and try again."
  exit 1
fi

# ---------------------------------------------------------------------------
# 2. Start LocalStack
# ---------------------------------------------------------------------------
echo "Starting LocalStack..."
docker-compose up -d

# ---------------------------------------------------------------------------
# 3. Wait for LocalStack to be ready
# ---------------------------------------------------------------------------
echo "Waiting for LocalStack to be ready..."
sleep 5

# ---------------------------------------------------------------------------
# 4. Create S3 bucket (idempotent)
# ---------------------------------------------------------------------------
echo "Creating S3 bucket: document-intake"
aws --endpoint-url="$LOCALSTACK_URL" \
    s3 mb s3://document-intake \
    --region "$REGION" || true

# ---------------------------------------------------------------------------
# 5. Create SQS queue (idempotent)
# ---------------------------------------------------------------------------
echo "Creating SQS queue: document-processing"
aws --endpoint-url="$LOCALSTACK_URL" \
    sqs create-queue \
    --queue-name document-processing \
    --region "$REGION" || true

# ---------------------------------------------------------------------------
# 6. Start the API
# ---------------------------------------------------------------------------
echo ""
echo "-----------------------------------------------------------------------"
echo "  Document Intake Service is starting."
echo "  Base URL : $BASE_URL"
echo "  LocalStack: $LOCALSTACK_URL"
echo "-----------------------------------------------------------------------"
echo ""

dotnet run --project src/DocumentIntake.Api
