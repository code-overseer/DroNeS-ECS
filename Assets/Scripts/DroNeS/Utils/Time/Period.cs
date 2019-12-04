using UnityEngine;

namespace DroNeS.Utils.Time
{
    /// <summary>
    ///   <para>Specifies routine period in seconds.</para>
    /// </summary>
    public struct Period
    {
        public Period(float val) => Value = val;
        public float Value;
    }
}
