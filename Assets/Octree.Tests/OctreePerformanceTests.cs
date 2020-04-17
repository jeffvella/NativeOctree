using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using NativeOctree;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Unity.Physics;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public unsafe class OctreePerformanceTests : SimpleTestFixture
{

    [Test, Performance]
    public void BuildTree([Values(10, 100, 1000, 25000)] int items, [Values(10, 100, 1000, 25000)] int extents)
    {
        var system = base.m_World.GetOrCreateSystem<OctreeTestSystem>();
        system.DebugLogging = false;

        var systemOriginal = base.m_World.GetOrCreateSystem<OctreeTestSystem2>();
        systemOriginal.DebugLogging = false;

        var measurements = 25;
        var warmups = 5;

        var group = new SampleGroupDefinition
        {
            AggregationType = AggregationType.Average,
            Name = $"BuildTree, Items:{items} Extents:{extents}",
            SampleUnit = SampleUnit.Millisecond
        };

        var groupOriginal = new SampleGroupDefinition
        {
            AggregationType = AggregationType.Average,
            Name = $"BuildTree.Original, Items:{items} Extents:{extents}",
            SampleUnit = SampleUnit.Millisecond
        };

        for (int i = 0; i < measurements; i++)
        {
            if (i < warmups)
            {
                var bounds = new AABB { Center = 0, Extents = extents };
                var tree = new NativeOctree<int>(bounds, Allocator.Temp);
                var treeOriginal = new NativeOctree_Original<int>(bounds, Allocator.Temp);
                var elements = new NativeArray<OctElement<int>>(items, Allocator.Temp);
                system.Build(tree, elements);
                systemOriginal.Build(treeOriginal, elements);
            }
            else
            {
                var bounds = new AABB { Center = 0, Extents = extents };
                var tree = new NativeOctree<int>(bounds, Allocator.Temp);
                var elements = new NativeArray<OctElement<int>>(items, Allocator.Temp);
                for (int j = 0; j < items; j++)
                {
                    elements[j] = new OctElement<int>
                    {
                        pos = new float3(Random.insideUnitSphere * extents),
                        element = j
                    };
                }
       
                system.Build(tree, elements);
        
                Measure.Custom(group, system.BuildStopWatch.Elapsed.TotalMilliseconds);

                bounds = new AABB { Center = 0, Extents = extents };
                var treeOriginal = new NativeOctree_Original<int>(bounds, Allocator.Temp);
                elements = new NativeArray<OctElement<int>>(items, Allocator.Temp);
                for (int j = 0; j < items; j++)
                {
                    elements[j] = new OctElement<int>
                    {
                        pos = new float3(Random.insideUnitSphere * extents),
                        element = j
                    };
                }

                systemOriginal.Build(treeOriginal, elements);

                Measure.Custom(groupOriginal, systemOriginal.BuildStopWatch.Elapsed.TotalMilliseconds);

                Debug.Log($"NativeOctree.ClearAndBulkInsert Original={systemOriginal.BuildStopWatch.Elapsed.TotalMilliseconds:N4}ms Modified={system.BuildStopWatch.Elapsed.TotalMilliseconds:N4}ms");

            }
        }

    }

    public enum RangeQueryTargetAreaPosition
    {
        Centered,
        Random,
    }

    [Test, Performance]
    public void RangeQuery([Values(10, 100, 1000, 10000)] int items, [Values(10, 100,1000, 10000)] int extents, [Values(true, false)] bool resultResize, [Values(0.01f, 0.1f, 0.25f, 0.5f, 1.1f)] float queryBounds, [Values(RangeQueryTargetAreaPosition.Centered, RangeQueryTargetAreaPosition.Random)] RangeQueryTargetAreaPosition targetPosition)
    {
        var system = base.m_World.GetOrCreateSystem<OctreeTestSystem>();
        system.DebugLogging = false;

        var systemOriginal = base.m_World.GetOrCreateSystem<OctreeTestSystem2>();
        systemOriginal.DebugLogging = false;

        var measurements = 15;
        var warmups = 2;

        float3 targetAreaPosition = targetPosition == RangeQueryTargetAreaPosition.Random ?
            new float3(Random.insideUnitSphere * extents) 
            : 0;

        var bounds = new AABB { Center = 0, Extents = extents };
        var queryAABB = new AABB { Center = targetAreaPosition, Extents = bounds.Extents * queryBounds };
        var queryAabb = new Aabb { Min = queryAABB.Min, Max = queryAABB.Max };

        var group = new SampleGroupDefinition
        {
            AggregationType = AggregationType.Average,
            Name = $"RangeQuery, Items:{items} Extents:{extents} QueryAABB:{queryAABB}",
            SampleUnit = SampleUnit.Millisecond
        };

        var groupOriginal = new SampleGroupDefinition
        {
            AggregationType = AggregationType.Average,
            Name = $"RangeQuery.Original, Items:{items} Extents:{extents} QueryAABB:{queryAABB}",
            SampleUnit = SampleUnit.Millisecond
        };

        var elements = new NativeArray<OctElement<int>>(items, Allocator.Temp);
        for (int j = 1; j < items; j++) // element 0 should always be a result.
        {
            elements[j] = new OctElement<int>
            {
                pos = new float3(Random.insideUnitSphere * extents),
                element = j
            };
        }

        for (int i = 0; i < measurements; i++)
        {
            if (i < warmups)
            {
                var tree = new NativeOctree<int>(bounds, Allocator.Temp);
                var results = new NativeList<OctElement<int>>(resultResize ? 1 : items, Allocator.Temp);
                system.Build(tree, elements);
                system.RangeQuery(tree, queryAABB, results);

                var treeOriginal = new NativeOctree_Original<int>(bounds, Allocator.Temp);
                var resultsOriginal = new NativeList<OctElement<int>>(resultResize ? 1 : items * 25, Allocator.Temp);
                systemOriginal.Build(treeOriginal, elements);
                systemOriginal.RangeQuery(treeOriginal, queryAABB, resultsOriginal);
            }
            else
            {
                var tree = new NativeOctree<int>(bounds, Allocator.Temp);
                var results = new NativeList<OctElement<int>>(resultResize ? 1 : items, Allocator.Temp);
                system.Build(tree, elements);
                system.RangeQuery(tree, queryAABB, results);
                Measure.Custom(group, system.QueryStopWatch.Elapsed.TotalMilliseconds);

                var treeOriginal = new NativeOctree_Original<int>(bounds, Allocator.Temp);
                var resultsOriginal = new NativeList<OctElement<int>>(resultResize ? 1 : items * 25, Allocator.Temp);
                systemOriginal.Build(treeOriginal, elements);
                systemOriginal.RangeQuery(treeOriginal, queryAABB, resultsOriginal);
                Measure.Custom(groupOriginal, systemOriginal.QueryStopWatch.Elapsed.TotalMilliseconds);

                if(targetPosition == RangeQueryTargetAreaPosition.Centered)
                    Assert.IsTrue(resultsOriginal.Length > 0);

                Assert.AreEqual(resultsOriginal.Length, results.Length);
                Debug.Log($"NativeOctree.RangeQuery Original={systemOriginal.QueryStopWatch.Elapsed.TotalMilliseconds:N4}ms Modified={system.QueryStopWatch.Elapsed.TotalMilliseconds:N4}ms for {results.Length} results");

                Debug.Log($"TreeData Elements={*tree.Data}");
            }
        }

    }

    [DisableAutoCreation]
    public class OctreeTestSystem : SystemBase
    {
        public Stopwatch BuildStopWatch;
        public Stopwatch QueryStopWatch;

        public bool DebugLogging { get; set; }

        protected override void OnUpdate() { }

        protected override void OnCreate()
        {
            BuildStopWatch = Stopwatch.StartNew();
            QueryStopWatch = Stopwatch.StartNew();
        }

        public void Build(NativeOctree<int> tree, NativeArray<OctElement<int>> elements)
        {
            BuildStopWatch.Restart();

            Job.WithCode(() =>
            {
                tree.ClearAndBulkInsert(elements);

            }).Run();

            BuildStopWatch.Stop();
            if (DebugLogging)
                Debug.Log($"NativeOctree.ClearAndBulkInsert took {BuildStopWatch.Elapsed.TotalMilliseconds:N4}ms for {elements.Length} elements");
        }

        public void RangeQuery(NativeOctree<int> tree, AABB bounds2, NativeList<OctElement<int>> results)
        {
            QueryStopWatch.Restart();

            Job.WithCode(() =>
            {
                tree.RangeQuery(bounds2, results);

            }).Run();

            QueryStopWatch.Stop();
            if (DebugLogging)
                Debug.Log($"NativeOctree.RangeQuery took {QueryStopWatch.Elapsed.TotalMilliseconds:N4}ms to find {results.Length} results");
        }
    }

    [DisableAutoCreation]
    public class OctreeTestSystem2 : SystemBase
    {
        public Stopwatch BuildStopWatch;
        public Stopwatch QueryStopWatch;

        public bool DebugLogging { get; set; }

        protected override void OnUpdate() { }

        protected override void OnCreate()
        {
            BuildStopWatch = Stopwatch.StartNew();
            QueryStopWatch = Stopwatch.StartNew();
        }

        public void Build(NativeOctree_Original<int> tree, NativeArray<OctElement<int>> elements)
        {
            BuildStopWatch.Restart();

            Job.WithCode(() =>
            {
                tree.ClearAndBulkInsert(elements);

            }).Run();

            BuildStopWatch.Stop();
            if (DebugLogging)
                Debug.Log($"NativeOctree_Original.ClearAndBulkInsert took {BuildStopWatch.Elapsed.TotalMilliseconds:N4}ms for {elements.Length} elements");
        }

        public void RangeQuery(NativeOctree_Original<int> tree, AABB bounds, NativeList<OctElement<int>> results)
        {
            QueryStopWatch.Restart();

            Job.WithCode(() =>
            {
                tree.RangeQuery(bounds, results);

            }).Run();

            QueryStopWatch.Stop();
            if (DebugLogging)
                Debug.Log($"NativeOctree_Original.RangeQuery took {QueryStopWatch.Elapsed.TotalMilliseconds:N4}ms to find {results.Length} results");
        }
    }
}

public class SimpleTestFixture
{
    protected World m_PreviousWorld;
    protected World m_World;
    protected EntityManager m_Manager;


    [SetUp]
    virtual public void Setup()
    {
        m_PreviousWorld = World.DefaultGameObjectInjectionWorld;
        m_World = World.DefaultGameObjectInjectionWorld = new World("Test World");
        m_Manager = m_World.EntityManager;
    }

    [TearDown]
    virtual public void TearDown()
    {
        if (m_Manager != null)
        {
            m_World.Dispose();
            m_World = null;

            World.DefaultGameObjectInjectionWorld = m_PreviousWorld;
            m_PreviousWorld = null;
            m_Manager = null;
        }
    }
}