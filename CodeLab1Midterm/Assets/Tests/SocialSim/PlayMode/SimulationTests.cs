using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SocialSim;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class SimulationTests
{
    [UnityTest]
    public IEnumerator ConfiguredStudentsExecuteAllActivities()
    {
        Random.InitState(6434);
        yield return SceneManager.LoadSceneAsync("SocialSim");
        yield return null;
        var manager = Object.FindFirstObjectByType<SocialManager>();
        Assert.That(manager.Students.Count, Is.EqualTo(manager.agentCount));
        var visited = new HashSet<StudentAction>();
        float previousScale = Time.timeScale;
        Time.timeScale = 10f;
        try
        {
            float end = Time.time + 180f;
            while (Time.time < end)
            {
                foreach (var student in manager.Students)
                {
                    Assert.That(student.Academic, Is.InRange(0f, 100f));
                    Assert.That(student.Fatigue, Is.InRange(0f, 100f));
                    Assert.That(student.Stress, Is.InRange(0f, 100f));
                    if (student.Phase == StudentPhase.Acting) visited.Add(student.CurrentAction);
                }
                yield return null;
            }
            Assert.That(visited, Does.Contain(StudentAction.Study));
            Assert.That(visited, Does.Contain(StudentAction.Home));
            Assert.That(visited, Does.Contain(StudentAction.Playground));
        }
        finally { Time.timeScale = previousScale; }
    }
}
