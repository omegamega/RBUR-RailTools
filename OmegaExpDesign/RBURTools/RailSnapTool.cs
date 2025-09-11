using UnityEngine;
using frou01.RigidBodyTrain;
using Cinemachine;
using System;

namespace omegaExpDesign.RigidBodyTrain
{
    public class RailSnapTool : MonoBehaviour
    {
        [Tooltip("スナップするレール。RailSnapToolと同じGameObject")]
        public Rail_Script rail;

        [Tooltip("スナップ距離")]
        public float snapDistance = 2f;

        [Tooltip("他のCinemachinePathを描画する")]
        public bool drawPath = true;

        [Tooltip("ドラッグ移動でRail_Scriptのprev,nextを外し、スナップしたらprev.nextを自動設定する")]
        public bool autoConnect = true;
    }
}
