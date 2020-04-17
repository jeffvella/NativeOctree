namespace NativeOctree
{
	public struct OctNode
	{
		// Points to this node's first child index in elements
		public int firstChildIndex;

		// Number of elements in the leaf
		public short count;
		public bool isLeaf;
	}


}
