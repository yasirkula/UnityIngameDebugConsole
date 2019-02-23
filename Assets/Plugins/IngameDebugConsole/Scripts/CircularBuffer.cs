using UnityEngine;

namespace IngameDebugConsole
{
	public class CircularBuffer<T>
	{
		private T[] arr;
		private int index;

		public T this[int index] { get { return arr[( this.index + index ) % arr.Length]; } }
		public int Count { get; private set; }

		public CircularBuffer( int capacity )
		{
			arr = new T[capacity];
		}

		public void Add( T value )
		{
			if( Count < arr.Length )
				arr[Count++] = value;
			else
			{
				arr[index] = value;
				if( ++index >= arr.Length )
					index = 0;
			}
		}
	}
}