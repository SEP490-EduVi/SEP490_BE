namespace EduVi.Contracts.DTOs.Profile;

public class StaffProfileResponse
{
    public string UserCode { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public string AvatarUrl { get; set; }
    public string Department { get; set; }
    public DateOnly? HireDate { get; set; }
}
