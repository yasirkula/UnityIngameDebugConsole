namespace IngameDebugConsole
{
	public class DebugLogIndexList
	{
		private int[] indices;
		private int size;

		public int Count { get { return size; } }
		public int this[int index] { get { return indices[index]; } }

		public DebugLogIndexList()
		{
			indices = new int[64];
			size = 0;
		}

		public void Add( int index )
		{
			if( size == indices.Length )
			{
				int[] indicesNew = new int[size * 2];
				System.Array.Copy( indices, 0, indicesNew, 0, size );
				indices = indicesNew;
			}

			indices[size++] = index;
		}

		public void Clear()
		{
			size = 0;
		}
	}
}