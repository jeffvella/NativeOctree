using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace NativeOctree
{
	public struct Octobool
	{
		public bool4 A;
		public bool4 B;
	}

	[DebuggerDisplay("UnsafeOctree: (DebugInfo)")]
	public unsafe struct UnsafeOctree
	{
		[NativeDisableUnsafePtrRestriction]
		public UnsafeList* Elements;

		[NativeDisableUnsafePtrRestriction]
		public UnsafeList* NodeInfo;

		[NativeDisableUnsafePtrRestriction]
		public UnsafeList* Nodes;

		[NativeDisableUnsafePtrRestriction]
		public UnsafeList* Quads;

		public int MaxDepth;

		public short MaxElementsPerLeaf;

		public Aabb RootBounds; // NOTE: Currently assuming uniform

		public int TotalElements;
		public int TotalBranches;

		/// <summary>
		/// Total Leaf nodes that contain elements
		/// </summary>
		public int TotalLeaves;
		public int TotalDepth;
		public Allocator Allocator;

		public override string ToString() => DebugInfo;

		public string DebugInfo =>
			$"Elements={TotalElements} Branches={TotalBranches} " +
			$"Leaves={TotalLeaves} Depth={TotalDepth} Allocator={Allocator}";
	}

	public struct NodeInfo
	{
		public int ElementCount;
	}

	/// <summary>
	/// An Octree aimed to be used with Burst, supports fast bulk insertion and querying.
	///
	/// TODO:
	/// - Better test coverage
	/// - Automated depth / bounds / max leaf elements calculation
	/// </summary>
	public unsafe partial struct NativeOctree<T> : IDisposable where T : unmanaged
	{
		[NativeDisableUnsafePtrRestriction]
		public UnsafeOctree* Data;
		public bool IsCreated => Data != null;

		public NativeOctree(AABB bounds, Allocator allocator = Allocator.Temp, int maxDepth = 5, short maxLeafElements = 16, int initialElementsCapacity = 256) 
			: this(new Aabb { Min = bounds.Min, Max = bounds.Max }, allocator, maxDepth, maxLeafElements, initialElementsCapacity) { }

		/// <summary>
		/// Create a new Octree.
		/// - Ensure the bounds are not way bigger than needed, otherwise the buckets are very off. Probably best to calculate bounds
		/// - The higher the depth, the larger the overhead, it especially goes up at a depth of 7/8
		/// </summary>
		public NativeOctree(Aabb bounds, Allocator allocator = Allocator.Temp, int maxDepth = 5, short maxLeafElements = 16, int initialElementsCapacity = 256) //: this()
		{
			Data = (UnsafeOctree*)UnsafeUtility.Malloc(sizeof(UnsafeOctree), UnsafeUtility.AlignOf<UnsafeOctree>(), allocator);
			UnsafeUtility.MemClear(Data, sizeof(UnsafeOctree));

			Data->RootBounds = bounds;
			Data->MaxDepth = maxDepth;
			Data->MaxElementsPerLeaf = maxLeafElements;
			Data->Allocator = allocator;

			if(maxDepth > 8)
			{
				// Currently no support for higher depths, the morton code lookup tables would have to support it
				throw new InvalidOperationException();
			}

			var totalSize = LookupTables.DepthSizeLookup[maxDepth+1];

			Data->NodeInfo = UnsafeList.Create(UnsafeUtility.SizeOf<int>(),
				UnsafeUtility.AlignOf<int>(),
				totalSize,
				allocator,
				NativeArrayOptions.ClearMemory);

			Data->Nodes = UnsafeList.Create(UnsafeUtility.SizeOf<OctNode>(),
				UnsafeUtility.AlignOf<OctNode>(),
				totalSize,
				allocator,
				NativeArrayOptions.ClearMemory);

			Data->Elements = UnsafeList.Create(UnsafeUtility.SizeOf<OctElement<T>>(),
				UnsafeUtility.AlignOf<OctElement<T>>(),
				initialElementsCapacity,
				allocator);

			Data->Quads = UnsafeList.Create(UnsafeUtility.SizeOf<QuadGroup>(),
				UnsafeUtility.AlignOf<QuadGroup>(),
				totalSize, // todo, allocate after counting used branches.
				allocator);
		}

		public void ClearAndBulkInsert(NativeArray<OctElement<T>> incomingElements)
		{
			// Always have to clear before bulk insert as otherwise the lookup and node 
			// allocations need to account for existing data.
			Clear();

			if(Data->Elements->Capacity < Data->TotalElements + incomingElements.Length)
			{
				Data->Elements->Resize<OctElement<T>>(math.max(incomingElements.Length, Data->Elements->Capacity*2));
			}

			// Aabb extents are double that of AABB
			var extents = Data->RootBounds.Extents / 2;

			// Prepare morton codes
			var mortonCodes = new NativeArray<int>(incomingElements.Length, Allocator.Temp);
			var depthExtentsScaling = LookupTables.DepthLookup[Data->MaxDepth] / extents;
			for (var i = 0; i < incomingElements.Length; i++)
			{
				var incPos = incomingElements[i].pos;
				incPos -= Data->RootBounds.Center; // Offset by center
				incPos.y = -incPos.y; // World -> array
				var pos = (incPos + extents) * .5f; // Make positive
				// Now scale into available space that belongs to the depth
				pos *= depthExtentsScaling;
				// And interleave the bits for the morton code
				mortonCodes[i] =(int) (LookupTables.MortonLookup[(int) pos.x] | (LookupTables.MortonLookup[(int) pos.y] << 1) | (LookupTables.MortonLookup[(int) pos.z] << 2));
			}

			// Index total child element count per node (total, so parent's counts include those of child nodes)
			
			for (var i = 0; i < mortonCodes.Length; i++)
			{
				int atIndex = 0;
				for (int depth = 0; depth <= Data->MaxDepth; depth++)
				{
					// Increment the node on this depth that this element is contained in
					UnsafeUtilityEx.ArrayElementAsRef<int>(Data->NodeInfo->Ptr, atIndex)++;
					atIndex = IncrementIndex(depth, mortonCodes, i, atIndex);
				}
			}

			// Prepare the tree leaf nodes
			var q1Ptr = (QuadGroup*)((byte*)Data->Quads->Ptr + Data->Quads->Length++ * sizeof(QuadGroup));
			var q2Ptr = (QuadGroup*)((byte*)Data->Quads->Ptr + Data->Quads->Length++ * sizeof(QuadGroup));
			RecursivePrepareLeaves(Data->RootBounds, 1, 1, q1Ptr, q2Ptr);

			// Add elements to leaf nodes
			for (var i = 0; i < incomingElements.Length; i++)
			{
				int atIndex = 0;
				for (int depth = 0; depth <= Data->MaxDepth; depth++)
				{
					ref var node = ref UnsafeUtilityEx.ArrayElementAsRef<OctNode>(Data->Nodes->Ptr, atIndex);
					if(node.isLeaf)
					{
						// We found a leaf, add this element to it and move to the next element
						UnsafeUtility.WriteArrayElement(Data->Elements->Ptr, node.firstChildIndex + node.count, incomingElements[i]);
						node.count++;
						break;
					}
					// No leaf found, we keep going deeper until we find one
					atIndex = IncrementIndex(depth, mortonCodes, i, atIndex);
				}
			}

			mortonCodes.Dispose();
		}

		int IncrementIndex(int depth, NativeArray<int> mortonCodes, int i, int atIndex)
		{
			var atDepth = math.max(0, Data->MaxDepth - depth);
			// Shift to the right and only get the first three bits
			int shiftedMortonCode = (mortonCodes[i] >> ((atDepth - 1) * 3)) & 0b111;
			// so the index becomes that... (0,1,2,3)
			atIndex += LookupTables.DepthSizeLookup[atDepth] * shiftedMortonCode;
			atIndex++; // offset for self
			return atIndex;
		}

		void RecursivePrepareLeaves(Aabb parentBounds, int prevOffset, int depth, QuadGroup* parentQuadA, QuadGroup* parentQuadB)
		{
			var depthSize = LookupTables.DepthSizeLookup[Data->MaxDepth - depth + 1];

			if (depth > Data->TotalDepth)
				Data->TotalDepth = depth;

			var octoBounds = Aabb4x2.CreateOctreeBounds(parentBounds);
			parentQuadA->Bounds = octoBounds.A;
			parentQuadB->Bounds = octoBounds.B;

			ProcessGroup(parentQuadA, prevOffset, depth, depthSize, 0);
			ProcessGroup(parentQuadB, prevOffset, depth, depthSize, 4);
		}

		private void ProcessGroup(QuadGroup* group, int prevOffset, int depth, int depthSize, int offset)
		{
			var notMaxDepth = depth < Data->MaxDepth;

			for (int j = 0; j < 4; j++)
			{
				var i = j + offset; // index of 8
				var at = prevOffset + i * depthSize;

				group->Offsets[j] = at;

				var elementCount = UnsafeUtility.ReadArrayElement<int>(Data->NodeInfo->Ptr, at);

				if (elementCount > Data->MaxElementsPerLeaf && notMaxDepth)
				{
					Data->TotalBranches++;

					var q1Ptr = (QuadGroup*)((byte*)Data->Quads->Ptr + Data->Quads->Length++ * sizeof(QuadGroup));
					var q2Ptr = (QuadGroup*)((byte*)Data->Quads->Ptr + Data->Quads->Length++ * sizeof(QuadGroup));

					RecursivePrepareLeaves(group->Bounds[j], at + 1, depth + 1, q1Ptr, q2Ptr);
				}
				else if (elementCount != 0)
				{
					// We either hit max depth or there's less than the max elements on this node, make it a leaf
					var node = new OctNode
					{
						firstChildIndex = Data->TotalElements,
						count = 0,
						isLeaf = true
					};

					UnsafeUtility.WriteArrayElement(Data->Nodes->Ptr, at, node);

					Data->TotalElements += elementCount;
					Data->TotalLeaves++;
				}
			}
		}

		public void Clear()
		{
			UnsafeUtility.MemClear(Data->NodeInfo->Ptr, Data->NodeInfo->Capacity * UnsafeUtility.SizeOf<int>());
			UnsafeUtility.MemClear(Data->Nodes->Ptr, Data->Nodes->Capacity * UnsafeUtility.SizeOf<OctNode>());
			UnsafeUtility.MemClear(Data->Elements->Ptr, Data->Elements->Capacity * UnsafeUtility.SizeOf<OctElement<T>>());
			UnsafeUtility.MemClear(Data->Quads->Ptr, Data->Quads->Capacity * UnsafeUtility.SizeOf<QuadGroup>());

			Data->TotalElements = 0;
			Data->TotalDepth = 0;
			Data->TotalLeaves = 0;
			Data->TotalBranches = 0;
		}

		public void Dispose()
		{
			if (!IsCreated)
				return;

			UnsafeList.Destroy(Data->Elements);
			UnsafeList.Destroy(Data->NodeInfo);
			UnsafeList.Destroy(Data->Nodes);
			UnsafeList.Destroy(Data->Quads);
			UnsafeUtility.Free(Data, Data->Allocator);
		}
	}
}
