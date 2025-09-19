#if UNITY_EDITOR 
using frou01.RigidBodyTrain;
using UnityEditor;
using UnityEngine;

namespace omegaExpDesign.RigidBodyTrain
{
    public class TrainSceneFollower : EditorWindow
    {
        private static Train target;
        private bool isAutoFollow = false;
        private bool nowFollowing = false;
        private float speedLowLimit = 0.00001f;

        [MenuItem("Tools/OmegaExpDesign - RBUR RailTools/TrainFollower")]
        private static void Open()
        {
            GetWindow<TrainSceneFollower>("TrainSceneFollower");
        }

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.LabelField("�ҏW���͑���ł��܂���");
                return;
            }

            var e = new GUIStyle(EditorStyles.label);
            e.fontSize = 18;
            GUILayout.Label(new GUIContent("Train Follower", "�V�[����ʂŗ�Ԃ������ǔ����܂�"), e);

            target = (Train)EditorGUILayout.ObjectField("�ǐՑΏ�", target, typeof(Train), true);

            isAutoFollow = EditorGUILayout.Toggle(new GUIContent("Auto follow", "���s���̎ԗ���T���A�����t�H�[�J�X���܂�"), isAutoFollow);


            if (GUILayout.Button("�ǐՊJ�n"))
            {
                if (target != null)
                {
                    SceneView.duringSceneGui += OnSceneGUI;
                }
            }

            if (GUILayout.Button("�ǐՒ�~"))
            {
                SceneView.duringSceneGui -= OnSceneGUI;
            }

            EditorGUILayout.Space();
            GUILayout.Box("", GUILayout.Height(3), GUILayout.ExpandWidth(true));

            // ----

            GUILayout.Label(new GUIContent("Train info", "��ԏ��"), e);
            if (target)
            {
                float v = target.controllerAnimator.GetFloat(Animator.StringToHash("RigidBodySpeed"));
                GUILayout.Label($"Speed Raw {v}");
                GUILayout.Label($"Speed Raw {(v*3.6f * 100f).ToString("F1")} km/h");    // Train�̎d�l�ɂ��ARigidbody.Velocity��1/100������
            }
        }

        void Update()
        {
            if (isAutoFollow)
            {
                if (target == null)
                {
                    foreach (var train in GameObject.FindObjectsOfType<Train>())
                    {
                        if (train.controllerAnimator.GetFloat(Animator.StringToHash("RigidBodySpeed")) > speedLowLimit)
                        {
                            target = train;
                            break;
                        }
                    }
                }
                else
                {
                    if (target.controllerAnimator.GetFloat(Animator.StringToHash("RigidBodySpeed")) < speedLowLimit)
                    {
                        target = null;
                    }
                }
            }

            if (target)
            {
                Repaint();
            }
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (target == null || !EditorApplication.isPlaying) return;

            // Scene�J������ΏۂɒǏ]
            var cam = sceneView.camera;
            if (cam != null)
            {
                sceneView.LookAt(target.transform.position);
            }
        }

    }
}
#endif