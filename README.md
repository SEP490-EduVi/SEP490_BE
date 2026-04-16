# EduVi Backend


.NET 8 Web API for the EduVi education platform, featuring an asynchronous AI pipeline for lesson analysis, slide generation, video generation, and game generation.

## Architecture

```
EduVi.WebAPI          -> Controllers, Middleware, SignalR Hubs, BackgroundServices
EduVi.Services        -> Business logic (accessed via DI interfaces)
EduVi.Repositories    -> Data access via EF Core, Unit of Work pattern
EduVi.Contracts       -> DTOs, shared constants, API response wrappers
```

Layering rules:
- Controller: handles HTTP concerns only, delegates to Service.
- Service: business logic, accesses repositories exclusively through IUnitOfWork.
- Repository: data access and EF Core queries.
- External APIs use Code fields (for example SubjectCode, GradeCode, LessonCode), not internal database IDs.

## Tech Stack

| Component | Technology |
|---|---|
| Framework | .NET 8, ASP.NET Core Web API |
| Database | SQL Server (EF Core) |
| Cache / Session | Redis (StackExchange.Redis) |
| Message Queue | RabbitMQ |
| Real-time | SignalR (Redis backplane) |
| File Storage | Google Cloud Storage (GCS) |
| Auth | JWT Bearer tokens |
| Payments | PayOS |

## API Modules

| Controller | Scope |
|---|---|
| AuthController | Registration, login, OTP, JWT, Google login |
| AdminController | User management, plans, financial overview |
| SubjectController | Subject management |
| GradeController | Grade management |
| LessonController | Lesson management |
| ClassroomController | Classroom and student management |
| ProjectController | Teacher project management |
| InputDocumentController | Input document upload and management |
| PipelineController | AI analysis/slide/video task operations |
| ProductController | Digital content (product) management |
| VideoController | Product video management |
| GamesController | Game generation and task status |
| MaterialController | Material marketplace operations |
| PaymentController | Wallet, top-up, subscription purchases |
| WithdrawalController | Expert withdrawal flow |
| ExpertController | Expert verification and profile operations |
| TeacherController | Teacher profile operations |
| StaffController | Staff profile operations |
| CurriculumIngestionController | Curriculum ingestion flow |
| TextbookIngestionController | Textbook ingestion flow |

## AI Pipeline Flow

```
Teacher uploads document (GCS)
  -> POST /api/Pipeline/lesson-analysis
  -> Creates/reuses Product
  -> Publishes task to RabbitMQ
  -> Python AI worker processes document
  -> Worker publishes result to RabbitMQ
  -> Background consumer receives result
  -> Pushes real-time progress via SignalR (PipelineHub)
  -> Persists result to database
  -> Teacher receives result via SignalR or status endpoint
```

### SignalR Hub

- Endpoint: /hubs/pipeline
- Event: PipelineProgress
- Group model: user_{userId}
- Development test page: https://localhost:7191/signalr-test.html

### Product Status Flow

| Status | Value | Meaning |
|---|---|---|
| New | 0 | Created |
| Processing | 1 | AI worker is processing |
| Evaluated | 2 | Lesson analysis completed |
| Failed | 3 | Processing failed |
| GeneratingSlides | 4 | Slide generation in progress |
| SlidesGenerated | 5 | Slides generated successfully |
| SlidesFailed | 6 | Slide generation failed |
| Deleted | 7 | Soft deleted |

### Video Status Flow

| Status | Value | Meaning |
|---|---|---|
| Queued | 0 | Video task queued |
| Completed | 1 | Video generated successfully |
| Failed | 2 | Video generation failed |
| Deleted | 3 | Soft deleted |

## Getting Started

### Prerequisites

- .NET 8 SDK
- SQL Server
- Redis
- RabbitMQ
- GCP service account key (for GCS)

### Configuration

Create appsettings.json in EduVi.WebAPI/ with:

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
  },
  "PayOS": {
    "ClientId": "<your-client-id>",
    "ApiKey": "<your-api-key>",
    "ChecksumKey": "<your-checksum-key>"
  }
}
```

Place your GCP service account key at private/gcp-key.json.

Local launch profiles:
- http://localhost:5202
- https://localhost:7191

### Run

```bash
dotnet build SEP490_BE.slnx
dotnet run --project EduVi.WebAPI/EduVi.WebAPI.csproj
```

Access:
- Swagger: http://localhost:5202/swagger
- SignalR test page: http://localhost:5202/signalr-test.html

## Notes

- UI-facing responses are localized to Vietnamese in the backend.
- Model validation responses are standardized in Program.cs to avoid default ASP.NET English validation messages.
- Field-level validation messages can be customized via DTO validation attributes.

## Quick Onboarding

1. Start with Program.cs to understand dependency registration and middleware pipeline.
2. Read controllers for the module you are implementing.
3. Read the matching service layer to understand business logic.
4. Read repositories and UnitOfWork for data flow and transactions.
5. Check DTOs in EduVi.Contracts/DTOs before changing any API contract.