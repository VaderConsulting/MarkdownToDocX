namespace Converter;

internal sealed class GuiConversionProgressReporter(Main Form) : MDConverter.IConversionProgressReporter
{
    private readonly Main _Form = Form;

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
