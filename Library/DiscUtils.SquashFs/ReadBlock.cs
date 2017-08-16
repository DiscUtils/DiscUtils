using DiscUtils.Streams;

namespace DiscUtils.SquashFs
{
    internal delegate Block ReadBlock(long pos, int diskLen);
}