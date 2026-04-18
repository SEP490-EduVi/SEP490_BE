using EduVi.Repositories.DBContext;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using EduVi.Contracts.Common;
using Microsoft.EntityFrameworkCore;

namespace EduVi.Repositories.Repositories;

public class GameRepository : IGameRepository
{
    private readonly EduViContext _context;

    public GameRepository(EduViContext context)
    {
        _context = context;
    }

    public async Task<ProductGames> CreateProductGameAsync(ProductGames productGame)
    {
        var entry = await _context.ProductGames.AddAsync(productGame);
        return entry.Entity;
    }

    public async Task<ProductGames?> GetProductGameByTaskIdAsync(Guid taskId)
    {
        return await _context.ProductGames
            .Include(productGame => productGame.Product)
            .FirstOrDefaultAsync(productGame => productGame.TaskId == taskId);
    }

    public async Task<ProductGames?> GetProductGameByCodeAndTeacherAsync(string productGameCode, int teacherId)
    {
        return await _context.ProductGames
            .Include(productGame => productGame.Product)
            .FirstOrDefaultAsync(productGame =>
                productGame.ProductGameCode == productGameCode
                && productGame.Product != null
                && productGame.Product.TeacherId == teacherId);
    }

    public async Task<List<ProductGames>> GetActiveProductGamesByTeacherAsync(int teacherId)
    {
        return await _context.ProductGames
            .Include(productGame => productGame.Product)
            .Where(productGame =>
                productGame.Product != null
                && productGame.Product.TeacherId == teacherId
                && productGame.Status != GameStatusConstants.Deleted)
            .OrderByDescending(productGame => productGame.CreatedAt)
            .ToListAsync();
    }

    public void UpdateProductGame(ProductGames productGame)
    {
        _context.ProductGames.Update(productGame);
    }
}
