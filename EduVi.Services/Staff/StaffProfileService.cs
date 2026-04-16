using EduVi.Contracts.DTOs.Profile;
using EduVi.Repositories.Interfaces;

namespace EduVi.Services.Staff;

public class StaffProfileService : IStaffProfileService
{
    private readonly IUnitOfWork _unitOfWork;

    public StaffProfileService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<StaffProfileResponse> GetProfileAsync(int userId)
    {
        var staff = await _unitOfWork.StaffRepository.GetProfileByUserIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy thông tin nhân viên.");

        return new StaffProfileResponse
        {
            UserCode    = staff.Staff.UserCode,
            FullName    = staff.Staff.FullName,
            Email       = staff.Staff.Email,
            PhoneNumber = staff.Staff.PhoneNumber,
            AvatarUrl   = staff.Staff.AvatarUrl,
            Department  = staff.Department,
            HireDate    = staff.HireDate,
        };
    }

    public async Task UpdateProfileAsync(int userId, UpdateStaffProfileRequest request)
    {
        var staff = await _unitOfWork.StaffRepository.GetProfileByUserIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy thông tin nhân viên.");

        if (request.Department is not null)
            staff.Department = request.Department;

        await _unitOfWork.SaveChangesAsync();
    }
}
