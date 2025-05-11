using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sled.Core
{
    public interface ISledCodec {
        ReadOnlyMemory<byte> Encode(ReadOnlySpan<byte> payload);
        bool TryDecode(ref ReadOnlySequence<byte> input , out ReadOnlyMemory<byte> payload);
    }
}
