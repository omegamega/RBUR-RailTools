#if UNITY_EDITOR 
using UnityEditor;
using UnityEngine;
using Cinemachine;
using System.Collections.Generic;
using frou01.RigidBodyTrain;
using UnityEngine.SceneManagement;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Ocsp;
using System.Diagnostics.Eventing.Reader;

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
        GameObject targetObject; // 探索対象のレール(自身か子にRail_Scriptを持つオブジェクト)
        private Vector2 scrollPosition = Vector2.zero;

        private bool showRailendInfo = true;    // 終端レールのinfoを表示する

        [MenuItem("Tools/OmegaExpDesign - RBUR RailTools/RailChecker")]
        public static void Open()
        {
            GetWindow<RailChecker>("RailChecker");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("prev,nextのないレールが見つかった時、この距離内のレールをサジェストします");
            searchSuggestDistance = EditorGUILayout.FloatField("未接続レール探索距離", searchSuggestDistance);
            EditorGUILayout.LabelField("走査対象。Noneの場合は全探索、指定した場合はその配下を探索します");
            targetObject = (GameObject)EditorGUILayout.ObjectField("テスト対象のRail", targetObject, typeof(GameObject),true);

            if (GUILayout.Button("チェック実行"))
            {
                StartCheckRail(targetObject);
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

        private void StartCheckRail(GameObject targetObject)
        {   
            resultCount = 0;
            railResults = new List<(GameObject, GameObject, string, MessageType, FixAction)>();

            if (targetObject != null)
            {
                CheckRail(targetObject);
            }
            else
            {
                var scene = SceneManager.GetActiveScene();
                var rootObjects = scene.GetRootGameObjects();
                foreach (var obj in rootObjects)
                {
                    CheckRail(obj);
                }
            }
        }


        private void CheckRail(GameObject targetObject,bool root = true)
        {
            if(root){
                Rail_Script rail = targetObject.GetComponent<Rail_Script>();
                if (rail != null)
                {
                    CheckRailAndWaypoint(rail);
                }
            }

            foreach (Transform child in targetObject.transform)
            {
                Rail_Script rail = child.GetComponent<Rail_Script>();
                if (rail != null)
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

            if (start == null || end == null) return;
        
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
                    // ポイントレールの場合、単方向接続しているのは正しいのでここでは簡易的にパスする(もうちょっと丁寧に検知してもいいかもしれない)
                    if (!isPointRail(rail)) {
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
                    if (!isPointRail(rail))
                    {
                        railResults.Add((rail.gameObject, rail.next.gameObject, "nextはあるけど、そのレールはこのレールに繋がっていない\n" + rail.prev.gameObject.name + "と片方向のみ繋がってるよ！", MessageType.Error, FixAction.ConnectEach));
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
                    if ((position - start).magnitude < searchSuggestDistance && r.prev == null)
                    {
                        return (r, RailWaypoint.Start);
                    }
                    if ((position - end).magnitude < searchSuggestDistance && r.next == null)
                    {
                        return (r, RailWaypoint.End);
                    }
                }
            }
            return (null, RailWaypoint.None);
        }

        private bool isPointRail(Rail_Script rail)
        {
            foreach (var point in GameObject.FindObjectsOfType<Point_Script>()){
                if(point.pointPrevRail == rail || point.pointPrevRail_sub == rail || point.pointNextRail1 == rail || point.pointNextRail2 == rail)
                {
                    return true;
                }
            }
            return false;
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