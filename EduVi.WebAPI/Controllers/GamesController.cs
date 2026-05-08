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
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<GameTaskResponseDto>.Fail(ex.Message, 401));
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

    /// <summary>
    /// Lấy danh sách game của giáo viên hiện tại (không bao gồm game đã xóa mềm).
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<GameSummaryDto>>>> GetGames()
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _gameService.GetGamesByTeacherAsync(userId);
            return Ok(ApiResponse<List<GameSummaryDto>>.Success(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<List<GameSummaryDto>>.Fail(ex.Message, 400));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<List<GameSummaryDto>>.Fail(ex.Message, 401));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting games list for current user");
            return StatusCode(500, ApiResponse<List<GameSummaryDto>>.Fail("Lỗi khi lấy danh sách game", 500));
        }
    }

    /// <summary>
    /// Lấy chi tiết game theo mã game.
    /// </summary>
    [HttpGet("{gameCode}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<GameDetailDto>>> GetGameByCode(string gameCode)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _gameService.GetGameByCodeAsync(userId, gameCode);
            return Ok(ApiResponse<GameDetailDto>.Success(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<GameDetailDto>.Fail(ex.Message, 400));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<GameDetailDto>.Fail(ex.Message, 404));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<GameDetailDto>.Fail(ex.Message, 401));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting game detail for game code {GameCode}", gameCode);
            return StatusCode(500, ApiResponse<GameDetailDto>.Fail("Lỗi khi lấy chi tiết game", 500));
        }
    }

    /// <summary>
    /// Lấy ResultJson của game theo ProductGameCode.
    /// </summary>
    [HttpGet("{productGameCode}/result-json")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<GameResultJsonDto>>> GetGameResultJsonByCode(string productGameCode)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _gameService.GetGameResultJsonByCodeAsync(userId, productGameCode);
            return Ok(ApiResponse<GameResultJsonDto>.Success(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<GameResultJsonDto>.Fail(ex.Message, 400));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<GameResultJsonDto>.Fail(ex.Message, 404));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<GameResultJsonDto>.Fail(ex.Message, 401));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting game result json for game code {ProductGameCode}", productGameCode);
            return StatusCode(500, ApiResponse<GameResultJsonDto>.Fail("Lỗi khi lấy ResultJson của game", 500));
        }
    }

    /// <summary>
    /// [Teacher] Lưu ResultJson của game sau khi chỉnh sửa.
    /// </summary>
    [HttpPut("{productGameCode}/result-json")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<GameResultJsonDto>>> SaveGameResultJson(
        string productGameCode,
        [FromBody] SaveGameResultJsonRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _gameService.SaveGameResultJsonAsync(userId, productGameCode, request);
            return Ok(ApiResponse<GameResultJsonDto>.Success(result, "Lưu game sau khi chỉnh sửa thành công"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<GameResultJsonDto>.Fail(ex.Message, 400));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<GameResultJsonDto>.Fail(ex.Message, 404));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<GameResultJsonDto>.Fail(ex.Message, 401));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving edited game result json for game code {ProductGameCode}", productGameCode);
            return StatusCode(500, ApiResponse<GameResultJsonDto>.Fail("Lỗi khi lưu game đã chỉnh sửa", 500));
        }
    }

    /// <summary>
    /// Xóa mềm game theo mã game.
    /// </summary>
    [HttpDelete("{gameCode}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> SoftDeleteGame(string gameCode)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _gameService.SoftDeleteGameAsync(userId, gameCode);
            return Ok(ApiResponse<object>.Success(new { GameCode = gameCode }, "Xóa mềm game thành công"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message, 400));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message, 404));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<object>.Fail(ex.Message, 401));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error soft deleting game with code {GameCode}", gameCode);
            return StatusCode(500, ApiResponse<object>.Fail("Lỗi khi xóa game", 500));
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
