using EduVi.Contracts.DTOs.Admin.Request;
using EduVi.Contracts.DTOs.Admin.Response;
using EduVi.Contracts.DTOs.Material;
using EduVi.Contracts.Common;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using EduVi.Services.Authentication;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace EduVi.Services.Admin;

public class AdminService : IAdminService
{
    private const string ExpertMarketplaceHiddenByBanReason = "Tạm ẩn khỏi marketplace do tài khoản Expert bị khóa bởi Admin.";
    private const string AdminSoftDeletedMaterialReason = "Bị quản trị viên khóa: ẩn khỏi marketplace.";

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

    public async Task<AdminUserResponse> CreateUserAsync(CreateUserRequest request)
    {
        var normalizedUsername = request.Username.Trim();
        var normalizedEmail = request.Email.Trim();

        if (await _unitOfWork.AuthenticationRepository.UsernameExistsAsync(normalizedUsername))
            throw new InvalidOperationException("Tên đăng nhập đã tồn tại.");

        if (await _unitOfWork.AuthenticationRepository.EmailExistsAsync(normalizedEmail))
            throw new InvalidOperationException("Email đã tồn tại.");

        var role = await _unitOfWork.AuthenticationRepository.GetRoleByIdAsync(request.RoleId)
            ?? throw new InvalidOperationException($"Vai trò có ID {request.RoleId} không tồn tại.");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var user = new Users
            {
                Username = normalizedUsername,
                PasswordHash = _authService.HashPassword(request.Password),
                Email = normalizedEmail,
                FullName = request.FullName.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
                AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim(),
                RoleId = request.RoleId,
                Status = 1,
                IsEmailVerified = true,
                CreatedAt = DateTime.UtcNow
            };

            user = await _unitOfWork.AuthenticationRepository.CreateUserAsync(user);

            if (string.Equals(role.RoleName, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                await _unitOfWork.AuthenticationRepository.CreateAdminAsync(user.UserId);
            }
            else if (string.Equals(role.RoleName, "Staff", StringComparison.OrdinalIgnoreCase))
            {
                await _unitOfWork.AuthenticationRepository.CreateStaffAsync(user.UserId);
            }
            else if (string.Equals(role.RoleName, "Expert", StringComparison.OrdinalIgnoreCase))
            {
                await _unitOfWork.AuthenticationRepository.CreateExpertAsync(user.UserId);
            }
            else if (string.Equals(role.RoleName, "Teacher", StringComparison.OrdinalIgnoreCase))
            {
                await _unitOfWork.AuthenticationRepository.CreateTeacherAsync(user.UserId);

                // Đồng bộ chính sách cấp quota khởi tạo giống flow đăng ký.
                await _unitOfWork.PaymentRepository.CreateOrUpdateQuotaAsync(
                    user.UserId,
                    analysisQuotaToAdd: 3,
                    slideQuotaToAdd: 3,
                    videoQuotaToAdd: 3,
                    gameQuotaToAdd: 3   );
            }
            else
            {
                throw new InvalidOperationException("Role hiện tại chưa được hỗ trợ tạo qua Admin.");
            }

            await _unitOfWork.AuthenticationRepository.CreateWalletAsync(user.UserId);
            await _unitOfWork.CommitTransactionAsync();

            var createdUser = await _unitOfWork.AdminRepository.GetUserByCodeAsync(user.UserCode)
                ?? throw new KeyNotFoundException($"Không tìm thấy người dùng với code {user.UserCode}.");

            _logger.LogInformation("Admin created user {UserCode} with role {RoleName}", createdUser.UserCode, role.RoleName);
            return MapToUserResponse(createdUser);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
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

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var success = await _unitOfWork.AdminRepository.UpdateUserStatusAsync(user.UserId, 0);
            if (!success)
                throw new KeyNotFoundException($"Không tìm thấy người dùng với code {userCode}.");

            var roleName = user.Role?.RoleName;
            if (string.Equals(roleName, "Teacher", StringComparison.OrdinalIgnoreCase))
            {
                var removedOwnershipCount = await _unitOfWork.AdminRepository.RemoveTeacherOwnedMaterialsAsync(user.UserId);
                _logger.LogWarning("Teacher {UserCode} bị ban: đã xóa {RemovedOwnershipCount} bản ghi sở hữu học liệu",
                    userCode, removedOwnershipCount);
            }
            else if (string.Equals(roleName, "Expert", StringComparison.OrdinalIgnoreCase))
            {
                var hiddenMaterialsCount = await _unitOfWork.AdminRepository.HideApprovedMaterialsByExpertAsync(
                    user.UserId,
                    ExpertMarketplaceHiddenByBanReason);

                _logger.LogWarning("Expert {UserCode} bị ban: đã ẩn {HiddenMaterialsCount} học liệu khỏi marketplace",
                    userCode, hiddenMaterialsCount);
            }

            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

        // CRITICAL: Revoke Token ngay lập tức → user bị đẩy ra khỏi hệ thống
        await _authService.RevokeTokenAsync(user.UserId);

        _logger.LogWarning("Admin BANNED user {UserCode} (UserId: {UserId}) and revoked token", userCode, user.UserId);
        return true;
    }

    public async Task<bool> UnbanUserAsync(string userCode)
    {
        var user = await _unitOfWork.AdminRepository.GetUserByCodeAsync(userCode)
            ?? throw new KeyNotFoundException($"Không tìm thấy người dùng với code {userCode}.");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var success = await _unitOfWork.AdminRepository.UpdateUserStatusAsync(user.UserId, 1);
            if (!success)
                throw new KeyNotFoundException($"Không tìm thấy người dùng với code {userCode}.");

            var roleName = user.Role?.RoleName;
            if (string.Equals(roleName, "Teacher", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Teacher {UserCode} unban: giữ nguyên quota/wallet, không khôi phục ownership học liệu đã bị xóa khi ban",
                    userCode);
            }
            else if (string.Equals(roleName, "Expert", StringComparison.OrdinalIgnoreCase))
            {
                var restoredMaterialsCount = await _unitOfWork.AdminRepository.RestoreMaterialsHiddenByExpertBanAsync(
                    user.UserId,
                    ExpertMarketplaceHiddenByBanReason);

                _logger.LogInformation("Expert {UserCode} unban: đã mở lại {RestoredMaterialsCount} học liệu lên marketplace",
                    userCode, restoredMaterialsCount);
            }

            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

        _logger.LogInformation("Admin UNBANNED user {UserCode} (UserId: {UserId})", userCode, user.UserId);
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

    public async Task<PagedResponse<AdminMaterialSalesResponse>> GetMaterialSalesAnalyticsAsync(AdminMaterialSalesFilterRequest filter)
    {
        var normalizedSubjectCode = NormalizeOptionalText(filter.SubjectCode);
        var normalizedGradeCode = NormalizeOptionalText(filter.GradeCode);
        var normalizedExpertCode = NormalizeOptionalText(filter.ExpertCode);
        var normalizedMaterialCode = NormalizeOptionalText(filter.MaterialCode);

        var (items, totalCount) = await _unitOfWork.AdminRepository.GetMaterialSalesAnalyticsAsync(
            filter.FromDate,
            filter.ToDate,
            normalizedSubjectCode,
            normalizedGradeCode,
            normalizedExpertCode,
            normalizedMaterialCode,
            filter.Page,
            filter.PageSize);

        return new PagedResponse<AdminMaterialSalesResponse>
        {
            Items = items.Select(item => new AdminMaterialSalesResponse
            {
                MaterialCode = item.MaterialCode,
                Title = item.Title,
                SubjectCode = item.SubjectCode,
                GradeCode = item.GradeCode,
                ExpertCode = item.ExpertCode,
                ExpertName = item.ExpertName,
                SoldCount = item.SoldCount,
                UniqueBuyerCount = item.UniqueBuyerCount,
                GrossRevenue = item.GrossRevenue,
                LastPurchasedDate = item.LastPurchasedDate
            }).ToList(),
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<PagedResponse<AdminExpertSalesResponse>> GetExpertSalesAnalyticsAsync(AdminExpertSalesFilterRequest filter)
    {
        var normalizedSubjectCode = NormalizeOptionalText(filter.SubjectCode);
        var normalizedGradeCode = NormalizeOptionalText(filter.GradeCode);
        var normalizedExpertCode = NormalizeOptionalText(filter.ExpertCode);
        var normalizedMaterialCode = NormalizeOptionalText(filter.MaterialCode);

        var (items, totalCount) = await _unitOfWork.AdminRepository.GetExpertSalesAnalyticsAsync(
            filter.FromDate,
            filter.ToDate,
            normalizedSubjectCode,
            normalizedGradeCode,
            normalizedExpertCode,
            normalizedMaterialCode,
            filter.Page,
            filter.PageSize);

        return new PagedResponse<AdminExpertSalesResponse>
        {
            Items = items.Select(item => new AdminExpertSalesResponse
            {
                ExpertCode = item.ExpertCode,
                ExpertName = item.ExpertName,
                SoldMaterialCount = item.SoldMaterialCount,
                SoldCount = item.SoldCount,
                UniqueBuyerCount = item.UniqueBuyerCount,
                GrossRevenue = item.GrossRevenue,
                LastPurchasedDate = item.LastPurchasedDate
            }).ToList(),
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<AdminRevenueForecastResponse> GetRevenueForecastAsync(AdminRevenueForecastFilterRequest filter)
    {
        var normalizedSubjectCode = NormalizeOptionalText(filter.SubjectCode);
        var normalizedGradeCode = NormalizeOptionalText(filter.GradeCode);
        var normalizedExpertCode = NormalizeOptionalText(filter.ExpertCode);
        var normalizedMaterialCode = NormalizeOptionalText(filter.MaterialCode);

        var toDateUtc = filter.ToDate?.ToUniversalTime() ?? DateTime.UtcNow;
        var fromDateUtc = filter.FromDate?.ToUniversalTime() ?? toDateUtc.AddDays(-30);
        if (fromDateUtc > toDateUtc)
            throw new InvalidOperationException("FromDate không được lớn hơn ToDate");

        var periodDays = Math.Max(1, (int)Math.Ceiling((toDateUtc - fromDateUtc).TotalDays));
        var previousToDateUtc = fromDateUtc.AddSeconds(-1);
        var previousFromDateUtc = previousToDateUtc.AddDays(-periodDays);

        var analytics = await _unitOfWork.AdminRepository.GetRevenueForecastAnalyticsAsync(
            fromDateUtc,
            toDateUtc,
            previousFromDateUtc,
            previousToDateUtc,
            normalizedSubjectCode,
            normalizedGradeCode,
            normalizedExpertCode,
            normalizedMaterialCode);

        var averageDailyRevenue = analytics.CurrentRevenue / periodDays;
        var forecastRevenue = averageDailyRevenue * filter.ForecastDays;

        var revenueGrowthRatePercent = analytics.PreviousRevenue == 0
            ? (analytics.CurrentRevenue > 0 ? 100 : 0)
            : ((analytics.CurrentRevenue - analytics.PreviousRevenue) / analytics.PreviousRevenue) * 100;

        return new AdminRevenueForecastResponse
        {
            FromDate = fromDateUtc,
            ToDate = toDateUtc,
            PeriodDays = periodDays,
            ForecastDays = filter.ForecastDays,
            CurrentRevenue = analytics.CurrentRevenue,
            PreviousRevenue = analytics.PreviousRevenue,
            RevenueGrowthRatePercent = decimal.Round(revenueGrowthRatePercent, 2),
            AverageDailyRevenue = decimal.Round(averageDailyRevenue, 2),
            ForecastRevenue = decimal.Round(forecastRevenue, 2),
            CurrentSoldCount = analytics.CurrentSoldCount,
            PreviousSoldCount = analytics.PreviousSoldCount,
            CurrentUniqueBuyerCount = analytics.CurrentUniqueBuyerCount,
            PreviousUniqueBuyerCount = analytics.PreviousUniqueBuyerCount
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

    #region Materials (Admin CRUD)

    public async Task<PagedResponse<MaterialResponseDto>> GetMaterialsForAdminAsync(AdminMaterialFilterRequest filter)
    {
        var normalizedType = NormalizeOptionalText(filter.Type);
        var normalizedSubjectCode = NormalizeOptionalText(filter.SubjectCode);
        var normalizedGradeCode = NormalizeOptionalText(filter.GradeCode);
        var normalizedExpertCode = NormalizeOptionalText(filter.ExpertCode);
        var normalizedSearchTerm = NormalizeOptionalText(filter.Search);

        var (items, totalCount) = await _unitOfWork.AdminRepository.GetMaterialsForAdminAsync(
            filter.ApprovalStatus,
            normalizedType,
            normalizedSubjectCode,
            normalizedGradeCode,
            normalizedExpertCode,
            normalizedSearchTerm,
            filter.Page,
            filter.PageSize);

        return new PagedResponse<MaterialResponseDto>
        {
            Items = items.Select(MapToMaterialResponse).ToList(),
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<MaterialResponseDto> GetMaterialDetailForAdminAsync(string materialCode)
    {
        var normalizedMaterialCode = NormalizeRequiredText(materialCode, "MaterialCode");

        var material = await _unitOfWork.AdminRepository.GetMaterialByCodeWithDetailsAsync(normalizedMaterialCode)
            ?? throw new KeyNotFoundException($"Không tìm thấy học liệu với mã {normalizedMaterialCode}.");

        return MapToMaterialResponse(material);
    }

    public async Task<MaterialResponseDto> CreateMaterialForAdminAsync(CreateAdminMaterialRequest request)
    {
        var normalizedTitle = NormalizeRequiredText(request.Title, "Title");
        var normalizedType = NormalizeRequiredText(request.Type, "Type").ToLowerInvariant();
        var normalizedResourceUrl = NormalizeRequiredText(request.ResourceUrl, "ResourceUrl");
        var normalizedPreviewUrl = NormalizeOptionalText(request.PreviewUrl);
        var normalizedDescription = NormalizeOptionalText(request.Description);
        var expertId = await ResolveExpertIdFromCodeAsync(request.ExpertCode);

        var subjectId = await ResolveSubjectIdFromCodeAsync(request.SubjectCode);
        var gradeId = await ResolveGradeIdFromCodeAsync(request.GradeCode);

        if (request.ApprovalStatus.HasValue)
            ValidateMaterialApprovalState(request.ApprovalStatus.Value, request.RejectionReason);

        var normalizedRejectionReason = request.ApprovalStatus is 2 or 3
            ? NormalizeOptionalText(request.RejectionReason)
            : null;

        var materialCode = await GenerateAdminMaterialCodeAsync(normalizedType);

        var createdMaterial = new Materials
        {
            MaterialCode = materialCode,
            ExpertId = expertId,
            SubjectId = subjectId,
            GradeId = gradeId,
            Title = normalizedTitle,
            Description = normalizedDescription,
            Type = normalizedType,
            Price = request.Price ?? 0,
            ResourceUrl = normalizedResourceUrl,
            PreviewUrl = normalizedPreviewUrl,
            ApprovalStatus = request.ApprovalStatus,
            RejectionReason = normalizedRejectionReason,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.AdminRepository.CreateMaterialAsync(createdMaterial);
        await _unitOfWork.SaveChangesAsync();

        var material = await _unitOfWork.AdminRepository.GetMaterialByCodeWithDetailsAsync(materialCode)
            ?? throw new KeyNotFoundException($"Không tìm thấy học liệu với mã {materialCode}.");

        return MapToMaterialResponse(material);
    }

    public async Task<MaterialResponseDto> UpdateMaterialForAdminAsync(string materialCode, UpdateAdminMaterialRequest request)
    {
        var normalizedMaterialCode = NormalizeRequiredText(materialCode, "MaterialCode");

        var material = await _unitOfWork.AdminRepository.GetMaterialByCodeWithDetailsAsync(normalizedMaterialCode)
            ?? throw new KeyNotFoundException($"Không tìm thấy học liệu với mã {normalizedMaterialCode}.");

        if (request.ExpertCode != null)
        {
            var normalizedExpertCode = NormalizeOptionalText(request.ExpertCode);
            if (normalizedExpertCode is null)
            {
                // Cho phép Admin bỏ gán Expert khi để trống ExpertCode.
                material.ExpertId = null;
            }
            else
            {
                var expert = await _unitOfWork.AdminRepository.GetExpertByCodeAsync(normalizedExpertCode)
                    ?? throw new KeyNotFoundException($"Không tìm thấy chuyên gia với mã {normalizedExpertCode}.");

                material.ExpertId = expert.ExpertId;
            }
        }

        if (request.Title != null)
            material.Title = NormalizeRequiredText(request.Title, "Title");

        if (request.Description != null)
            material.Description = NormalizeOptionalText(request.Description);

        if (request.Type != null)
            material.Type = NormalizeRequiredText(request.Type, "Type").ToLowerInvariant();

        if (request.Price.HasValue)
            material.Price = request.Price.Value;

        if (request.ResourceUrl != null)
            material.ResourceUrl = NormalizeRequiredText(request.ResourceUrl, "ResourceUrl");

        if (request.PreviewUrl != null)
            material.PreviewUrl = NormalizeOptionalText(request.PreviewUrl);

        if (request.SubjectCode != null)
            material.SubjectId = await ResolveSubjectIdFromCodeAsync(request.SubjectCode);

        if (request.GradeCode != null)
            material.GradeId = await ResolveGradeIdFromCodeAsync(request.GradeCode);

        if (request.ApprovalStatus.HasValue)
        {
            ValidateMaterialApprovalState(request.ApprovalStatus.Value, request.RejectionReason);
            material.ApprovalStatus = request.ApprovalStatus.Value;
            material.RejectionReason = request.ApprovalStatus.Value is 2 or 3
                ? NormalizeOptionalText(request.RejectionReason)
                : null;
        }
        else if (request.RejectionReason != null)
        {
            if (material.ApprovalStatus is not (2 or 3))
                throw new InvalidOperationException("Chỉ được cập nhật lý do khi học liệu đang ở trạng thái Từ chối hoặc Bị khóa");

            material.RejectionReason = NormalizeOptionalText(request.RejectionReason);
        }

        _unitOfWork.AdminRepository.UpdateMaterial(material);
        await _unitOfWork.SaveChangesAsync();

        var updatedMaterial = await _unitOfWork.AdminRepository.GetMaterialByCodeWithDetailsAsync(normalizedMaterialCode)
            ?? throw new KeyNotFoundException($"Không tìm thấy học liệu với mã {normalizedMaterialCode}.");

        return MapToMaterialResponse(updatedMaterial);
    }

    public async Task<bool> DeleteMaterialForAdminAsync(string materialCode)
    {
        var normalizedMaterialCode = NormalizeRequiredText(materialCode, "MaterialCode");

        var material = await _unitOfWork.AdminRepository.GetMaterialByCodeWithDetailsAsync(normalizedMaterialCode)
            ?? throw new KeyNotFoundException($"Không tìm thấy học liệu với mã {normalizedMaterialCode}.");

        // Admin ban: ẩn khỏi marketplace bằng trạng thái Bị khóa, không xóa bản ghi vật lý.
        material.ApprovalStatus = 3;
        material.RejectionReason = AdminSoftDeletedMaterialReason;

        _unitOfWork.AdminRepository.UpdateMaterial(material);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    #endregion

    #region Mapping Helpers

        private async Task<int?> ResolveExpertIdFromCodeAsync(string? expertCode)
        {
            var normalizedExpertCode = NormalizeOptionalText(expertCode);
            if (normalizedExpertCode is null)
                return null;

            var expert = await _unitOfWork.AdminRepository.GetExpertByCodeAsync(normalizedExpertCode)
                ?? throw new KeyNotFoundException($"Không tìm thấy chuyên gia với mã {normalizedExpertCode}.");

            return expert.ExpertId;
        }

    private async Task<int?> ResolveSubjectIdFromCodeAsync(string? subjectCode)
    {
        var normalizedSubjectCode = NormalizeOptionalText(subjectCode);
        if (normalizedSubjectCode is null)
            return null;

        var subject = await _unitOfWork.AdminRepository.GetSubjectByCodeAsync(normalizedSubjectCode)
            ?? throw new KeyNotFoundException($"Không tìm thấy môn học với mã {normalizedSubjectCode}.");

        return subject.SubjectId;
    }

    private async Task<int?> ResolveGradeIdFromCodeAsync(string? gradeCode)
    {
        var normalizedGradeCode = NormalizeOptionalText(gradeCode);
        if (normalizedGradeCode is null)
            return null;

        var grade = await _unitOfWork.AdminRepository.GetGradeByCodeAsync(normalizedGradeCode)
            ?? throw new KeyNotFoundException($"Không tìm thấy khối lớp với mã {normalizedGradeCode}.");

        return grade.GradeId;
    }

    private async Task<string> GenerateAdminMaterialCodeAsync(string type)
    {
        var normalizedType = new string(type.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedType))
            normalizedType = "material";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var timestampSegment = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            var randomSegment = Random.Shared.Next(1000, 9999);
            var generatedMaterialCode = $"mat_admin_{normalizedType}_{timestampSegment}_{randomSegment}";

            var exists = await _unitOfWork.AdminRepository.MaterialCodeExistsAsync(generatedMaterialCode);
            if (!exists)
                return generatedMaterialCode;
        }

        throw new InvalidOperationException("Không thể tạo mã học liệu duy nhất. Vui lòng thử lại.");
    }

    private static void ValidateMaterialApprovalState(int approvalStatus, string? rejectionReason)
    {
        if (approvalStatus is < 0 or > 3)
            throw new InvalidOperationException("Trạng thái duyệt không hợp lệ");

        if (approvalStatus is 2 or 3 && string.IsNullOrWhiteSpace(rejectionReason))
            throw new InvalidOperationException("Phải cung cấp lý do khi học liệu ở trạng thái Từ chối hoặc Bị khóa");
    }

    private static string NormalizeRequiredText(string? value, string fieldName)
    {
        var normalizedValue = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedValue))
            throw new InvalidOperationException($"{fieldName} không được để trống");
        return normalizedValue;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (value is null)
            return null;

        var normalizedValue = value.Trim();
        return string.IsNullOrWhiteSpace(normalizedValue) ? null : normalizedValue;
    }

    private static MaterialResponseDto MapToMaterialResponse(Materials material) => new()
    {
        MaterialCode = material.MaterialCode ?? string.Empty,
        Title = material.Title ?? string.Empty,
        Description = material.Description,
        Type = material.Type ?? string.Empty,
        TypeName = MaterialTypeConstants.GetDisplayName(material.Type),
        Price = material.Price,
        PreviewUrl = material.PreviewUrl,
        ResourceUrl = material.ResourceUrl,
        SubjectCode = material.Subject?.SubjectCode,
        SubjectName = material.Subject?.SubjectName,
        GradeCode = material.Grade?.GradeCode,
        GradeName = material.Grade?.GradeName,
        ApprovalStatus = material.ApprovalStatus ?? 0,
        ApprovalStatusName = MaterialApprovalStatusConstants.GetStatusName(material.ApprovalStatus),
        RejectionReason = material.RejectionReason,
        ExpertCode = material.Expert?.ExpertCode,
        ExpertName = material.Expert?.Expert?.FullName,
        CreatedAt = material.CreatedAt
    };

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
        StatusName = u.Status == 0 ? "Bị khóa" : "Hoạt động",
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
        TransactionType = WalletTransactionTypeConstants.GetDisplayName(t.TransactionType),
        TransactionTypeCode = t.TransactionType,
        Amount = t.Amount,
        BalanceBefore = t.BalanceBefore,
        BalanceAfter = t.BalanceAfter,
        Status = t.Status,
        StatusName = t.Status switch
        {
            0 => "Đang chờ",
            1 => "Thành công",
            2 => "Thất bại",
            3 => "Đã hủy",
            _ => "Không xác định"
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
            0 => "Đang chờ",
            1 => "Thành công",
            2 => "Thất bại",
            3 => "Đã hủy",
            _ => "Không xác định"
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
