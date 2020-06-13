using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using NLog;

namespace commanet
{
    public class ApplicationBase
    {
        #region Public Properties
        public static bool IsInteractiveMode { get; set; } = false;
        public static Logger? Logger { get; set; } = null;
        public static object? Options { get; set; } = null;
        public static string StartDirectory { get; protected set; } = GetStartDirectory();
        public static List<string> CommandLineArgs { get; } = new List<string>();
        public static string ServiceName { get; set; } = "";
        #endregion

        #region Protected Properties (to be used by inherit implementations)
        protected IHostApplicationLifetime? AppLifeTime { get; set; } = null;
        protected IConfiguration? Configuration { get; set; }
        protected static bool IsHelpPrint { get; set; } = false;
        protected static IApplicationManager? Manager { get; set; }
        protected static List<string> ConfigurationFiles { get; } = new List<string>();
        #endregion

        #region Private Methods
        private static string GetStartDirectory()
        {
            #pragma warning disable CA1303 // Do not pass literals as localized parameters
            var curProcess = Process.GetCurrentProcess();
            if (curProcess == null)
                throw new Exception("Can't GetCurrentProcess");
            var mainModule = curProcess.MainModule;
            if (mainModule == null)
                throw new Exception("Can't get process main module");
            if(mainModule.FileName == null)
                throw new Exception("MainModule file name is not defined");
            var startDir = Path.GetDirectoryName(mainModule.FileName);
            if(startDir == null)
                throw new Exception("Can't evaluate start directory");
            #pragma warning restore CA1303 // Do not pass literals as localized parameters
            return startDir;
        }
        #endregion
    }
}