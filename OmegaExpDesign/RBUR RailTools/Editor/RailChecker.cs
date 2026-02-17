#if UNITY_EDITOR 
using UnityEditor;
using UnityEngine;
using Cinemachine;
using System.Collections.Generic;
using frou01.RigidBodyTrain;
using System;
using UnityEngine.SceneManagement;
using System.Linq;
using System.Reflection;

namespace omegaExpDesign.RBURTool
{
    public class RailChecker : EditorWindow
    {
        enum RailWaypoint{
            None,
            Start,
            End
        }

        enum FixAction {
            None,
            ConnectEach
        }

        private float searchSuggestDistance = 1.0f;    // 近いレールを探す範囲
        private float connectedDistance = 0.01f;      // 正しく接続されている判定をするレール終端距離
        private List<(CinemachinePathBase a, CinemachinePathBase b, float distance)> results;
        private List<(GameObject rail, GameObject targetRail, string message, MessageType messageType,FixAction fixAction)> railResults;
        private int resultCount = 0;
        private Vector2 scrollPosition = Vector2.zero;

        private bool showRailendInfo = true;    // 終端レールのinfoを表示する

        [MenuItem("Tools/OmegaExpDesign - RBUR RailTools/RailChecker")]
        public static void Open()
        {
            GetWindow<RailChecker>("RailChecker");
        }

        private void OnGUI()
        {
            var e = new GUIStyle(EditorStyles.label);
            e.fontSize = 18;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent("Rail Checker", "レールの接続を精査して、問題がありそうな箇所を列挙します"), e);
                if (GUILayout.Button("Wikiを開く/Show Wiki", EditorStyles.linkLabel))
                {
                    Application.OpenURL("https://github.com/omegamega/RBUR-RailTools/wiki/RailSnapTool");
                }
            }

            EditorGUILayout.LabelField("prev,nextのないレールが見つかった時、この距離内のレールをサジェストします");
            searchSuggestDistance = EditorGUILayout.FloatField("未接続レール探索距離", searchSuggestDistance);

            if (GUILayout.Button("チェック実行"))
            {
                StartCheckRail();
            }

            if(railResults != null)
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                showRailendInfo = EditorGUILayout.Toggle("終端レールのinfoを表示する", showRailendInfo);

                EditorGUILayout.HelpBox($"走査レール総数 : {resultCount}", MessageType.None);

