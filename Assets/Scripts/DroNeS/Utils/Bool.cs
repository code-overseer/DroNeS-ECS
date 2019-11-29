namespace DroNeS.Utils
{
    public struct Bool
    {
        public byte Value;

        public Bool(bool val)
        {
            Value = System.Convert.ToByte(val);
        }

        public static implicit operator bool(Bool val)
        {
            return val.Value != 0;
        }

        public static implicit operator Bool(bool val)
        {
            return new Bool(val);
        } 
    }
}
