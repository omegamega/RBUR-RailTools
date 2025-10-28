#if UNITY_EDITOR 
using UnityEngine;
using UnityEditor;
using frou01.RigidBodyTrain;
using System.Collections.Generic;
using Cinemachine;

namespace omegaExpDesign.RigidBodyTrain
{
    public class SnapToolWindow : EditorWindow
        {
        private bool isSnapActive = false;
        private float snapDistance = 20f;
        private bool autoConnect = true;
        private float autoConnectDistance = 0.01f;

        private bool isWaypointSnapActive = false;
        private Vector3 prevWaypointStartPos = Vector3.zero;
        private Vector3 prevWaypointEndPos = Vector3.zero;
        private float snapWaypointDistance = 20f;
        private bool autoWaypointConnect = true;
        private float straightThreshold = 5f;
        private float easyCantAngle = 3f;

        private bool isVisualizeActive = false;
        private bool isNameVisible = true;
        private bool isNameAllVisible = false;
        private bool isSelectionHighLight = true;
        private bool isLengthVisible = true;
        private bool isGradientVisible = true;
        private bool isPositionYVisible = true;

        private Camera tempCamera = null;
        private Vector2 scrollpos = Vector2.zero;

        private List<Vector3> selectionSnapPos = new List<Vector3>();           // 選択済みレールのうちスナップ対象座標
        private List<Vector3> otherSnapPos = new List<Vector3>();               // 非選択レールのうちスナップ対象座標
        private List<Rail_Script> selectionRails = new List<Rail_Script>();     // 選択済みレールすべて
        private List<Rail_Script> otherRails = new List<Rail_Script>();         // 非選択レールすべて
        [MenuItem("Tools/OmegaExpDesign - RBUR RailTools/RailSnapTool")]
        public static void Open()
        {
            var window = GetWindow<SnapToolWindow>("SnapTool");
            window.minSize = new Vector2(300, 200);
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.postprocessModifications += OnModifications;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.postprocessModifications -= OnModifications;
        }

        private UndoPropertyModification[] OnModifications(UndoPropertyModification[] mods)
        {
            // Waypointの編集検知方法がこれしか見つからなかったので、この方法で検知している
            if (!isWaypointSnapActive) return mods;

            var rail = Selection.activeGameObject?.GetComponent<Rail_Script>();
            if (rail == null) return mods;

            foreach (var mod in mods)
            {
                if (mod.currentValue.target is CinemachinePathBase && rail.cinemachinePath == mod.currentValue.target)
                {
                    //Debug.Log($"OnMod {mod.previousValue.target.ToString()} {mod.currentValue.target.ToString()}");
                    var current_st = (GetWaypointStartPosition((CinemachinePathBase)mod.currentValue.target));
                    if (current_st != prevWaypointStartPos && tempCamera)
                    {
                        prevWaypointStartPos = current_st;

                        if (autoWaypointConnect)
                        {
                            // まずstart側=prev側が接続されていれば、接続を外す
                            if (rail.prev)
                            {
                                if (rail.prev.prev == rail)
                                {
                                    Undo.RecordObject(rail.prev, "Disconnect rail");
                                    rail.prev.prev = null;
                                }
                                if (rail.prev.next == rail)
                                {
                                    Undo.RecordObject(rail.prev, "Disconnect rail");
                                    rail.prev.next = null;
                                }
                                Undo.RecordObject(rail, "Disconnect rail");
                                rail.prev = null;
                            }
                        }

                        // スナップ対象レールを更新
                        OnSelectionChanged();

                        // 改めてスナップできる場所を探す
                        Vector3 pos = rail.cinemachinePath.EvaluatePositionAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized);
                        var result = searchSnapPosition(tempCamera, pos);       // 本当はsceneView.cameraを使いたいがここからは取れなさそうなので雑に保存しておいたカメラを使う
                        if (result.screenDistance < snapWaypointDistance)
                        {
                            // スナップする
                            Vector3 v = GetWaypointStartPosition(rail.cinemachinePath);
                            v += rail.gameObject.transform.InverseTransformVector(result.diffDistance);
                            SetWaypointStartPosition(rail.cinemachinePath, v);
                        }

                        if (autoWaypointConnect)
                        {
                            foreach (var otherRail in otherRails)
                            {
                                TryConnectRail(rail, otherRail);
                            }
                        }
                    }

                    var current_end = (GetWaypointEndPosition((CinemachinePathBase)mod.currentValue.target));
                    if (current_end != prevWaypointEndPos)
                    {
                        prevWaypointEndPos = current_end;

                        if (autoWaypointConnect)
                        {
                            // まずend側=next側が接続されていれば、接続を外す
                            if (rail.next)
                            {
                                if (rail.next.prev == rail)
                                {
                                    Undo.RecordObject(rail.next, "Disconnect rail");
                                    rail.next.prev = null;
                                }
                                if (rail.next.next == rail)
                                {
                                    Undo.RecordObject(rail.next, "Disconnect rail");
                                    rail.next.next = null;
                                }
                                Undo.RecordObject(rail, "Disconnect rail");
                                rail.next = null;
                            }
                        }

                        // スナップ対象レールを更新
                        OnSelectionChanged();

                        // 改めてスナップできる場所を探す
                        Vector3 pos = rail.cinemachinePath.EvaluatePositionAtUnit(1f, CinemachinePathBase.PositionUnits.Normalized);
                        var result = searchSnapPosition(tempCamera, pos);       // 本当はsceneView.cameraを使いたいがここからは取れなさそうなので雑に保存しておいたカメラを使う
                        if (result.screenDistance < snapWaypointDistance)
                        {
                            // スナップする
                            Vector3 v = GetWaypointEndPosition(rail.cinemachinePath);
                            v += rail.gameObject.transform.InverseTransformVector(result.diffDistance);
                            SetWaypointEndPosition(rail.cinemachinePath, v);
                        }

                        if (autoWaypointConnect)
                        {
                            foreach (var otherRail in otherRails)
                            {
                                TryConnectRail(rail, otherRail);
                            }
                        }
                    }
                }
            }
            return mods;
        }

