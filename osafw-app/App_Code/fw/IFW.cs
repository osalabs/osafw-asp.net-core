using System;

namespace osafw;

public interface IFW : IDisposable
{
    T model<T>() where T : new();
}
