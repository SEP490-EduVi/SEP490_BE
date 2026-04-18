using EduVi.Repositories.Models;

namespace EduVi.Repositories.Interfaces;

public interface IGameRepository
{
    Task<ProductGames> CreateProductGameAsync(ProductGames productGame);
    Task<ProductGames?> GetProductGameByTaskIdAsync(Guid taskId);
    Task<ProductGames?> GetProductGameByCodeAndTeacherAsync(string productGameCode, int teacherId);
    Task<List<ProductGames>> GetActiveProductGamesByTeacherAsync(int teacherId);
    void UpdateProductGame(ProductGames productGame);
}
