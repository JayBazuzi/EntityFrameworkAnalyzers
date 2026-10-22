using System.Runtime.CompilerServices;

namespace Bazuzi.EntityFrameworkAnalyzers.Examples;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifierSettings.InitializePlugins();
    }
}
