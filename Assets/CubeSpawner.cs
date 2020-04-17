using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Assets
{
    public class CubeSpawner : MonoBehaviour, ISerializationCallbackReceiver
    {
        public int CubeCount;
        public int Radius;

        private SpawnCubesSystem _system;

        private void Start()
        {
            _system = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<SpawnCubesSystem>();
            UpdateCubes();
        }

        public void OnAfterDeserialize()  { }

        public void OnBeforeSerialize() => UpdateCubes();

        private void UpdateCubes()
        {
            if(_system != null)
            {
                _system.DesiredCubeCount = CubeCount;
                _system.DesiredRadius = Radius;
            }
        }

        public class SpawnCubesSystem : SystemBase
        {
            private Entity _prefab;
            private EntityQuery _prefabQuery;
            private EntityQuery _cubesQuery;
            private NativeArray<Entity> _entities;
            private int _currentCubeCount;
            private int _currentRadius;

            internal int DesiredCubeCount;
            internal int DesiredRadius;

            protected override void OnCreate()
            {
                _prefabQuery = EntityManager.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[] {
                     ComponentType.ReadOnly<Prefab>(),
                     ComponentType.ReadOnly<CubeTag>(),
                 },
                    Options = EntityQueryOptions.IncludePrefab
                });

                _cubesQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<CubeTag>());
            }

            protected override void OnUpdate()
            {
                if (_prefab == Entity.Null)
                {
                    if (_prefabQuery.IsEmptyIgnoreFilter)
                        return;

                    _prefab = _prefabQuery.GetSingletonEntity();
                }

                if (_currentCubeCount != DesiredCubeCount || _currentRadius != DesiredRadius)
                {
                    SpawnCubes();
                }
            }

            private void SpawnCubes()
            {
                EntityManager.DestroyEntity(_cubesQuery);

                if (_entities.IsCreated)
                    _entities.Dispose();

                _entities = new NativeArray<Entity>(DesiredCubeCount, Allocator.Persistent);
                EntityManager.Instantiate(_prefab, _entities);
                for (int i = 0; i < _entities.Length; i++)
                {
                    EntityManager.SetComponentData(_entities[i], new Translation
                    {
                        Value = UnityEngine.Random.insideUnitSphere * DesiredRadius * 0.01f
                    });

                }
                _currentCubeCount = DesiredCubeCount;
                _currentRadius = DesiredRadius;
            }

            protected override void OnDestroy()
            {
                _entities.Dispose();
            }
        }
    }


}
