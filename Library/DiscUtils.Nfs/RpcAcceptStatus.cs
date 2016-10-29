namespace DiscUtils.Nfs
{
    internal enum RpcAcceptStatus
    {
        Success = 0,
        ProgramUnavailable = 1,
        ProgramVersionMismatch = 2,
        ProcedureUnavailable = 3,
        GarbageArguments = 4
    }
}