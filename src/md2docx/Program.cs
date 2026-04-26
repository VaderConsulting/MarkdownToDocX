using System;
using System.IO;
using MDConverter;
using MDConverter.Options;

namespace md2docx;

internal static class Program
{
    private static int Main(string[] Args)
    {
        if (Args.Length < 2)
        {
            Console.Error.WriteLine("Usage: md2docx <input.md> <output.docx> [--profile <name>]");
            return 1;
        }

        var InputPath = Args[0];
        var OutputPath = Args[1];
        var Profile = "default";

        for (var I = 2; I < Args.Length; I++)
        {
            if (Args[I] == "--profile" && I + 1 < Args.Length)
            {
                Profile = Args[I + 1];
                I++;
            }
        }

        if (!File.Exists(InputPath))
        {
            Console.Error.WriteLine("Input file not found: " + InputPath);
            return 1;
        }

        var Options = new MarkdownToDocxOptions
        {
            Profile = Profile
        };

        try
        {
            var Converter = new MarkdownToDocxConverter(Options);
            Converter.ConvertFile(InputPath, OutputPath);
            Console.WriteLine("Converted: " + InputPath + " -> " + OutputPath);
            return 0;
        }
        catch (MDConverter.MarkdownToDocxException Ex)
        {
            Console.Error.WriteLine("Error: " + Ex.Message);
            return 1;
        }
    }
}
