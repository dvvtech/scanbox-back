using Microsoft.AspNetCore.Mvc;
using Scanbox.Api.Models;
using Scanbox.Api.Services;

namespace Scanbox.Api.Controllers;

[ApiController]
[Route("api/school")]
public sealed class SchoolController(ISchoolDataStore schoolData) : ControllerBase
{
    [HttpGet("subjects")]
    [ProducesResponseType(typeof(IReadOnlyList<SchoolSubject>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<SchoolSubject>> GetSubjects() =>
        Ok(schoolData.Subjects);

    [HttpGet("classes/{className}/schedule")]
    [ProducesResponseType(typeof(ClassSchedule), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ClassSchedule> GetClassSchedule(string className)
    {
        var schedule = schoolData.GetSchedule(className);

        return schedule is null
            ? NotFound(new { message = $"Расписание для класса «{className}» не найдено." })
            : Ok(schedule);
    }
}