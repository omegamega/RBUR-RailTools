using NUnit.Framework;
using omegaExpDesign.RBURTool;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class Checker_Test : MonoBehaviour
{
    RailChecker checker;
    [SetUp]
    public void TestSetup()
    {
        checker = new RailChecker();
    }
    [Test]
    public void Test0_NoProblem()
    {
        EditorSceneManager.OpenScene("Packages/com.omegamega.rbur-railtools/Test/Test0_No_Problem.unity");
        checker.StartCheckRail();
        string resultString = "";
        foreach ((GameObject rail, GameObject targetRail, string message, MessageType messageType, RailChecker.FixAction fixAction) result in checker.railResults)
        {
            resultString += result.ToString() + "\n";
        }
        Debug.Log(resultString);
        Assert.AreEqual(
            resultString,
            "(RailPrefab_SmoothPath_-1 (UnityEngine.GameObject), , prevがありません。終端レールかも？, Info, None)\n(TurnOutRail (UnityEngine.GameObject), , nextがありません。終端レールかも？, Info, None)\n(TurnOutRail (UnityEngine.GameObject), , nextがありません。終端レールかも？, Info, None)\n(RailPrefab_SmoothPath_1 (UnityEngine.GameObject), , nextがありません。終端レールかも？, Info, None)\n");
    }
    [Test]
    public void Test1_No_CinemachinePath()
    {
        EditorSceneManager.OpenScene("Packages/com.omegamega.rbur-railtools/Test/Test1_No_CinemachinePath.unity");
        checker.StartCheckRail();
        string resultString = "";
        foreach ((GameObject rail, GameObject targetRail, string message, MessageType messageType, RailChecker.FixAction fixAction) result in checker.railResults)
        {
            resultString += result.ToString() + "\n";
        }
        Debug.Log(resultString);
        Assert.AreEqual(
            resultString,
            "(RailPrefab_SmoothPath_-1 (UnityEngine.GameObject), , レールのCinemachinePathが取得できません。\nPathがセットされてないのかも？, Error, None)\n(TurnOutRail (UnityEngine.GameObject), , nextがありません。終端レールかも？, Info, None)\n(TurnOutRail (UnityEngine.GameObject), , nextがありません。終端レールかも？, Info, None)\n(RailPrefab_SmoothPath_1 (UnityEngine.GameObject), , nextがありません。終端レールかも？, Info, None)\n");
    }
    [TearDown]
    public void Teardown()
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
    }
}
