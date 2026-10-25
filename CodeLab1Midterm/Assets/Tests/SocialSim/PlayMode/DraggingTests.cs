using System.Collections;
using NUnit.Framework;
using SocialSim;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class DraggingTests
{
    [UnityTest]
    public IEnumerator DraggingPausesAIRejectsInvalidPositionsAndResumes()
    {
        yield return SceneManager.LoadSceneAsync("SocialSim");
        yield return null;
        var manager = Object.FindAnyObjectByType<SocialManager>();
        var student = manager.Students[0];
        Assert.That(manager.GetComponent<StudentDragController>(), Is.Not.Null);
        Assert.That(student.BeginDrag(), Is.True);
        Assert.That(student.Phase, Is.EqualTo(StudentPhase.Dragging));
        Assert.That(student.TryDragTo(manager.spawnPoints[4].position), Is.True);
        Vector3 placed = student.transform.position;
        float fatigue = student.Fatigue;
        yield return new WaitForSeconds(0.2f);
        Assert.That(student.Phase, Is.EqualTo(StudentPhase.Dragging));
        Assert.That(Vector3.Distance(student.transform.position, placed), Is.LessThan(0.05f));
        Assert.That(student.Fatigue, Is.EqualTo(fatigue));
        Assert.That(student.TryDragTo(new Vector3(1000, 1000, 1000)), Is.False);
        Assert.That(Vector3.Distance(student.transform.position, placed), Is.LessThan(0.05f));
        student.EndDrag(false);
        Assert.That(student.Phase, Is.EqualTo(StudentPhase.Deciding));
        yield return null;
        Assert.That(student.Phase, Is.Not.EqualTo(StudentPhase.Dragging));
    }

    [UnityTest]
    public IEnumerator SelectionAllowsNearMissesButCannotPickThroughScenery()
    {
        yield return SceneManager.LoadSceneAsync("SocialSim");
        yield return null;
        var manager = Object.FindAnyObjectByType<SocialManager>();
        manager.enableGroups = false;
        var student = manager.Students[0];
        foreach (var other in manager.Students) if (other != student) other.gameObject.SetActive(false);
        Assert.That(student.BeginDrag(), Is.True);
        Assert.That(student.TryDragTo(manager.spawnPoints[2].position), Is.True);
        Physics.SyncTransforms();
        var controller = manager.GetComponent<StudentDragController>();
        var camera = Camera.main;
        bool initializedBounds = false;
        Bounds bounds = default;
        foreach (var renderer in student.GetComponentsInChildren<Renderer>())
        {
            if (!initializedBounds) { bounds = renderer.bounds; initializedBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        var center = (Vector2)camera.WorldToScreenPoint(bounds.center);
        Assert.That(controller.TryPickStudent(camera, center, out var picked), Is.True);
        Assert.That(picked, Is.SameAs(student));
        var nearMiss = (Vector2)camera.WorldToScreenPoint(bounds.center + camera.transform.right * bounds.extents.magnitude) + Vector2.right * 4f;
        controller.pickPaddingPixels = 0;
        Assert.That(controller.TryPickStudent(camera, nearMiss, out _), Is.False);
        controller.pickPaddingPixels = manager.mousePickPaddingPixels;
        Assert.That(controller.TryPickStudent(camera, nearMiss, out picked), Is.True);
        Assert.That(picked, Is.SameAs(student));
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            wall.transform.position = Vector3.Lerp(camera.transform.position, bounds.center, 0.6f);
            wall.transform.rotation = camera.transform.rotation;
            wall.transform.localScale = new Vector3(4, 4, 0.3f);
            Physics.SyncTransforms();
            Assert.That(controller.TryPickStudent(camera, center, out _), Is.False);
            Assert.That(controller.TryPickStudent(camera, nearMiss, out _), Is.False);
        }
        finally { Object.Destroy(wall); }
    }
}
