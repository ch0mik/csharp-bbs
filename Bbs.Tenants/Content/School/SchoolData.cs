namespace Bbs.Tenants.Content.School;

internal sealed class SchoolCourseRecord
{
    public required string StudentName { get; init; }
    public required string ClassNumber { get; init; }
    public required string CourseTitle { get; init; }
    public char Grade { get; set; }
    public required string Teacher { get; init; }
    public required string Period { get; init; }
    public required string Room { get; init; }
}

internal sealed class SchoolSessionState
{
    public List<SchoolCourseRecord> Records { get; } = CreateRecords();

    public IReadOnlyList<SchoolCourseRecord> Search(string? query)
    {
        var value = (query ?? string.Empty).Trim();
        if (value.Length == 0) return Array.Empty<SchoolCourseRecord>();
        return Records
            .Where(r => r.StudentName.Contains(value, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public bool TryChangeGrade(IEnumerable<SchoolCourseRecord> studentRecords, string? classNumber, string? grade)
    {
        var normalizedClass = (classNumber ?? string.Empty).Trim();
        var normalizedGrade = (grade ?? string.Empty).Trim().ToUpperInvariant();
        if (normalizedGrade.Length != 1 || !"ABCDEFU".Contains(normalizedGrade[0])) return false;

        var record = studentRecords.FirstOrDefault(r =>
            string.Equals(r.ClassNumber, normalizedClass, StringComparison.OrdinalIgnoreCase));
        if (record is null) return false;
        record.Grade = normalizedGrade[0];
        return true;
    }

    private static List<SchoolCourseRecord> CreateRecords() =>
    [
        Course("Lightman, David L.", "S-202", "BIOLOGY 2", 'F', "LIGGET", "3", "214"),
        Course("Lightman, David L.", "E-314", "ENGLISH 11B", 'D', "TURMAN", "5", "172"),
        Course("Lightman, David L.", "H-221", "WORLD HISTORY 11B", 'C', "DWYMER", "2", "108"),
        Course("Lightman, David L.", "M-106", "TRIG 2", 'B', "DICKERSON", "4", "315"),
        Course("Lightman, David L.", "PE-02", "PHYSICAL EDUCATION", 'C', "COMSTOCK", "1", "GYM"),
        Course("Lightman, David L.", "M-122", "CALCULUS 1", 'B', "LOGAN", "6", "240"),
        Course("Mack, Jennifer K.", "S-202", "BIOLOGY 2", 'F', "LIGGET", "3", "214"),
        Course("Mack, Jennifer K.", "E-325", "ENGLISH 11B", 'A', "ROBINSON", "1", "114"),
        Course("Mack, Jennifer K.", "H-221", "WORLD HISTORY 11B", 'B', "DWYER", "2", "108"),
        Course("Mack, Jennifer K.", "M-104", "GEOMETRY 2", 'D', "HALQUIST", "4", "307"),
        Course("Mack, Jennifer K.", "B-107", "ECONOMICS", 'D', "MARKS", "5", "122"),
        Course("Mack, Jennifer K.", "PE-02", "PHYSICAL EDUCATION", 'C', "COMSTOCK", "6", "GYM")
    ];

    private static SchoolCourseRecord Course(string name, string number, string title, char grade, string teacher, string period, string room)
        => new()
        {
            StudentName = name,
            ClassNumber = number,
            CourseTitle = title,
            Grade = grade,
            Teacher = teacher,
            Period = period,
            Room = room
        };
}

internal static class SchoolInput
{
    public static bool IsPasswordValid(string? value)
        => string.Equals((value ?? string.Empty).Trim(), "PENCIL", StringComparison.OrdinalIgnoreCase);

    public static bool IsQuit(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant() is "." or "QUIT" or "EXIT";
}
