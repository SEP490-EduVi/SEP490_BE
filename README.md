# EduVi Backend

.NET 8 Web API for the EduVi education platform — AI-powered lesson plan evaluation for Vietnamese teachers.

## Architecture

```
EduVi.WebAPI          → Controllers, Middleware, SignalR Hubs, BackgroundServices
EduVi.Services        → Business logic (accessed via DI interfaces)
EduVi.Repositories    → Data access via EF Core, Unit of Work pattern
EduVi.Contracts       → DTOs, shared constants, API response wrappers
```

**Layering rules:**
- **Controller** → handles HTTP concerns only, delegates to Service.
- **Service** → business logic, accesses repositories exclusively through `IUnitOfWork`.
- **Repository** → data access, EF Core queries.
- External APIs use **Code** (e.g., `SubjectCode`, `GradeCode`) — never internal database IDs.

## Tech Stack

| Component | Technology |
|---|---|
| Framework | .NET 8, ASP.NET Core Web API |
| Database | SQL Server (EF Core) |
| Cache / Session | Redis (StackExchange.Redis) |
| Message Queue | RabbitMQ (async task pipeline) |
| Real-time | SignalR (Redis backplane) |
| File Storage | Google Cloud Storage (GCS) |
| Auth | JWT Bearer tokens |
| Payments | PayOS |

## API Modules

| Controller | Scope |
|---|---|
| `AuthController` | Registration, login, OTP, JWT tokens |
| `AdminController` | User management, financial overview, plans |
| `SubjectController` | CRUD subjects |
| `GradeController` | CRUD grades |
| `LessonController` | CRUD lessons |
| `ProjectController` | Teacher project management (CRUD) |
| `PipelineController` | Upload documents, trigger AI analysis, check task status |
| `PaymentController` | Wallet, orders, subscriptions |

## AI Pipeline Flow

```
Teacher uploads document (GCS)
  → POST /api/Pipeline/lesson-analysis
  → Creates/reuses Product (status: NEW)
  → Publishes task to RabbitMQ (lesson.analysis.requests)
  → Python AI worker processes document
  → Worker publishes result to RabbitMQ (pipeline.results)
  → PipelineResultConsumerService receives result
  → Pushes real-time progress via SignalR (PipelineHub)
  → Persists EvaluationResult to Product in DB
  → Teacher receives result via SignalR or GET /api/Pipeline/status/{taskId}
```

### SignalR Hub

- **Endpoint:** `/hubs/pipeline`
- **Event:** `PipelineProgress` — receives `PipelineProgressDto` with `taskId`, `productId`, `status`, `step`, `progress`, `result`, `error`
- **Groups:** Clients join `user_{userId}` to receive their own updates
- **Test page:** `https://localhost:7191/signalr-test.html` (Development only)

### Product Status Flow

| Status | Value | Meaning |
|---|---|---|
| `New` | 0 | Created, queued for AI worker |
| `Processing` | 1 | AI worker is processing |
| `Evaluated` | 2 | AI worker completed successfully |
| `Failed` | 3 | AI worker encountered an error |

## Getting Started

### Prerequisites

- .NET 8 SDK
- SQL Server
- Redis
- RabbitMQ
- GCP service account key (for GCS uploads)

### Configuration

Create `appsettings.json` in `EduVi.WebAPI/` with:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "<sql-server-connection-string>",
    "RedisConnection": "<redis-connection-string>"
  },
  "Jwt": {
    "SecretKey": "<your-secret>",
    "Issuer": "<issuer>",
    "Audience": "<audience>"
  },
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest",
    "VirtualHost": "/"
  },
  "GCS": {
    "BucketName": "<your-gcs-bucket>"
  }
}
```

Place your GCP service account key at `private/gcp-key.json` (gitignored).

### Run

```bash
cd EduVi.WebAPI
dotnet run
```

- Swagger: `https://localhost:7191/swagger`
- SignalR test: `https://localhost:7191/signalr-test.html`