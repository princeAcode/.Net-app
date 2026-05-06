# Solution Design — Document Intake Service

## Overview

The Document Intake Service is a .NET 8 Web API that accepts legal document submissions via multipart HTTP upload, stores the file payload in S3-compatible object storage, and asynchronously generates a text preview through a background processing worker. Every state transition — from receipt through storage, queueing, and processing — is recorded in an append-only audit trail attached to each document record. The design prioritises a clean domain model, observable state, and infrastructure abstractions that allow the system to run locally against LocalStack with no code changes required to deploy to real AWS.

---

## Design Decisions

- **`InMemoryQueueService` (Channel-based) as the default queue.** `System.Threading.Channels` provides a high-performance, allocation-efficient producer/consumer primitive built into .NET. Using it as the default means the API and worker start with zero external dependencies — no SQS, no Docker — which makes the development loop fast and the CI pipeline trivial. `SqsQueueService` implements the same `IQueueService` interface and is a configuration-level swap for production deployments.

- **In-memory repository over a database.** The `InMemoryDocumentRepository` stores documents in a `ConcurrentDictionary`, which is safe for concurrent access without explicit locking and avoids introducing a database dependency for an assignment scope. The `IDocumentRepository` interface means a production implementation (e.g. Entity Framework Core + PostgreSQL) could be substituted without touching the service or worker layer.

- **Deduplication via a composite key (`provider` + `sourceDocumentId`).** These two fields together represent the logical identity of a document as seen by an external provider. The comparison is case-insensitive (`StringComparison.OrdinalIgnoreCase`) to be tolerant of inconsistent casing from upstream systems. When a duplicate is detected the original document is returned immediately and an audit entry is appended rather than silently discarding the request, giving the caller full visibility into what happened.

- **Append-only audit trail.** `Document.AuditTrail` is a `List<AuditEntry>` that is only ever appended to, never mutated. Each entry captures the event name, a UTC timestamp, and an optional detail string. This gives a complete, ordered history of every state transition for a given document, supporting debugging, compliance, and customer support use cases without requiring a separate event log table.

- **`BackgroundService` for the processing worker.** `BackgroundService` is the idiomatic .NET abstraction for long-running background tasks. It integrates cleanly with the hosted service lifecycle, respects `IHostApplicationLifetime`, and ensures the worker is started and stopped deterministically alongside the API process. The worker loop catches exceptions per message to ensure a single malformed or unprocessable document cannot halt processing for all subsequent messages.

---

## Trade-offs

- **In-memory state is lost on restart.** The `ConcurrentDictionary` backing the repository is process-local; a restart loses all documents. This is an acceptable trade-off for an assignment but in production a durable store (relational database or document store) would be required. The `IDocumentRepository` interface makes this a contained change.

- **Preview is text-only with a binary fallback.** Rather than implementing format-specific extractors (PDF, DOCX, etc.), the worker checks the `ContentType` and falls back to a human-readable message for non-text files. A production system would integrate a document conversion library (e.g. `DocumentFormat.OpenXml`, iTextSharp, or a cloud extraction service) behind the same interface.

- **No authentication or authorisation.** All endpoints are publicly accessible. A production deployment would add ASP.NET Core authentication middleware (JWT bearer tokens or API keys), role-based authorisation on controller actions, and audit logging of who submitted each document.

- **Single-instance worker.** The background worker is a singleton within one process. Under high load a single worker would become a bottleneck. At scale this would be replaced with a competing consumers pattern — multiple worker processes or instances each polling SQS independently, relying on SQS visibility timeouts to prevent duplicate processing.

---

## Cloud Compatibility

The service is designed so that moving from LocalStack to real AWS requires only configuration changes, not code changes. `IAmazonS3` and `IAmazonSQS` are registered in `Program.cs` with explicit `ServiceURL` and dummy credentials pointing at `http://localhost:4566`. In a production deployment, removing `ServiceURL` and `ForcePathStyle` and supplying real credentials (via environment variables, IAM role, or AWS Secrets Manager) is sufficient. Similarly, swapping `InMemoryQueueService` for `SqsQueueService` is a single line change in `Program.cs`, or can be made configuration-driven using a feature flag or environment variable. No business logic, domain model, or controller code is aware of the underlying infrastructure.

---

## If I Had More Time

- **Persistent storage.** Replace `InMemoryDocumentRepository` with an Entity Framework Core implementation backed by PostgreSQL, including proper migrations, optimistic concurrency, and indexed lookups on the deduplication key.

- **Authentication and multi-tenancy.** Add JWT bearer authentication and scope the deduplication check and document queries to the authenticated caller's tenant, so providers cannot see each other's documents.

- **Integration tests.** Add a second test project using `WebApplicationFactory<Program>` and a real LocalStack container (via `Testcontainers.LocalStack`) to test the full HTTP → S3 → SQS → worker → processed flow end-to-end.

- **Pagination and filtering on `GET /documents`.** Add a collection endpoint with cursor-based pagination, filtering by `provider`, `status`, and date range, and structured JSON logging (e.g. Serilog + structured output) to make the service production-observable.
