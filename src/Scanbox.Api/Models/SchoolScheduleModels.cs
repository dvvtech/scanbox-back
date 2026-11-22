namespace Scanbox.Api.Models;

public sealed record SchoolSubject(int Id, string Name);

public sealed record ScheduledLesson(int LessonNumber, int SubjectId);

public sealed record SchoolDaySchedule(
    int DayOfWeek,
    string DayName,
    IReadOnlyList<ScheduledLesson> Lessons);

public sealed record ClassSchedule(
    string ClassName,
    IReadOnlyList<SchoolDaySchedule> Days);