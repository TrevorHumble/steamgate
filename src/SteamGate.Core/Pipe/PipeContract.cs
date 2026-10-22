namespace SteamGate.Core.Pipe;

/// <summary>What the pipe server and every pipe client must agree on.</summary>
public static class PipeContract
{
    /// <summary>The pipe's name, as the server creates it.</summary>
    public const string PipeName = "SteamGate";

    /// <summary>The pipe's full path, as a client opens it.</summary>
    public const string PipePath = @"\\.\pipe\" + PipeName;

    /// <summary>
    /// The access a client asks for and the pipe grants a standard user: read data, write data, read attributes, read extended
    /// attributes, read permissions, synchronize. Never generic read or write, which the pipe refuses a standard user, and never
    /// the right to create an instance. The bits are the same in the pipe access rights the server grants.
    /// </summary>
    public const uint ClientAccessMask = FileReadData | FileWriteData | FileReadEa | FileReadAttributes | ReadControl | Synchronize;

    private const uint FileReadData = 0x0001;
    private const uint FileWriteData = 0x0002;
    private const uint FileReadEa = 0x0008;
    private const uint FileReadAttributes = 0x0080;
    private const uint ReadControl = 0x00020000;
    private const uint Synchronize = 0x00100000;
}
