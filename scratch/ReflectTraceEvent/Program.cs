using System;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;

class Program {
    static void Main() {
        foreach(var name in Enum.GetNames(typeof(CreateDisposition))) {
            var val = (int)Enum.Parse(typeof(CreateDisposition), name);
            Console.WriteLine(name + " = " + val);
        }
    }
}
