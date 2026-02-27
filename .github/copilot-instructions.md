# Copilot Instructions

## Project Guidelines
- The user follows a strict Repository-Service-Controller layered architecture pattern where each layer has its own role: Repository handles data access, Service handles business logic, Controller handles HTTP concerns only.
- All services must access repositories exclusively through IUnitOfWork (Unit of Work pattern). Services should never directly inject or instantiate repositories — always go through _unitOfWork.RepositoryName.
- External API endpoints must use Code (e.g., SubjectCode, GradeCode, LessonCode) instead of internal database IDs. IDs are for internal use only (FK relationships, DB joins). All CRUD route parameters and request DTOs should reference entities by their Code.