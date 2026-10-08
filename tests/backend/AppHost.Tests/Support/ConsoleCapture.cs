using System.Text;

namespace Apogeu.AppHost.Tests.Support;

/// <summary>
/// Redirects the process console (stdout and stderr) to an in-memory buffer. The console logger binds to the
/// console writers when its provider is created, so the capture must start before the Host is built. Tests using it
/// run in the non-parallel <see cref="ConsoleOutputGroup"/>.
/// </summary>
internal sealed class ConsoleCapture : IDisposable
{
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalError;
    private readonly StringWriter _buffer = new(new StringBuilder(), System.Globalization.CultureInfo.InvariantCulture);
    private readonly TextWriter _synchronized;

    public ConsoleCapture()
    {
        _originalOut = Console.Out;
        _originalError = Console.Error;
        _synchronized = TextWriter.Synchronized(_buffer);
        Console.SetOut(_synchronized);
        Console.SetError(_synchronized);
    }

    public string Text
    {
        get
        {
            _synchronized.Flush();
            lock (_synchronized)
            {
                return _buffer.ToString();
            }
        }
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
        _synchronized.Dispose();
        _buffer.Dispose();
    }
}
