# Document Intake Service

A .NET 8 Web API that ingests legal documents, stores them in S3, and asynchronously generates text previews via a background processing worker.

---

## Prerequisites

| Tool | Version |
|---|---|
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.0.x |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Latest |
| [AWS CLI](https://docs.aws.amazon.com/cli/latest/userguide/install-cliv2.html) | v2 |
| Git | Any recent version |

---

## Quick Start

```bash
git clone <repo-url>
cd DocumentIntake
chmod +x run.sh
./run.sh
```

`run.sh` will:
1. Verify Docker is running
2. Start LocalStack (S3 + SQS) via Docker Compose
3. Create the required S3 bucket and SQS queue
4. Start the API at `http://localhost:5000`

---

## Manual Setup

If you prefer to run each step yourself:

```bash
# Start LocalStack
docker-compose up -d

# Wait ~5 seconds, then create resources
aws --endpoint-url=http://localhost:4566 s3 mb s3://document-intake --region us-east-1
aws --endpoint-url=http://localhost:4566 sqs create-queue --queue-name document-processing --region us-east-1

# Start the API
dotnet run --project src/DocumentIntake.Api
```

---

## API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/documents` | Submit a document (`multipart/form-data` with metadata and file) |
| `GET` | `/documents/{id}` | Retrieve the full document record by ID |
| `GET` | `/documents/{id}/status` | Get processing status and full audit trail |
| `GET` | `/documents/{id}/preview` | Get the generated text preview (returns `409` if not yet processed) |

### Example: Submit a document

```bash
curl -X POST http://localhost:5000/documents \
  -F "sourceDocumentId=SRC-001" \
  -F "provider=acme" \
  -F "title=Sample Contract" \
  -F "jurisdiction=US" \
  -F "file=@/path/to/document.txt"
```

---

## Running Tests

Tests run entirely in-memory — no Docker or AWS credentials required.

```bash
dotnet test
```

---

## Architecture

**Intake flow.** When a document is submitted via `POST /documents`, the `DocumentService` checks for an existing document using a case-insensitive composite key of `provider + sourceDocumentId`. If a duplicate is detected, an audit entry is appended and the existing record is returned. For new documents, the file is uploaded to S3 (LocalStack locally, real AWS in production), the document status progresses from `Received` → `Stored` → `Queued`, and each transition is recorded in an append-only audit trail before the record is persisted to the in-memory repository.

**Background processing.** `DocumentProcessingWorker` is a .NET `BackgroundService` that continuously dequeues messages from `IQueueService`. For each message it fetches the document from the repository, retrieves the file bytes from S3, generates a text preview (first 500 characters for `text/*` content types, or a binary fallback message for other types), and updates the document status to `Processed`. All failures are caught per-message, the document is marked `Failed` with the error detail, and the worker loop continues — a single bad message cannot bring down the worker.

**Local vs. cloud design.** The `IQueueService` abstraction decouples the worker from any specific transport. Locally, `InMemoryQueueService` (backed by `System.Threading.Channels`) is used with no external dependencies. Switching to SQS in a deployed environment is a single line change in `Program.cs`. The same pattern applies to `IStorageService` — the S3 client is pre-configured for LocalStack but works against real AWS with no code changes.
# .Net-app
# .Net-app
