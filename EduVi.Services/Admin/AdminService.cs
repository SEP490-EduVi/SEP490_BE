using EduVi.Contracts.DTOs.Admin.Request;
using EduVi.Contracts.DTOs.Admin.Response;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using EduVi.Services.Authentication;
using Microsoft.Extensions.Logging;

namespace EduVi.Services.Admin;

public class AdminService : IAdminService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthenticationService _authService;
    private readonly ILogger<AdminService> _logger;

    public AdminService(IUnitOfWork unitOfWork, IAuthenticationService authService, ILogger<AdminService> logger)
    {
        _unitOfWork = unitOfWork;
        _authService = authService;
        _logger = logger;
    }

    #region User Management

    public async Task<PagedResponse<AdminUserResponse>> GetUsersAsync(UserFilterRequest filter)
    {
        var (items, totalCount) = await _unitOfWork.AdminRepository.GetUsersAsync(
            filter.RoleId, filter.Status, filter.Search,
            filter.FromDate, filter.ToDate,
            filter.Page, filter.PageSize);

        return new PagedResponse<AdminUserResponse>
        {
            Items = items.Select(MapToUserResponse).ToList(),
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<AdminUserResponse> GetUserByCodeAsync(string userCode)
    {
        var user = await _unitOfWork.AdminRepository.GetUserByCodeAsync(userCode)
            ?? throw new KeyNotFoundException($"Không tìm thấy người dùng với code {userCode}.");

        return MapToUserResponse(user);
    }

    public async Task<AdminUserResponse> UpdateUserAsync(string userCode, UpdateUserRequest request)
    {
        var user = await _unitOfWork.AdminRepository.GetUserByCodeAsync(userCode)
            ?? throw new KeyNotFoundException($"Không tìm thấy người dùng với code {userCode}.");

        // Chỉ cập nhật field được gửi lên (non-null)
        if (request.FullName != null) user.FullName = request.FullName;
        if (request.PhoneNumber != null) user.PhoneNumber = request.PhoneNumber;
        if (request.AvatarUrl != null) user.AvatarUrl = request.AvatarUrl;

        await _unitOfWork.AdminRepository.UpdateUserAsync(user);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Admin updated user {UserCode} (UserId: {UserId})", userCode, user.UserId);

        // Re-fetch để trả về data mới nhất
        var updated = await _unitOfWork.AdminRepository.GetUserByCodeAsync(userCode);
        return MapToUserResponse(updated!);
    }

    public async Task<bool> BanUserAsync(string userCode)
    {
        var user = await _unitOfWork.AdminRepository.GetUserByCodeAsync(userCode)
            ?? throw new KeyNotFoundException($"Không tìm thấy người dùng với code {userCode}.");

        var success = await _unitOfWork.AdminRepository.UpdateUserStatusAsync(user.UserId, 0);
        if (!success)
            throw new KeyNotFoundException($"Không tìm thấy người dùng với code {userCode}.");

        await _unitOfWork.SaveChangesAsync();

        // CRITICAL: Revoke Token ngay lập tức → user bị đẩy ra khỏi hệ thống
        await _authService.RevokeTokenAsync(user.UserId);

        _logger.LogWarning("Admin BANNED user {UserCode} (UserId: {UserId}) and revoked token", userCode, user.UserId);
        return true;
    }

    public async Task<bool> UnbanUserAsync(string userCode)
    {
        var user = await _unitOfWork.AdminRepository.GetUserByCodeAsync(userCode)
            ?? throw new KeyNotFoundException($"Không tìm thấy người dùng với code {userCode}.");

        var success = await _unitOfWork.AdminRepository.UpdateUserStatusAsync(user.UserId, 1);
        if (!success)
            throw new KeyNotFoundException($"Không tìm thấy người dùng với code {userCode}.");

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Admin UNBANNED user {UserCode} (UserId: {UserId})", userCode, user.UserId);
        return true;
    }

    public async Task<bool> ChangeUserRoleAsync(string userCode, ChangeUserRoleRequest request)
    {
        // Validate role tồn tại
        if (!await _unitOfWork.AdminRepository.RoleExistsAsync(request.RoleId))
            throw new InvalidOperationException($"Vai trò có ID {request.RoleId} không tồn tại.");

        var user = await _unitOfWork.AdminRepository.GetUserByCodeAsync(userCode)
            ?? throw new KeyNotFoundException($"Không tìm thấy người dùng với code {userCode}.");

        var success = await _unitOfWork.AdminRepository.ChangeUserRoleAsync(user.UserId, request.RoleId);
        if (!success)
            throw new KeyNotFoundException($"Không tìm thấy người dùng với code {userCode}.");

        await _unitOfWork.SaveChangesAsync();

        // Revoke Token → user phải login lại để nhận token mới với role mới
        await _authService.RevokeTokenAsync(user.UserId);

        _logger.LogWarning("Admin changed role of user {UserCode} (UserId: {UserId}) to RoleId={RoleId}, token revoked", userCode, user.UserId, request.RoleId);
        return true;
    }

    public async Task<bool> DeleteUserAsync(string userCode)
    {
        var user = await _unitOfWork.AdminRepository.GetUserByCodeAsync(userCode)
            ?? throw new KeyNotFoundException($"Không tìm thấy người dùng với code {userCode}.");

        var success = await _unitOfWork.AdminRepository.DeleteUserAsync(user.UserId);
        if (!success)
            throw new KeyNotFoundException($"Không tìm thấy người dùng với code {userCode}.");

        await _unitOfWork.SaveChangesAsync();

        // Revoke token khi xóa user
        await _authService.RevokeTokenAsync(user.UserId);

        _logger.LogWarning("Admin DELETED user {UserCode} (UserId: {UserId})", userCode, user.UserId);
        return true;
    }

    public async Task<List<RoleResponse>> GetAllRolesAsync()
    {
        var roles = await _unitOfWork.AdminRepository.GetAllRolesAsync();
        return roles.Select(r => new RoleResponse
        {
            RoleId = r.RoleId,
            RoleName = r.RoleName ?? "",
            Description = r.Description
        }).ToList();
    }

    #endregion

    #region Financial

    public async Task<FinancialOverviewResponse> GetFinancialOverviewAsync()
    {
        var (totalUsers, activeUsers, bannedUsers) = await _unitOfWork.AdminRepository.GetUserCountsAsync();
        var totalBalance = await _unitOfWork.AdminRepository.GetTotalWalletBalanceAsync();
        var totalWallets = (await _unitOfWork.AdminRepository.GetAllWalletsAsync(1, 1)).TotalCount;
        var (topUpAmount, topUpCount) = await _unitOfWork.AdminRepository.GetTopUpStatsAsync();
        var (subAmount, subCount) = await _unitOfWork.AdminRepository.GetSubscriptionStatsAsync();
        var (totalOrders, completedOrders) = await _unitOfWork.AdminRepository.GetOrderCountsAsync();

        return new FinancialOverviewResponse
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            BannedUsers = bannedUsers,
            TotalWallets = totalWallets,
            TotalBalance = totalBalance,
            TotalTopUpAmount = topUpAmount,
            TotalTopUpCount = topUpCount,
            TotalSubscriptionRevenue = subAmount,
            TotalSubscriptionCount = subCount,
            TotalOrders = totalOrders,
            CompletedOrders = completedOrders
        };
    }

    public async Task<PagedResponse<AdminWalletResponse>> GetAllWalletsAsync(int page, int pageSize)
    {
        var (items, totalCount) = await _unitOfWork.AdminRepository.GetAllWalletsAsync(page, pageSize);

        return new PagedResponse<AdminWalletResponse>
        {
            Items = items.Select(w => new AdminWalletResponse
            {
                WalletId = w.WalletId,
                UserId = w.UserId,
                Username = w.User?.Username,
                FullName = w.User?.FullName,
                Email = w.User?.Email,
                Balance = w.Balance ?? 0,
                LastUpdated = w.LastUpdated
            }).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResponse<AdminTransactionResponse>> GetAllTransactionsAsync(TransactionFilterRequest filter)
    {
        var (items, totalCount) = await _unitOfWork.AdminRepository.GetAllTransactionsAsync(
            filter.UserId, filter.TransactionType, filter.Status,
            filter.FromDate, filter.ToDate,
            filter.Page, filter.PageSize);

        return new PagedResponse<AdminTransactionResponse>
        {
            Items = items.Select(MapToTransactionResponse).ToList(),
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<PagedResponse<AdminOrderResponse>> GetAllOrdersAsync(OrderFilterRequest filter)
    {
        var (items, totalCount) = await _unitOfWork.AdminRepository.GetAllOrdersAsync(
            filter.TeacherId, filter.Status, filter.PaymentMethod,
            filter.FromDate, filter.ToDate,
            filter.Page, filter.PageSize);

        return new PagedResponse<AdminOrderResponse>
        {
            Items = items.Select(MapToOrderResponse).ToList(),
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    #endregion

    #region Subscription Plans

    public async Task<List<PlanResponse>> GetAllPlansAsync()
    {
        var plans = await _unitOfWork.AdminRepository.GetAllPlansAsync();
        return plans.Select(MapToPlanResponse).ToList();
    }

    public async Task<PlanResponse> GetPlanByIdAsync(int planId)
    {
        var plan = await _unitOfWork.AdminRepository.GetPlanByIdAsync(planId)
            ?? throw new KeyNotFoundException($"Không tìm thấy gói cước với ID {planId}.");

        return MapToPlanResponse(plan);
    }

    public async Task<PlanResponse> CreatePlanAsync(CreatePlanRequest request)
    {
        var plan = new QuotaPlans
        {
            PlanName = request.PlanName,
            Price = request.Price,
            AnalysisQuotaAmount = request.AnalysisQuotaAmount,
            SlideQuotaAmount = request.SlideQuotaAmount,
            VideoQuotaAmount = request.VideoQuotaAmount,
            GameQuotaAmount = request.GameQuotaAmount,
            Description = request.Description,
            IsActive = true
        };

        await _unitOfWork.AdminRepository.CreatePlanAsync(plan);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Admin created plan: {PlanName}, Price={Price}", plan.PlanName, plan.Price);
        return MapToPlanResponse(plan);
    }

    public async Task<PlanResponse> UpdatePlanAsync(int planId, UpdatePlanRequest request)
    {
        var plan = await _unitOfWork.AdminRepository.GetPlanByIdAsync(planId)
            ?? throw new KeyNotFoundException($"Không tìm thấy gói cước với ID {planId}.");

        // Chỉ cập nhật field được gửi lên (non-null)
        if (request.PlanName != null) plan.PlanName = request.PlanName;
        if (request.Description != null) plan.Description = request.Description;
        if (request.Price.HasValue) plan.Price = request.Price.Value;
        if (request.AnalysisQuotaAmount.HasValue) plan.AnalysisQuotaAmount = request.AnalysisQuotaAmount.Value;
        if (request.SlideQuotaAmount.HasValue) plan.SlideQuotaAmount = request.SlideQuotaAmount.Value;
        if (request.VideoQuotaAmount.HasValue) plan.VideoQuotaAmount = request.VideoQuotaAmount.Value;
        if (request.GameQuotaAmount.HasValue) plan.GameQuotaAmount = request.GameQuotaAmount.Value;
        if (request.IsActive.HasValue) plan.IsActive = request.IsActive.Value;

        await _unitOfWork.AdminRepository.UpdatePlanAsync(plan);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Admin updated plan {PlanId}", planId);
        return MapToPlanResponse(plan);
    }

    public async Task<bool> DeletePlanAsync(int planId)
    {
        var success = await _unitOfWork.AdminRepository.DeletePlanAsync(planId);
        if (!success)
            throw new KeyNotFoundException($"Không tìm thấy gói cước với ID {planId}.");

        await _unitOfWork.SaveChangesAsync();

        _logger.LogWarning("Admin soft-deleted plan {PlanId}", planId);
        return true;
    }

    #endregion

    #region Mapping Helpers

    private static AdminUserResponse MapToUserResponse(Users u) => new()
    {
        UserId = u.UserId,
        UserCode = u.UserCode ?? "",
        Username = u.Username ?? "",
        Email = u.Email ?? "",
        FullName = u.FullName,
        PhoneNumber = u.PhoneNumber,
        AvatarUrl = u.AvatarUrl,
        Status = u.Status ?? 1,
        StatusName = u.Status == 0 ? "Banned" : "Active",
        IsEmailVerified = u.IsEmailVerified,
        CreatedAt = u.CreatedAt,
        RoleId = u.RoleId,
        RoleName = u.Role?.RoleName ?? "",
        AdminId = u.Admins?.AdminId,
        TeacherId = u.Teachers?.TeacherId,
        ExpertId = u.Experts?.ExpertId,
        StaffId = u.Staffs?.StaffId
    };

    private static AdminTransactionResponse MapToTransactionResponse(WalletTransactions t) => new()
    {
        TransactionId = t.TransactionId,
        OrderCode = t.OrderCode,
        WalletId = t.WalletId,
        UserId = t.Wallet?.UserId,
        Username = t.Wallet?.User?.Username,
        FullName = t.Wallet?.User?.FullName,
        TransactionType = t.TransactionType,
        Amount = t.Amount,
        BalanceBefore = t.BalanceBefore,
        BalanceAfter = t.BalanceAfter,
        Status = t.Status,
        StatusName = t.Status switch
        {
            0 => "Pending",
            1 => "Completed",
            2 => "Failed",
            3 => "Cancelled",
            _ => "Unknown"
        },
        Description = t.Description,
        PlanId = t.PlanId,
        PlanName = t.Plan?.PlanName,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };

    private static AdminOrderResponse MapToOrderResponse(Orders o) => new()
    {
        OrderId = o.OrderId,
        TeacherId = o.TeacherId,
        TeacherName = o.Teacher?.Teacher?.FullName,
        TotalAmount = o.TotalAmount,
        OrderDate = o.OrderDate,
        Status = o.Status,
        StatusName = o.Status switch
        {
            0 => "Pending",
            1 => "Completed",
            2 => "Failed",
            3 => "Cancelled",
            _ => "Unknown"
        },
        PaymentMethod = o.PaymentMethod
    };

    private static PlanResponse MapToPlanResponse(QuotaPlans p) => new()
    {
        PlanId = p.PlanId,
        PlanName = p.PlanName ?? "",
        Price = p.Price ?? 0,
        AnalysisQuotaAmount = p.AnalysisQuotaAmount ?? 0,
        SlideQuotaAmount = p.SlideQuotaAmount ?? 0,
        VideoQuotaAmount = p.VideoQuotaAmount ?? 0,
        GameQuotaAmount = p.GameQuotaAmount ?? 0,
        Description = p.Description,
        IsActive = p.IsActive ?? false
    };

    #endregion
}
