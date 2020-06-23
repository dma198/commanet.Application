# commanet.Application
--------------------------
Package for creating service or command line utilities applications

Provides:
- Application lifecycle management
- Configuration (JSON files)
- Logs (NLog based) 

Supported 2 kind of applications:

- **Services**: Application runs in background on server side.
- **Console Applications**: Command line utilities. Works in interactive mode to perform on-shot tasks.    

Console Applications does not use Logs functionality. For them all input/output is only in console.


#### Hello World
1. Create *DotNet Core* console application.
2. Add dependency to commanet.Application package
3. Made 2 modules in application
   - Program.cs
   - Manager.cs
-----------------------
```c#
// Program.cs
using System.Threading.Tasks;
using commanet;

namespace MyHelloWorldApplication // <= The only identifer of application
{                                 //    namespace to be changed for particular
    class Program                 //    application  
    {
        static async Task Main(string[] args)
        {    
            // RunAsync method called below has argument
            // bool InteractiveMode = false
            // Set it to true if wants made not Service application 
            //           false if console utility           
            await new Application<Manager>(args)
                .RunAsync()
                .ConfigureAwait(false);                                          
        }
    }
}
```
-----------------------
```c#
// Manager.cs
using Microsoft.Extensions.Configuration;

using NLog;
using commanet;

namespace MyHelloWorldApplication
{
    public class Manager : ManagerBase
    {
       public override string Description => "Hello commanet World Application";
     	public override bool Startup(ApplicationBase app,IConfiguration config)
     	{
           // Here should be code to initialize 
           // and start to perform required program
           // activities
           Logger.Info("Hello commanet World !");
		
           return true;
     	}
    
    	public override void Shutdown()
    	{
           // Here should be code for graceful program
           // shutdown
           Logger.Info("Bye!");
    	}
    }
}
```
-----------------------
This application is completed. It can be built and runs in Debug mode (Visual Studio or VS Code) or after publishing as standalone executable.

#### Command Line Arguments (Options)

All argument are in formats:
- *Short:* -\<one letter name\> [\<value\>] 
  
  *Example:* -p 30000
- *Long:* --\<name\>[=\<value\>] 

  *Example:* --port=30000 


Currently supported standard command line options:

Option        |Description                      | Default Value |Example
--------------|---------------------------------|---------------|-----
-h, --help    | Print on console help message   |               | 
-p, --prefix  | Service Name prefix.            |No prefix used |Bingo_

