namespace DiscUtils.Nfs
{
    internal enum RpcAuthenticationStatus
    {
        None = 0,
        BadCredentials = 1,
        RejectedCredentials = 2,
        BadVerifier = 3,
        RejectedVerifier = 4,
        TooWeak = 5
    }
}