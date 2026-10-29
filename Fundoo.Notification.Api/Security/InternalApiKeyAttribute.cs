using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace Fundoo.Notification.Api.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class InternalApiKeyAttribute : Attribute, IAsyncActionFilter
{
    public const string HeaderName = "X-Internal-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var expected = context.HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()["Notification:ApiKey"];

        if (string.IsNullOrWhiteSpace(expected))
        {
            context.Result = new ObjectResult(new ProblemDetails { Status = 500, Title = "Service is not configured." }) { StatusCode = 500 };
            return;
        }

        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();
        var ok = CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));

        if (!ok) { context.Result = new UnauthorizedResult(); return; }
        await next();
    }
}