        private void OnGUI()
        {
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.LabelField("Play中は操作できません");
                return;
            }

            scrollpos = GUILayout.BeginScrollView(scrollpos);

            var e = new GUIStyle(EditorStyles.label);
            e.fontSize = 18;
            GUILayout.Label(new GUIContent("GameObject Snap", "レール付きのGameObjectを選択、ドラッグした時にスナップさせます"), e);
            isSnapActive = EditorGUILayout.Toggle(new GUIContent("Enable", "GameObjectスナップを有効にする"), isSnapActive);
            using (new EditorGUI.DisabledScope(!isSnapActive))
            {
                snapDistance = EditorGUILayout.FloatField(new GUIContent("Snap Distance", "スナップする時の最小距離。スクリーンpx単位"), snapDistance);
                autoConnect = EditorGUILayout.Toggle(new GUIContent("Auto Connect & Disconnect", "レール移動時にprev,nextも自動的に切断し、移動後は自動的に近くのレールに接続します"), autoConnect);
                //            autoConnectDistance = EditorGUILayout.FloatField(new GUIContent("Auto Connect Distance","AutoConnect時に接続する距離です。Unity距離単位。基本的に調整する必要はありません"), autoConnectDistance);
            }

            EditorGUILayout.Space();
            GUILayout.Box("", GUILayout.Height(3), GUILayout.ExpandWidth(true));

            // ----

            GUILayout.Label(new GUIContent("Waypoint Snap", "レールのCinemachinePath Waypointを移動編集するとき、他のレール終端にスナップさせます"), e);

            isWaypointSnapActive = EditorGUILayout.Toggle(new GUIContent("Enable", "Waypointスナップを有効にする"), isWaypointSnapActive);
            using (new EditorGUI.DisabledScope(!isWaypointSnapActive))
            {
                snapWaypointDistance = EditorGUILayout.FloatField(new GUIContent("Snap Distance", "スナップする時の最小距離。スクリーンpx単位"), snapWaypointDistance);
                autoWaypointConnect = EditorGUILayout.Toggle(new GUIContent("Auto Connect & Disconnect", "Waypoint移動時にprev,nextも自動的に切断し、移動後は自動的に近くのレールに接続します"), autoWaypointConnect);
            }

            EditorGUILayout.Space();
            GUILayout.Box("", GUILayout.Height(3), GUILayout.ExpandWidth(true));

            // ----


            GUILayout.Label(new GUIContent("Waypoint Adjust", "レールのCinemachinePath Waypointを細かく調整します"), e);
            // WaypointAdjustはレールを1つ選択している時のみ操作できる
            using (new EditorGUI.DisabledScope(!(Selection.count == 1 && Selection.activeGameObject?.GetComponent<Rail_Script>() != null)))
            {
                GUILayout.Label(new GUIContent("Endpoint Tagent","終端のTangentを修正します"));
                var rail = Selection.activeGameObject?.GetComponent<Rail_Script>();
                using (new EditorGUI.DisabledScope(!(rail?.prev ?? false)))
                {
                    if (GUILayout.Button("prev側Tangentを接続先Tangentに合わせる"))
                    {
                        CorrectWaypointTangentWithPrev(rail);
                    }
                }
                using (new EditorGUI.DisabledScope(!(rail?.next ?? false)))
                {
                    if (GUILayout.Button("next側Tangentを接続先Tangentに合わせる"))
                    {
                        CorrectWaypointTangentWithNext(rail);
                    }
                }

                EditorGUILayout.Space();
                GUILayout.Label(new GUIContent("Slope","Waypoint全体を編集し、スロープにします"));
                if (GUILayout.Button("全WaypointのTangent.Yを整え、滑らかな坂にする。スムース化(簡易)"))
                {
                    SmoothWaypointTangentToSlope(rail);
                }
                if (GUILayout.Button("始点と終点のY座標に基づいて、中間Waypoint.Yを編集して、均一なスロープにする(簡易)"))
                {
                    SmoothWaypointHeightToSlope(rail);
                }

                EditorGUILayout.Space();
                GUILayout.Label(new GUIContent("Cant", "Waypoint.rollを設定します"));
                easyCantAngle = EditorGUILayout.FloatField(new GUIContent("Cant Angle", "カントの傾斜"), easyCantAngle);
                straightThreshold = EditorGUILayout.FloatField(new GUIContent("Cant Threshold", "曲線と見なすカーブ角度"), straightThreshold);
                if (GUILayout.Button("カントを設定する(簡易)"))
                {
                    ApplyEasyCantWaypoint(rail, straightThreshold,easyCantAngle);
                }
            }


