#if UNITY_EDITOR 
using frou01.RigidBodyTrain;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.Udon;

namespace omegaExpDesign.RigidBodyTrain
{
    public class TrainSceneFollower : EditorWindow
    {
        private static Train target;
        private bool isAutoFocus = false;
        private float speedLowLimit = 0.00001f;
        private int selectedTrainIndex = 0;
        private bool showDebugInfo = false;

        private bool showDebugBogieF = false;
        private bool showDebugBogieB = false;

        private List<Train> trains = new List<Train>();
        private List<string> trainNames = new List<string>();

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
                trains = new List<Train>();
                trainNames = new List<string>();
                return;
            }

            var e = new GUIStyle(EditorStyles.label);
            e.fontSize = 18;
            GUILayout.Label(new GUIContent("Train Follower", "�V�[����ʂŗ�Ԃ������ǔ����܂�"), e);

            if (trains.Count == 0)
            {
                foreach (var t in GameObject.FindObjectsOfType<Train>())
                {
                    if (t.transform.parent != null)
                    {
                        trains.Add(t);
                        trainNames.Add(t.transform.parent.name);
                    }
                    else
                    {
                        trains.Add(t);
                        trainNames.Add(t.name);
                    }
                }
            }

            selectedTrainIndex = EditorGUILayout.Popup("Target Train", selectedTrainIndex, trainNames.ToArray());
            if (target == null)
            {
                target = trains[selectedTrainIndex];
            }

            isAutoFocus = EditorGUILayout.Toggle(new GUIContent("Auto Target", "���s���̎ԗ���T���A�����I�����܂�"), isAutoFocus);
            if (GUILayout.Button(new GUIContent("Start Tracking", "�ԗ��������ǔ����܂�"))){
                SceneView.duringSceneGui += OnSceneGUI;
            }
            if (GUILayout.Button(new GUIContent("Stop Tracking", "�ԗ��̒ǐՂ��~�߂܂�"))){
                SceneView.duringSceneGui -= OnSceneGUI;
            }


            EditorGUILayout.Space();
            GUILayout.Box("", GUILayout.Height(3), GUILayout.ExpandWidth(true));

            // ----

