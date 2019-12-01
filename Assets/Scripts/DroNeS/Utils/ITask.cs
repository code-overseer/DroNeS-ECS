using System.Runtime.InteropServices;

namespace DroNeS.Utils
{
    public interface ITask
    {
        GCHandle Handle { get; }
        void Execute();
    }


}
