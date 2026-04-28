using Xunit;

namespace MaichessDatabaseService.Tests.Support;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class RequiresEnvVarFactAttribute : FactAttribute
{
    public RequiresEnvVarFactAttribute(string envVar)
    {
        if (Environment.GetEnvironmentVariable(envVar) is null)
        {
            Skip = $"{envVar} not set";
        }
    }
}
