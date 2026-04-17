using EduVi.Repositories.DBContext;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace EduVi.Repositories.Repositories;

public class StudentListRepository : IStudentListRepository
{
    private readonly EduViContext _context;

    public StudentListRepository(EduViContext context)
    {
        _context = context;
    }

    public async Task<List<StudentLists>> GetStudentListsByTeacherAsync(int teacherId)
    {
        return await _context.StudentLists
            .Where(studentList => studentList.TeacherId == teacherId)
            .OrderByDescending(studentList => studentList.CreatedAt)
            .ToListAsync();
    }

    public async Task<StudentLists?> GetStudentListByCodeAsync(string studentListCode)
    {
        return await _context.StudentLists
            .FirstOrDefaultAsync(studentList => studentList.StudentListCode == studentListCode);
    }

    public async Task<StudentLists?> GetStudentListByCodeAndTeacherAsync(string studentListCode, int teacherId)
    {
        return await _context.StudentLists
            .FirstOrDefaultAsync(studentList =>
                studentList.StudentListCode == studentListCode &&
                studentList.TeacherId == teacherId);
    }

    public async Task<StudentLists> CreateStudentListAsync(StudentLists studentList)
    {
        var entry = await _context.StudentLists.AddAsync(studentList);
        return entry.Entity;
    }

    public void UpdateStudentList(StudentLists studentList)
    {
        _context.StudentLists.Update(studentList);
    }

    public void DeleteStudentList(StudentLists studentList)
    {
        _context.StudentLists.Remove(studentList);
    }
}
