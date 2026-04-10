using System.Collections.Generic;
using Unity.Burst;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

namespace MultiplayerARPG
{
    public class EquipmentModelBonesSetupByHumanBodyBonesUpdateManager : MonoBehaviour
    {
        private static EquipmentModelBonesSetupByHumanBodyBonesUpdateManager _instance;
        public static EquipmentModelBonesSetupByHumanBodyBonesUpdateManager Instance => _instance != null ? _instance : (_instance = CreateInstance());

        private static EquipmentModelBonesSetupByHumanBodyBonesUpdateManager CreateInstance()
        {
            var gameObject = new GameObject(nameof(EquipmentModelBonesSetupByHumanBodyBonesUpdateManager))
            {
                hideFlags = HideFlags.DontSave,
            };
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                gameObject.hideFlags = HideFlags.HideAndDontSave;
            }
            else
#endif
            {
                DontDestroyOnLoad(gameObject);
            }
            return gameObject.AddComponent<EquipmentModelBonesSetupByHumanBodyBonesUpdateManager>();
        }

        private readonly List<Transform> _allSrc = new List<Transform>(4096);
        private Transform[] _cachedSrcArray = new Transform[1024];
        private readonly List<Transform> _allDst = new List<Transform>(4096);
        private Transform[] _cachedDstArray = new Transform[1024];

        private TransformAccessArray _srcArray;
        private TransformAccessArray _dstArray;

        private JobHandle _jobHandle;

        public void Register(TransformAccessArray src, TransformAccessArray dst)
        {
            int length = src.length;

            for (int i = 0; i < length; i++)
            {
                _allSrc.Add(src[i].transform);
                _allDst.Add(dst[i].transform);
            }
        }

        private void LateUpdate()
        {
            // Ensure previous job is done
            _jobHandle.Complete();

            if (_allSrc.Count == 0)
                return;

            // Dispose previous arrays
            if (_srcArray.isCreated)
                _srcArray.Dispose();

            if (_dstArray.isCreated)
                _dstArray.Dispose();

            // Create new arrays (batched)
            // Make it realloc only when the size is bigger than before, otherwise just copy to cached arrays to avoid GC alloc
            if (_cachedSrcArray.Length < _allSrc.Count)
                _cachedSrcArray = new Transform[_allSrc.Count];
            _allSrc.CopyTo(_cachedSrcArray);
            if (_cachedDstArray.Length < _allDst.Count)
                _cachedDstArray = new Transform[_allDst.Count];
            _allDst.CopyTo(_cachedDstArray);

            _srcArray = new TransformAccessArray(_cachedSrcArray);
            _dstArray = new TransformAccessArray(_cachedDstArray);

            // Schedule ONE big job
            CopyTransformsJob job = new CopyTransformsJob
            {
                sourceTransforms = _srcArray
            };

            _jobHandle = job.Schedule(_dstArray);

            // Clear for next frame
            _allSrc.Clear();
            _allDst.Clear();
        }

        private void OnDestroy()
        {
            _jobHandle.Complete();

            if (_srcArray.isCreated)
                _srcArray.Dispose();

            if (_dstArray.isCreated)
                _dstArray.Dispose();
        }

        [BurstCompile]
        private struct CopyTransformsJob : IJobParallelForTransform
        {
            public TransformAccessArray sourceTransforms;

            public void Execute(int index, TransformAccess destination)
            {
                if (!destination.isValid)
                    return;

                TransformHandle source = sourceTransforms.GetTransformHandle(index);

                destination.position = source.position;
                destination.rotation = source.rotation;
                destination.localScale = source.localScale;
            }
        }
    }
}