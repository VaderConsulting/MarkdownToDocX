namespace MDConverter;

/// <summary>
/// Receives percentage-complete updates during Markdown or diagram conversion.
/// </summary>
public interface IConversionProgressReporter
{
    /// <summary>
    /// Reports progress. <paramref name="PercentComplete"/> is 0–100.
    /// </summary>
    void Report(int PercentComplete, string Message);
}
