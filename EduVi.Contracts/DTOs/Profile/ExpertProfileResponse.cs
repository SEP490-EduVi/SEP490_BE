namespace EduVi.Contracts.DTOs.Profile;

public class ExpertProfileResponse
{
    public string UserCode { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string PhoneNumber { get; set; }
    public string AvatarUrl { get; set; }
    public string Bio { get; set; }
    public bool? IsVerified { get; set; }
}
