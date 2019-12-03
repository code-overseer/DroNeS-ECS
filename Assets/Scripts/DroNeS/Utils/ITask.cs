using System.Runtime.InteropServices;

namespace DroNeS.Utils
{
    public interface ITask
    {
        GCHandle TaskHandle { get; }
        void Execute();
    }


}
