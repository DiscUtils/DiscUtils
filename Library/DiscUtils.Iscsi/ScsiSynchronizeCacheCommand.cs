using System;

namespace DiscUtils.Iscsi;

/// <summary>
/// SYNCHRONIZE CACHE (10): asks the target to put every write it has acknowledged on stable storage.
/// </summary>
/// <remarks>
/// LBA 0 with 0 blocks covers the whole medium, also beyond what a 32-bit LBA can address. IMMED is clear, so the
/// target only answers once the data is stored.
/// </remarks>
internal class ScsiSynchronizeCache10Command : ScsiCommand
{
    public ScsiSynchronizeCache10Command(ulong targetLun)
        : base(targetLun) { }

    public override int Size => 10;

    public override TaskAttributes TaskAttributes => TaskAttributes.Simple;

    public override int ReadFrom(ReadOnlySpan<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public override void WriteTo(Span<byte> buffer)
    {
        buffer[..10].Clear();
        buffer[0] = (byte)ScsiOpCode.SynchronizeCache10;
    }
}
