using DiscUtils.Internal;

namespace DiscUtils.SquashFs
{
    internal delegate Block ReadBlock(long pos, int diskLen);
}