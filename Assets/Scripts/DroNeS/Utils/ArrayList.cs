using System;

namespace DroNeS.Utils
{
    public class ArrayList<T>
    {
        private T[] _array;
        private int _index;

        public ArrayList(int length)
        {
            _array = new T[length];
            _index = 0;
        }

        public ArrayList(ref T[] array, int length)
        {
            if (array != null) throw new ArgumentException("Input array must be null");
            _array = array;
            _array = new T[length];
            _index = 0;
            array = null;
        }

        public int Length => _array.Length;

        public T this[int index]
        {
            get
            {
                if (index >= _index) 
                    throw new IndexOutOfRangeException("Cannot access elements that are not added");
                return _array[index];
            }
        }

        public void Add(T item)
        {
            _array[_index++] = item;
        }

        public T[] GetArray()
        {
            var output = _array;
            _array = null;
            return output;
        }
        
    }
}
