using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Games.Request;
using EduVi.Contracts.DTOs.Games.Response;
using EduVi.Services.Games;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduVi.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GamesController : ControllerBase
{
    private readonly IGameService _gameService;
    private readonly ILogger<GamesController> _logger;

    public GamesController(IGameService gameService, ILogger<GamesController> logger)
    {
        _gameService = gameService;
        _logger = logger;
    }

    /// <summary>
    /// Tạo task generate payload playable cho mini-game dựa trên template + slide edited.
    /// </summary>
    [HttpPost("playable")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<GameTaskResponseDto>>> CreatePlayableGame(
        [FromBody] GameConfigRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _gameService.CreatePlayableGameTaskAsync(userId, request);
            return Ok(ApiResponse<GameTaskResponseDto>.Success(result, "Công việc đã được đưa vào hàng đợi xử lý"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<GameTaskResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating playable game");
            return StatusCode(500, ApiResponse<GameTaskResponseDto>.Fail("Lỗi khi tạo game", 500));
        }
    }

    /// <summary>
    /// Kiểm tra trạng thái task tạo game.
    /// </summary>
    [HttpGet("status/{taskId:guid}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<GameProgressDto>>> GetGameStatus(Guid taskId)
    {
        try
        {
            var result = await _gameService.GetGameStatusAsync(taskId);
            if (result is null)
                return NotFound(ApiResponse<GameProgressDto>.Fail("Không tìm thấy công việc", 404));

            return Ok(ApiResponse<GameProgressDto>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting game status. TaskId={TaskId}", taskId);
            return StatusCode(500, ApiResponse<GameProgressDto>.Fail("Lỗi khi kiểm tra trạng thái công việc", 500));
        }
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Không tìm thấy người dùng");
        return userId;
    }
}
