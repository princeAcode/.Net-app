#!/bin/bash
set -e

echo "Creating S3 bucket: document-intake"
awslocal s3 mb s3://document-intake

echo "Creating SQS queue: document-processing"
awslocal sqs create-queue --queue-name document-processing

echo "LocalStack resources initialised."
