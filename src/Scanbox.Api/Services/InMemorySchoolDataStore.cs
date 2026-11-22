using Scanbox.Api.Models;

namespace Scanbox.Api.Services;

public interface ISchoolDataStore
{
    IReadOnlyList<SchoolSubject> Subjects { get; }
    ClassSchedule? GetSchedule(string className);
}

public sealed class InMemorySchoolDataStore : ISchoolDataStore
{
    // Demo data is kept in memory and resets whenever the API process restarts.
    private readonly IReadOnlyList<SchoolSubject> _subjects =
    [
        new(1, "Русский язык"),
        new(2, "Литература"),
        new(3, "Алгебра"),
        new(4, "Геометрия"),
        new(5, "Английский язык"),
        new(6, "Информатика"),
        new(7, "История"),
        new(8, "География"),
        new(9, "Биология"),
        new(10, "Физика"),
        new(11, "Обществознание"),
        new(12, "Музыка"),
        new(13, "Труд (технология)"),
        new(14, "Физическая культура"),
        new(15, "Основы безопасности жизнедеятельности")
    ];

    private readonly IReadOnlyDictionary<string, ClassSchedule> _schedules =
        new Dictionary<string, ClassSchedule>(StringComparer.OrdinalIgnoreCase)
        {
            ["8В"] = new(
                "8В",
                [
                    new(1, "Понедельник", [new(1, 3), new(2, 1), new(3, 5), new(4, 7), new(5, 8), new(6, 14)]),
                    new(2, "Вторник", [new(1, 4), new(2, 2), new(3, 5), new(4, 9), new(5, 10), new(6, 13)]),
                    new(3, "Среда", [new(1, 1), new(2, 3), new(3, 6), new(4, 11), new(5, 2), new(6, 14)]),
                    new(4, "Четверг", [new(1, 4), new(2, 5), new(3, 7), new(4, 9), new(5, 12), new(6, 15)]),
                    new(5, "Пятница", [new(1, 3), new(2, 1), new(3, 8), new(4, 10), new(5, 6), new(6, 14)])
                ])
        };

    public IReadOnlyList<SchoolSubject> Subjects => _subjects;

    public ClassSchedule? GetSchedule(string className) =>
        _schedules.TryGetValue(className.Trim(), out var schedule) ? schedule : null;
}
