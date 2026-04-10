using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Jobs;

namespace MultiplayerARPG
{
    [DefaultExecutionOrder(int.MaxValue - 1)]
    public class EquipmentModelBonesSetupByHumanBodyBonesUpdater : MonoBehaviour
    {
        [System.Serializable]
        public struct PredefinedBone
        {
            public HumanBodyBones boneType;
            public Transform boneTransform;
        }

        public PredefinedBone[] predefinedBones = new PredefinedBone[0];

        private TransformAccessArray _srcTransforms;
        private TransformAccessArray _dstTransforms;

        private Dictionary<HumanBodyBones, Transform> _predefinedBonesDict;
        public Dictionary<HumanBodyBones, Transform> PredefinedBonesDict
        {
            get
            {
                if (_predefinedBonesDict == null)
                {
                    _predefinedBonesDict = new Dictionary<HumanBodyBones, Transform>();
                    for (int i = 0; i < predefinedBones.Length; ++i)
                    {
                        _predefinedBonesDict.Add(predefinedBones[i].boneType, predefinedBones[i].boneTransform);
                    }
                }
                return _predefinedBonesDict;
            }
        }

        public void PrepareTransforms(Animator src, Animator dst)
        {
#if !UNITY_SERVER
            if (src == null || dst == null)
                return;

            if (_dstTransforms.isCreated)
                _dstTransforms.Dispose();

            if (_srcTransforms.isCreated)
                _srcTransforms.Dispose();

            List<Transform> tempSrc = new List<Transform>();
            List<Transform> tempDst = new List<Transform>();

            for (int i = 0; i < (int)HumanBodyBones.LastBone; ++i)
            {
                Transform srcTransform = src.GetBoneTransform((HumanBodyBones)i);
                if (srcTransform == null)
                    continue;

                Transform dstTransform = null;
                try
                {
                    dstTransform = dst.GetBoneTransform((HumanBodyBones)i);
                }
                catch { }

                if (dstTransform != null ||
                    PredefinedBonesDict.TryGetValue((HumanBodyBones)i, out dstTransform))
                {
                    tempSrc.Add(srcTransform);
                    tempDst.Add(dstTransform);
                }
            }

            _srcTransforms = new TransformAccessArray(tempSrc.ToArray());
            _dstTransforms = new TransformAccessArray(tempDst.ToArray());
#endif
        }

        private void LateUpdate()
        {
#if !UNITY_SERVER
            if (!_srcTransforms.isCreated || !_dstTransforms.isCreated)
                return;

            if (EquipmentModelBonesSetupByHumanBodyBonesUpdateManager.Instance == null)
                return;

            // Register instead of scheduling job
            EquipmentModelBonesSetupByHumanBodyBonesUpdateManager.Instance.Register(_srcTransforms, _dstTransforms);
#endif
        }

        private void OnDestroy()
        {
#if !UNITY_SERVER
            if (_srcTransforms.isCreated)
                _srcTransforms.Dispose();

            if (_dstTransforms.isCreated)
                _dstTransforms.Dispose();
#endif
        }
    }
}