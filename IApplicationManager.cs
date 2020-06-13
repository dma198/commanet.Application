using System;

using Microsoft.Extensions.Configuration;

using NLog;

namespace commanet
{
    public interface IApplicationManager
    {
        string Description { get; }
        string ExampleOfUsage { get; }
        Type OptionsClass { get; }
        bool Startup(ApplicationBase app, IConfiguration? config);
        void Shutdown();
        void SetLogger(Logger logger);

    }
}
