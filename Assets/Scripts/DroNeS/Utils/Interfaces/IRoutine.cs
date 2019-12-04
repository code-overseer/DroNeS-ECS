using System;
using System.Collections;
using DroNeS.Utils.Time;

namespace DroNeS.Utils.Interfaces
{
    public interface IRoutine : IEnumerator, IDisposable
    {
        Period Period { get; }
        CustomTimer Timer { get; }
    }
}
