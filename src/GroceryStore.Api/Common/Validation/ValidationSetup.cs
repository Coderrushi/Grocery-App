using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;

namespace GroceryStore.Api.Common.Validation;

internal static partial class ValidationSetup
{
    public static IServiceCollection AddAppValidation(this IServiceCollection services)
    {
        // Error keys use the JSON (camelCase) property names so the web app can attach each message
        // to its form field directly, while default messages still read naturally ("Full Name must ...").
        ValidatorOptions.Global.PropertyNameResolver = (_, member, _) =>
            member is null ? null : JsonNamingPolicy.CamelCase.ConvertName(member.Name);
        ValidatorOptions.Global.DisplayNameResolver = (_, member, _) =>
            member is null ? null : WordBoundary().Replace(member.Name, " $1");

        return services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);
    }

    [GeneratedRegex("(?<=[a-z])([A-Z])")]
    private static partial Regex WordBoundary();
}
