using System;
using System.Collections.Generic;
using Google.Protobuf;
using UnityEngine;

namespace RebornFrame.Spell.Particles
{
    /// <summary>粒子轨道种类，作者 0、本地编号 1。</summary>
    public static class ParticleSpellTracks
    {
        /// <summary>注册给技能包的轨道种类。</summary>
        public static readonly SpellPresentationTrackId TypeId = SpellPresentationTrackId.Combine(0, 1);

        private static readonly Dictionary<string, GameObject> Prefabs = new();
        private static readonly Dictionary<ulong, Transform> Anchors = new();

        /// <summary>登记单位身上的粒子挂点。</summary>
        /// <param name="stableId">单位稳定 ID。</param>
        /// <param name="anchor">粒子要跟随的变换。脱离时只取开始时的位置。</param>
        public static void BindAnchor(ulong stableId, Transform anchor)
        {
            if (stableId == 0) throw new ArgumentException("稳定 ID 不能为 0。", nameof(stableId));

            if (anchor == null) Anchors.Remove(stableId);
            else Anchors[stableId] = anchor;
        }

        /// <summary>
        ///     预先登记粒子预制体。预备时优先使用这里的对象，不再从 Resources 加载。
        /// </summary>
        /// <param name="key">与轨道参数里的 prefab_key 相同的键。</param>
        /// <param name="prefab">带 ParticleSystem 的预制体。</param>
        public static void RegisterPrefab(string key, GameObject prefab)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("粒子键不能为空。", nameof(key));

            if (prefab == null || prefab.GetComponent<ParticleSystem>() == null)
                throw new ArgumentException("粒子预制体需要带 ParticleSystem。", nameof(prefab));

            Prefabs[key] = prefab;
        }

        /// <summary>
        ///     收下数据包解开后的预制体。没有 ParticleSystem 的预制体不收。
        ///     数据包不会把 AssetBundle 的字节交到这里。
        /// </summary>
        /// <param name="key">预制体键。</param>
        /// <param name="prefab">预制体。</param>
        public static void AcceptPrefab(string key, GameObject prefab)
        {
            if (string.IsNullOrEmpty(key) || prefab == null) return;

            if (prefab.GetComponentInChildren<ParticleSystem>(true) == null) return;

            Prefabs[key] = prefab;
        }

        /// <summary>取出已经收下的粒子预制体。</summary>
        public static bool TryGetPrefab(string key, out GameObject prefab)
        {
            if (string.IsNullOrEmpty(key))
            {
                prefab = null;
                return false;
            }

            return Prefabs.TryGetValue(key, out prefab);
        }

        /// <summary>向技能包注册粒子轨道。重复调用没有效果。</summary>
        public static void EnsureRegistered()
        {
            if (SpellPresentation.IsRegistered(TypeId)) return;

            SpellPresentation.Register(TypeId, new Factory());
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterOnLoad()
        {
            EnsureRegistered();
        }

        private sealed class Factory : ISpellPresentationTrackFactory
        {
            public void Prepare(ReadOnlySpan<byte> parameters)
            {
                var config = ParticleSpellTrackConfig.Parser.ParseFrom(parameters.ToArray());
                if (string.IsNullOrEmpty(config.PrefabKey))
                    throw new ArgumentException("粒子轨道没有预制体键。");

                if (Prefabs.ContainsKey(config.PrefabKey)) return;

                var prefab = Resources.Load<GameObject>(config.PrefabKey);
                if (prefab == null || prefab.GetComponent<ParticleSystem>() == null)
                    throw new ArgumentException($"粒子预制体 {config.PrefabKey} 不存在或没有 ParticleSystem。");

                Prefabs[config.PrefabKey] = prefab;
            }

            public ISpellPresentationTrack Create(ReadOnlySpan<byte> parameters, in SpellPresentationContext context)
            {
                var config = ParticleSpellTrackConfig.Parser.ParseFrom(parameters.ToArray());
                if (!Prefabs.TryGetValue(config.PrefabKey, out var prefab))
                    throw new InvalidOperationException($"粒子预制体 {config.PrefabKey} 还没有预备。");

                Anchors.TryGetValue(context.OwnerStableId, out var anchor);
                var instance = anchor == null
                    ? UnityEngine.Object.Instantiate(prefab)
                    : UnityEngine.Object.Instantiate(prefab, anchor, false);
                instance.SetActive(false);
                return new Track(instance, config.Detach);
            }
        }

        private sealed class Track : ISpellPresentationTrack
        {
            private readonly bool _detach;
            private readonly GameObject _instance;

            public Track(GameObject instance, bool detach)
            {
                _instance = instance;
                _detach = detach;
            }

            public void Play()
            {
                if (_instance == null) return;

                if (_detach) _instance.transform.SetParent(null, true);

                _instance.SetActive(true);
                _instance.GetComponent<ParticleSystem>().Play(true);
            }

            public void Stop(SpellEndReason reason)
            {
                if (_instance == null) return;

                DestroyObject(_instance);
            }

            public void Show(int frame, bool play)
            {
                if (_instance == null) return;

                if (_detach) _instance.transform.SetParent(null, true);

                _instance.SetActive(true);
                var system = _instance.GetComponent<ParticleSystem>();
                var seconds = Math.Max(0, frame) * SpellPresentation.FrameDuration;
                system.Simulate(seconds, true, true, true);
                if (play) system.Play(true);
            }

            private static void DestroyObject(UnityEngine.Object target)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(target);
                else UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
