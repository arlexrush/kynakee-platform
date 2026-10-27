using Kynakee.Modules.SharedKernel.Application;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace Kynakee.Api.Endpoints;

internal static class ApiProblems
{
    internal static IResult FromError(ApplicationError error, int statusCode, IReadOnlyDictionary<string, string>? fieldNames = null) =>
        error.Type == ErrorType.Validation && error.Errors is not null
            ? Results.ValidationProblem(
                error.Errors.GroupBy(pair => fieldNames is not null && fieldNames.TryGetValue(pair.Key, out var name) ? name : pair.Key, StringComparer.Ordinal)
                    .ToDictionary(static group => group.Key, static group => group.SelectMany(static pair => pair.Value).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal),
                title: error.Code,
                detail: error.Message,
                statusCode: statusCode)
            : Results.Problem(title: error.Code, detail: error.Message, statusCode: statusCode);
}
