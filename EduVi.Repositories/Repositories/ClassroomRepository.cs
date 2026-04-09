using EduVi.Repositories.DBContext;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace EduVi.Repositories.Repositories;

public class ClassroomRepository : IClassroomRepository
{
    private readonly EduViContext _context;

    public ClassroomRepository(EduViContext context)
    {
        _context = context;
    }

    public async Task<List<Classrooms>> GetClassroomsByTeacherAsync(int teacherId)
    {
        return await _context.Classrooms
            .Where(classroom => classroom.TeacherId == teacherId)
            .OrderByDescending(classroom => classroom.CreatedAt)
            .ToListAsync();
    }

    public async Task<Classrooms?> GetClassroomByCodeAsync(string classroomCode)
    {
        return await _context.Classrooms
            .FirstOrDefaultAsync(classroom => classroom.ClassroomCode == classroomCode);
    }

    public async Task<Classrooms?> GetClassroomByCodeAndTeacherAsync(string classroomCode, int teacherId)
    {
        return await _context.Classrooms
            .FirstOrDefaultAsync(classroom =>
                classroom.ClassroomCode == classroomCode &&
                classroom.TeacherId == teacherId);
    }

    public async Task<Classrooms> CreateClassroomAsync(Classrooms classroom)
    {
        var entry = await _context.Classrooms.AddAsync(classroom);
        return entry.Entity;
    }

    public void UpdateClassroom(Classrooms classroom)
    {
        _context.Classrooms.Update(classroom);
    }

    public void DeleteClassroom(Classrooms classroom)
    {
        _context.Classrooms.Remove(classroom);
    }
}
