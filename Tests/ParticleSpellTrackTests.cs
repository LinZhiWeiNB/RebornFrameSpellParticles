using System.Collections.Generic;
using Google.Protobuf;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace RebornFrame.Spell.Particles.Tests
{
    public class ParticleSpellTrackTests
    {
        private readonly List<GameObject> _objects = new();
        private SpellStartup _startup;
        private World _world;

        [TearDown]
        public void TearDown()
        {
            _startup?.Dispose();
            if (_world is { IsCreated: true }) _world.Dispose();
            SpellPresentation.Reset();
            for (var index = 0; index < _objects.Count; index++)
                if (_objects[index] != null)
                    Object.DestroyImmediate(_objects[index]);
        }

        [Test]
        public void Play_StartsPreparedPrefab()
        {
            var prefab = new GameObject("ember");
            prefab.AddComponent<ParticleSystem>();
            _objects.Add(prefab);
            ParticleSpellTracks.RegisterPrefab("ember", prefab);
            ParticleSpellTracks.EnsureRegistered();
            SpellPresentation.SetFrameDuration(0.1f);

            _world = new World(nameof(Play_StartsPreparedPrefab));
            _startup = new SpellStartup();
            var spell = SpellId.Combine(1, 1);
            var data = new SpellModuleData
            {
                Spells =
                {
                    new SpellData
                    {
                        Id = spell.Value,
                        Activation = (int)SpellActivation.Active,
                        DurationFrames = 4,
                        PresentationTracks =
                        {
                            new SpellPresentationTrack
                            {
                                TypeId = ParticleSpellTracks.TypeId.Value,
                                Parameters = new ParticleSpellTrackConfig { PrefabKey = "ember" }.ToByteString()
                            }
                        }
                    }
                }
            };
            _startup.AddModuleData(data);
            _startup.Start(_world);
            var unit = _world.EntityManager.CreateEntity();
            _startup.InitializeUnit(unit, 1);
            Assert.IsTrue(SpellAccess.TryBeginCast(_world.EntityManager, spell, 1, 0));
            _startup.Tick(0);

            var systems = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var playing = 0;
            for (var index = 0; index < systems.Length; index++)
            {
                if (!systems[index].gameObject.name.StartsWith("ember")) continue;

                _objects.Add(systems[index].gameObject);
                if (systems[index].isPlaying) playing++;
            }

            Assert.AreEqual(1, playing);
        }
    }
}
