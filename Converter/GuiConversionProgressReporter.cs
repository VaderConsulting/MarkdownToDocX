namespace Converter;

internal sealed class GuiConversionProgressReporter : MDConverter.IConversionProgressReporter
{
    private readonly Main _Form;

    public GuiConversionProgressReporter(Main Form)
    {
        _Form = Form;
    }

    public void Report(int PercentComplete, string Message)
    {
        if (_Form.InvokeRequired)
        {
            _Form.Invoke(new Action(() => { Report(PercentComplete, Message); }));
            return;
        }

        _Form.UpdateProgress(PercentComplete, Message);
    }
}
