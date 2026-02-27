using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Admin.Request;

/// <summary>
/// Admin thay đổi vai trò của user (RBAC).
/// Khi thay đổi role → hệ thống tự revoke token để user phải login lại với quyền mới.
/// </summary>
public class ChangeUserRoleRequest
{
    [Required(ErrorMessage = "RoleId is required")]
    public int RoleId { get; set; }
}
