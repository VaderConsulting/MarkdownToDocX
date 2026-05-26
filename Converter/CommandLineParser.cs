namespace Converter;

internal static class CommandLineParser
{
    public static bool TryParse(string[] Args, out CommandLineRequest Request, out string ErrorMessage)
    {
        Request = new CommandLineRequest();
        ErrorMessage = string.Empty;

        for (int Index = 0; Index < Args.Length; Index++)
        {
            string Arg = Args[Index];
            if (IsHelpFlag(Arg))
            {
                Request.ShowHelp = true;
                continue;
            }

            if (IsInputFlag(Arg))
            {
                if (!TryReadValue(Args, ref Index, out string Value, out ErrorMessage))
                {
                    return false;
                }

                Request.AddInputPathArgument(Value);
                continue;
            }

            if (IsOutputFlag(Arg))
            {
                if (!TryReadValue(Args, ref Index, out string Value, out ErrorMessage))
                {
                    return false;
                }

                Request.OutputPath = Path.GetFullPath(Value);
                continue;
            }

            if (IsDocxFlag(Arg))
            {
                if (!TryReadValue(Args, ref Index, out string Value, out ErrorMessage))
                {
                    return false;
                }

                Request.DocxPath = Path.GetFullPath(Value);
                continue;
            }

            if (IsPngFlag(Arg))
            {
                if (!TryReadValue(Args, ref Index, out string Value, out ErrorMessage))
                {
                    return false;
                }

                Request.PngPath = Path.GetFullPath(Value);
                continue;
            }

            if (IsSvgFlag(Arg))
            {
                if (!TryReadValue(Args, ref Index, out string Value, out ErrorMessage))
                {
                    return false;
                }

                Request.SvgPath = Path.GetFullPath(Value);
                continue;
            }

            if (IsIndexFlag(Arg))
            {
                if (!TryReadIntValue(Args, ref Index, out int Value, out ErrorMessage))
                {
                    return false;
                }

                Request.MermaidBlockIndex = Value;
                continue;
            }

            ErrorMessage = "Unrecognised argument: " + Arg;
            return false;
        }

        return true;
    }

    private static bool TryReadValue(string[] Args, ref int Index, out string Value, out string ErrorMessage)
    {
        Value = string.Empty;
        ErrorMessage = string.Empty;

        if (Index + 1 >= Args.Length)
        {
            ErrorMessage = "Missing value after " + Args[Index] + ".";
            return false;
        }

        Index++;
        Value = Args[Index];
        if (string.IsNullOrWhiteSpace(Value))
        {
            ErrorMessage = "Empty path after " + Args[Index - 1] + ".";
            return false;
        }

        return true;
    }

    private static bool TryReadIntValue(string[] Args, ref int Index, out int Value, out string ErrorMessage)
    {
        Value = 0;
        ErrorMessage = string.Empty;

        if (Index + 1 >= Args.Length)
        {
            ErrorMessage = "Missing value after " + Args[Index] + ".";
            return false;
        }

        Index++;
        string Text = Args[Index];
        if (!int.TryParse(Text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out Value))
        {
            ErrorMessage = "Invalid integer after " + Args[Index - 1] + ": " + Text;
            return false;
        }

        if (Value < 1)
        {
            ErrorMessage = "Index must be at least 1 after " + Args[Index - 1] + ".";
            return false;
        }

        return true;
    }

    private static bool IsIndexFlag(string Arg)
    {
        return string.Equals(Arg, "-index", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Arg, "-n", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHelpFlag(string Arg)
    {
        return string.Equals(Arg, "-h", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Arg, "-help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Arg, "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Arg, "/?", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInputFlag(string Arg)
    {
        return string.Equals(Arg, "-in", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Arg, "-i", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOutputFlag(string Arg)
    {
        return string.Equals(Arg, "-out", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Arg, "-o", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDocxFlag(string Arg)
    {
        return string.Equals(Arg, "-docx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Arg, "-d", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPngFlag(string Arg)
    {
        return string.Equals(Arg, "-png", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Arg, "-p", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSvgFlag(string Arg)
    {
        return string.Equals(Arg, "-svg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Arg, "-s", StringComparison.OrdinalIgnoreCase);
    }
}
