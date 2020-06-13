using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;

namespace commanet
{

    public class CommandLineParser
    {
        private class CmdOptions
        {
            public PropertyInfo? Pi { get; set; }
            public string? ShortName { get; set; }
            public string? LongName { get; set; }
            public string? Description { get; set; }
            public bool IsOptional { get; set; }
            public bool IsValueOptional { get; set; }
            public object? DefaultValue { get; set; }
            public bool OptionProvided { get; set; } = false;

        }

        private readonly List<CmdOptions> options = new List<CmdOptions>();

        private readonly string exeName;
        public string? Header { get; set; }
        public string? Footer { get; set;}
        public bool PrintHelpIfNoArguments { get; set; } = false;

        public CommandLineParser()
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly == null)
                #pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("Can't get entry assembly");
                #pragma warning restore CA1303 // Do not pass literals as localized parameters
            exeName =Path.GetFileNameWithoutExtension(entryAssembly.Location);
        }

        public object Parse(string[] args, Type t)
        {
            if (args == null)
                #pragma warning disable CA2208 // Instantiate argument exceptions correctly
                throw new ArgumentNullException(nameof(args));
                #pragma warning restore CA2208 // Instantiate argument exceptions correctly
            if (t == null)
                #pragma warning disable CA2208 // Instantiate argument exceptions correctly
                throw new ArgumentNullException("Type");
                #pragma warning restore CA2208 // Instantiate argument exceptions correctly

            var res = Activator.CreateInstance(t);
            if (res == null)
                throw new Exception($"Can't create instance of type {t.Name}");

            options.Clear();

            var op = new CmdOptions()
            {
                ShortName = "h",
                LongName = "help",
                IsOptional = true,
                IsValueOptional = true,
                Description = "Show Help"
            };
            options.Add(op);

            foreach (var p in res.GetType().GetProperties())
            {
                op = new CmdOptions() { Pi = p };
                if (op == null)
                    throw new Exception($"Can't create instance of {typeof(CmdOptions).Name}");

                var at = (CommandLineOptionAttribute?)p.GetCustomAttribute(typeof(CommandLineOptionAttribute));
                if (at != null)
                {
                    op.ShortName = at.ShortName;
                    op.LongName = at.LongName;
                    op.Description = at.Description;
                    op.IsOptional = at.IsOptional;
                    op.IsValueOptional = at.IsValueOptional;
                    op.DefaultValue = at.DefaultValue;
                }
                if (string.IsNullOrEmpty(op.ShortName) && string.IsNullOrEmpty(op.LongName))
                    #pragma warning disable CA1308 // Normalize strings to uppercase
                    op.LongName = p.Name.ToLowerInvariant();
                    #pragma warning restore CA1308 // Normalize strings to uppercase
                options.Add(op);
            }

            var helpRequested = false;

            for (int i = 0; i < args.Length;)
            {
                var ArgumentName = "";
                string? ArgumentValue = null;

                if (args[i].StartsWith("--", StringComparison.InvariantCulture))
                {
                    var argument = args[i].Substring(2);
                    var idxEq = argument?.IndexOf('=',StringComparison.InvariantCulture) ?? 0;
                    if (idxEq >= 0)
                    {
                        ArgumentName = argument?.Substring(0, idxEq);
                        ArgumentValue = argument?.Substring(idxEq + 1);
                    }
                    else
                        ArgumentName = argument?.Trim();
                    i++;
                }
                else if (args[i].StartsWith("-", StringComparison.InvariantCulture) ||
                            args[i].StartsWith("/", StringComparison.InvariantCulture))
                {
                    ArgumentName = args[i].Substring(1);
                    var lop = options.Find(o => o.ShortName == ArgumentName || o.LongName == ArgumentName);
                    if (lop != null && lop.Pi != null && lop.Pi.PropertyType != typeof(bool) && !lop.IsValueOptional)
                    {
                        if (i + 1 < args.Length &&
                            !args[i + 1].StartsWith("--", StringComparison.InvariantCulture) &&
                            !args[i + 1].StartsWith("-", StringComparison.InvariantCulture) &&
                            !args[i + 1].StartsWith("/", StringComparison.InvariantCulture)
                            )
                        {
                            ArgumentValue = args[i + 1];
                            i++;
                        }
                    }
                    i++;
                }
                else
                {
                    //Console.WriteLine("WARNING! Options must starts from '-', '--' or '/' characters. Option {0} will be ignored", args[i]);
                    i++;
                    continue;
                }

                if (ArgumentName == "h")
                {
                    PrintHelp();
                    helpRequested = true;
                }
                else
                {
                    op = options.Find(o => o.ShortName == ArgumentName || o.LongName == ArgumentName);

                    if (op == null)
                    {
                        Console.WriteLine($"WARNING! Unknown option {ArgumentName}. Will be ignored");
                        continue;
                    }

                    op.OptionProvided = true;
                    if (op.Pi != null)
                    {
                        if (op.Pi.PropertyType != typeof(bool) && !op.IsValueOptional && ArgumentValue == null)
                        {
                            Console.WriteLine($"WARNING! Required value is not provided for argument {0}. Will be ignored");
                            continue;
                        }
                        if (op.Pi.PropertyType == typeof(string))
                        {
                            op.Pi.SetValue(res, ArgumentValue?.Trim('\"'));
                        }
                        else if (op.Pi.PropertyType == typeof(int))
                        {
                            if (int.TryParse(ArgumentValue, out int v))
                                op.Pi.SetValue(res, v);
                            else
                                Console.WriteLine($"WARNING! Can't set value {ArgumentName} to option {ArgumentValue} which type is {op.Pi.PropertyType.Name}");
                        }
                        else if (op.Pi.PropertyType == typeof(double))
                        {
                            if (double.TryParse(ArgumentValue, out double v))
                                op.Pi.SetValue(res, v);
                            else
                                Console.WriteLine($"WARNING! Can't set value {ArgumentName} to option {ArgumentValue} which type is {op.Pi.PropertyType.Name}");
                        }
                        else if (op.Pi.PropertyType == typeof(bool))
                        {
                            op.Pi.SetValue(res, !(bool)(op.Pi?.GetValue(res) ?? false));
                        }
                        else Console.WriteLine($"WARNING! Unsupported type {op.Pi.PropertyType.Name} for optyion {ArgumentName}");
                    }
                }
            }

            if (args.Length == 0 && PrintHelpIfNoArguments) PrintHelp();

            foreach (var o in options)
            {
                if (!o.IsOptional && !o.OptionProvided && !helpRequested)
                {
                    Console.WriteLine($"ERROR! Command line argument {o.ShortName}({ o.LongName}) is required! Press ENTER for help");
                    Console.ReadLine();
                    PrintHelp();
                    break;
                }
                else if (!o.OptionProvided && o.Pi != null && o.DefaultValue != null)
                {
                    o.Pi.SetValue(res, o.DefaultValue);
                }
            }

            return res;
        }


        public T Parse<T>(string[] args)
            where T : class, new()
        {
            return (T)Parse(args, typeof(T));
        }

        public Action? AfterPrintHelp { get; set; }


        private static string GetOptionValue(CmdOptions o,bool IsLongName=false)
        {            
            var sb = new StringBuilder();
            if (o.Pi!=null && o.Pi.PropertyType!=typeof(bool))
            {
                char c = IsLongName ? '=' : ' ';
                if (o.IsValueOptional)
                    sb.AppendFormat(CultureInfo.InvariantCulture, $"[{c}<value>]");
                else
                    sb.AppendFormat(CultureInfo.InvariantCulture, $"{c}<value>");
            }
            return sb.ToString();
        }

        private static string GetOptionName(CmdOptions o)
        {
            var sb = new StringBuilder();
            var shortNameExists = false;
            if (!string.IsNullOrEmpty(o.ShortName))
            {
                shortNameExists = true;
                sb.AppendFormat(CultureInfo.InvariantCulture, $" -{o.ShortName}{GetOptionValue(o)}");
            }
            if (!string.IsNullOrEmpty(o.LongName))
            {
                if (shortNameExists) sb.Append(" or");
                sb.AppendFormat(CultureInfo.InvariantCulture, $" --{o.LongName}{GetOptionValue(o, true)}");
            }
            return sb.ToString();
        }

        private const int MAX_LEN = 80;
        private static string GetDescription(CmdOptions o, int shift)
        {
            var sb = new StringBuilder();
            var ss=o.Description?.Split(' ') ?? Array.Empty<string>();
            var l = shift;
            foreach(var s in ss)
            {
                if (l + s.Length <= MAX_LEN)
                {
                    sb.Append(s); sb.Append(" ");
                    l += s.Length+1;
                }else
                {
                    sb.Append("\n");
                    for (int i = 0; i < shift; i++) sb.Append(" ");
                    sb.Append(s); sb.Append(" ");
                    l = shift+s.Length+1;
                }                
            }
            return sb.ToString();
        }
        private static string GetDefaultValue(CmdOptions o, int shift)
        {            
            var sb = new StringBuilder();
            if (o.DefaultValue != null)
            {
                sb.Append("\n");
                for (int i = 0; i < shift; i++) sb.Append(" ");
                sb.AppendFormat(CultureInfo.InvariantCulture, "DEFAULT: {0}",o.DefaultValue);
            }
            return sb.ToString();
        }

        public void PrintHelp()
        {
            Console.WriteLine(Header);
            Console.WriteLine($"\nUsage: {exeName} [options]");
            #pragma warning disable CA1303 // Do not pass literals as localized parameters
            Console.WriteLine("\nOptions:\n");
            #pragma warning restore CA1303 // Do not pass literals as localized parameters
            var namesLen = 0;
            foreach (var o in options)
            {
                var s = GetOptionName(o);
                if (namesLen < s.Length) namesLen=s.Length;
            }

            var shift = namesLen + 3;
            if (shift >10) shift = 10;


            foreach (var o in options)
            {
                var sb = new StringBuilder();
                sb.Append(GetOptionName(o));
                var pos = sb.Length;
                if(sb.Length+3>shift)
                {
                    sb.Append("\n");
                    pos = 0;
                }
                while (pos < shift) { sb.Append(" "); pos++; }
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0}", GetDescription(o,shift));
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0}", GetDefaultValue(o, shift));
                Console.WriteLine(sb);    
            }
            Console.WriteLine(Footer);
            AfterPrintHelp?.Invoke();
        }


    }
}
