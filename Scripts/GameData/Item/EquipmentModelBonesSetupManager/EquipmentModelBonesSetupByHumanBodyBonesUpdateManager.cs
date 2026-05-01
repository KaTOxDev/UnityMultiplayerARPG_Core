using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace MultiplayerARPG
{
    [DefaultExecutionOrder(int.MaxValue)]
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
            return gameObject.AddComponent<EquipmentModelBonesSetupByHumanBodyBonesUpdateManager>();
        }

        [Tooltip("0 = not null, 1 = null")]
        public byte copyScale = 0;
        private readonly Dictionary<int, Transform[]> _allSrc = new Dictionary<int, Transform[]>();
        private readonly Dictionary<int, NativeArray<TransformData>> _allNSrc = new Dictionary<int, NativeArray<TransformData>>();
        private readonly Dictionary<int, DstData> _allDst = new Dictionary<int, DstData>();
        private readonly HashSet<int> _destroyedSrcIds = new HashSet<int>();
        private readonly HashSet<int> _destroyedDstIds = new HashSet<int>();
        private JobHandle _jobHandle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void ResetInstance()
        {
            _instance = null;
        }

        private void Awake()
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            _jobHandle.Complete();

            foreach (var kv in _allNSrc)
            {
                if (kv.Value.IsCreated)
                    kv.Value.Dispose();
            }

            foreach (var kv in _allDst)
            {
                if (kv.Value.dstArray.isCreated)
                    kv.Value.dstArray.Dispose();
            }

            _allNSrc.Clear();
            _allSrc.Clear();
            _allDst.Clear();
        }

        public void Register(AnimatorHandle srcHandle, Transform[] src, AnimatorHandle dstHandle, Transform[] dst)
        {
            if (srcHandle == null)
                return;
            if (dstHandle == null)
                return;
            srcHandle.OnDestroyed -= OnSrcAnimatorHandleDestroyed;
            srcHandle.OnDestroyed += OnSrcAnimatorHandleDestroyed;
            dstHandle.OnDestroyed -= OnDstAnimatorHandleDestroyed;
            dstHandle.OnDestroyed += OnDstAnimatorHandleDestroyed;
            _allSrc[srcHandle.Id] = src;
            if (!_allNSrc.ContainsKey(srcHandle.Id))
            {
                NativeArray<TransformData> srcNList = new NativeArray<TransformData>(src.Length, Allocator.Persistent);
                _allNSrc[srcHandle.Id] = srcNList;
            }
            if (!_allDst.ContainsKey(dstHandle.Id))
            {
                TransformAccessArray dstTransforms = new TransformAccessArray(dst);
                _allDst[dstHandle.Id] = new DstData()
                {
                    dstArray = dstTransforms,
                    srcId = srcHandle.Id,
                };
            }
        }

        private void OnSrcAnimatorHandleDestroyed(AnimatorHandle handle)
        {
            _destroyedSrcIds.Add(handle.Id);
        }

        private void OnDstAnimatorHandleDestroyed(AnimatorHandle handle)
        {
            _destroyedDstIds.Add(handle.Id);
        }

        private void LateUpdate()
        {
            // Ensure previous job is done
            _jobHandle.Complete();

            // Remove destroyed sources
            foreach (int id in _destroyedSrcIds)
            {
                if (_allNSrc.TryGetValue(id, out NativeArray<TransformData> srcNList))
                {
                    if (srcNList.IsCreated)
                        srcNList.Dispose();
                    _allNSrc.Remove(id);
                }
                ;
                _allSrc.Remove(id);
            }
            _destroyedSrcIds.Clear();

            // Remove destroyed destinations
            foreach (int id in _destroyedDstIds)
            {
                if (_allDst.TryGetValue(id, out DstData dstData))
                {
                    if (dstData.dstArray.isCreated)
                        dstData.dstArray.Dispose();
                    _allDst.Remove(id);
                }
            }
            _destroyedDstIds.Clear();

            foreach (KeyValuePair<int, Transform[]> srcKvp in _allSrc)
            {
                int srcId = srcKvp.Key;
                if (!_allNSrc.TryGetValue(srcId, out NativeArray<TransformData> srcNList))
                    continue;
                Transform[] srcTransforms = srcKvp.Value;
                for (int i = 0; i < srcTransforms.Length; ++i)
                {
                    Transform srcTransform = srcTransforms[i];
                    TransformData transformData = default;
                    if (srcTransform != null)
                    {
                        transformData.isNull = 0;
                        transformData.position = srcTransform.position;
                        transformData.rotation = srcTransform.rotation;
                        transformData.localScale = srcTransform.localScale;
                    }
                    else
                    {
                        transformData.isNull = 1;
                    }
                    srcNList[i] = transformData;
                }
            }
            JobHandle combinedHandle = default;
            bool hasJob = false;
            foreach (KeyValuePair<int, DstData> dstKvp in _allDst)
            {
                if (!_allNSrc.TryGetValue(dstKvp.Value.srcId, out NativeArray<TransformData> srcNList))
                    continue;
                // Schedule ONE big job
                CopyTransformsJob job = new CopyTransformsJob
                {
                    sourceTransforms = srcNList,
                    copyScale = copyScale,
                };
                JobHandle handle = job.Schedule(dstKvp.Value.dstArray);
                combinedHandle = hasJob
                    ? JobHandle.CombineDependencies(combinedHandle, handle)
                    : handle;
                hasJob = true;
            }
            _jobHandle = combinedHandle;
        }

        [BurstCompile]
        private struct CopyTransformsJob : IJobParallelForTransform
        {
            [ReadOnly]
            public NativeArray<TransformData> sourceTransforms;
            [ReadOnly]
            public byte copyScale;

            public void Execute(int index, TransformAccess destination)
            {
                if (!destination.isValid)
                    return;

                if (index >= sourceTransforms.Length)
                    return;

                TransformData source = sourceTransforms[index];
                if (source.isNull == 1)
                    return;

                destination.position = source.position;
                destination.rotation = source.rotation;
                if (copyScale == 1)
                    destination.localScale = source.localScale;
            }
        }

        [BurstCompile]
        private struct TransformData
        {
            public float3 position;
            public quaternion rotation;
            public float3 localScale;
            /// <summary>
            /// 0 = not null, 1 = null
            /// </summary>
            public byte isNull;
        }

        [BurstCompile]
        private struct DstData
        {
            public int srcId;
            public TransformAccessArray dstArray;
        }
    }
}