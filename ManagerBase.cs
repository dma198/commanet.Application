using System;
using System.Globalization;
using System.Threading;

using Microsoft.Extensions.Configuration;

using NLog;

namespace commanet
{
    public abstract class ManagerBase : IApplicationManager
    {
        public virtual string Description { get; } = "";
        public virtual string ExampleOfUsage { get; } = "";

        public Logger? Logger { get; private set; }

        public void SetLogger(Logger logger)
        {
            Logger=logger;
        }

        public virtual Type OptionsClass { get; } = typeof(object);
        public abstract bool Startup(ApplicationBase app, IConfiguration? config);

        public abstract void Shutdown();
        protected T GetParameter<T>(IConfiguration config, string name, T DefaultValue=default)
        {
            var res = config.GetValue(name,DefaultValue);
            if (res == null)
            {
                if (DefaultValue == null)
                    Logger?.Error($"{name} configuration parameter missing");
                else
                {
                    Logger?.Warn($"{name} configuration parameter missing. Will be used default: {DefaultValue}");
                    if (typeof(T) != DefaultValue.GetType() && Nullable.GetUnderlyingType(typeof(T)) != DefaultValue.GetType())
                        Logger?.Warn($"GetParameter: Parameter type <{typeof(T).Name}> mismatch with type of provided default value <{DefaultValue.GetType().Name}>. Will try to convert.");
                    res = (T)Convert.ChangeType(DefaultValue, typeof(T),CultureInfo.InvariantCulture);                    
                }
            }
            return res;
        }
 
        public static void WaitCtlC()
        {
            using var shutdown = new ManualResetEvent(false);
            Console.CancelKeyPress += delegate(object sender, ConsoleCancelEventArgs e) {
              shutdown.Set();
            };
            shutdown.WaitOne();
        }
    }
}