            GUILayout.Label(new GUIContent("Train Info", "��ԏ��"), e);
            if (target)
            {
                float v = target.controllerAnimator.GetFloat(Animator.StringToHash("RigidBodySpeed"));
                GUILayout.Label(new GUIContent($"Speed (Animator Raw) : {v}","Train.controllerAnimator�̎����ݑ��x"));
                GUILayout.Label(new GUIContent($"Speed (HumanReadable) : {(v*3.6f * 100f).ToString("F1")} km/h","�l�Ԃɂ킩��₷���ϊ��������ݑ��x"));    // Train�̎d�l�ɂ��ARigidbody.Velocity��1/100������

                EditorGUILayout.Space();

                var udon = target.GetComponent<UdonBehaviour>();
                var rf = udon.GetProgramVariable("BogieRail_F") as UdonBehaviour;
                GUILayout.Label($"BogieRail_F : {rf?.name}");
                GUILayout.Label($"RailID_F : {udon.GetProgramVariable<int>("RailID_F")}");
                var rb = udon.GetProgramVariable("BogieRail_B") as UdonBehaviour;
                GUILayout.Label($"BogieRail_B : {rb?.name}");
                GUILayout.Label($"RailID_B : {udon.GetProgramVariable<int>("RailID_B")}");

                EditorGUILayout.Space();

                GUILayout.Label(new GUIContent("Debug Info", "�f�o�b�O�����̂��ׂ��ȏ���\�����܂�"), e);
                showDebugInfo = EditorGUILayout.Toggle(new GUIContent("Enable", "�L���ɂ���"), showDebugInfo);

                if (showDebugInfo)
                {
                    GUILayout.Label($"onRailPoint_F : {udon.GetProgramVariable<float>("onRailPoint_F").ToString("F2")}");
                    GUILayout.Label($"BogieToWheelPosLengthF : {udon.GetProgramVariable<float>("BogieToWheelPosLengthF").ToString("F2")}");
                    GUILayout.Label($"onRailPosition_F - RailEnd__Point_F : {(udon.GetProgramVariable<Vector3>("onRailPosition_F") - udon.GetProgramVariable<Vector3>("RailEnd__Point_F")).sqrMagnitude.ToString("F2")}");
                    GUILayout.Label($"onRailPosition_F - RailStartPoint_F : {(udon.GetProgramVariable<Vector3>("onRailPosition_F") - udon.GetProgramVariable<Vector3>("RailStartPoint_F")).sqrMagnitude.ToString("F2")}");
                    GUILayout.Label($"TooLongDif : {(udon.GetProgramVariable<Vector3>("onRailPosition_F") - udon.GetProgramVariable<Vector3>("positionBogie_F")).sqrMagnitude.ToString("F2")}");
                    showDebugBogieF = EditorGUILayout.Toggle(new GUIContent("Visualize BogieF", "BogieF�̏���Scene�ɕ`�悵�܂�"), showDebugBogieF);

                    EditorGUILayout.Space();

                    GUILayout.Label($"onRailPoint_B : {udon.GetProgramVariable<float>("onRailPoint_B").ToString("F2")}");
                    GUILayout.Label($"BogieToWheelPosLengthB : {udon.GetProgramVariable<float>("BogieToWheelPosLengthB").ToString("F2")}");
                    GUILayout.Label($"onRailPosition_B - RailEnd__Point_B : {(udon.GetProgramVariable<Vector3>("onRailPosition_B") - udon.GetProgramVariable<Vector3>("RailEnd__Point_B")).sqrMagnitude.ToString("F2")}");
                    GUILayout.Label($"onRailPosition_B - RailStartPoint_B : {(udon.GetProgramVariable<Vector3>("onRailPosition_B") - udon.GetProgramVariable<Vector3>("RailStartPoint_B")).sqrMagnitude.ToString("F2")}");
                    GUILayout.Label($"TooLongDif : {(udon.GetProgramVariable<Vector3>("onRailPosition_B") - udon.GetProgramVariable<Vector3>("positionBogie_B")).sqrMagnitude.ToString("F2")}");
                    showDebugBogieB = EditorGUILayout.Toggle(new GUIContent("Visualize BogieB", "BogieB�̏���Scene�ɕ`�悵�܂�"), showDebugBogieB);

                    EditorGUILayout.Space();

                    GUILayout.Label($"pathResolution : {udon.GetProgramVariable<int>("pathResolution").ToString()}");

                    EditorGUILayout.Space();
                    GUILayout.Label(new GUIContent("Nearest Rails by compute","�G�f�B�^���Ō��������u��Ԃɍł��߂����[���v�ł��B���ꂪBogieRail�ƈقȂ�ꍇ�́A�E����Ԃ��\�z����܂�"));
                    {
                        Vector3 t = udon.GetProgramVariable<Vector3>("positionBogie_F");
                        var r = getNearestRail(t);
                        GUILayout.Label($"Bogie Near_F : {r.rail.name}");
                    }
                    {
                        Vector3 t = udon.GetProgramVariable<Vector3>("positionBogie_B");
                        var r = getNearestRail(t);
                        GUILayout.Label($"Bogie Near_B : {r.rail.name}");
                    }
                }
            }
            else
            {
                GUILayout.Label("no train selected");
            }

        }
        private void OnSceneGUI(SceneView sceneView)
        {
            if (target == null || !EditorApplication.isPlaying) return;

            // Scene�J������ΏۂɒǏ]
            var cam = sceneView.camera;
            if (cam != null)
            {
                sceneView.LookAt(target.transform.position);
            }

            if (showDebugInfo)
            {
                var udon = target.GetComponent<UdonBehaviour>();
                if (showDebugBogieF)
                {
                    drawSceneCrossHair(udon.GetProgramVariable<Vector3>("onRailPosition_F"), 0.5f, Color.red, "onRailPosition_F");
                    drawSceneCrossHair(udon.GetProgramVariable<Vector3>("RailEnd__Point_F"), 0.5f, Color.green, "RailEnd__Point_F");
                    drawSceneCrossHair(udon.GetProgramVariable<Vector3>("RailStartPoint_F"), 0.5f, Color.green, "RailStartPoint_F");
                }
                if (showDebugBogieB)
                { 
                    drawSceneCrossHair(udon.GetProgramVariable<Vector3>("onRailPosition_B"), 0.5f, Color.red, "onRailPosition_B");
                    drawSceneCrossHair(udon.GetProgramVariable<Vector3>("RailEnd__Point_B"), 0.5f, Color.green, "RailEnd__Point_B");
                    drawSceneCrossHair(udon.GetProgramVariable<Vector3>("RailStartPoint_B"), 0.5f, Color.green, "RailStartPoint_B");
                }
            }
        }

        private void drawSceneCrossHair(Vector3 pos,float size,Color c,string text)
        {
            Handles.color = c;
            Handles.DrawLine(pos + Vector3.left * size, pos + Vector3.right * size);
            Handles.DrawLine(pos + Vector3.forward * size, pos + Vector3.back * size);
            Handles.DrawLine(pos + Vector3.up * size, pos + Vector3.down * size);

            Handles.Label(pos + Vector3.up * size * 2f + Vector3.right * size, text, new GUIStyle()
            {
                normal = { textColor = c},
                fontStyle = FontStyle.Bold
            });

        }
        (float distance,Rail_Script rail) getNearestRail(Vector3 position)
        {
            float distance = Mathf.Infinity;
            Rail_Script rail = null;
            foreach(var r in GameObject.FindObjectsOfType<Rail_Script>())
            {
                float c = r.cinemachinePath.FindClosestPoint(position, 0, 50, 50);
                var p = r.cinemachinePath.EvaluatePositionAtUnit(c, Cinemachine.CinemachinePath.PositionUnits.PathUnits);
                if((p - position).magnitude < distance)
                {
                    rail = r;
                    distance = (p - position).magnitude;
                }
            }
            return (distance, rail);
        }

        void Update()
        {
            if (isAutoFocus)
            {
                if (target == null)
                {
                    foreach (var train in GameObject.FindObjectsOfType<Train>())
                    {
                        if (train.controllerAnimator && train.controllerAnimator.GetFloat(Animator.StringToHash("RigidBodySpeed")) > speedLowLimit)
                        {
                            target = train;
                            break;
                        }
                    }
                }
                else
                {
                    if (target.controllerAnimator && target.controllerAnimator.GetFloat(Animator.StringToHash("RigidBodySpeed")) < speedLowLimit)
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

    }
}
#endif