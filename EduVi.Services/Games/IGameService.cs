using EduVi.Contracts.DTOs.Games.Request;
using EduVi.Contracts.DTOs.Games.Response;

namespace EduVi.Services.Games;

public interface IGameService
{
    Task<GameTaskResponseDto> CreatePlayableGameTaskAsync(int userId, GameConfigRequest request);
    Task<GameProgressDto?> GetGameStatusAsync(Guid taskId);
}
