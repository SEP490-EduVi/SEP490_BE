using EduVi.Repositories.Models;

namespace EduVi.Repositories.Interfaces;

public interface IGameRepository
{
    Task<TeacherGames> CreateTeacherGameAsync(TeacherGames teacherGame);
    Task<TeacherGames?> GetTeacherGameByTaskIdAsync(Guid taskId);
    Task<TeacherGames?> GetTeacherGameByCodeAndTeacherAsync(string teacherGameCode, int teacherId);
    Task<List<TeacherGames>> GetActiveTeacherGamesByTeacherAsync(int teacherId);
    void UpdateTeacherGame(TeacherGames teacherGame);
}
