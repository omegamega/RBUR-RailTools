using UnityEditor;
using UnityEngine;
using Cinemachine;
using frou01.RigidBodyTrain;
using PlasticPipe.PlasticProtocol.Messages;
using System;
using UnityEngine.Scripting.APIUpdating;

namespace omegaExpDesign.RigidBodyTrain
{
    [CustomEditor(typeof(RailSnapTool))]
    public class RailSnapToolEditor : Editor
    {
        enum RailConnection
        {
            Unknown,
            Start,
            End,
            Both
        }
        private Vector3 dragWaypointStart = new Vector3();
        private Vector3 dragWaypointEnd = new Vector3();
        public override void OnInspectorGUI()
        {
            if (((RailSnapTool)target).rail == null)
            {
                EditorGUILayout.HelpBox("RBUR��Rail_Script�t���I�u�W�F�N�g�ɂ��Ă�������", MessageType.Error);
            }
            else
            {
                base.OnInspectorGUI();


                Rail_Script rail = ((RailSnapTool)target).gameObject.GetComponent<Rail_Script>();
                if (!rail) return;


                using (new EditorGUI.DisabledGroupScope(rail.cinemachinePath is not CinemachinePath))
                {
                    using (new EditorGUI.DisabledScope(!rail.prev))
                    {
                        if (GUILayout.Button("prev��Tangent�𑵂���"))
                        {
                            CinemachinePath c = (CinemachinePath)rail.cinemachinePath;
                            if (rail.prev.next == rail)
                            {
                                // prev�Ɍq�����Ă���̂́Aprev���[����next��������
                                Vector3 v = rail.prev.cinemachinePath.EvaluateTangentAtUnit(1f, CinemachinePathBase.PositionUnits.Normalized);
                                Undo.RecordObject(rail, "Correct waypoint tangent");
                                c.m_Waypoints[0].tangent = v;
                            }
                            else if (rail.prev.prev == rail)
                            {
                                // prev�Ɍq�����Ă���̂́Aprev���[����prev��������
                                Vector3 v = rail.prev.cinemachinePath.EvaluateTangentAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized);
                                Undo.RecordObject(rail, "Correct waypoint tangent");
                                c.m_Waypoints[0].tangent = -v;
                            }
                            else
                            {
                                // prev�͎��g�̃��[���Ɍq�����Ă��Ȃ�
                                Debug.Log($"RailSnapTool : {rail.name} prev�����[�����ڑ�����Ă��Ȃ���Ԃł��B����{rail.prev.name}");
                                if (EditorUtility.DisplayDialog("RailSnapTool", $"{rail.name} prev�����[�����ڑ�����Ă��Ȃ���Ԃł��B����{rail.prev.name}", "OK"))
                                {
                                    Selection.activeObject = rail.prev.gameObject;
                                    EditorGUIUtility.PingObject(rail.prev.gameObject);
                                }
                            }
                            SceneView.RepaintAll();
                        }
                    }
                    using (new EditorGUI.DisabledGroupScope(!rail.next))
                    {
                        if (GUILayout.Button("next��Tangent�𑵂���"))
                        {
                            CinemachinePath c = (CinemachinePath)rail.cinemachinePath;
                            if (rail.next.next == rail)
                            {
                                // next�Ɍq�����Ă���̂́Anext���[����next��������
                                Vector3 v = rail.next.cinemachinePath.EvaluateTangentAtUnit(1f, CinemachinePathBase.PositionUnits.Normalized);
                                Undo.RecordObject(rail, "Correct waypoint tangent");
                                c.m_Waypoints[c.m_Waypoints.Length - 1].tangent = -v;
                            }
                            else if (rail.next.prev == rail)
                            {
                                // next�Ɍq�����Ă���̂́Anext���[����prev��������
                                Vector3 v = rail.next.cinemachinePath.EvaluateTangentAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized);
                                Undo.RecordObject(rail, "Correct waypoint tangent");
                                c.m_Waypoints[c.m_Waypoints.Length - 1].tangent = v;
                            }
                            else
                            {
                                // prev�͎��g�̃��[���Ɍq�����Ă��Ȃ�
                                Debug.Log($"RailSnapTool : {rail.name} next�����[�����ڑ�����Ă��Ȃ���Ԃł��B����{rail.next.name}");
                                if (EditorUtility.DisplayDialog("RailSnapTool", $"{rail.name} next�����[�����ڑ�����Ă��Ȃ���Ԃł��B����{rail.next.name}", "OK"))
                                {
                                    Selection.activeObject = rail.prev.gameObject;
                                    EditorGUIUtility.PingObject(rail.next.gameObject);
                                }
                            }
                            SceneView.RepaintAll();
                        }
                    }
                }

