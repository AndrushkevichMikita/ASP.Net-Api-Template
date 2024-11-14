using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApiTemplate.SharedKernel.ExceptionHandler
{
    public static class ExceptionHandlerExtension
    {
        private static readonly Dictionary<Type, Func<HttpContext, Exception, Task>> _exceptionHandlers = new()
        {
            { typeof(MyApplicationException), HandleMyApplicationException },
            { typeof(OperationCanceledException), HandleOperationCanceledException },
            { typeof(BadHttpRequestException), (c,e)=> Task.CompletedTask  }, // https://github.com/dotnet/aspnetcore/issues/23949
        };

        private static async Task HandleMyApplicationException(HttpContext httpContext, Exception ex)
        {
            var exception = (MyApplicationException)ex;
            httpContext.Response.StatusCode = exception.ErrorStatus switch
            {
                ErrorStatus.InvalidData => StatusCodes.Status400BadRequest,
                ErrorStatus.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorStatus.Forbidden => StatusCodes.Status403Forbidden,
                ErrorStatus.NotFound => StatusCodes.Status404NotFound,
                ErrorStatus.NotAcceptable => StatusCodes.Status406NotAcceptable,
                ErrorStatus.PayloadLarge => StatusCodes.Status413RequestEntityTooLarge,
                _ => throw ex
            };

            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails()
            {
                Detail = exception.Message,
                Status = httpContext.Response.StatusCode,
            });
        }

        private static async Task HandleOperationCanceledException(HttpContext httpContext, Exception ex)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails()
            {
                Detail = "Your submission was canceled.",
                Status = httpContext.Response.StatusCode,
            });
        }

        private static async Task HandleUnknownException(HttpContext httpContext, Exception exception)
        {
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            var r = new ProblemDetails()
            {
                Detail = "Something went wrong...",
                Status = httpContext.Response.StatusCode,
            };
#if DEBUG
            r.Title = exception.Message;
            r.Detail = exception.StackTrace;
#endif
            await httpContext.Response.WriteAsJsonAsync(r);
        }

        public static void HandleExceptions(this IApplicationBuilder app)
        {
#if DEBUG
            app.UseDeveloperExceptionPage();
#endif
            app.Use(async (httpContext, next) =>
            {
                try
                {
                    await next();
                }
                catch (Exception exception)
                {
                    var logger = app.ApplicationServices.GetRequiredService<ILogger<Exception>>();
                    logger.LogError(exception, exception.Message);

                    var exceptionType = exception.GetType();
                    if (_exceptionHandlers.ContainsKey(exceptionType))
                        await _exceptionHandlers[exceptionType].Invoke(httpContext, exception);
                    else
                        await HandleUnknownException(httpContext, exception);
                }
            });
        }
    }
}