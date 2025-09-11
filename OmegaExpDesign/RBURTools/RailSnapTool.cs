using UnityEngine;
using frou01.RigidBodyTrain;
using Cinemachine;
using System;

namespace omegaExpDesign.RigidBodyTrain
{
    public class RailSnapTool : MonoBehaviour
    {
        [Tooltip("�X�i�b�v���郌�[���BRailSnapTool�Ɠ���GameObject")]
        public Rail_Script rail;

        [Tooltip("�X�i�b�v����")]
        public float snapDistance = 2f;

        [Tooltip("����CinemachinePath��`�悷��")]
        public bool drawPath = true;

        [Tooltip("�h���b�O�ړ���Rail_Script��prev,next���O���A�X�i�b�v������prev.next�������ݒ肷��")]
        public bool autoConnect = true;
    }
}
