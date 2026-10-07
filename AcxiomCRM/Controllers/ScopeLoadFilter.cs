using AcxiomCRM.Services;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AcxiomCRM.Controllers;

/// <summary>Resolves the caller's role/data scope once per request, after authorization has run.</summary>
public class ScopeLoadFilter : IAsyncActionFilter
{
    private readonly IScopeService _scope;
    public ScopeLoadFilter(IScopeService scope) => _scope = scope;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        await _scope.LoadAsync();
        await next();
    }
}
