using System;

namespace ClinicFlow.Ois
{
    public interface IOisEntity
    {
        string Id { get; }
        int Version { get; }
    }
}
