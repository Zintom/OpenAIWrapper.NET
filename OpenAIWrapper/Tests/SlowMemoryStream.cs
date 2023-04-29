using System;
using System.IO;
using System.Threading;

namespace Tests;
internal class SlowMemoryStream : MemoryStream
{
    private readonly int _maxRead;

    private readonly int _readLatency;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="buffer"></param>
    /// <param name="maxRead">The maximum number of bytes able to be read in one call to <see cref="Read(Span{byte})"/></param>
    /// <param name="readLatency">The latency between <see cref="Read(Span{byte})"/> being called and the caller getting a response.</param>
    public SlowMemoryStream(byte[] buffer, int maxRead = 16, int readLatency = 32) : base(buffer)
    {
        _maxRead = maxRead;
        _readLatency = readLatency;
    }

    public override int Read(Span<byte> buffer)
    {
        long toRead = _maxRead;
        if (base.Position + toRead > base.Length)
            toRead = base.Length - base.Position;

        Thread.Sleep(_readLatency);

        return base.Read(buffer[..(int)toRead]);
    }

}
