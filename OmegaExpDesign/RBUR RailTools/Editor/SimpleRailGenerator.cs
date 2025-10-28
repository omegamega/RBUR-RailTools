using Cinemachine;
using frou01.RigidBodyTrain;
using UnityEditor;
using UnityEngine;
using VRC.Udon;

namespace omegaExpDesign.RigidBodyTrain
{
    public class SimpleRailGenerator : EditorWindow
    {
        private string defaultRailName = " (Generated)";
        private float straightRailLength = 40f;
        private float curveRailRadius = 240f;
        private float curveRailAngle = 90f;
        private int curveSplit = 24;
        private float curveRailEasyCant = 2f;
        private float railEndGap = 5f;      // �`���^�����O�h�~�̂��߂ɁA���[���I�[�ɍ��E�F�C�|�C���g�̊Ԋu

        [MenuItem("Tools/OmegaExpDesign - RBUR RailTools/SimpleRailGenerator")]
        private static void Open()
        {
            GetWindow<SimpleRailGenerator>("SimpleRailGenerator");
        }

        private void OnGUI()
        {
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.LabelField("���s���͑���ł��܂���");
            }


            var e = new GUIStyle(EditorStyles.label);
            e.fontSize = 18;
            GUILayout.Label(new GUIContent("Generate Rail", "���[���𐶐�����"), e);
            defaultRailName = EditorGUILayout.TextField(new GUIContent("Default Rail Name", "Default "), defaultRailName);

            EditorGUILayout.Space();

            GUILayout.Label(new GUIContent("Straight Rail", "�������[��"), e);

            straightRailLength = EditorGUILayout.FloatField(new GUIContent("Length", "���[������"), straightRailLength);
            if (GUILayout.Button(new GUIContent("Generate", "�������[���𐶐����܂�")))
            {
                if (straightRailLength < railEndGap * 3)
                {
                    Vector3[] pos = { new Vector3(0, 0, 0), new Vector3(0, 0, straightRailLength / 2), new Vector3(0, 0, straightRailLength) };
                    Vector3[] tan = { new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 1) };
                    float[] rol = { 0f, 0f, 0f };

                    GameObject go = generateRail(
                        pos, tan, rol
                        );

                    go.name = $"S{straightRailLength.ToString("F0")}{defaultRailName}";
                }
                else
                {
                    Vector3[] pos = { new Vector3(0, 0, 0), new Vector3(0, 0, railEndGap), new Vector3(0, 0, straightRailLength - railEndGap), new Vector3(0, 0, straightRailLength) };
                    Vector3[] tan = { new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 1) };
                    float[] rol = { 0f, 0f, 0f ,0f };

                    GameObject go = generateRail(
                        pos, tan, rol
                        );

                    go.name = $"S{straightRailLength.ToString("F0")}{defaultRailName}";
                }
            }

            EditorGUILayout.Space();

            GUILayout.Label(new GUIContent("Curve Rail", "�Ȑ����[��"), e);

            curveRailRadius = EditorGUILayout.FloatField(new GUIContent("Radius", "���[�����a"), curveRailRadius);
            curveRailAngle = EditorGUILayout.FloatField(new GUIContent("Angle", "�p�x"), curveRailAngle);
            curveSplit = EditorGUILayout.IntField(new GUIContent("Split", "Angle=360�̎��̕�����"), curveSplit);
            curveRailEasyCant = EditorGUILayout.FloatField(new GUIContent("Cant(Easy)", "�J���g(�Ȉ�)"), curveRailEasyCant);
            if (GUILayout.Button(new GUIContent("Generate", "�Ȑ����[���𐶐����܂�")))
            {
                int c = Mathf.RoundToInt(curveRailAngle / 360f * curveSplit);
                Vector3[] pos = new Vector3[c+1];
                Vector3[] tan = new Vector3[c+1];
                float[] rol = new float[c+1];

                Vector3 start = new Vector3(curveRailRadius * Mathf.Cos(0), 0, curveRailRadius * Mathf.Sin(0));
                
                for (int i =0;i <= c; i++)
                {
                    float r = curveRailAngle / 180f * Mathf.PI * i / c;
                    float t = (4f / 3f) * Mathf.Tan(Mathf.PI / curveSplit / 2f) * curveRailRadius;
                    pos[i] = new Vector3(curveRailRadius * Mathf.Cos(r), 0, curveRailRadius * Mathf.Sin(r)) - start;
                    tan[i] = new Vector3(-t * Mathf.Sin(r), 0, t * Mathf.Cos(r));
                    if (i == 0 || i == c)
                    {
                        rol[i] = 0f;
                    }
                    else
                    {
                        rol[i] = curveRailEasyCant;
                    }
                }

                GameObject go = generateRail(
                    pos, tan, rol
                    );

                go.name = $"R{curveRailRadius.ToString("F0")}_{curveRailAngle.ToString("F0")}{defaultRailName}";
            }
        }

        private GameObject generateRail(Vector3[] positions, Vector3[] tangents, float[] rolls )
        {
            if(positions.Length != tangents.Length || tangents.Length != rolls.Length)
            {
                Debug.Log("generateRail Fail with param length un-match");
                return null;
            }

            // SceneView�̃J�������擾
            SceneView view = SceneView.lastActiveSceneView;
            Vector3 pos = Vector3.zero;
            if (view != null)
            {
                pos = view.pivot;
                pos.y = 0;
            }
            else
            {
                Debug.LogWarning("SceneView��������܂���B");
            }


            // �V����GameObject�𐶐�
            GameObject go = new GameObject("Rail(Generated)");
            go.transform.position = pos;

            // Undo�Ή�
            Undo.RegisterCreatedObjectUndo(go, "Generate Rail");

            // �R���|�[�l���g��ǉ�
            Rail_Script s = Undo.AddComponent<Rail_Script>(go);
            CinemachinePath c = Undo.AddComponent<CinemachinePath>(go);
            var u = go.GetComponent<UdonBehaviour>();
            s.cinemachinePath = c;

            {
                var so = new SerializedObject(c);
                var prop = so.FindProperty("m_Waypoints");

                // ���ׂď����ĐV�K�ɒǉ�
                prop.ClearArray();
                int count = positions.Length;
                prop.arraySize = count;

                for (int i = 0; i < count; i++)
                {
                    var elem = prop.GetArrayElementAtIndex(i);

                    // position, tangent, roll �̊e�t�B�[���h��ݒ�
                    elem.FindPropertyRelative("position").vector3Value = positions[i];
                    elem.FindPropertyRelative("tangent").vector3Value = tangents[i];
                    elem.FindPropertyRelative("roll").floatValue = rolls[i];
                }

                // �ύX���m��
                so.ApplyModifiedProperties();

                // �L���b�V���X�V��Dirty�ʒm
                c.InvalidateDistanceCache();
                EditorUtility.SetDirty(c);
            }
            // �V�[���ύX��Unity�ɒʒm
            EditorUtility.SetDirty(go);

            // �Q�[���I�u�W�F�N�g��I�����Ă���΂��̌Z��ɒǉ�����
            if ( Selection.activeTransform?.parent != null)
            {
                Undo.SetTransformParent(go.transform, Selection.activeTransform.parent, "Set Parent");
            }

            // ���������I�u�W�F�N�g��I��
            Selection.activeGameObject = go;

            return go;
        }
    }
}