            EditorGUILayout.Space();
            GUILayout.Label(new GUIContent("Miscs", "雑多なツール群です"), e);

            {
                // 選択レールから非選択レールへの接続数を調べる。ついでに接続位置とズレ角も
                int connectionCount = 0;
                Vector3 connectionPosition = Vector3.zero;
                float angleDiff = 0f;
                Vector3 sv = Vector3.zero;
                Vector3 ov = Vector3.zero;
                foreach (var rail in selectionRails)
                {
                    // 選択レールのprevは非選択レールへ接続している
                    if (rail.prev && otherRails.Contains(rail.prev))
                    {
                        connectionCount++;
                        connectionPosition = rail.cinemachinePath.EvaluatePositionAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized);

                        if (rail.prev.prev == rail)
                        {
                            // railのprev側と、rail.prevのprev側が接続している
                            sv = rail.cinemachinePath.EvaluateTangentAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized);
                            ov = rail.prev.cinemachinePath.EvaluateTangentAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized);
                        }
                        if (rail.prev.next == rail)
                        {
                            // railのprev側と、rail.prevのnext側が接続している
                            sv = rail.cinemachinePath.EvaluateTangentAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized);
                            ov = rail.prev.cinemachinePath.EvaluateTangentAtUnit(1f, CinemachinePathBase.PositionUnits.Normalized);
                        }
                    }

                    // 選択レールのnextは非選択レールへ接続している
                    if (rail.next && otherRails.Contains(rail.next))
                    {
                        connectionCount++;
                        connectionPosition = rail.cinemachinePath.EvaluatePositionAtUnit(1f, CinemachinePathBase.PositionUnits.Normalized);

                        if (rail.next.prev == rail)
                        {
                            // railのnext側と、rail.prevのprev側が接続している
                            sv = rail.cinemachinePath.EvaluateTangentAtUnit(1f, CinemachinePathBase.PositionUnits.Normalized);
                            ov = rail.next.cinemachinePath.EvaluateTangentAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized);
                        }
                        if (rail.next.next == rail)
                        {
                            // railのnext側と、rail.prevのnext側が接続している
                            sv = rail.cinemachinePath.EvaluateTangentAtUnit(1f, CinemachinePathBase.PositionUnits.Normalized);
                            ov = rail.next.cinemachinePath.EvaluateTangentAtUnit(1f, CinemachinePathBase.PositionUnits.Normalized);
                        }
                    }
                }
                if (connectionPosition != Vector3.zero) {
                    sv.y = 0;   // XZ平面でのみ見る
                    ov.y = 0;
                    if (Vector3.Cross(sv, ov) == Vector3.zero)
                    {
                        angleDiff = 180;
                    }
                    else
                    {
                        if (Vector3.Cross(sv, ov).z > 0)
                        {
                            angleDiff = -Vector3.Angle(ov, sv);
                        }
                        else
                        {
                            angleDiff = Vector3.Angle(ov, sv);
                        }
                    }
                    
                }

                using (new EditorGUI.DisabledScope(connectionCount != 1 && Selection.count == 1))
                {
                    if (GUILayout.Button("接続が1箇所の時、接続点のTangentに合わせて選択オブジェクトを回す"))
                    {
                        // 選択GameObjectを動かす(個々のレールではなく)
                        // TODO:複数選択ドラッグと同様に、同一階層制限はあっていい(がそんなにパターンは多くなさそう？破綻しても許せるそうな気もする)
                        foreach (var obj in Selection.gameObjects)
                        {
                            Undo.RecordObject(obj, "snap rotation/translate");
                            obj.transform.Translate(-connectionPosition, Space.World);
                            obj.transform.rotation *= Quaternion.AngleAxis(angleDiff, Vector3.up);
                            obj.transform.Translate(connectionPosition, Space.World);
                        }
                    }
                }
            }
            
            EditorGUILayout.Space();
            GUILayout.Box("", GUILayout.Height(3), GUILayout.ExpandWidth(true));

            // ----

            GUILayout.Label(new GUIContent("Visualize", "Sceneに補助情報を表示します"), e);

            isVisualizeActive = EditorGUILayout.Toggle(new GUIContent("Enable", "ビジュアライズ機能を有効化します"), isVisualizeActive);
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!isVisualizeActive))
            {
                GUILayout.Label(new GUIContent("All Rail", "シーン中のすべてのレールについて、詳細を表示します"));
                isNameAllVisible = EditorGUILayout.Toggle(new GUIContent("Show All Rail Name", "すべてのレールの名前を表示します"), isNameAllVisible);
                EditorGUILayout.Space();
                GUILayout.Label(new GUIContent("Selected Rail", "選択中のレールについて、詳細を表示します"));
                isSelectionHighLight = EditorGUILayout.Toggle(new GUIContent("Highlight Selection", "選択レールを強調表示します"), isSelectionHighLight);
                isNameVisible = EditorGUILayout.Toggle(new GUIContent("Show Rail Name", "レールの名前を表示します"), isNameVisible);
                isLengthVisible = EditorGUILayout.Toggle(new GUIContent("Show Length", "レールの長さを表示します"), isLengthVisible);
                isGradientVisible = EditorGUILayout.Toggle(new GUIContent("Show Gradient", "レール勾配を簡易表示します"), isGradientVisible);
                isPositionYVisible = EditorGUILayout.Toggle(new GUIContent("Show PositionY", "レールの標高、Y座標を表示します"), isPositionYVisible);
                EditorGUILayout.Space();
            }

            EditorGUILayout.Space();
            GUILayout.Box("", GUILayout.Height(3), GUILayout.ExpandWidth(true));

            // ----

            GUILayout.Label(new GUIContent("Rail Info", "選択中レールの情報を表示します"), e);

            GUILayout.Label(new GUIContent($"Selected Rail Count : {selectionRails.Count}", "選択中のレールの個数です"));
            float l = 0;
            foreach (var r in selectionRails)
            {
                l += r.cinemachinePath.PathLength;
            }
            GUILayout.Label(new GUIContent($"Selected Rail Length : {l.ToString("F2")} m", "選択中のレールの総距離です"));


            EditorGUILayout.Space();
            GUILayout.Box("", GUILayout.Height(3), GUILayout.ExpandWidth(true));

            // ----
            GUILayout.EndScrollView();

            // 設定が変わった時だけシーンビューを更新するのが筋だが、Window.OnGUI()のRepaintにしかトリガーされないっぽいので、毎回読んでしまっても許してくれ
            SceneView.lastActiveSceneView.Repaint();
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            tempCamera = sceneView.camera;
            if (isSnapActive) SnapGameObjectOnSceneGUI(sceneView);
            if(isVisualizeActive) showVisualize();
        }
        private void showVisualize() {         
            if (isNameAllVisible)
            {
                foreach (var rail in GameObject.FindObjectsOfType<Rail_Script>())
                {
                    if (selectionRails.Contains(rail)) continue;

                    var guiStyle = new GUIStyle { fontSize = 12, normal = { textColor = Color.white } };
                    if (isNameVisible)
                    {
                        Handles.Label(rail.cinemachinePath.EvaluatePositionAtUnit(0.5f, CinemachinePathBase.PositionUnits.Normalized), $"{rail.name}", guiStyle);
                    }
                }
            }

            foreach (var rail in selectionRails)
            {
                if (rail.cinemachinePath == null) continue;

                if (isSelectionHighLight)
                {
                    Handles.color = Color.cyan;

                    int steps = 50; // 曲線分割数
                    Vector3 prevPos = rail.cinemachinePath.EvaluatePositionAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized);

                    for (int i = 1; i <= steps; i++)
                    {
                        float st = (float)i / steps;
                        Vector3 pos = rail.cinemachinePath.EvaluatePositionAtUnit(st, CinemachinePathBase.PositionUnits.Normalized);
                        Handles.DrawAAPolyLine(15f, prevPos, pos);
                        prevPos = pos;
                    }
                }

                // GameObject名を表示
                string s = "";
                var guiStyle = new GUIStyle { fontSize = 16, normal = { textColor = Color.cyan } };
                if (isNameVisible)
                {
                    s += $"{rail.name}\n";
                }
                if (isLengthVisible)
                {
                    s += $"L {rail.cinemachinePath.PathLength.ToString("F2")} m\n";
                }
                if (isGradientVisible)
                {
                    Vector3 WaypointDiff = GetWaypointEndPosition(rail.cinemachinePath) - GetWaypointStartPosition(rail.cinemachinePath);
                    float averageGradient = WaypointDiff.y / rail.cinemachinePath.PathLength * 1000;
                    s += $"{averageGradient.ToString("F2")} ‰\n";
                }
                Handles.Label(rail.cinemachinePath.EvaluatePositionAtUnit(0.5f, CinemachinePathBase.PositionUnits.Normalized), s , guiStyle);

                if (isPositionYVisible)
                {
                    Vector3 st = rail.cinemachinePath.EvaluatePositionAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized);
                    Vector3 end = rail.cinemachinePath.EvaluatePositionAtUnit(1f, CinemachinePathBase.PositionUnits.Normalized);
                    Handles.Label(st, $"Y {st.y.ToString("F2")} m", guiStyle);
                    Handles.Label(end, $"Y {end.y.ToString("F2")} m", guiStyle);
                }
            }
        }
        private void SnapGameObjectOnSceneGUI(SceneView sceneView)
        {
            // 階層が違うオブジェクトを選んでる場合は警告表示をしてDisableにする
            if(selectionRails.Count == 0) { return; }   // レールが含まれなかったらそもそも何もしない
            bool isAllSibling = true;
            var activeObj = Selection.activeGameObject;
            foreach(var obj in Selection.gameObjects)
            {
                if(activeObj.transform.parent !=  obj.transform.parent) isAllSibling = false;
            }
            if (!isAllSibling)
            {
                Vector3 pos = Selection.activeGameObject.transform.position;
                var guiStyle = new GUIStyle { fontSize = 16, normal = { textColor = Color.red } };
                Handles.Label(pos, "RailSnalTool\n" +
                    "複数オブジェクトのスナップ処理は\n同一ヒエラルキー階層である必要があります", guiStyle);
                return;
            }


            // ドラック操作
            Event e = Event.current;

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                // ドラッグしたら、選択配下のRail_Scriptのうち、選択配下以外のレールへ繋がっている接続を外す
                if (autoConnect)
                {
                    foreach (var rail in selectionRails)
                    {
                        if (rail.prev && !selectionRails.Contains(rail.prev))
                        {
                            if (rail.prev.prev == rail)
                            {
                                Undo.RecordObject(rail.prev, "Disconnect rail");
                                rail.prev.prev = null;
                            }
                            if (rail.prev.next == rail)
                            {
                                Undo.RecordObject(rail.prev, "Disconnect rail");
                                rail.prev.next = null;
                            }
                            Undo.RecordObject(rail, "Disconnect rail");
                            rail.prev = null;
                        }
                        if (rail.next && !selectionRails.Contains(rail.next))
                        {
                            if (rail.next.prev == rail)
                            {
                                Undo.RecordObject(rail.next, "Disconnect rail");
                                rail.next.prev = null;
                            }
                            if (rail.next.next == rail)
                            {
                                Undo.RecordObject(rail.next, "Disconnect rail");
                                rail.next.next = null;
                            }
                            Undo.RecordObject(rail, "Disconnect rail");
                            rail.next = null;
                        }
                    }
                }

                // 改めてレールスナップ対象を更新する
                OnSelectionChanged();

                // スナップ対象を探す
                Vector3 diffDistance = new Vector3(99999, 99999, 99999);
                float screenDistance = Mathf.Infinity;
                Camera cam = sceneView.camera;
                foreach (var snapPos in selectionSnapPos)
                {
                    foreach(var otherPos in otherSnapPos)
                    {
                        // スクリーン座標に変換してから距離比較する。Zがマイナスの場合はカメラの後ろにいるので除外
                        Vector3 snapScreenPos = cam.WorldToScreenPoint(snapPos);
                        Vector3 otherScreenPos = cam.WorldToScreenPoint(otherPos);
                        float d = ((Vector2)(cam.WorldToScreenPoint(snapPos) - cam.WorldToScreenPoint(otherPos))).magnitude;
                        if (snapScreenPos.z > 0 && otherScreenPos.z > 0
                            && d < screenDistance)
                        {
                            diffDistance = otherPos - snapPos;
                            screenDistance = d;
                        }
                    }
                }

                //Debug.Log($"SNAP SCREEN DISTANCE {screenDistance}");
                // スナップ距離内ならスナップ処理(選択済みレールを移動)を行う
                if(screenDistance < snapDistance)
                {
                    // 選択済みGameObjectを動かす(個々のレールではなく)
                    foreach(var obj in Selection.gameObjects)
                    {
                        Undo.RecordObject(obj, "snap position");
                        obj.transform.position += diffDistance;
                    }

                    // 自動接続がオンならレール接続も行う
                    if (autoConnect)
                    {
                        foreach (var rail in selectionRails)
                        {
                            foreach (var otherRail in otherRails)
                            {
                                TryConnectRail(rail, otherRail);
                            }
                        }
                    }
                }
            }
        }

        private (Vector3 diffDistance,float screenDistance) searchSnapPosition(Camera cam,Vector3 pos)
        {
            Vector3 diffDistance = new Vector3(99999, 99999, 99999);
            float screenDistance = Mathf.Infinity;
            foreach (var otherPos in otherSnapPos)
            {
                // スクリーン座標に変換してから距離比較する。Zがマイナスの場合はカメラの後ろにいるので除外
                Vector3 snapScreenPos = cam.WorldToScreenPoint(pos);
                Vector3 otherScreenPos = cam.WorldToScreenPoint(otherPos);
                float d = ((Vector2)(cam.WorldToScreenPoint(pos) - cam.WorldToScreenPoint(otherPos))).magnitude;
                if (snapScreenPos.z > 0 && otherScreenPos.z > 0
                    && d < screenDistance)
                {
                    diffDistance = otherPos - pos;
                    screenDistance = d;
                }
            }
            return (diffDistance,screenDistance);
        }

        // レールとレールの接続を挑戦する。next,prevが空いていて、かつ距離がautoConnectDistance未満であれば接続する
        private void TryConnectRail(Rail_Script rail,Rail_Script otherRail)
        {
            if (!rail.cinemachinePath && !otherRail.cinemachinePath) return;

            CinemachinePathBase.PositionUnits u = CinemachinePathBase.PositionUnits.Normalized;
            if (!rail.prev)
            {
                if (!otherRail.prev
                    && (rail.cinemachinePath.EvaluatePositionAtUnit(0f, u) - otherRail.cinemachinePath.EvaluatePositionAtUnit(0f, u)).magnitude < autoConnectDistance)
                {
                    Undo.RecordObject(rail, "Connect rail");
                    rail.prev = otherRail;
                    Undo.RecordObject(otherRail, "Connect rail");
                    otherRail.prev = rail;
                }
                if (!otherRail.next
                    && (rail.cinemachinePath.EvaluatePositionAtUnit(0f, u) - otherRail.cinemachinePath.EvaluatePositionAtUnit(1f, u)).magnitude < autoConnectDistance)
                {
                    Undo.RecordObject(rail, "Connect rail");
                    rail.prev = otherRail;
                    Undo.RecordObject(otherRail, "Connect rail");
                    otherRail.next = rail;
                }
            }
            if (!rail.next)
            {
                if (!otherRail.prev
                    && (rail.cinemachinePath.EvaluatePositionAtUnit(1f, u) - otherRail.cinemachinePath.EvaluatePositionAtUnit(0f, u)).magnitude < autoConnectDistance)
                {
                    Undo.RecordObject(rail, "Connect rail");
                    rail.next = otherRail;
                    Undo.RecordObject(otherRail, "Connect rail");
                    otherRail.prev = rail;
                }
                if (!otherRail.next
                    && (rail.cinemachinePath.EvaluatePositionAtUnit(1f, u) - otherRail.cinemachinePath.EvaluatePositionAtUnit(1f, u)).magnitude < autoConnectDistance)
                {
                    Undo.RecordObject(rail, "Connect rail");
                    rail.next = otherRail;
                    Undo.RecordObject(otherRail, "Connect rail");
                    otherRail.next = rail;
                }
            }
        }

        private void CorrectWaypointTangentWithPrev(Rail_Script rail)
        {
            if (!rail.prev) return;

            if(rail.prev.next == rail)
            {
                Undo.RecordObject(rail, "Correct prev waypoint tangent");
                Debug.Log("Correct prev waypoint tangent");
                // 接続しているレールから見て、next側で接続しているなら、end側(1f)のタンジェントを持ってくる
                var v = rail.prev.cinemachinePath.EvaluateTangentAtUnit(1f, CinemachinePathBase.PositionUnits.Normalized);
                var originalTangent = rail.cinemachinePath.EvaluateTangentAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized);
                SetWaypointStartTangent(rail.cinemachinePath, rail.transform.InverseTransformDirection(v.normalized * originalTangent.magnitude));
            }
            if (rail.prev.prev == rail)
            {
                Undo.RecordObject(rail, "Correct prev waypoint tangent");
                Debug.Log("Correct next waypoint tangent");
                // 接続しているレールから見て、prev側で接続しているなら、start側(0f)のタンジェントを持ってくる
                var v = rail.prev.cinemachinePath.EvaluateTangentAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized);
                var originalTangent = rail.cinemachinePath.EvaluateTangentAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized);
                SetWaypointStartTangent(rail.cinemachinePath, rail.transform.InverseTransformDirection(-v.normalized * originalTangent.magnitude));
            }
            SceneView.lastActiveSceneView.Repaint();
        }

        private void CorrectWaypointTangentWithNext(Rail_Script rail)
        {
            if (!rail.next) return;

            if (rail.next.next == rail)
            {
                Undo.RecordObject(rail, "Correct next waypoint tangent");
                // 接続しているレールから見て、next側で接続しているなら、end側(1f)のタンジェントを持ってくる
                var v = rail.next.cinemachinePath.EvaluateTangentAtUnit(1f, CinemachinePathBase.PositionUnits.Normalized);
                var originalTangent = rail.cinemachinePath.EvaluateTangentAtUnit(1f, CinemachinePathBase.PositionUnits.Normalized);
                SetWaypointEndTangent(rail.cinemachinePath, rail.transform.InverseTransformDirection(-v.normalized * originalTangent.magnitude));
            }
            if (rail.next.prev == rail)
            {
                Undo.RecordObject(rail, "Correct next waypoint tangent");
                // 接続しているレールから見て、prev側で接続しているなら、start側(0f)のタンジェントを持ってくる
                var v = rail.next.cinemachinePath.EvaluateTangentAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized);
                var originalTangent = rail.cinemachinePath.EvaluateTangentAtUnit(1f, CinemachinePathBase.PositionUnits.Normalized);
                SetWaypointEndTangent(rail.cinemachinePath, rail.transform.InverseTransformDirection(v.normalized * originalTangent.magnitude));
            }
            SceneView.lastActiveSceneView.Repaint();
        }

        private void SmoothWaypointTangentToSlope(Rail_Script rail)
        {
            Undo.RecordObject(rail.cinemachinePath, "Correct waypoint tangent(Slope)");
            CinemachinePath c = rail.cinemachinePath as CinemachinePath;

            if (c == null)
            {
                EditorUtility.DisplayDialog("SnapTool", "この機能は(Smoothでない)CinemachinePathでしか利用できません", "OK");
                return;
            }
            for (int i = 0 ; i < c.m_Waypoints.Length ; i++){
                var current = c.m_Waypoints[i];
                var prev = c.m_Waypoints[i];
                float segmentLength = 0f;
                if (i > 0) {
                    prev = c.m_Waypoints[i - 1];
                    segmentLength += GetSegmentLength(rail.cinemachinePath, i - 1);
                }
                var next = c.m_Waypoints[i];
                if (i < c.m_Waypoints.Length - 1)
                {
                    next = c.m_Waypoints[i + 1];
                    segmentLength += GetSegmentLength(rail.cinemachinePath, i);
                }

                var yDiff = next.position.y - prev.position.y;
                var gradient = yDiff / segmentLength;
                var currentVectorXZ = new Vector3(current.tangent.x,0,current.tangent.z);

                c.m_Waypoints[i].tangent.y = gradient * currentVectorXZ.magnitude;
            }
        }

        private void SmoothWaypointHeightToSlope(Rail_Script rail)
        {
            Undo.RecordObject(rail.cinemachinePath, "Correct waypoint height(Slope)");
            CinemachinePath c = rail.cinemachinePath as CinemachinePath;

            if (c == null)
            {
                EditorUtility.DisplayDialog("SnapTool", "この機能は(Smoothでない)CinemachinePathでしか利用できません", "OK");
                return;
            }
            float startHeight = c.m_Waypoints[0].position.y;
            float endHeight = c.m_Waypoints[c.m_Waypoints.Length-1].position.y;
            float length = 0;

            for (int i = 0; i < c.m_Waypoints.Length; i++)
            {
                length += GetSegmentLength(rail.cinemachinePath, i);
            }
            float lengthSum = 0;
            for (int i = 0; i < c.m_Waypoints.Length; i++)
            {
                var current = c.m_Waypoints[i];
                var gradient = (endHeight - startHeight) / length;
                var currentVectorXZ = new Vector3(current.tangent.x, 0, current.tangent.z);

                lengthSum += GetSegmentLength(rail.cinemachinePath, i);
                var currentHeight = (endHeight - startHeight) / length * lengthSum + startHeight;
                //if (i == c.m_Waypoints.Length - 1) currentHeight = endHeight;
                c.m_Waypoints[i].position.y = currentHeight;
                c.m_Waypoints[i].tangent.y = gradient * currentVectorXZ.magnitude;
            }
        }
        private void ApplyEasyCantWaypoint(Rail_Script rail,float straightThreshold,float cantAngle)
        {
            Undo.RecordObject(rail.cinemachinePath, "Correct waypoint easy cant");
            CinemachinePath c = rail.cinemachinePath as CinemachinePath;

            c.m_Waypoints[0].roll = 0;
            c.m_Waypoints[c.m_Waypoints.Length - 1].roll = 0;
            for (int i = 1; i < c.m_Waypoints.Length - 1; i++)
            {
                Vector3 prevToCurrent = c.m_Waypoints[i].position - c.m_Waypoints[i - 1].position;
                Vector3 currentToNext = c.m_Waypoints[i + 1].position - c.m_Waypoints[i].position;
                prevToCurrent.Normalize();
                currentToNext.Normalize();
                if (Vector3.Angle(prevToCurrent,currentToNext) < straightThreshold)
                {
                    // 直線区間
                    c.m_Waypoints[i].roll = 0f;
                }
                else if (Vector3.Dot(Vector3.Cross(prevToCurrent,currentToNext), Vector3.up) < 0)
                {
                    // 右
                    c.m_Waypoints[i].roll = cantAngle;
                }
                else
                {
                    // 左
                    c.m_Waypoints[i].roll = -cantAngle;
                }

            }
        }


        // 選択内容が変更されたら、スナップすべき座標リストを計算しなおす
        private void OnSelectionChanged()
        {
            List<Rail_Script> selectRails = new List<Rail_Script>();
            foreach (var selectionObj in Selection.gameObjects)
            {
                Rail_Script rail = selectionObj.GetComponent<Rail_Script>();
                if (rail)
                {
                    selectRails.Add(rail);
                }
                foreach (var childRail in selectionObj.GetComponentsInChildren<Rail_Script>())
                {
                        selectRails.Add(childRail);
                }
            }

            selectionSnapPos = new List<Vector3>();
            otherSnapPos = new List<Vector3>();
            selectionRails = new List<Rail_Script>();
            otherRails = new List<Rail_Script>();
            foreach (var rail in GameObject.FindObjectsOfType<Rail_Script>())
            {
                if (selectRails.Contains(rail))
                {
                    // 選択しているなら、それはスナップ元座標になる
                    if (!rail.prev)
                    {
                        selectionSnapPos.Add(rail.cinemachinePath.EvaluatePositionAtUnit(0f, Cinemachine.CinemachinePathBase.PositionUnits.Normalized));
                    }
                    if (!rail.next)
                    {
                        selectionSnapPos.Add(rail.cinemachinePath.EvaluatePositionAtUnit(1f, Cinemachine.CinemachinePathBase.PositionUnits.Normalized));
                    }
                    selectionRails.Add(rail);
                }
                else
                {
                    // 選択してないなら、スナップ先座標になる
                    if (!rail.prev)
                    {
                        otherSnapPos.Add(rail.cinemachinePath.EvaluatePositionAtUnit(0f, Cinemachine.CinemachinePathBase.PositionUnits.Normalized));
                    }
                    if (!rail.next)
                    {
                        otherSnapPos.Add(rail.cinemachinePath.EvaluatePositionAtUnit(1f, Cinemachine.CinemachinePathBase.PositionUnits.Normalized));
                    }
                    otherRails.Add(rail);
                }
            }

            // アクティブなCinemachineのWaypointもメモっておく(ドラッグ検知のため)
            var c = Selection.activeGameObject?.GetComponent<CinemachinePathBase>();
            if(c != null)
            {
                prevWaypointStartPos = GetWaypointStartPosition(c);
                prevWaypointEndPos = GetWaypointEndPosition(c);
            }

            Repaint();
            //Debug.Log($"selectionSnapPos.Count {selectionSnapPos.Count}");
            //Debug.Log($"otherSnapPos.Count {otherSnapPos.Count}");
        }

        //
        // Cinemchine Waypoint Utils
        //
        private Vector3 GetWaypointStartPosition(CinemachinePathBase path)
        {
            if (path is CinemachinePath p) return p.m_Waypoints[0].position;
            if (path is CinemachineSmoothPath sp) return sp.m_Waypoints[0].position;
            return Vector3.zero;
        }

        private Vector3 GetWaypointEndPosition(CinemachinePathBase path)
        {
            if (path is CinemachinePath p) return p.m_Waypoints[p.m_Waypoints.Length - 1].position;
            if (path is CinemachineSmoothPath sp) return sp.m_Waypoints[sp.m_Waypoints.Length - 1].position;
            return Vector3.zero;
        }

        private static void SetWaypointStartPosition(CinemachinePathBase path, Vector3 pos)
        {
            if (path is CinemachinePath p)
            {
                var wp = p.m_Waypoints;
                wp[0].position = pos;
                p.m_Waypoints = wp;
            }
            else if (path is CinemachineSmoothPath sp)
            {
                var wp = sp.m_Waypoints;
                wp[0].position = pos;
                sp.m_Waypoints = wp;
            }
        }
        private static void SetWaypointEndPosition(CinemachinePathBase path, Vector3 pos)
        {
            if (path is CinemachinePath p)
            {
                var wp = p.m_Waypoints;
                wp[wp.Length - 1].position = pos;
                p.m_Waypoints = wp;
            }
            else if (path is CinemachineSmoothPath sp)
            {
                var wp = sp.m_Waypoints;
                wp[wp.Length - 1].position = pos;
                sp.m_Waypoints = wp;
            }
        }
        private static void SetWaypointStartTangent(CinemachinePathBase path, Vector3 tan)
        {
            if (path is CinemachinePath p)
            {
                var wp = p.m_Waypoints;
                wp[0].tangent = tan;
                p.m_Waypoints = wp;
            }
        }
        private static void SetWaypointEndTangent(CinemachinePathBase path, Vector3 tan)
        {
            if (path is CinemachinePath p)
            {
                var wp = p.m_Waypoints;
                wp[wp.Length - 1].tangent = tan;
                p.m_Waypoints = wp;
            }
        }
        private float GetSegmentLength(CinemachinePathBase path, int index, int steps = 50)
        {
            if (path == null) return 0f;
            if (index < 0 || index >= path.PathLength) return 0f;

            float start = path.FromPathNativeUnits(index, CinemachinePathBase.PositionUnits.PathUnits);
            float end = path.FromPathNativeUnits(index + 1, CinemachinePathBase.PositionUnits.PathUnits);

            Vector3 prev = path.EvaluatePosition(start);
            float length = 0f;

            for (int i = 1; i <= steps; i++)
            {
                float t = Mathf.Lerp(start, end, (float)i / steps);
                Vector3 curr = path.EvaluatePosition(t);
                length += Vector3.Distance(prev, curr);
                prev = curr;
            }

            return length;
        }
    }
}
#endif