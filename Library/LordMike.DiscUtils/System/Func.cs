
#if NET20
namespace System
{
    internal delegate TResult Func<T, TResult>(T arg);
}
#endif