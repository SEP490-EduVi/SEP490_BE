# Copilot Instructions

## Project Guidelines
- The user follows a strict Repository-Service-Controller layered architecture pattern where each layer has its own role: Repository handles data access, Service handles business logic, Controller handles HTTP concerns only.
- All services must access repositories exclusively through IUnitOfWork (Unit of Work pattern). Services should never directly inject or instantiate repositories — always go through _unitOfWork.RepositoryName.
- External API endpoints must use Code (e.g., SubjectCode, GradeCode, LessonCode) instead of internal database IDs. IDs are for internal use only (FK relationships, DB joins). All CRUD route parameters and request DTOs should reference entities by their Code.
- Material selection/upload should happen during product creation, and each product must have its own distinct selected material set for the edit-slide phase.
- Adhere to the tech debt principle: do the minimum work to make something production-ready. If you need to revisit a piece of code, refactor it then. Plan for future needs without implementing them prematurely.
- For production deployment, the project uses MSSQL via Google Cloud SQL (not a container). The GCP VM has a service account attached for ADC (Application Default Credentials).

## Naming Conventions
- Always write full variable and method names. Avoid abbreviations like "resp", "res", "numofsomething" — typing cost is negligible, readability matters more.

## Logging Guidelines
- Only log API calls with timing to identify slow operations and failures at the point of failure with unique, thoughtful error messages. Avoid logging routine operations to prevent cluttering the logs.
- Never delete or remove existing log statements when making other code changes. If a log statement needs modification, update it — don't remove it.