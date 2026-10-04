namespace Converter;

internal static class CommandLineHelp
{
    public static void WriteToConsole(TextWriter Writer)
    {
        Writer.WriteLine("Markdown to DOCX converter (Converter.exe) - command-line usage");
        Writer.WriteLine();
        Writer.WriteLine("Headless mode runs when you pass -docx, -png, -svg, both -in and -out for one file,");
        Writer.WriteLine("or batch -in (glob, folder, or repeated -in). Progress is written to stdout as");
        Writer.WriteLine("[nnn%] messages; the process exits 0 on success, 1 on error.");
        Writer.WriteLine();
        Writer.WriteLine("GUI mode (not detailed here): no arguments; or a single -in file path only; or -out alone.");
        Writer.WriteLine();
        Writer.WriteLine("Requirements");
        Writer.WriteLine("  -in <path>   Required for every headless job except -h. May be repeated.");
        Writer.WriteLine("  Each -in value may be a file, a folder, or a path search pattern (wildcards in the file name).");
        Writer.WriteLine("  At least one output is required unless batch -in applies (see below):");
        Writer.WriteLine("    - -out <file.docx> or -docx <file.docx> with exactly one input file, or");
        Writer.WriteLine("    - -png <file.png> and/or -svg <file.svg> (one input file), or");
        Writer.WriteLine("    - batch -in with no -out/-docx (writes <name>.docx beside each source file).");
        Writer.WriteLine();
        Writer.WriteLine("Help");
        Writer.WriteLine("  Converter.exe -h");
        Writer.WriteLine("  Converter.exe -help | --help | /?");
        Writer.WriteLine("      Prints this text to stdout and exits 0.");
        Writer.WriteLine();
        Writer.WriteLine("Batch convert Markdown/text to DOCX (implicit output paths)");
        Writer.WriteLine("  Converter.exe -in *.md");
        Writer.WriteLine("  Converter.exe -in doc??.md");
        Writer.WriteLine("  Converter.exe -in .\\docs");
        Writer.WriteLine("  Converter.exe -in \"\\folder\\files*.md\"");
        Writer.WriteLine("  Converter.exe -in \"c:\\folder1\\folder2\\files*.md\"");
        Writer.WriteLine("  Converter.exe -in chapter1.md -in chapter2.md");
        Writer.WriteLine("      Converts every matching .md, .markdown, and .txt file.");
        Writer.WriteLine("      Wildcards apply to the file name in the given folder (top level).");
        Writer.WriteLine("      Use \"**\" in the path for recursive search (e.g. .\\docs\\**\\files*.md).");
        Writer.WriteLine("      In PowerShell, quote the pattern so the shell does not expand * before Converter runs.");
        Writer.WriteLine("      Writes each output next to its source as <same-filename>.docx.");
        Writer.WriteLine("      Runs conversions in parallel (up to processor count minus one workers).");
        Writer.WriteLine("      Do not use -out or -docx with multiple inputs (that is an error).");
        Writer.WriteLine("      -index does not apply to batch DOCX conversion.");
        Writer.WriteLine();
        Writer.WriteLine("Convert one Markdown file to DOCX");
        Writer.WriteLine("  Converter.exe -in <file.md> -out <file.docx>");
        Writer.WriteLine("  Converter.exe -in <file.md> -docx <file.docx>");
        Writer.WriteLine("      Converts the full file (all diagrams). -out and -docx are equivalent.");
        Writer.WriteLine();
        Writer.WriteLine("Export Mermaid diagram(s) to PNG and/or SVG (one input file only)");
        Writer.WriteLine("  Converter.exe -in <source> -png <file.png>");
        Writer.WriteLine("  Converter.exe -in <source> -svg <file.svg>");
        Writer.WriteLine("  Converter.exe -in <source> -png <file.png> -svg <file.svg>");
        Writer.WriteLine("      Input: .md / .markdown with a ```mermaid block, or a raw .mmd / text file.");
        Writer.WriteLine("      For multiple ```mermaid blocks in one .md file, add -index N (1 = first).");
        Writer.WriteLine("      Example: Converter.exe -in flow.md -png flow-png\\F24.png -index 24");
        Writer.WriteLine();
        Writer.WriteLine("Combine DOCX conversion and diagram export (one input file)");
        Writer.WriteLine("  Converter.exe -in <file.md> -docx <file.docx> -png <file.png> -svg <file.svg>");
        Writer.WriteLine("      DOCX conversion runs first, then diagram export. -out is ignored if -docx/-png/-svg");
        Writer.WriteLine("      are present; use -docx for the DOCX path.");
        Writer.WriteLine();
        Writer.WriteLine("Progress and output");
        Writer.WriteLine("  Headless jobs report [nnn%] progress on stdout.");
        Writer.WriteLine("  Batch jobs print worker count, then [i/n] filename per started conversion.");
        Writer.WriteLine("  Per-file [nnn%] progress is omitted during parallel batch (avoids interleaved output).");
        Writer.WriteLine("  On success: \"Converted: ...\" or \"Wrote: ...\" lines.");
        Writer.WriteLine("  Errors go to stderr; bad arguments also print this help on stderr.");
        Writer.WriteLine();
        Writer.WriteLine("Flags (long and short forms)");
        Writer.WriteLine("  -in,    -i       Input path(s): file, glob, or folder (repeatable)");
        Writer.WriteLine("  -out,   -o       Output DOCX for a single -in file (not for batch)");
        Writer.WriteLine("  -docx,  -d       Output DOCX for a single -in file (not for batch)");
        Writer.WriteLine("  -png,   -p       Output PNG (Mermaid export, single input)");
        Writer.WriteLine("  -svg,   -s       Output SVG (Mermaid export, single input)");
        Writer.WriteLine("  -index, -n N     ```mermaid block index for export (default 1)");
        Writer.WriteLine("  -h,     -help    Show help (--help and /? also accepted)");
    }

    public static void WriteToConsole()
    {
        WriteToConsole(Console.Out);
    }
}
