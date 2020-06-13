using System;

namespace commanet
{
    [AttributeUsage(AttributeTargets.Property)]
    public class CommandLineOptionAttribute:Attribute
    {

        public string ShortName { get; set; } = "";
        public string LongName { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsOptional { get; set; } = true;
        public bool IsValueOptional { get; set; } = false;
        public object? DefaultValue { get; set; } = null;
        public CommandLineOptionAttribute()
        {
            
        }
    }
}
