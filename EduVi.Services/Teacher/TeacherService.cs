using EduVi.Contracts.DTOs.Profile;
using EduVi.Repositories.Interfaces;

namespace EduVi.Services.Teacher;

public class TeacherService : ITeacherService
{
    private readonly IUnitOfWork _unitOfWork;

    public TeacherService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<TeacherProfileResponse> GetProfileAsync(int userId)
    {
        var teacher = await _unitOfWork.TeacherRepository.GetProfileByUserIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy thông tin giáo viên.");

        return new TeacherProfileResponse
        {
            UserCode    = teacher.Teacher.UserCode,
            FullName    = teacher.Teacher.FullName,
            Email       = teacher.Teacher.Email,
            PhoneNumber = teacher.Teacher.PhoneNumber,
            AvatarUrl   = teacher.Teacher.AvatarUrl,
            SchoolName  = teacher.SchoolName,
        };
    }

    public async Task UpdateProfileAsync(int userId, UpdateTeacherProfileRequest request)
    {
        var teacher = await _unitOfWork.TeacherRepository.GetProfileByUserIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy thông tin giáo viên.");

        if (request.SchoolName is not null)
            teacher.SchoolName = request.SchoolName;

        await _unitOfWork.SaveChangesAsync();
    }
}