                Vector3 WaypointDiff = GetWaypointEndPosition(rail.cinemachinePath) - GetWaypointStartPosition(rail.cinemachinePath);
                float averageGradient = WaypointDiff.y / rail.cinemachinePath.PathLength * 1000;
                EditorGUILayout.LabelField($"Average Gradient {averageGradient}��");
            }
        }

        private void OnEnable()
        {
            RailSnapTool railSnapTool = (RailSnapTool)target;
            railSnapTool.rail = railSnapTool.gameObject.GetComponent<Rail_Script>();
        }

        private void OnSceneGUI()
        {
            var railSnapTool = (RailSnapTool)target;
            if (railSnapTool.rail == null) return;
            var rail = railSnapTool.rail;

            Transform t = rail.transform;

            if (rail.cinemachinePath)
            {
                // Waypoint���[���h���b�O������A�߂��̃��[����Waypoint���[�փX�i�b�v����
                if (dragWaypointStart != GetWaypointStartPosition(rail.cinemachinePath))
                {
                    // Waypoint�n�_�̃X�i�b�v
                    if (railSnapTool.autoConnect)
                    {
                        DisconnectStartRail(railSnapTool.rail);
                    }
                    SnapStartWaypointToNearby(railSnapTool);
                    if (railSnapTool.autoConnect)
                    {
                        ConnectStartRailNearBy(railSnapTool.rail);
                    }
                }
                if (dragWaypointEnd != GetWaypointEndPosition(rail.cinemachinePath))
                {
                    // Waypoint�I�_�̃X�i�b�v
                    if (railSnapTool.autoConnect)
                    {
                        DisconnectEndRail(railSnapTool.rail);
                    }
                    SnapEndWaypointToNearby(railSnapTool);
                    if (railSnapTool.autoConnect)
                    {
                        ConnectEndRailNearBy(railSnapTool.rail);
                    }
                }
                Event e = Event.current;
                // GameObject���ƃh���b�O�ňړ�������A�X�i�b�v����GameObject.transform���ƈړ�������A
                // GameObject�ړ��̂Ƃ��̏���
                if (e.type == EventType.MouseUp && e.button == 0)
                {
                    if (railSnapTool.autoConnect)
                    {
                        DisconnectStartRail(railSnapTool.rail);
                        DisconnectEndRail(railSnapTool.rail);
                    }
                    SnapTransformToNearby(railSnapTool);
                    if (railSnapTool.autoConnect)
                    {
                        ConnectStartRailNearBy(railSnapTool.rail);
                        ConnectEndRailNearBy(railSnapTool.rail);
                    }
                }
                dragWaypointStart = GetWaypointStartPosition(rail.cinemachinePath);
                dragWaypointEnd = GetWaypointEndPosition(rail.cinemachinePath);
            }

            // 
            // �V�[������Rail_Script��T���A�p�X��`�悷��
            //
            if (railSnapTool.drawPath)
            {
                var rails = GameObject.FindObjectsOfType<Rail_Script>();

                foreach (Rail_Script otherRail in rails)
                {
                    // �p�X�S���h���Ă݂����ǁA�{��Rail_Script�Ő������Ă����ƂɋC�Â����c
                    if (otherRail == rail)
                    {
                        if (otherRail == null || otherRail.cinemachinePath == null) continue;

                        Handles.color = Color.cyan;

                        int steps = 50; // �Ȑ�������
                        Vector3 prevPos = otherRail.cinemachinePath.EvaluatePositionAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized);

                        for (int i = 1; i <= steps; i++)
                        {
                            float st = (float)i / steps;
                            Vector3 pos = otherRail.cinemachinePath.EvaluatePositionAtUnit(st, CinemachinePathBase.PositionUnits.Normalized);
                            Handles.DrawAAPolyLine((otherRail == rail) ? 15f : 5f, prevPos, pos);
                            prevPos = pos;
                        }
                    }
                    // SceneView �Ƀp�X����\��
                    var guiStyle = new GUIStyle {fontSize = 16, normal = {textColor = Color.cyan } };
                    Handles.Label(otherRail.cinemachinePath.EvaluatePositionAtUnit(0.5f, CinemachinePathBase.PositionUnits.Normalized), otherRail.name, guiStyle);
                }
            }
        }

        // GameObject���ƈړ������ꍇ�A���[���I�[���m�ŃX�i�b�v���ATransform���g���Ĉړ�������
        private void SnapTransformToNearby(RailSnapTool railSnapTool)
        {
            Transform t = railSnapTool.rail.transform;
            Vector3 myStart = railSnapTool.rail.cinemachinePath.EvaluatePositionAtUnit(0, CinemachinePathBase.PositionUnits.Normalized);
            Vector3 myEnd = railSnapTool.rail.cinemachinePath.EvaluatePositionAtUnit(1, CinemachinePathBase.PositionUnits.Normalized);

            Rail_Script[] rails = GameObject.FindObjectsOfType<Rail_Script>();
            Vector3 snapOffset = new Vector3();
            float minDist = Mathf.Infinity;

            foreach (var other in rails)
            {
                if (other == railSnapTool.rail) continue;

                Vector3 otherStart = other.cinemachinePath.EvaluatePositionAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized);
                Vector3 otherEnd = other.cinemachinePath.EvaluatePositionAtUnit(1f, CinemachinePathBase.PositionUnits.Normalized);

                // �n�_/�I�_x�n�_/�I�_��4�p�^�[�����`�F�b�N
                if (!other.prev) CheckSnap(myStart, otherStart, ref minDist, ref snapOffset);
                if (!other.next) CheckSnap(myStart, otherEnd, ref minDist, ref snapOffset);
                if (!other.prev) CheckSnap(myEnd, otherStart, ref minDist, ref snapOffset);
                if (!other.next) CheckSnap(myEnd, otherEnd, ref minDist, ref snapOffset);
            }

            // �X�i�b�v�ʒu�� transform �ɔ��f
            if (minDist < Mathf.Infinity && minDist < railSnapTool.snapDistance)
            {
                Undo.RecordObject(t, "Snap Path");
                t.position += snapOffset;
            }
        }

        // ���[����Waypoint�n�_���A�߂��̃��[�����[�ɃX�i�b�v����(Waypoint�ҏW�h���b�O���̃X�i�b�v����)
        private void SnapStartWaypointToNearby(RailSnapTool railSnapTool)
        {
            Vector3 myStart = railSnapTool.rail.cinemachinePath.EvaluatePositionAtUnit(0, CinemachinePathBase.PositionUnits.Normalized);

            Rail_Script[] rails = GameObject.FindObjectsOfType<Rail_Script>();
            Vector3 snapOffset = new Vector3();
            float minDist = Mathf.Infinity;

            foreach (var other in rails)
            {
                if (other == railSnapTool.rail) continue;

                Vector3 otherStart = other.cinemachinePath.EvaluatePositionAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized);
                Vector3 otherEnd = other.cinemachinePath.EvaluatePositionAtUnit(1f, CinemachinePathBase.PositionUnits.Normalized);

                if (!other.prev) CheckSnap(myStart, otherStart, ref minDist, ref snapOffset);
                if (!other.next) CheckSnap(myStart, otherEnd, ref minDist, ref snapOffset);
            }

            // �X�i�b�v����
            if (minDist < Mathf.Infinity && minDist < railSnapTool.snapDistance)
            {
                Undo.RecordObject(railSnapTool.rail.cinemachinePath, "Snap Path") ;
                Vector3 v = GetWaypointStartPosition(railSnapTool.rail.cinemachinePath);
                v += railSnapTool.transform.InverseTransformVector(snapOffset);
                SetWaypointStartPosition(railSnapTool.rail.cinemachinePath, v);
            }
        }

        // ���[����Waypoint�I�_���A�߂��̃��[�����[�ɃX�i�b�v����(Waypoint�ҏW�h���b�O���̃X�i�b�v����)
        private void SnapEndWaypointToNearby(RailSnapTool railSnapTool)
        {
            Vector3 myEnd = railSnapTool.rail.cinemachinePath.EvaluatePositionAtUnit(1, CinemachinePathBase.PositionUnits.Normalized);

            Rail_Script[] rails = GameObject.FindObjectsOfType<Rail_Script>();
            Vector3 snapOffset = new Vector3();
            float minDist = Mathf.Infinity;

            foreach (var other in rails)
            {
                if (other == railSnapTool.rail) continue;

                Vector3 otherStart = other.cinemachinePath.EvaluatePositionAtUnit(0f, CinemachinePathBase.PositionUnits.Normalized);
                Vector3 otherEnd = other.cinemachinePath.EvaluatePositionAtUnit(1f, CinemachinePathBase.PositionUnits.Normalized);

                if(!other.prev) CheckSnap(myEnd, otherStart, ref minDist, ref snapOffset);
                if(!other.next) CheckSnap(myEnd, otherEnd,  ref minDist, ref snapOffset);
            }

            // �X�i�b�v����
            if (minDist < Mathf.Infinity && minDist < railSnapTool.snapDistance)
            {
                Undo.RecordObject(railSnapTool.rail.cinemachinePath, "Snap Path");
                Vector3 v = GetWaypointEndPosition(railSnapTool.rail.cinemachinePath);
                v += railSnapTool.transform.InverseTransformVector(snapOffset);
                SetWaypointEndPosition(railSnapTool.rail.cinemachinePath, v);
            }
        }
        // myPoint �� targetPoint �̍������g���� transform �̈ړ��ʂ��v�Z
        private bool CheckSnap(Vector3 myPoint, Vector3 targetPoint, ref float minDist, ref Vector3 snapOffset)
        {
            float d = Vector3.Distance(myPoint, targetPoint);
            if (d < minDist)
            {
                minDist = d;
                snapOffset = targetPoint - myPoint;
                return true;
            }
            return false;
        }

        // prev,next�̐ڑ����O���B�ݒ肳��Ă������背�[��������̐ڑ����O��
        private void DisconnectStartRail(Rail_Script rail)
        {
            // �h���b�O��prev,next���O���B��J���đ��背�[��������̐ڑ����O��
            if (rail.prev)
            {
                if (rail.prev.prev == rail)
                {
                    rail.prev.prev = null;
                }
                if (rail.prev.next == rail)
                {
                    rail.prev.next = null;
                }
                rail.prev = null;
            }
        }
        private void DisconnectEndRail(Rail_Script rail) 
        { 
            if (rail.next)
            {
                if (rail.next.prev == rail)
                {
                    rail.next.prev = null;
                }
                if (rail.next.next == rail)
                {
                    rail.next.next = null;
                }
                rail.next = null;
            }
        }

        // ���ӂ̃��[����T�����AmyRail��prev�̂ݐڑ����܂��B�ڑ������prev,next�����l�ɐݒ肵�A���݃��X�g��Ԃ��\�z���܂�
        private void ConnectStartRailNearBy(Rail_Script myRail)
        {
            Rail_Script[] rails = GameObject.FindObjectsOfType<Rail_Script>();
            Vector3 myStart = myRail.cinemachinePath.EvaluatePositionAtUnit(0, CinemachinePathBase.PositionUnits.Normalized);
            Vector3 myEnd = myRail.cinemachinePath.EvaluatePositionAtUnit(1, CinemachinePathBase.PositionUnits.Normalized);
            float connectDistance = 0.01f;

            foreach (var other in rails)
            {
                if (other == myRail) continue;

                Vector3 otherStart = other.cinemachinePath.EvaluatePositionAtUnit(0, CinemachinePathBase.PositionUnits.Normalized);
                Vector3 otherEnd = other.cinemachinePath.EvaluatePositionAtUnit(1, CinemachinePathBase.PositionUnits.Normalized);

                if((myStart - otherStart).magnitude < connectDistance && !other.prev) {
                    myRail.prev = other;
                    other.prev = myRail;
                }
                if ((myStart - otherEnd).magnitude < connectDistance && !other.next)
                {
                    myRail.prev = other;
                    other.next = myRail;
                }
            }
            return;
        }

        // ���ӂ̃��[����T�����AmyRail��next�̂ݐڑ����܂��B�ڑ������prev,next�����l�ɐݒ肵�A���݃��X�g��Ԃ��\�z���܂�
        private void ConnectEndRailNearBy(Rail_Script myRail)
        {
            Rail_Script[] rails = GameObject.FindObjectsOfType<Rail_Script>();
            Vector3 myStart = myRail.cinemachinePath.EvaluatePositionAtUnit(0, CinemachinePathBase.PositionUnits.Normalized);
            Vector3 myEnd = myRail.cinemachinePath.EvaluatePositionAtUnit(1, CinemachinePathBase.PositionUnits.Normalized);
            float connectDistance = 0.01f;

            foreach (var other in rails)
            {
                if (other == myRail) continue;

                Vector3 otherStart = other.cinemachinePath.EvaluatePositionAtUnit(0, CinemachinePathBase.PositionUnits.Normalized);
                Vector3 otherEnd = other.cinemachinePath.EvaluatePositionAtUnit(1, CinemachinePathBase.PositionUnits.Normalized);

                if ((myEnd - otherStart).magnitude < connectDistance && !other.prev)
                {
                    myRail.next = other;
                    other.prev = myRail;
                }
                if ((myEnd - otherEnd).magnitude < connectDistance && !other.next)
                {
                    myRail.next = other;
                    other.next = myRail;
                }
            }
            return;
        }

        //
        // Cinemchine Waypoint Utils
        //
        private  Vector3 GetWaypointStartPosition(CinemachinePathBase path)
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

        private void SetWaypointStartPosition(CinemachinePathBase path, Vector3 pos)
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
        private void SetWaypointEndPosition(CinemachinePathBase path, Vector3 pos)
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
    }

}