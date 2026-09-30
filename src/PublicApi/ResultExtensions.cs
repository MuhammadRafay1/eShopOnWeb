using System.Linq;
using Ardalis.Result;
using Microsoft.AspNetCore.Http;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace Microsoft.eShopWeb.PublicApi;

/// <summary>Maps a non-success <see cref="Ardalis.Result"/> outcome to the matching HTTP response.</summary>
public static class ResultExtensions
{
    public static IResult ToErrorResult<T>(this Result<T> result) => ToErrorResult(result.Status, result.ValidationErrors, result.Errors);

    public static IResult ToErrorResult(this Result result) => ToErrorResult(result.Status, result.ValidationErrors, result.Errors);

    private static IResult ToErrorResult(ResultStatus status, System.Collections.Generic.List<ValidationError> validationErrors, System.Collections.Generic.IEnumerable<string> errors) =>
        status switch
        {
            ResultStatus.NotFound => Results.NotFound(),
            ResultStatus.Invalid => Results.ValidationProblem(validationErrors
                .GroupBy(e => e.Identifier)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),
            ResultStatus.Forbidden => Results.Forbid(),
            ResultStatus.Unauthorized => Results.Unauthorized(),
            _ => Results.Problem(string.Join("; ", errors), statusCode: 500)
        };
}
