namespace Converter;

internal sealed class ConsoleConversionProgressReporter : MDConverter.IConversionProgressReporter
{
    public void Report(int PercentComplete, string Message)
    {
        int ClampedPercent = PercentComplete;
        if (ClampedPercent < 0)
        {
            ClampedPercent = 0;
        }

        if (ClampedPercent > 100)
        {
            ClampedPercent = 100;
        }

        Console.Out.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"[{ClampedPercent,3}%] {Message}"));
    }
}
