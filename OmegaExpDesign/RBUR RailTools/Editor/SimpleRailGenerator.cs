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
        private float railEndGap = 5f;      // チャタリング防止のために、レール終端に作るウェイポイントの間隔

        [MenuItem("Tools/OmegaExpDesign - RBUR RailTools/SimpleRailGenerator")]
        private static void Open()
        {
            GetWindow<SimpleRailGenerator>("SimpleRailGenerator");
        }

        private void OnGUI()
        {
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.LabelField("実行中は操作できません");
            }

            var e = new GUIStyle(EditorStyles.label);
            e.fontSize = 18;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent("SimpleRailGenerator", "レールを生成する"), e);
                if (GUILayout.Button("Wikiを開く/Show Wiki", EditorStyles.linkLabel))
                {
                    Application.OpenURL("https://github.com/omegamega/RBUR-RailTools/wiki/SimpleRailGenerator");
                }
            }
            defaultRailName = EditorGUILayout.TextField(new GUIContent("Default Rail Name", "Default "), defaultRailName);

            EditorGUILayout.Space();

            GUILayout.Label(new GUIContent("Straight Rail", "直線レール"), e);

            straightRailLength = EditorGUILayout.FloatField(new GUIContent("Length", "レール長さ"), straightRailLength);
            if (GUILayout.Button(new GUIContent("Generate rail", "直線レールを生成します")))
            {
                if (straightRailLength < railEndGap * 3)
                {
                    Vector3[] pos = { new Vector3(0, 0, 0), new Vector3(0, 0, straightRailLength / 2), new Vector3(0, 0, straightRailLength) };
                    Vector3[] tan = { new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 1) };
                    float[] rol = { 0f, 0f, 0f };

                    GameObject go = generateRail(
                        pos, tan, rol, Vector3.zero
                        );

                    go.name = $"S{straightRailLength.ToString("F0")}{defaultRailName}";
                }
                else
                {
                    Vector3[] pos = { new Vector3(0, 0, 0), new Vector3(0, 0, railEndGap), new Vector3(0, 0, straightRailLength - railEndGap), new Vector3(0, 0, straightRailLength) };
                    Vector3[] tan = { new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 1) };
                    float[] rol = { 0f, 0f, 0f, 0f };

                    GameObject go = generateRail(
                        pos, tan, rol, Vector3.zero
                        );

                    go.name = $"S{straightRailLength.ToString("F0")}{defaultRailName}";
                }
            }

            EditorGUILayout.Space();

            GUILayout.Label(new GUIContent("Curve Rail", "曲線レール"), e);

            curveRailRadius = EditorGUILayout.FloatField(new GUIContent("Radius", "レール半径"), curveRailRadius);
            curveRailAngle = EditorGUILayout.FloatField(new GUIContent("Angle", "カーブの旋回角度。単位は度。360で一周分"), curveRailAngle);
            curveSplit = EditorGUILayout.IntField(new GUIContent("Split", "Angle=360の時の分割数"), curveSplit);
            curveRailEasyCant = EditorGUILayout.FloatField(new GUIContent("Cant(Easy)", "カント(簡易)。単位は度"), curveRailEasyCant);
            if (GUILayout.Button(new GUIContent("Generate rail", "曲線レールを生成します")))
            {
                int c = Mathf.RoundToInt(curveRailAngle / 360f * curveSplit);
                Vector3[] pos = new Vector3[c + 1];
                Vector3[] tan = new Vector3[c + 1];
                float[] rol = new float[c + 1];

                Vector3 start = new Vector3(curveRailRadius * Mathf.Cos(0), 0, curveRailRadius * Mathf.Sin(0));

                for (int i = 0; i <= c; i++)
                {
                    float r = curveRailAngle / 180f * Mathf.PI * i / c;
                    float t = (4f / 3f) * Mathf.Tan(Mathf.PI / curveSplit / 2f) * curveRailRadius;  // ベジエ曲線で円弧を近似する定数
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
                    pos, tan, rol, Vector3.zero
                    );

                go.name = $"R{curveRailRadius.ToString("F0")}_{curveRailAngle.ToString("F0")}{defaultRailName}";
            }

            /* 
             * TODO:2レール間を繋ぐ機能は技術検証レベルでしか出来てないので一旦コメントアウト
            EditorGUILayout.Space();

            GUILayout.Label(new GUIContent("Bridging Curve Rail(super-alpha)", "2つのレール間を接続する曲線レールを生成します"), e);
            EditorGUILayout.LabelField("離れた2つのレールを選択してください");


            int selectedRailCount = 0;
            Rail_Script[] selectedRails = new Rail_Script[2];
            foreach(var obj in Selection.gameObjects)
            {
                if (obj.GetComponent<Rail_Script>() != null)
                {
                    if (selectedRailCount < 2)
                    {
                        selectedRails[selectedRailCount] = obj.GetComponent<Rail_Script>();
                    }
                    selectedRailCount++;
                }
            }
            using (new EditorGUI.DisabledScope(selectedRailCount != 2))
            {
                if (GUILayout.Button(new GUIContent("Generate rail(super-alpha)", "接続する曲線レールを生成します")))
                {
                    // 選択済みレール0番1番から近い終端ペアを探す。そのタンジェントも
                    Vector3 newStartPos, newEndPos;                     // 新しいレールの起点/終点
                    Vector3 newStartTangent, newEndTangent;             // 新しいレールの起点Tangent/終点Tangent

                    Vector3 start0 = selectedRails[0].cinemachinePath.EvaluatePositionAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized);
                    Vector3 end0 = selectedRails[0].cinemachinePath.EvaluatePositionAtUnit(1f, CinemachinePathBase.PositionUnits.Normalized);
                    Vector3 start1 = selectedRails[1].cinemachinePath.EvaluatePositionAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized);
                    Vector3 end1 = selectedRails[1].cinemachinePath.EvaluatePositionAtUnit(1f, CinemachinePathBase.PositionUnits.Normalized);

                    Vector3 center0 = (start0 + end0) / 2.0f;
                    Vector3 center1 = (start1 + end1) / 2.0f;
                    if ((start0 - center1).magnitude < (end0 - center1).magnitude)
                    {
                        newStartPos = start0;  // 新レール起点を0番レールのstart側とする
                        newStartTangent = selectedRails[0].cinemachinePath.EvaluateTangentAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized) * -1f;   // StartとStartが繋がっているなら、タンジェントは反転している
                    }
                    else
                    {
                        newStartPos = end0;    // 新レール起点を0番レールのend側とする
                        newStartTangent = selectedRails[0].cinemachinePath.EvaluateTangentAtUnit(1f, CinemachinePathBase.PositionUnits.Normalized) * 1f;
                    }

                    if ((start1 - center0).magnitude < (end1 - center0).magnitude)
                    {
                        newEndPos = start1;    // 新レール終点を1番レールのstart側とする
                        newEndTangent = selectedRails[1].cinemachinePath.EvaluateTangentAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized) * 1f;
                    }
                    else
                    {
                        newEndPos = end1;      // 新レール終点を1番レールのend側とする
                        newEndTangent = selectedRails[1].cinemachinePath.EvaluateTangentAtUnit(1f, CinemachinePathBase.PositionUnits.Normalized) * -1f;   // EndとEndが繋がっているなら、タンジェントは反転しているはず
                    }

                    // タンジェント算出のために、XZ平面での交点を算出する
                    Vector2 newStartPos2D = new Vector2(newStartPos.x, newStartPos.z);
                    Vector2 newStartTangent2D = new Vector2(newStartTangent.x, newStartTangent.z);
                    Vector2 newEndPos2D = new Vector2(newEndPos.x, newEndPos.z);
                    Vector2 newEndTangent2D = new Vector2(newEndTangent.x, newEndTangent.z);
                    float theta = Vector3.Angle(newStartTangent, newEndTangent);
                    Vector2 intersectionPos2D;
                    float TangentLength0, TangentLength1;
                    // とはいえタンジェント算出も現状は雑であり、どうすべきかは悩ましい
                    if (TryGetIntersection2D(newStartPos2D, newStartTangent2D, newEndPos2D, newEndTangent2D, out intersectionPos2D))
                    {
                        // 並行でないなら、まあ雑に交点との半分にしておこう
                        // TODO:マシな感じにする。マシってどうやるんだ？
                        // 交点が取れてるなら、円弧+直線の組み合わせで行けると思うが…
                        TangentLength0 = (newStartPos2D - intersectionPos2D).magnitude;
                        TangentLength1 = (newStartPos2D - intersectionPos2D).magnitude;
                    }
                    else
                    {
                        // 並行なら、…うーんどうしような…。円弧にしたいが近似式が90度以上で使えないため、暫定的なパスを引く
                        // TODO:マシな感じにする。マシってどうやるんだ？
                        // UターンかS字かの2択で繋ぐべきなのだろうが、今のところアイデアがない
                        // Uターンなら180カーブと直線(つまり上のif文パターンの特殊ケース)、S字なら180カーブ2つになる所だが、それってどう算出するのだ？
                        TangentLength0 = TangentLength1 = (newStartPos - newEndPos).magnitude / 2;
                    }

                    // ベジエ計算のために、仮のCinemachinePathを作って、そこから頂点/Tanglentをサンプリングする
                    int c = 1;
                    Vector3[] pos = new Vector3[c + 1];
                    Vector3[] tan = new Vector3[c + 1];
                    float[] rol = new float[c + 1];

                    pos[0] = Vector3.zero;
                    tan[0] = newStartTangent.normalized * TangentLength0;
                    pos[1] = newEndPos - newStartPos;
                    tan[1] = newEndTangent.normalized * TangentLength1;

                    GameObject go = generateRail(
                        pos, tan, rol, newStartPos
                        );
                    float l = go.GetComponent<Rail_Script>().cinemachinePath.PathLength;
                    go.name = $"BC{l.ToString("F0")} {defaultRailName}";

                    // 設置して即座に繋ぐ機能(SnapToolのAuto Connect)がRailGenerator側にないので、横にポン置きすることとした
                    // TODO:AutoConnectした方がいいと思う
                    go.transform.position += new Vector3(1f, 0f, 1f);
                }
            }
            */
        }

        private GameObject generateRail(Vector3[] positions, Vector3[] tangents, float[] rolls, Vector3 gameObjectPos)
        {
            if(positions.Length != tangents.Length || tangents.Length != rolls.Length)
            {
                Debug.Log("generateRail Fail with param length un-match");
                return null;
            }

            // SceneViewのカメラを取得
            SceneView view = SceneView.lastActiveSceneView;
            Vector3 pos = Vector3.zero;
            if (view != null)
            {
                pos = view.pivot;
                pos.y = 0;
            }
            else
            {
                Debug.LogWarning("SceneViewが見つかりません。");
            }


            // 新しいGameObjectを生成
            GameObject go = new GameObject("Rail(Generated)");
            if (gameObjectPos.Equals(Vector3.zero))
            {
                go.transform.position = pos;
            }
            else
            {
                go.transform.position = gameObjectPos;
            }

            // Undo対応
            Undo.RegisterCreatedObjectUndo(go, "Generate Rail");

            // コンポーネントを追加
            Rail_Script s = Undo.AddComponent<Rail_Script>(go);
            CinemachinePath c = Undo.AddComponent<CinemachinePath>(go);
            var u = go.GetComponent<UdonBehaviour>();
            s.cinemachinePath = c;

            var so = new SerializedObject(c);
            var prop = so.FindProperty("m_Waypoints");

            // すべて消して新規に追加
            prop.ClearArray();
            int count = positions.Length;
            prop.arraySize = count;

            for (int i = 0; i < count; i++)
            {
                var elem = prop.GetArrayElementAtIndex(i);

                // position, tangent, roll の各フィールドを設定
                elem.FindPropertyRelative("position").vector3Value = positions[i];
                elem.FindPropertyRelative("tangent").vector3Value = tangents[i];
                elem.FindPropertyRelative("roll").floatValue = rolls[i];
            }

            // 変更を確定
            so.ApplyModifiedProperties();

            // キャッシュ更新とDirty通知
            c.InvalidateDistanceCache();
            EditorUtility.SetDirty(c);
            // シーン変更をUnityに通知
            EditorUtility.SetDirty(go);

            // ゲームオブジェクトを選択していればその兄弟に追加する
            if ( Selection.activeTransform?.parent != null)
            {
                Undo.SetTransformParent(go.transform, Selection.activeTransform.parent, "Set Parent");
            }

            // 生成したオブジェクトを選択
            Selection.activeGameObject = go;

            return go;
        }

        public static bool TryGetIntersection2D(
            Vector2 p1, Vector2 v1,
            Vector2 p2, Vector2 v2,
            out Vector2 intersection)
        {
            intersection = Vector2.zero;

            float cross = v1.x * v2.y - v1.y * v2.x;

            // 平行チェック
            if (Mathf.Abs(cross) < 0.0001f)
                return false;

            Vector2 diff = p2 - p1;
            float t = (diff.x * v2.y - diff.y * v2.x) / cross;

            intersection = p1 + v1 * t;
            return true;
        }

        void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
        }

        void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        void OnSelectionChanged()
        {
            Repaint();
        }
    }
}
