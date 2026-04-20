using EduVi.Contracts.DTOs.Games.Request;
using EduVi.Contracts.DTOs.Games.Response;

namespace EduVi.Services.Games;

public interface IGameService
{
    Task<GameTaskResponseDto> CreatePlayableGameTaskAsync(int userId, GameConfigRequest request);
    Task<GameProgressDto?> GetGameStatusAsync(Guid taskId);
    Task<List<GameSummaryDto>> GetGamesByTeacherAsync(int userId);
    Task<GameDetailDto> GetGameByCodeAsync(int userId, string gameCode);
    Task<GameResultJsonDto> GetGameResultJsonByCodeAsync(int userId, string productGameCode);
    Task SoftDeleteGameAsync(int userId, string gameCode);
}
