#if UNITY_EDITOR 
using Cinemachine;
using frou01.RigidBodyTrain;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace omegaExpDesign.RBURTool
{
    public class TrainRerailer : EditorWindow
    {
        private Train targetTrain = null;
        private bool isAutoCoupling = false;
        private float rerailDistance = 5f;
        private bool isForceRerail = false;
//        private float coupleDistance = 5f;
        private List<(Train train, Train train2, Rail_Script rail, string message, MessageType type)> results = null;

        [MenuItem("Tools/OmegaExpDesign - RBUR RailTools/TrainRerailer")]
        public static void Open()
        {
            GetWindow<TrainRerailer>("TrainRerailer");
        }

        private void OnGUI()
        {
            var e = new GUIStyle(EditorStyles.label);
            e.fontSize = 18;
            GUILayout.Label(new GUIContent("Train Rerailer", "レールに車両を載せます"), e);

            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.LabelField("Play中は操作できません");
                return;
            }

            EditorGUILayout.LabelField("対象の車両をレールに乗せます。Noneの場合はすべての車両、指定した場合はその車両を乗せます");
            targetTrain = (Train)EditorGUILayout.ObjectField("Target Train", targetTrain, typeof(Train), true);
            EditorGUILayout.LabelField("既にレールに乗っていても、探索し直してレールに乗せ直します");
            isForceRerail = EditorGUILayout.Toggle("ForceRerail", isForceRerail);
            //            isAutoCoupling = EditorGUILayout.Toggle("AutoCoupling", isAutoCoupling);
            //            coupleDistance = EditorGUILayout.FloatField("Distance to couple", coupleDistance);
            string buttonText = isAutoCoupling ? "Rerail & couple train" : "Rerail train";
            if (GUILayout.Button(buttonText))
            {
                results = new List<(Train, Train, Rail_Script, string, MessageType)>();

                if (targetTrain != null)
                {
                    GameObject trainParent = targetTrain.transform.parent?.gameObject; // Trainの親オブジェクトを操作する
                    if (trainParent == null)
                    {
                        results.Add((null, null, null, "Trainの親オブジェクトの取得に失敗しました", MessageType.Error));
                    }
                    else 
                    {
                        // TODO:            coupleTrain(targetTrain);
                        // レールに乗せる
                        if ((targetTrain.BogieRail_B == null && targetTrain.BogieRail_F == null) || isForceRerail)
                        {
                            rerailTrain(targetTrain, trainParent);
                            Debug.Log("rerailed " + trainParent.name);
                        }
                        else if (targetTrain.BogieRail_B && targetTrain.BogieRail_F)
                        {
                            // レールにすでに乗っている
                        }
                        else
                        {
                            results.Add((targetTrain, null, null, "Trainの台車(bogie)が片側だけ設定されています", MessageType.Warning));
                        }
                    }
                }
                else
                {
                    foreach (var train in GameObject.FindObjectsOfType<Train>())
                    {

                        GameObject trainParent = train.transform.parent?.gameObject; // Trainの親オブジェクトを操作する
                        if (trainParent == null)
                        {
                            results.Add((null, null, null, "Trainの親オブジェクトの取得に失敗しました", MessageType.Error));
                        }
                        else
                        {
                            if ((train.BogieRail_B == null && train.BogieRail_F == null) || isForceRerail)
                            {
                                rerailTrain(train,trainParent);
                                Debug.Log("rerailed " + trainParent.name);
                            }
                            else if(train.BogieRail_B && train.BogieRail_F)
                            {
                                // レールにすでに乗っている
                            }
                            else
                            {
                                results.Add((train, null, null, "Trainの台車(bogie)が片側だけ設定されています", MessageType.Warning));
                            }
                        }
                    }
                }
                if(results.Count == 0)
                {
                    results.Add((null, null, null, "リレールすべき車両はありませんでした", MessageType.Info));
                }
            }

            if (results != null)
            {
                foreach (var result in results)
                {
                    if (result.train && result.rail)
                    {
                        EditorGUILayout.HelpBox($"{result.train.transform.parent.name} を {result.rail.name}に " + result.message, result.type);
                    }
                    else if (result.train && result.train2)
                    {
                        EditorGUILayout.HelpBox($"{result.train.transform.parent.name} {result.train.transform.parent.name} " + result.message, result.type);
                    }
                    else if (result.train)
                    {
                        EditorGUILayout.HelpBox($"{result.train.transform.parent.name}" + result.message, result.type);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox(result.message, result.type);
                    }
                }
            }
            
        }

        // 対象の車両をレールに乗せる
        public void rerailTrain(Train targetTrain,GameObject trainParent)
        {
            Rail_Script rail = null;
            Vector3 point = Vector3.zero;
            float minDist;
            Vector3 tangent = Vector3.zero;
            (rail, point, minDist, tangent) = FindClosestRail(trainParent.transform.position);

            if (minDist < rerailDistance && rail)
            {
                // 親オブジェクトごと車両を移動させる
                Undo.RecordObject(trainParent.transform, "Snap to rail(move)");
                trainParent.transform.position = point + new Vector3(0f, 0f, 0f);   // 0着地させるとレールにのらない現象があった謎(BogieRail_F/Rがnullになる
                Undo.RecordObject(targetTrain, "Snap to rail(set rail)");
                targetTrain.BogieRail_F = rail;
                targetTrain.BogieRail_B = rail;
                EditorUtility.SetDirty(targetTrain);

                // 車両を回転させる
                Vector3 direction = targetTrain.CouplerF.transform.position - targetTrain.CouplerB.transform.position; // 連結器方向から方向を絞る
                if (Vector3.Dot(direction, tangent) > 0)
                {
                    // 線路のタンジェントと車両の方向が同じ
                    trainParent.transform.rotation = Quaternion.FromToRotation(Vector3.forward, tangent);
                }
                else
                {
                    // 線路のタンジェントと車両の方向が逆
                    trainParent.transform.rotation = Quaternion.FromToRotation(Vector3.back, tangent);
                }

                results.Add((targetTrain, null ,rail, "車両をレールに乗せました", MessageType.Info));
            }
            else
            {
                results.Add((targetTrain, null, null, "車両の近くにレールがないようです", MessageType.Error));
            }
        }

        // 最も近いレールを探す
        private (Rail_Script,Vector3 point,float distance, Vector3 tangent)FindClosestRail(Vector3 target,int resolution = 10)
        {
            Rail_Script closestRail = null;
            Vector3 closestPoint = Vector3.zero;
            float minDist = Mathf.Infinity;
            Vector3 tangent = Vector3.zero;

            foreach (var rail in GameObject.FindObjectsOfType<Rail_Script>())
            {
                // パス上の最近点（Normalized で返す）
                float t = rail.cinemachinePath.FindClosestPoint(target, 0, -1, resolution);

                // ワールド座標に変換
                Vector3 pos = rail.cinemachinePath.EvaluatePositionAtUnit(t, CinemachinePathBase.PositionUnits.PathUnits);

                float dist = Vector3.Distance(target, pos);
                if (dist < minDist)
                {
                    minDist = dist;
                    closestRail = rail;
                    closestPoint = pos;
                    tangent = rail.cinemachinePath.EvaluateTangentAtUnit(t, CinemachinePathBase.PositionUnits.PathUnits);
                }
            }

            return (closestRail, closestPoint, minDist,tangent);
        }

        // シーン全体を探索し、対象の車両に連結する
        // TODO:ちゃんと機能するようにする。ブレーキ弁も開く。分離処理はどうする？いっそRerailer含めてCustomEditorにした方がいいかも
        public void coupleTrain(Train train)
        {
            if(train.CouplerF && train.CouplerF.connectedCoupler == null)
            {
                var closestCoupler = FindClosestCoupler(train.CouplerF);
                if (closestCoupler.coupler)
                {
                    Undo.RecordObject(train.CouplerF, "CouplerObj couple");
                    Undo.RecordObject(closestCoupler.coupler, "CouplerObj couple");
                    train.CouplerF = closestCoupler.coupler;
                    closestCoupler.coupler.connectedCoupler = train.CouplerF;

                    if(closestCoupler.coupler.TrainScript == null)
                    {
                        results.Add((closestCoupler.coupler.TrainScript, null, null, $"{closestCoupler.coupler.name} のTrainScriptがnullです", MessageType.Warning));
                        results.Add((closestCoupler.coupler.TrainScript, null, null, $"{train.transform.parent.name} と不明な車両を連結しました", MessageType.Info));
                    }
                    else
                    {
                        results.Add((closestCoupler.coupler.TrainScript, null, null, "車両同士を連結しました", MessageType.Info));
                    }
                }
            }
            if (train.CouplerB && train.CouplerB.connectedCoupler == null)
            {
                var closestCoupler = FindClosestCoupler(train.CouplerB);
                if (closestCoupler.coupler)
                {
                    Undo.RecordObject(train.CouplerB, "CouplerObj couple");
                    Undo.RecordObject(closestCoupler.coupler, "CouplerObj couple");
                    train.CouplerB = closestCoupler.coupler;
                    closestCoupler.coupler.connectedCoupler = train.CouplerB;

                    if (closestCoupler.coupler.TrainScript == null)
                    {
                        results.Add((closestCoupler.coupler.TrainScript, null, null, $"{closestCoupler.coupler.name} のTrainScriptがnullです", MessageType.Warning));
                        results.Add((closestCoupler.coupler.TrainScript, null, null, $"{train.transform.parent.name} と不明な車両を連結しました", MessageType.Info));
                    }
                    else
                    {
                        results.Add((closestCoupler.coupler.TrainScript, null, null, $"{train.transform.parent.name} {closestCoupler.train.name}を連結しました", MessageType.Info));
                    }
                }
            }
        }

        // 最も近い連結器を探す
        private (CouplerObj coupler,float minDistance, Train train) FindClosestCoupler(CouplerObj targetCoupler)
        {
            CouplerObj resultCoupler = null;
            float minDistance = Mathf.Infinity;
            Train resultTrain = null;
            foreach (var train in GameObject.FindObjectsOfType<Train>())
            {
                if (targetCoupler.TrainScript == train) continue;

                if (train.CouplerF && train.CouplerF.connectedCoupler == null)
                {
                    float dist = Vector3.Distance(targetCoupler.gameObject.transform.position, train.CouplerF.gameObject.transform.position);
                    if(dist < minDistance)
                    {
                        resultCoupler = train.CouplerF;
                        minDistance = dist;
                        resultTrain = train;
                    }
                }

                if (train.CouplerB && train.CouplerB.connectedCoupler == null)
                {
                    float dist = Vector3.Distance(targetCoupler.gameObject.transform.position, train.CouplerB.gameObject.transform.position);
                    if (dist < minDistance)
                    {
                        resultCoupler = train.CouplerB;
                        minDistance = dist;
                        resultTrain = train;
                    }
                }
            }
            return (resultCoupler, minDistance, resultTrain);
        }
        
    }
}

#endif