> **Note!** Service Name for Windows Service is configured in external service manager (Can use NSSM - [see below](#runassrv)). 
> Regardless of name in Windows (normally should be same) in commanet will be used 
> name of EXE plus given configured prefix.    

Console Applications can be configured for use its own custom command line options.
[See below](#appopt).



#### Configuration

Configuration based on *Microsoft.Extensions.Configuration.Json* package and uses JSON files format.
Configuration files to be placed in *Config* folder. This folder searched starting from one where placed application executables and if not found it going level by level upper till find folder with this name or reach disk root position.   
For example: Executables are in C:\MyProject\Bin\MyApplication. Config directory will be searched in order:
- C:\MyProject\Bin\MyApplication\Config
- C:\MyProject\Bin\Config
- C:\MyProject\Config
- C:\Config



Application loads configuration parameters from files:

- *Common.json* : It is shared between all applications accessed configuration folder
- *[ApplicationName].json* : Apllication specific configuration

In case of presense same parameter in Common and application specific file - application specific configuration file parameters will override common ones. 

**Configuration File Example**
-------------------------
```json
{
    "LOGS": "../../../../../../../../Bin.CC4/Output/Log",
    "NLOG_MINLOGLEVEL"  : "Trace",
    
    "DB_TYPE"       : "ORACLE",
    "DB_USER"       : "MYUSER",
    "DB_PASSWORD"   : "MYPASSWORD",
    "DB_CONNECTION" : "localhost:MYINSTANCE"
}
``` 
-------------------------

#### Logs

Functionality based on NLog package. Logger instance is available as out-of-the-box static property of *ManagerBase* class.
See HelloWorld example above.
Format of logs is fixed. Output is fixed to standard output and file destination.
Paramaters can be configured:

Parameter Name      | Default Value                            | Description
--------------------|------------------------------------------|---------------------------------------------------------------------------------
LOGS                | *Application executable folder*\Logs     | Logs destination folder. Path can be relational from *Application Exe Directory*
NLOG_MINLOGLEVEL    | Info                                     | Logs Minimum Level 
NLOG_MAXLOGLEVEL    | Fatal                                    | Logs Maximum Level 
NLOG_MAXARCHIVEDAYS | 15                                       | Max days to keep archived/historical files


#### <a id=runassrv>Run Application As Windows Service</a>
commanet applications are console type. Using it as Windows Service is possible with 3rd party utilities as [*nssm.exe*](https://nssm.cc).
For application all will be same as it runs in console. Gracefull shutdown performed by emulation by service manager press Ctrl-C.
Below is example of batch-script for registering application as Service in Windows with [*nssm.exe*](https://nssm.cc). 
Script suppose that application is published in directory **bin\\*ApplicationName***

```bat
@echo off
setlocal
rem ============================================================
rem  2 PARAMETERS TO BE CONFIGURED FOR EACH SERVICE
rem ============================================================

SET SERVICE_NAME=MyCommanetApplication
SET SERVICE_DESCRIPTION="commanet for Example"

rem ============================================================
rem BELOW CODE IS COMMON FOR ALL SERVICES
rem ============================================================
SET SERVICE_START_MODE=SERVICE_DEMAND_START
SET CUR_DIR=%~dp0
SET BATCH_SCRIPT_PATH="%CUR_DIR%..\Bin\%SERVICE_NAME%.exe"
rem SET EXECUTE_PATH="%CUR_DIR%..\Bin"

nssm install %SERVICE_NAME% %BATCH_SCRIPT_PATH% 
rem nssm set %SERVICE_NAME% AppDirectory %EXECUTE_PATH%  
nssm set %SERVICE_NAME% DisplayName %SERVICE_NAME% 
nssm set %SERVICE_NAME% Description %SERVICE_DESCRIPTION%
nssm set %SERVICE_NAME% Start %SERVICE_START_MODE% 

endlocal 
```


#### Console Applications

Console application structured same as *Service*. It should have 
*Program.cs* and *Manager.cs*.   
Difference is only in Program.cs must be set second parameter in *RunAsync* call to true.

```c#
// Program.cs
using System.Threading.Tasks;
using commanet;

namespace MyHelloWorldApplication // <= The only identifer of application
{                                 //    namespace to be changed for particular
    class Program                 //    application  
    {
        static async Task Main(string[] args)
        {    
            // RunAsync method called below  has second argument
            // bool InteractiveMode = false
            // Set it to true will tell to DA.SI.COre that application is not
            // a service but command line utility           
            await new Application<Manager>(args)
                .RunAsync(true)
                .ConfigureAwait(false);
        }
    }
}
```
Life cycle of Console application:

1. Executed *Manager.Start()* method
2. Executed *Manager.Stop()* method
3. Application shuted down 



##### <a id=appopt>Application Command Line Options</a>

First is needs to define class which will holds command line options.
It should contain public properties with attribute *CommandLineOption*
```c#
        public class MyAppOptions
        {
            [CommandLineOption(
                    ShortName = "t",
                    LongName = "test",
                    IsOptional = true,
                    DefaultValue = "Hello",
                    Description = "Test Option")]
            public string MyOption { get; set; } = "";
        }

```

Attribut parameters:

Parameter    | Example     |Example in command line | Description 
-------------|-------------|------------------------|--------------
ShortName    |"p"          | -p 5000                |One letter name 
LongName     |"port"       | --port=5000            |More than one letter name
IsOptional   |false        |                        |If it is *false* application will stop with error message in case if it is not provided in command line
DefaultValue |5000         |                        |Parameter value if it is not specified in command line
Description  |"TCP/IP Port"|                        |Description of parameter printed when application called with -h, --help options
  

*Manager* implementation must provide overrided property
```c#
public override Type OptionsClass { get; } = typeof(MyAppOptions);
``` 
And finally options values can be obtained in *Manager.Start()* implementation:
```c#
public override bool Start(IConfiguration config)
{
    var Options = (MyAppOptions)Application<Manager>.Options;
    Console.WriteLine("Command Line Option: {0}",Options.MyOption);
    ...    
``` 

Complete Example:

```c#
using System;
using Microsoft.Extensions.Configuration;

using commanet;

namespace MyApplication
{
    public class Manager : ManagerBase
    {
        // Define class holds options
        public class AppOptions
        {
            [CommandLineOption(
                    ShortName = "u",
                    LongName = "dbuser",
                    IsOptional = true,
                    DefaultValue = null,
                    Description = "Database User")] 
            public string DbUser { get; set; } = "";
        }    
        // Link options class to Application Manager
        public override Type OptionsClass { get; } = typeof(AppOptions);
                public override bool Start(IConfiguration config)
        {
            // Get command line options in instance of options class
            var Options = (AppOptions)Application<Manager>.Options;
            // Use options according application logic
            Console.WriteLine("Database User: {0}",Options.DbUser);
         }
    }
}
```

Notice that help text (-h, --help default options) will be automatically generated.

