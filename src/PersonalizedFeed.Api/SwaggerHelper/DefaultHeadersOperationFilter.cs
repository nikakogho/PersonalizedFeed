using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PersonalizedFeed.Api.SwaggerHelper;

public class DefaultHeadersOperationFilter : IOperationFilter
{
    private const string TenantId = "tenant_1";
    private const string TenantApiKey = "secret-api-key";
    private const string UserHash = "user_hash_123";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Parameters == null)
            return;

        foreach (var p in operation.Parameters.Where(p => p.In == ParameterLocation.Header))
        {
            switch (p.Name)
            {
                case "X-Tenant-Id":
                    p.Schema ??= new OpenApiSchema { Type = "string" };
                    p.Schema.Default = new OpenApiString(TenantId);
                    break;

                case "X-Api-Key":
                    p.Schema ??= new OpenApiSchema { Type = "string" };
                    p.Schema.Default = new OpenApiString(TenantApiKey);
                    break;

                case "X-User":
                    p.Schema ??= new OpenApiSchema { Type = "string" };
                    p.Schema.Default = new OpenApiString(UserHash);
                    break;
            }
        }
    }
}
