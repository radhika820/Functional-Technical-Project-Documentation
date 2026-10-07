using AcxiomCRM.Dtos;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AcxiomCRM.Controllers.Api;

/// <summary>
/// Base for REST controllers: JWT bearer only (no cookies, so no CSRF surface), JSON in/out,
/// consistent ProblemDetails errors and no stack traces or DB details in responses.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[IgnoreAntiforgeryToken]
[Produces("application/json")]
public abstract class ApiBaseController : ControllerBase
{
    protected static PagedResult<TOut> ToPage<TIn, TOut>(PagedList<TIn> page, Func<TIn, TOut> map) => new()
    {
        Items = page.Items.Select(map).ToList(), Page = page.Page, PageSize = page.PageSize,
        TotalCount = page.TotalCount, TotalPages = page.TotalPages
    };

    /// <summary>Translates a failed service result into 400 / 403 / 404 / 409.</summary>
    protected IActionResult Fail<T>(ServiceResult<T> result)
    {
        switch (result.Status)
        {
            case ResultStatus.NotFound:
                return NotFound(new ProblemDetails { Status = 404, Title = "The requested record was not found." });
            case ResultStatus.Forbidden:
                return StatusCode(403, new ProblemDetails { Status = 403, Title = "You do not have access to this record." });
        }

        var modelState = new ModelStateDictionary();
        foreach (var (field, message) in result.Errors)
            modelState.AddModelError(string.IsNullOrEmpty(field) ? "" : char.ToLowerInvariant(field[0]) + field[1..], message);

        if (result.Errors.Any(e => e.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)))
        {
            var conflict = new ValidationProblemDetails(modelState) { Status = 409, Title = "A record with the same unique value already exists." };
            return Conflict(conflict);
        }
        return ValidationProblem(modelState);
    }
}
