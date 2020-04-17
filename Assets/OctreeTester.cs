using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Assets;
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using NativeOctree;
using Unity.Mathematics;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using Unity.Rendering;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Assets
{
    public class OctreeTester : MonoBehaviour, ISerializationCallbackReceiver
    {
        public BoxCollider QueryBounds;
        public Material SelectedMaterial;
        public Material DeselectedMaterial;
        public bool AutoQuery;
        public bool DebugLogging;

        private World _world;
        private EntityManager _em;
        private CubeSpawner.SpawnCubesSystem _cubesSystem;
        private EntityQuery _cubesQuery;
        private OctreeSystem _octreeSystem;
        private NativeOctree_Original<Entity> _tree;
        private bool _isTreeCreated;
        private NativeList<OctElement<Entity>> _queryResult;
        private AABB _bounds;

        void Update()
        {
            if (AutoQuery && _octreeSystem.IsCreated)
            {
                QueryAABB();
            }
        }

        private void Start()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            _em = _world.EntityManager;
            _cubesSystem = _world.GetOrCreateSystem<CubeSpawner.SpawnCubesSystem>();
            _cubesQuery = _em.CreateEntityQuery(
                ComponentType.ReadOnly<CubeTag>(),
                ComponentType.ReadOnly<Translation>()
                );
            _octreeSystem = _world.GetOrCreateSystem<OctreeSystem>();
            _queryResult = new NativeList<OctElement<Entity>>(1, Allocator.Persistent);
        }

        public void BuildOctree()
        {
            var cubes = _cubesQuery.ToComponentDataArray<Translation>(Allocator.TempJob);
            var entities = _cubesQuery.ToEntityArray(Allocator.TempJob);
            var elements = new NativeArray<OctElement<Entity>>(cubes.Length, Allocator.TempJob);
            var bounds = new AABB { Center = 0, Extents = _cubesSystem.DesiredRadius };

            if(_isTreeCreated)
                _tree.Dispose();

            _tree = new NativeOctree_Original<Entity>(bounds, Allocator.Persistent);
            _isTreeCreated = true;

            for (int i = 0; i < cubes.Length; i++)
            {
                elements[i] = new OctElement<Entity>
                {
                    pos = cubes[i].Value,
                    element = entities[i]
                };
            }

            _octreeSystem.Build(_tree, elements);

            cubes.Dispose();
            entities.Dispose();
            elements.Dispose();
        }

        public void QueryAABB()
        {
            BuildOctree();

            for (int i = 0; i < _queryResult.Length; i++)
            {
                var entity = _queryResult[i].element;
                if (_em.Exists(entity))
                {
                    var rm = _em.GetSharedComponentData<RenderMesh>(entity);
                    rm.material = this.DeselectedMaterial;
                    _em.SetSharedComponentData(entity, rm);
                }
            }

            _queryResult.Clear();

            _bounds = new AABB 
            { 
                Center = QueryBounds.transform.position, 
                Extents = QueryBounds.size / 2
            };

            _octreeSystem.RangeQuery(_tree, _bounds, _queryResult);
            for (int i = 0; i < _queryResult.Length; i++)
            {
                var entity = _queryResult[i].element;
                if (_em.Exists(entity))
                {
                    var rm = _em.GetSharedComponentData<RenderMesh>(entity);
                    rm.material = this.SelectedMaterial;
                    _em.SetSharedComponentData(entity, rm);
                }
            }
        }

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize() 
        {
            if(_octreeSystem != null)
            {
                _octreeSystem.DebugLogging = DebugLogging;
            }
        }

        private class OctreeSystem : SystemBase
        {
            public bool IsCreated { get; private set; }

            private Stopwatch _sw1;
            private Stopwatch _sw2;

            public bool DebugLogging { get; set; }

            protected override void OnUpdate() { }

            protected override void OnCreate()
            {
                IsCreated = true;

                _sw1 = Stopwatch.StartNew();
                _sw2 = Stopwatch.StartNew();
            }

            public void Build(NativeOctree_Original<Entity> tree, NativeArray<OctElement<Entity>> elements)
            {
                _sw1.Restart();
                Job.WithCode(() =>
                {
                    tree.ClearAndBulkInsert(elements);

                }).Run();
                _sw1.Stop();
                if (DebugLogging)
                    Debug.Log($"NativeOctree_Original.ClearAndBulkInsert took {_sw1.Elapsed.TotalMilliseconds:N4}ms for {elements.Length} elements");
            }

            public void RangeQuery(NativeOctree_Original<Entity> tree, AABB bounds, NativeList<OctElement<Entity>> results)
            {
                _sw2.Restart();
                Job.WithCode(() =>
                {
                    tree.RangeQuery(bounds, results);

                }).Run();
                _sw2.Stop();
                if (DebugLogging)
                    Debug.Log($"NativeOctree_Original.RangeQuery took {_sw2.Elapsed.TotalMilliseconds:N4}ms to find {results.Length} results");
            }
        }
    }
}

#if UNITY_EDITOR

[CustomEditor(typeof(OctreeTester))]
[CanEditMultipleObjects]
public class PathTester_Editor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        foreach (var targ in targets.Cast<OctreeTester>())
        {
            if (!targ.AutoQuery)
            {
                if (GUILayout.Button("Build"))
                {
                    targ.BuildOctree();
                }
                if (GUILayout.Button("Query"))
                {
                    targ.QueryAABB();
                }
            }
        }
    }
}

#endif