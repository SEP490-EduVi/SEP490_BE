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

    public async Task<TeacherGames> CreateTeacherGameAsync(TeacherGames teacherGame)
    {
        var entry = await _context.TeacherGames.AddAsync(teacherGame);
        return entry.Entity;
    }

    public async Task<TeacherGames?> GetTeacherGameByTaskIdAsync(Guid taskId)
    {
        return await _context.TeacherGames
            .FirstOrDefaultAsync(teacherGame => teacherGame.TaskId == taskId);
    }

    public async Task<TeacherGames?> GetTeacherGameByCodeAndTeacherAsync(string teacherGameCode, int teacherId)
    {
        return await _context.TeacherGames
            .FirstOrDefaultAsync(teacherGame =>
                teacherGame.TeacherGameCode == teacherGameCode
                && teacherGame.TeacherId == teacherId);
    }

    public async Task<List<TeacherGames>> GetActiveTeacherGamesByTeacherAsync(int teacherId)
    {
        return await _context.TeacherGames
            .Where(teacherGame => teacherGame.TeacherId == teacherId && teacherGame.Status != GameStatusConstants.Deleted)
            .OrderByDescending(teacherGame => teacherGame.CreatedAt)
            .ToListAsync();
    }

    public void UpdateTeacherGame(TeacherGames teacherGame)
    {
        _context.TeacherGames.Update(teacherGame);
    }
}
