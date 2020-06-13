using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

using NLog;
using NLog.Config;
using NLog.Targets;

namespace commanet
{
    public class Application<T> : ApplicationBase, IHostedService
        where T : IApplicationManager,new()
    {
        #region Constructors
        public Application(string[] args)
        {
            CommandLineArgs.AddRange(args);

            var cmdParser = new CommandLineParser();

            EntryAssembly = Assembly.GetEntryAssembly();
            if (EntryAssembly == null)
            {
                #pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("Can't get entry assembly!");
                #pragma warning restore CA1303 // Do not pass literals as localized parameters
            }
            var entryAssemblyName = EntryAssembly.GetName()?.Name;
            if (entryAssemblyName == null)
            {
                #pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("Can't get entry assembly name!");
                #pragma warning restore CA1303 // Do not pass literals as localized parameters
            }


            if (!IsInteractiveMode)
            {
                Options = cmdParser.Parse<ApplicationOptions>(CommandLineArgs.ToArray());
                ServiceName = (((ApplicationOptions)Options).ServiceNamePrefix == ApplicationOptions.NOPREFIX ? "" : ((ApplicationOptions)Options).ServiceNamePrefix)
                             + entryAssemblyName;
            }
            else
            {
                if (Manager != null)
                    Options = cmdParser.Parse(CommandLineArgs.ToArray(), Manager.OptionsClass);
                ServiceName = entryAssemblyName;
            }

            Manager = new T();

        }

        public Application(
            #pragma warning disable IDE0060 // Remove unused parameter
            #pragma warning disable CA1801
            IConfiguration configuration,
            IHostEnvironment environment,
            Microsoft.Extensions.Logging.ILogger<Application<T>> logger,
            IHostApplicationLifetime lifeTime)
            #pragma warning restore CA1801 // Remove unused parameter
            #pragma warning restore IDE0060 // Remove unused parameter           
        {

            AppLifeTime = lifeTime;
            Configuration = configuration;

            ConfigureNLog();

            if (Logger != null)
                Manager?.SetLogger(Logger);

            if (IsInteractiveMode)
            {
                lifeTime?.StopApplication();
            }
        }
        #endregion

        #region Public Methods
        public Task RunAsync(bool Interactive=false)
        {
            var cmdParser = new CommandLineParser();

            IsInteractiveMode = Interactive;

            if (Manager != null)
            {
                cmdParser.Header = Manager.Description;
                if (!string.IsNullOrEmpty(Manager.ExampleOfUsage))
                    cmdParser.Footer = "\nExample of usage:\n\n" + Manager.ExampleOfUsage;
            }
            cmdParser.AfterPrintHelp = () => IsHelpPrint = true;
            
            if (IsHelpPrint)
            {
                IsInteractiveMode = true;
                var h = new HostBuilder()
                    .ConfigureServices((hostContext, services) =>
                    {
                        services.Configure<ConsoleLifetimeOptions>(opt => opt.SuppressStatusMessages = true);
                        services.AddHostedService<Application<T>>();
                    })
                    .Build();
                return h.RunAsync();
            };


            var host = new HostBuilder()
                .ConfigureAppConfiguration((hostContext, configApp) =>
                {
                    /* Supposed that Config directory placed in the same forlder where is EXE
                     * or will be searched closest Config directory in levels upper of EXE till root
                     * of disk. Ex:
                     *  C:\MyProject\bin\MyApplication\Application.exe
                     *  Will be searched first presented folder in:
                     *      - C:\MyProject\bin\MyApplication\Config
                     *      - C:\MyProject\bin\Config
                     *      - C:\MyProject\Config
                     *      - C:\Config
                     */

                    var cfgDir = StartDirectory;
                    if(cfgDir == null)
                    {
                        #pragma warning disable CA1303 // Do not pass literals as localized parameters
                        throw new Exception("Can't get entry assembly name!");
                        #pragma warning restore CA1303 // Do not pass literals as localized parameters
                    }

                    while (!Directory.Exists(Path.Combine(cfgDir, "Config")) && Path.GetPathRoot(cfgDir) != cfgDir)
                        cfgDir = Path.GetFullPath(Path.Combine(cfgDir, ".."));
                    cfgDir = Path.Combine(cfgDir, "Config");
                    if (Directory.Exists(cfgDir))
                    {
                        cfgDir = Path.GetFullPath(cfgDir);
                        cfgDir = cfgDir.TrimEnd('/', '\\');
                        var commonCfgFileName = Path.Combine(cfgDir, "Common.json");
                        var serviceCfgFileName = Path.Combine(cfgDir, ServiceName+".json");
                        if (File.Exists(commonCfgFileName))
                        {
                            configApp.AddJsonFile(Path.Combine(cfgDir, commonCfgFileName));
                            ConfigurationFiles.Add(commonCfgFileName);
                        }
                        if (File.Exists(serviceCfgFileName))
                        {
                            configApp.AddJsonFile(Path.Combine(cfgDir, serviceCfgFileName));
                            ConfigurationFiles.Add(serviceCfgFileName);
                        }
                    }

                })

                .ConfigureServices((hostContext, services) =>
                { 
                    if(Manager!=null && Logger != null)Manager.SetLogger(Logger);
                    services.Configure<ConsoleLifetimeOptions>(opt => opt.SuppressStatusMessages = true);
                    services.AddHostedService<Application<T>>();
                })
            
                .Build();
          
            return host.RunAsync();
        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (IsInteractiveMode)
            {
                AppLifeTime?.ApplicationStarted.Register(() =>
                {
                    if (!IsHelpPrint)
                    {
                        Manager?.Startup(this,Configuration);
                        Manager?.Shutdown();
                        LogManager.Shutdown();
                    }
                    AppLifeTime?.StopApplication();
                });
                return Task.CompletedTask;
            }

            AppLifeTime?.ApplicationStarted.Register(()=> {
                Logger?.Info($"Service {ServiceName} started");
                try
                {
                    if (Manager != null)
                    {
                        if (!Manager.Startup(this,Configuration))
                            AppLifeTime.StopApplication();
                        Logger?.Info("Service initialization done");
                    }
                }
                #pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
                {
                    Logger?.Error($"Unmanaged exception happens during service start: \n{ex}");                    
                    AppLifeTime.StopApplication();
                }
                #pragma warning restore CA1031 // Do not catch general exception types
            });
            AppLifeTime?.ApplicationStopped.Register(()=>
            {
                Manager?.Shutdown();
                Logger?.Info($"Service {ServiceName} shuted down");
                LogManager.Shutdown();
            });
            return Task.CompletedTask;
        }        
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
        #endregion

        #region Private Properties
        private Assembly? EntryAssembly { get; set; }

        #endregion

        #region Private Methods 
        private void ConfigureNLog()
        {
            var conf = new LoggingConfiguration();
      
            var sMinLogLevel=Configuration.GetValue<string>("NLOG_MINLOGLEVEL") ?? "Info";
            var minLogLevel = LogLevel.FromString(sMinLogLevel);
            var sMaxLogLevel = Configuration.GetValue<string>("NLOG_MAXLOGLEVEL") ?? "Fatal";
            var maxLogLevel = LogLevel.FromString(sMaxLogLevel);
            var sMaxArchiveDays = Configuration.GetValue<string>("NLOG_MAXARCHIVEDAYS") ?? "15";
            var maxArchiveDays = int.Parse(sMaxArchiveDays, CultureInfo.InvariantCulture);

            var logDir = Configuration.GetValue<string>("LOGS");
            if (logDir == null && StartDirectory != null)
            {
                logDir = Path.Combine(StartDirectory, "Log");
            }

            if (logDir != null && StartDirectory != null && !Path.IsPathRooted(logDir))
            {
                logDir = Path.Combine(StartDirectory, logDir);
            }

            if (logDir != null)
            {
                logDir = Path.GetFullPath(logDir);
                logDir = logDir.TrimEnd('/', '\\');
            }

            var header = "".PadRight(80, '=') + "\n" + $@"
 Service  Name : {ServiceName}
 Computer Name : {Environment.MachineName}
 PID           : {Process.GetCurrentProcess().Id}
 OS            : {System.Runtime.InteropServices.RuntimeInformation.OSDescription}
 OS Arch.      : {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture}
 .Net Ver.     : {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}
";
            header +=       " Config Files  : ";
            if (ConfigurationFiles.Count == 0) header += "NONE\n";
            else
            {
                header += "\n";
                foreach (var cf in ConfigurationFiles)
                    header += "   " + cf + "\n";
            }    
            header +=       " LogFolder     : " + logDir + "\n" +
                            " Started at    : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture)+"\n"+
                            "".PadRight(80, '=') + "\n";
            var layout = @"${date:format=yyyy-MM-dd HH\:mm\:ss.fff} ${level} ${message} ${exception}";
            
            using (var consoleTarget = new ConsoleTarget()
            {
                Name = "ConsoleTarget",
                Header = header,
                Layout = layout
            })
            using (var fileTarget = new FileTarget()
            {
                Name = "FileTarget",
                Header = header,
                Layout = layout,
                ArchiveAboveSize = 1024 * 1000,
                FileName = logDir + "/" + ServiceName + ".log",
                FileNameKind = FilePathKind.Absolute,
                ArchiveFileName = logDir + "/History/" + ServiceName + @"_${date:format=yyyy-MM-dd_HH\:mm\:ss}.log",
                ArchiveFileKind = FilePathKind.Absolute,
                MaxArchiveDays = maxArchiveDays
            }) 
            {
                conf.AddTarget(consoleTarget);
                conf.AddTarget(fileTarget);
                conf.AddRule(minLogLevel, maxLogLevel, consoleTarget);
                conf.AddRule(minLogLevel, maxLogLevel, fileTarget);
            }


            LogManager.Configuration = conf;
            Logger =  LogManager.GetLogger("DA.SI.Core.Logger");

            if (ConfigurationFiles.Count==0)
            {
                Logger?.Warn("NO ANY CONFIGURATION FILES FOUND! All parameters will be used with its default values.");
            }
        }
        #endregion
    }
}