                var count = 0;
                foreach (var r in railResults)
                {
                    if (r.rail == null) continue;
                    if (!showRailendInfo && r.messageType == MessageType.Info) continue;

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.HelpBox($"{r.rail.gameObject.name} : {r.message}", r.messageType, true);

                        if (GUILayout.Button("選択", GUILayout.Width(80)))
                        {
                            Selection.activeObject = r.rail.gameObject;
                            EditorGUIUtility.PingObject(r.rail.gameObject);
                        }
                        if (r.targetRail != null)
                        {
                            if (GUILayout.Button("相手レール", GUILayout.Width(80)))
                            {
                                Selection.activeObject = r.targetRail.gameObject;
                                EditorGUIUtility.PingObject(r.targetRail.gameObject);
                            }
                        }
                        /* 
                        if(r.fixAction != FixAction.None)
                        {
                            if (GUILayout.Button("AutoFix(TODO)", GUILayout.Width(80)))
                            {
                                Debug.Log("AutoFix Button");
                            }
                        }*/
                    }
                    count++;
                }
                if(count == 0)
                {
                    EditorGUILayout.HelpBox("問題は見つかりませんでした", MessageType.Info);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void StartCheckRail()
        {   
            resultCount = 0;
            railResults = new List<(GameObject, GameObject, string, MessageType, FixAction)>();

            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            foreach (var obj in rootObjects)
            {
                CheckRail(obj);
            }
        }


        private void CheckRail(GameObject targetObject,bool root = true)
        {
            if(root){
                Rail_Script rail = targetObject.GetComponent<Rail_Script>();
                if (rail != null && targetObject.activeInHierarchy)
                {
                    CheckRailAndWaypoint(rail);
                }
            }

            foreach (Transform child in targetObject.transform)
            {
                Rail_Script rail = child.GetComponent<Rail_Script>();
                if (rail != null && targetObject.activeInHierarchy)
                {
                    CheckRailAndWaypoint(rail);
                }
                CheckRail(child.gameObject,false);
            }
            return;
        }

        private void CheckRailAndWaypoint(Rail_Script rail)
        {
             TryGetEndpoints(rail.cinemachinePath, out var start, out var end);

            // 何故かCinemachinePathの始点終点が取れないレールがある
            if (start == null || end == null)
            {
                railResults.Add((rail.gameObject, null, "レールのCinemachinePathが取得できません。\nPathがセットされてないのかも？", MessageType.Error, FixAction.None));
                return;
            }

            // XまたはZスケールが反転(ミラー)しているレールはRBURでは正常に動作しない
            var v = rail.transform.lossyScale;
            if(v.x < 0 || v.z < 0)
            {
                railResults.Add((rail.gameObject, null, "レールのXまたはZスケールがマイナスになっています", MessageType.Error, FixAction.None));
            }
            
            // 単一のGameObjectに複数のRail_Scriptが付いているのは良くない
            {
                var siblingRails = rail.gameObject.GetComponents<Rail_Script>();
                if(siblingRails.Length > 1)
                {
                    railResults.Add((rail.gameObject, null, "単一のGameObjectに複数のRail_Scriptが付いてます。\nGameObjectとRail_Scriptは1:1対応しない場合、挙動は保証できません。", MessageType.Error, FixAction.None));
                }
            }

            // Rail_ScriptとそのCinemachinePathが異なるGameObjectについているのは良くない
            if(rail.gameObject != rail.cinemachinePath.gameObject)
            {
                railResults.Add((rail.gameObject, null, "Rail_ScriptとCinemachinePathが異なるGameObjectについています。\nGameObjectとRail_Scriptが別のGameObjectについている場合、挙動は保証できません。", MessageType.Error, FixAction.None));
            }

            if (rail.prev == null)
            {
                // prevがない
                // prev/path最初から近いレールを探す
                var ret = searchNearRailend(start, rail);
                if (ret.rail == null)
                {
                    // prev/path最初の近くにレール終端がない
                    railResults.Add((rail.gameObject, null, "prevがありません。終端レールかも？", MessageType.Info, FixAction.None));
                }
                else
                {
                    // prevの近くにレール終端がある
                    if (ret.rail.prev == rail)
                    {
                        railResults.Add((rail.gameObject, ret.rail.gameObject, "prevがないけど、近くにprevがこのレールになってるレールがある\n" + ret.rail.gameObject.name + "と片方向のみ繋がってるよ！", MessageType.Error, FixAction.ConnectEach));
                    }
                    else if (ret.rail.next == rail)
                    {
                        railResults.Add((rail.gameObject, ret.rail.gameObject, "prevがないけど、近くにnextがこのレールになってるレールがある\n" + ret.rail.gameObject.name + "と片方向のみ繋がってるよ！", MessageType.Error, FixAction.ConnectEach));
                    }
                    else
                    {
                        railResults.Add((rail.gameObject, ret.rail.gameObject, "prevがないけど、近くにレール端がある\n" + ret.rail.gameObject.name + "と繋いでるつもりだった？", MessageType.Warning, FixAction.ConnectEach));
                    }
                }
            } else if(rail.prev == rail)
            {
                // prevがなぜか自分自身を指している。どうして？
                railResults.Add((rail.gameObject, null, "prevが自分自身を指しているよ！", MessageType.Error, FixAction.None /* TODO */));
            }
            else
            {
                // prevはある。prevレールはこのレールにちゃんと繋がっているのか
                TryGetEndpoints(rail.prev.cinemachinePath, out var prevStart, out var prevEnd);
                if (rail.prev.prev == rail)
                {
                    // prevレールはprev(start)側でこのレールと接続しているらしい
                    if ((prevStart - start).magnitude > connectedDistance)
                    {
                        // 外れてるっぽいぞ
                        railResults.Add((rail.gameObject, rail.prev.gameObject, "prevと接続しているレールが離れている", MessageType.Error, FixAction.None));
                    }
                }
                else if (rail.prev.next == rail)
                {
                    // prevレールはnext(end)側でこのレールと接続しているらしい
                    if ((prevEnd - start).magnitude > connectedDistance)
                    {
                        // 外れてるっぽいぞ
                        railResults.Add((rail.gameObject, rail.prev.gameObject, "prevと接続しているレールが離れている", MessageType.Error, FixAction.None));
                    }
                }
                else
                {
                    // prevはこちらに接続してない。つまり単方向じゃん
                    // TODO:ポイントレールの場合、単方向になってるのは正しいが判定がムズイ
                    if (!isSwitchableRail(rail)) {
                        railResults.Add((rail.gameObject, rail.prev.gameObject, "prevはあるけど、そのレールはこのレールに繋がっていない\n" + rail.prev.gameObject.name + "と片方向のみ繋がってるよ！", MessageType.Error, FixAction.ConnectEach));
                    }
                }
            }


            if (rail.next == null)
            {
                // nextがない
                // next/path終端から近いレールを探す
                var ret = searchNearRailend(end, rail);
                if (ret.rail == null)
                {
                    // nextの近くにレール終端がない
                    railResults.Add((rail.gameObject, null, "nextがありません。終端レールかも？", MessageType.Info, FixAction.None));
                }
                else
                {
                    // prevの近くにレール終端がある
                    if (ret.rail.prev == rail)
                    {
                        railResults.Add((rail.gameObject, ret.rail.gameObject, "nextがないけど、近くにprevがこのレールになってるレールがある\n" + ret.rail.gameObject.name + "と片方向のみ繋がってるよ！", MessageType.Error, FixAction.ConnectEach));
                    }
                    else if (ret.rail.next == rail)
                    {
                        railResults.Add((rail.gameObject, ret.rail.gameObject, "nextがないけど、近くにnextがこのレールになってるレールがある\n" + ret.rail.gameObject.name + "と片方向のみ繋がってるよ！", MessageType.Error, FixAction.ConnectEach));
                    }
                    else
                    {
                        railResults.Add((rail.gameObject, ret.rail.gameObject, "nextがないけど、近くにレール端がある\n" + ret.rail.gameObject.name + "と繋いでるつもりだった？", MessageType.Warning, FixAction.ConnectEach));
                    }
                    
                }
            }else if (rail.next == rail)
            {
                // nextがなぜか自分自身を指している。どうして？
                railResults.Add((rail.gameObject, null, "nextが自分自身を指しているよ！", MessageType.Error, FixAction.None /* TODO */));
            }
            else
            {
                // nextはある。nextレールはこのレールにちゃんと繋がっているのか
                TryGetEndpoints(rail.next.cinemachinePath, out var prevStart, out var prevEnd);
                if (rail.next.prev == rail)
                {
                    // nextレールはprev(start)側でこのレールと接続しているらしい
                    if ((prevStart - end).magnitude > connectedDistance)
                    {
                        // 外れてるっぽいぞ
                        railResults.Add((rail.gameObject, rail.next.gameObject, "nextと接続しているレールが離れている", MessageType.Error, FixAction.None));
                    }
                }else if (rail.next.next == rail)
                {
                    // nextレールはnext(end)側でこのレールと接続しているらしい
                    if ((prevEnd - end).magnitude > connectedDistance)
                    {
                        // 外れてるっぽいぞ
                        railResults.Add((rail.gameObject, rail.next.gameObject, "nextと接続しているレールが離れている", MessageType.Error, FixAction.None));
                    }
                }
                else
                {
                    // nextはこちらに接続してない。つまり単方向じゃん
                    // ポイントレールの場合、単方向接続しているのは正しいのでここでは簡易的にパスする
                    if (!isSwitchableRail(rail))
                    {
                        railResults.Add((rail.gameObject, rail.next?.gameObject, "nextはあるけど、そのレールはこのレールに繋がっていない\n" + rail.prev?.gameObject.name + "と片方向のみ繋がってるよ！", MessageType.Error, FixAction.ConnectEach));
                    }
                }

            }


            resultCount++;    
            return;
        }

        private (Rail_Script rail, RailWaypoint waypoint) searchNearRailend(Vector3 position, Rail_Script exclusion = null)
        {
            // 他のレールのエンドポイントを探す
            var allRails = GameObject.FindObjectsOfType<Rail_Script>();
            foreach(var r in allRails)
            {
                if (r == exclusion) continue;

                if (TryGetEndpoints(r.cinemachinePath, out var start, out var end))
                {
                    //Debug.Log(r.gameObject.name + " start" + (position - start).magnitude + " end" + (position - end).magnitude);
                    if ((position - start).magnitude < searchSuggestDistance)
                    {
                        return (r, RailWaypoint.Start);
                    }
                    if ((position - end).magnitude < searchSuggestDistance)
                    {
                        return (r, RailWaypoint.End);
                    }
                }
            }
            return (null, RailWaypoint.None);
        }

        // Switchableレール(独自用語)は、Point/TurnTable/PointLever_Setter(5.0以降)のような、接続先が切り替わるレール。
        private bool isSwitchableRail(Rail_Script rail)
        {
            // Point_Script
            foreach (var point in GameObject.FindObjectsOfType<Point_Script>())
            {
                if (point.pointPrevRail == rail || point.pointPrevRail_sub == rail || point.pointNextRail1 == rail || point.pointNextRail2 == rail)
                {
                    return true;
                }
            }

            // TurnTable_Contoller
            foreach (var turnTable in GameObject.FindObjectsOfType<TurnTable_Controller>())
            {
                if (turnTable.targets.Contains(rail) || turnTable.mine == rail)
                {
                    return true;
                }
            }

            // PointLever_SetterはRBUR 5.0.0以降の実装なので、Reflectionを使ってRBUR b3.4との互換性を確保した
            Type t = FindType("frou01.RigidBodyTrain.PointLever_Setter");
            if (t != null)
            {
                foreach (var point in GameObject.FindObjectsByType(t, FindObjectsSortMode.None))
                {
                    FieldInfo from1Info = t.GetField("from1", BindingFlags.Public | BindingFlags.Instance);
                    FieldInfo from2Info = t.GetField("from2", BindingFlags.Public | BindingFlags.Instance);
                    FieldInfo to1Info = t.GetField("to1", BindingFlags.Public | BindingFlags.Instance);
                    FieldInfo to2Info = t.GetField("to2", BindingFlags.Public | BindingFlags.Instance);
                    if ((Rail_Script)from1Info.GetValue(point) == rail || (Rail_Script)from2Info.GetValue(point) == rail || (Rail_Script)to1Info.GetValue(point) == rail || (Rail_Script)to2Info.GetValue(point) == rail)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null)
                    return t;
            }
            return null;
        }
        private void CheckWaypoints()
        {
            results = new List<(CinemachinePathBase, CinemachinePathBase, float)>();

            var allPaths = GameObject.FindObjectsOfType<CinemachinePathBase>();
            var endpoints = new List<(CinemachinePathBase path, Vector3 start, Vector3 end)>();

            // 端点の収集
            foreach (var path in allPaths)
            {
                if (TryGetEndpoints(path, out var start, out var end))
                {
                    endpoints.Add((path, start, end));
                }
            }

            // 全組み合わせを比較
            for (int i = 0; i < endpoints.Count; i++)
            {
                for (int j = i + 1; j < endpoints.Count; j++)
                {
                    // 端点の全組み合わせ
                    float dist1 = Vector3.Distance(endpoints[i].start, endpoints[j].start);
                    float dist2 = Vector3.Distance(endpoints[i].start, endpoints[j].end);
                    float dist3 = Vector3.Distance(endpoints[i].end, endpoints[j].start);
                    float dist4 = Vector3.Distance(endpoints[i].end, endpoints[j].end);

                    float minDist = Mathf.Min(dist1, dist2, dist3, dist4);

                    if (minDist <= searchSuggestDistance)
                    {
                        results.Add((endpoints[i].path, endpoints[j].path, minDist));
                    }
                }
            }

            // 近い順にソート
            results.Sort((a, b) => a.distance.CompareTo(b.distance));
        }

        /// <summary>
        /// CinemachinePath / CinemachineSmoothPath の両端点を取得する
        /// </summary>
        private bool TryGetEndpoints(CinemachinePathBase path, out Vector3 start, out Vector3 end)
        {
            start = end = Vector3.zero;

            if (!path) return false;

            if (path is CinemachinePath p && p.m_Waypoints.Length > 0)
            {
                start = p.transform.TransformPoint(p.m_Waypoints[0].position);
                end = p.transform.TransformPoint(p.m_Waypoints[p.m_Waypoints.Length - 1].position);
                return true;
            }
            else if (path is CinemachineSmoothPath sp && sp.m_Waypoints.Length > 0)
            {
                start = sp.transform.TransformPoint(sp.m_Waypoints[0].position);
                end = sp.transform.TransformPoint(sp.m_Waypoints[sp.m_Waypoints.Length - 1].position);
                return true;
            }
            return false;
        }
    }

}
#endif