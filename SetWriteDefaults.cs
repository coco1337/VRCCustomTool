using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System;

public sealed class SetWriteDefaults : EditorWindow
{
  private AnimatorController targetAnimController;
  private bool targetBool;

  private bool animControllercheck;

  private Color white = new Color(1, 1, 1, 1);
  private Color red = new Color(1f, .1f, .1f, 1);
  private Color green = new Color(.1f, 1f, .1f, 1);

  [MenuItem("coco/SetWriteDefaults")]
  private static void Init()
  {
    var window = (SetWriteDefaults)EditorWindow.GetWindow(typeof(SetWriteDefaults));
    window.Show();
  }

  private void OnGUI()
  {
    GUILayoutOption[] defaultLayoutOption = { GUILayout.Width(position.width - 20) };

    GUILayout.Box("Attach target AnimatorController", defaultLayoutOption);
    this.animControllercheck = this.targetAnimController is AnimatorController;
    GUI.backgroundColor = this.animControllercheck ? green : red;
    this.targetAnimController = EditorGUILayout.ObjectField(this.targetAnimController, typeof(AnimatorController), true, default) as AnimatorController;
    GUI.backgroundColor = white;

    this.targetBool = EditorGUILayout.Toggle("Set write defaults", this.targetBool, defaultLayoutOption);

    GUI.color = this.animControllercheck ? green : red;
    GUI.enabled = this.animControllercheck;
    if (GUILayout.Button("Apply"))
    {
      ApplyWriteDefaults();
    }

    GUI.color = white;
  }

  private void ApplyWriteDefaults()
  {
    try
    {
      foreach (var layer in this.targetAnimController.layers)
        foreach(var state in layer.stateMachine.states)
          state.state.writeDefaultValues = this.targetBool;
    } 
    catch (Exception e)
    {
      Debug.Log(e.Message);
    }
  }
}
