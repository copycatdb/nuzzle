namespace CopycatDb.Nuzzle;

/// <summary>
/// Exception thrown by the Nuzzle SQL Server driver.
/// </summary>
public class NuzzleException : Exception
{
    public NuzzleException(string message) : base(message) { }
    public NuzzleException(string message, Exception inner) : base(message, inner) { }

    internal static NuzzleException FromNative()
    {
        var msg = NativeBindings.GetLastError() ?? "Unknown native error";
        return new NuzzleException(msg);
    }
}
