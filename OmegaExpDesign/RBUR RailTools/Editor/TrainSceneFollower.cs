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
                EditorGUILayout.LabelField("編集中は操作できません");
                return;
            }

            var e = new GUIStyle(EditorStyles.label);
            e.fontSize = 18;
            GUILayout.Label(new GUIContent("Train Follower", "シーン画面で列車を自動追尾します"), e);

            target = (Train)EditorGUILayout.ObjectField("追跡対象", target, typeof(Train), true);

            isAutoFollow = EditorGUILayout.Toggle(new GUIContent("Auto follow", "走行中の車両を探し、自動フォーカスします"), isAutoFollow);


            if (GUILayout.Button("追跡開始"))
            {
                if (target != null)
                {
                    SceneView.duringSceneGui += OnSceneGUI;
                }
            }

            if (GUILayout.Button("追跡停止"))
            {
                SceneView.duringSceneGui -= OnSceneGUI;
            }

            EditorGUILayout.Space();
            GUILayout.Box("", GUILayout.Height(3), GUILayout.ExpandWidth(true));

            // ----

            GUILayout.Label(new GUIContent("Train info", "列車情報"), e);
            if (target)
            {
                float v = target.controllerAnimator.GetFloat(Animator.StringToHash("RigidBodySpeed"));
                GUILayout.Label($"Speed Raw {v}");
                GUILayout.Label($"Speed Raw {(v*3.6f * 100f).ToString("F1")} km/h");    // Trainの仕様により、Rigidbody.Velocityの1/100が来る
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

            // Sceneカメラを対象に追従
            var cam = sceneView.camera;
            if (cam != null)
            {
                sceneView.LookAt(target.transform.position);
            }
        }

    }
}
#endif