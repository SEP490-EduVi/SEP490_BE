# Quick Reference: Role Codes Implementation

## 📋 Tóm tắt thay đổi

✅ Đã implement flow tạo tài khoản với auto-generated Code cho tất cả roles  
✅ **NEW:** Admin APIs đã chuyển sang dùng UserCode thay vì userId

## 🎯 Flow hoạt động

```
User Register → Tạo Users (UserCode: USER000001) → Tạo role record với Code → Gửi OTP
                  ↓                                        ↓
         UserId: 10, UserCode: USER000001        TeacherCode: TEACHER000001
```

## 🆕 Admin API Update

**Tất cả Admin user management endpoints đã đổi từ `userId` → `userCode`:**

```bash
# ❌ TRƯỚC (dùng userId - internal ID)
GET    /api/admin/users/123
PUT    /api/admin/users/123
POST   /api/admin/users/123/ban
DELETE /api/admin/users/123

# ✅ SAU (dùng userCode - external code)
GET    /api/admin/users/USER000001
PUT    /api/admin/users/USER000001
POST   /api/admin/users/USER000001/ban
DELETE /api/admin/users/USER000001
```

**Migration bắt buộc:** Chạy `Migrations/AddUserCodeToUsersTable.sql`  
**Chi tiết:** [AdminAPI_UserCode_Migration.md](docs/AdminAPI_UserCode_Migration.md)

## 📝 Files đã thay đổi

### Models (thêm Code fields):
- [Admins.cs](../EduVi.Repositories/Models/Admins.cs) → `AdminCode`
- [Experts.cs](../EduVi.Repositories/Models/Experts.cs) → `ExpertCode`  
- [Teachers.cs](../EduVi.Repositories/Models/Teachers.cs) → `TeacherCode`
- [Staffs.cs](../EduVi.Repositories/Models/Staffs.cs) → `StaffCode` (đã có sẵn)

### DbContext (thêm unique indexes):
- [EduViContext.cs](../EduVi.Repositories/DBContext/EduViContext.cs)

### Repository (thêm methods tạo role records):
- [IAuthenticationRepository.cs](../EduVi.Repositories/Interfaces/IAuthenticationRepository.cs)
  - `CreateAdminAsync()`
  - `CreateExpertAsync()`
  - `CreateTeacherAsync()`
  - `CreateStaffAsync()`
  - `GenerateUniqueCodeAsync()`

- [AuthenticationRepository.cs](../EduVi.Repositories/Repositories/AuthenticationRepository.cs)
  - Implementation của các methods trên

### Service (tự động tạo role records):
- [AuthenticationService.cs](../EduVi.Services/Authentication/AuthenticationService.cs)
  - `RegisterAsync()` → auto-create role record based on RoleId
  - `GoogleLoginAsync()` → auto-create Teacher record

### Migration & Docs:
- [AddCodeFieldsToRoleTables.sql](Migrations/AddCodeFieldsToRoleTables.sql) → Add role-specific codes
- [AddUserCodeToUsersTable.sql](Migrations/AddUserCodeToUsersTable.sql) → Add UserCode to Users
- [UserRegistrationFlow.md](docs/UserRegistrationFlow.md) → Registration flow details
- [AdminAPI_UserCode_Migration.md](docs/AdminAPI_UserCode_Migration.md) → Admin API migration guide

## 🚀 Next Steps

### 1. Chạy Migrations (BẮT BUỘC - theo thứ tự):
```bash
# Migration 1: Add role-specific codes (AdminCode, TeacherCode, etc.)
sqlcmd -S your_server -d your_database -i Migrations/AddCodeFieldsToRoleTables.sql

# Migration 2: Add UserCode to Users table
sqlcmd -S your_server -d your_database -i Migrations/AddUserCodeToUsersTable.sql
```

### 2. Test Registration:
```bash
# Start API
dotnet run --project EduVi.WebAPI

# Test đăng ký Teacher
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "teacher_test",
    "email": "test@example.com",
    "password": "Test123!@#",
    "fullName": "Test Teacher",
    "roleId": 5
  }'
```

### 3. Verify trong Database:
```sql
-- Check User và Teacher record được tạo - với UserCode mới
SELECT u.UserId, u.UserCode, u.Username, u.Email, u.RoleId, 
       t.TeacherCode, t.SchoolName, t.Degree
FROM Users u
LEFT JOIN Teachers t ON u.UserId = t.TeacherId
WHERE u.Email = 'test@example.com';

-- Expected:
-- UserId  UserCode    Username       Email              RoleId  TeacherCode     SchoolName  Degree
-- 10      USER000010  teacher_test   test@example.com   5       TEACHER000001   NULL        NULL

-- Test Admin API với UserCode
-- GET /api/admin/users/USER000010
```

## 🔑 Code Format

| Code Type | Code Pattern | Example | Usage |
|-----------|-------------|---------|-------|
| **User** | `USER` + 6 digits | `USER000001` | **Admin APIs** (external) |
| Admin | `ADMIN` + 6 digits | `ADMIN000001` | Role-specific (internal) |
| Expert | `EXPERT` + 6 digits | `EXPERT000001` | Role-specific (internal) |
| Teacher | `TEACHER` + 6 digits | `TEACHER000001` | Role-specific (internal) |
| Staff | `STAFF` + 6 digits | `STAFF000001` | Role-specific (internal) |

**Note:**
- **UserCode:** Universal identifier cho tất cả users, dùng cho Admin APIs
- **Role Codes:** Specific cho từng role (Admin, Teacher, etc.), chỉ dùng internal

## ⚙️ RoleId Mapping (verify với DB)

| RoleId | Role Name →  Table  →  Code Field |
|--------|-----------------------------------|
| 1 | Admin → Admins → AdminCode |
| 2 | Staff → Staffs → StaffCode |
| 3 | Expert → Experts → ExpertCode |
| 5 | Teacher → Teachers → TeacherCode |

## 📌 Lưu ý quan trọng

1. **Migration trước khi chạy code:** Phải chạy SQL migration trước, nếu không sẽ bị lỗi database schema mismatch

2. **Code là unique:** Database enforce unique constraint, không thể có 2 records cùng code

3. **Auto-increment:** Codes tự động tăng dần (001, 002, 003...)

4. **Optional fields:** SchoolName, Degree, Bio, etc. có thể NULL, user cập nhật sau

5. **External API dùng Code:** 
   - ✅ `GET /api/teachers/TEACHER000001` (dùng Code)
   - ❌ `GET /api/teachers/10` (không dùng ID nữa)

## 🐛 Troubleshooting

**Build errors?**
```bash
dotnet clean
dotnet build
```

**Database schema mismatch?**
→ Chạy migration script

**Code generation fails?**
→ Check console logs, verify database connection

**Want to customize code format?**
→ Edit `GenerateUniqueCodeAsync()` in AuthenticationRepository.cs

## 📚 Đọc thêm

Chi tiết đầy đủ: [UserRegistrationFlow.md](../docs/UserRegistrationFlow.md)
