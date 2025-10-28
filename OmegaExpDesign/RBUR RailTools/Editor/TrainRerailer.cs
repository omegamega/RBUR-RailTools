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
            GUILayout.Label(new GUIContent("Train Rerailer", "���[���Ɏԗ����ڂ��܂�"), e);

            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.LabelField("Play���͑���ł��܂���");
                return;
            }

            EditorGUILayout.LabelField("�Ώۂ̎ԗ������[���ɏ悹�܂��BNone�̏ꍇ�͂��ׂĂ̎ԗ��A�w�肵���ꍇ�͂��̎ԗ����悹�܂�");
            targetTrain = (Train)EditorGUILayout.ObjectField("Target Train", targetTrain, typeof(Train), true);
            EditorGUILayout.LabelField("���Ƀ��[���ɏ���Ă��Ă��A�T���������ă��[���ɏ悹�����܂�");
            isForceRerail = EditorGUILayout.Toggle("ForceRerail", isForceRerail);
            //            isAutoCoupling = EditorGUILayout.Toggle("AutoCoupling", isAutoCoupling);
            //            coupleDistance = EditorGUILayout.FloatField("Distance to couple", coupleDistance);
            string buttonText = isAutoCoupling ? "Rerail & couple train" : "Rerail train";
            if (GUILayout.Button(buttonText))
            {
                results = new List<(Train, Train, Rail_Script, string, MessageType)>();

                if (targetTrain != null)
                {
                    GameObject trainParent = targetTrain.transform.parent?.gameObject; // Train�̐e�I�u�W�F�N�g�𑀍삷��
                    if (trainParent == null)
                    {
                        results.Add((null, null, null, "Train�̐e�I�u�W�F�N�g�̎擾�Ɏ��s���܂���", MessageType.Error));
                    }
                    else 
                    {
                        // TODO:            coupleTrain(targetTrain);
                        // ���[���ɏ悹��
                        if ((targetTrain.BogieRail_B == null && targetTrain.BogieRail_F == null) || isForceRerail)
                        {
                            rerailTrain(targetTrain, trainParent);
                            Debug.Log("rerailed " + trainParent.name);
                        }
                        else if (targetTrain.BogieRail_B && targetTrain.BogieRail_F)
                        {
                            // ���[���ɂ��łɏ���Ă���
                        }
                        else
                        {
                            results.Add((targetTrain, null, null, "Train�̑��(bogie)���Б������ݒ肳��Ă��܂�", MessageType.Warning));
                        }
                    }
                }
                else
                {
                    foreach (var train in GameObject.FindObjectsOfType<Train>())
                    {

                        GameObject trainParent = train.transform.parent?.gameObject; // Train�̐e�I�u�W�F�N�g�𑀍삷��
                        if (trainParent == null)
                        {
                            results.Add((null, null, null, "Train�̐e�I�u�W�F�N�g�̎擾�Ɏ��s���܂���", MessageType.Error));
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
                                // ���[���ɂ��łɏ���Ă���
                            }
                            else
                            {
                                results.Add((train, null, null, "Train�̑��(bogie)���Б������ݒ肳��Ă��܂�", MessageType.Warning));
                            }
                        }
                    }
                }
                if(results.Count == 0)
                {
                    results.Add((null, null, null, "�����[�����ׂ��ԗ��͂���܂���ł���", MessageType.Info));
                }
            }

            if (results != null)
            {
                foreach (var result in results)
                {
                    if (result.train && result.rail)
                    {
                        EditorGUILayout.HelpBox($"{result.train.transform.parent.name} �� {result.rail.name}�� " + result.message, result.type);
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

        // �Ώۂ̎ԗ������[���ɏ悹��
        public void rerailTrain(Train targetTrain,GameObject trainParent)
        {
            Rail_Script rail = null;
            Vector3 point = Vector3.zero;
            float minDist;
            Vector3 tangent = Vector3.zero;
            (rail, point, minDist, tangent) = FindClosestRail(trainParent.transform.position);

            if (minDist < rerailDistance && rail)
            {
                // �e�I�u�W�F�N�g���Ǝԗ����ړ�������
                Undo.RecordObject(trainParent.transform, "Snap to rail(move)");
                trainParent.transform.position = point + new Vector3(0f, 0f, 0f);   // 0���n������ƃ��[���ɂ̂�Ȃ����ۂ���������(BogieRail_F/R��null�ɂȂ�
                Undo.RecordObject(targetTrain, "Snap to rail(set rail)");
                targetTrain.BogieRail_F = rail;
                targetTrain.BogieRail_B = rail;
                EditorUtility.SetDirty(targetTrain);

                // �ԗ�����]������
                Vector3 direction = targetTrain.CouplerF.transform.position - targetTrain.CouplerB.transform.position; // �A�����������������i��
                if (Vector3.Dot(direction, tangent) > 0)
                {
                    // ���H�̃^���W�F���g�Ǝԗ��̕���������
                    trainParent.transform.rotation = Quaternion.FromToRotation(Vector3.forward, tangent);
                }
                else
                {
                    // ���H�̃^���W�F���g�Ǝԗ��̕������t
                    trainParent.transform.rotation = Quaternion.FromToRotation(Vector3.back, tangent);
                }

                results.Add((targetTrain, null ,rail, "�ԗ������[���ɏ悹�܂���", MessageType.Info));
            }
            else
            {
                results.Add((targetTrain, null, null, "�ԗ��̋߂��Ƀ��[�����Ȃ��悤�ł�", MessageType.Error));
            }
        }

        // �ł��߂����[����T��
        private (Rail_Script,Vector3 point,float distance, Vector3 tangent)FindClosestRail(Vector3 target,int resolution = 10)
        {
            Rail_Script closestRail = null;
            Vector3 closestPoint = Vector3.zero;
            float minDist = Mathf.Infinity;
            Vector3 tangent = Vector3.zero;

            foreach (var rail in GameObject.FindObjectsOfType<Rail_Script>())
            {
                // �p�X��̍ŋߓ_�iNormalized �ŕԂ��j
                float t = rail.cinemachinePath.FindClosestPoint(target, 0, -1, resolution);

                // ���[���h���W�ɕϊ�
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

        // �V�[���S�̂�T�����A�Ώۂ̎ԗ��ɘA������
        // TODO:�����Ƌ@�\����悤�ɂ���B�u���[�L�ق��J���B���������͂ǂ�����H������Rerailer�܂߂�CustomEditor�ɂ���������������
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
                        results.Add((closestCoupler.coupler.TrainScript, null, null, $"{closestCoupler.coupler.name} ��TrainScript��null�ł�", MessageType.Warning));
                        results.Add((closestCoupler.coupler.TrainScript, null, null, $"{train.transform.parent.name} �ƕs���Ȏԗ���A�����܂���", MessageType.Info));
                    }
                    else
                    {
                        results.Add((closestCoupler.coupler.TrainScript, null, null, "�ԗ����m��A�����܂���", MessageType.Info));
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
                        results.Add((closestCoupler.coupler.TrainScript, null, null, $"{closestCoupler.coupler.name} ��TrainScript��null�ł�", MessageType.Warning));
                        results.Add((closestCoupler.coupler.TrainScript, null, null, $"{train.transform.parent.name} �ƕs���Ȏԗ���A�����܂���", MessageType.Info));
                    }
                    else
                    {
                        results.Add((closestCoupler.coupler.TrainScript, null, null, $"{train.transform.parent.name} {closestCoupler.train.name}��A�����܂���", MessageType.Info));
                    }
                }
            }
        }

        // �ł��߂��A�����T��
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