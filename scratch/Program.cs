using System;
using System.Linq;
using System.Reflection;
using Microsoft.Diagnostics.Tracing.Parsers;

class Program
{
    static void Main()
    {
        var type = typeof(KernelTraceEventParser);
        var events = type.GetEvents();
        foreach (var evt in events)
        {
            if (evt.Name.StartsWith("FileIO"))
            {
                Console.WriteLine($"{evt.Name} => {evt.EventHandlerType?.GetGenericArguments().FirstOrDefault()?.Name}");
            }
        }
    }
}
