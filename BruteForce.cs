using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using System;
using System.Linq;

public sealed class BruteForce : EditorWindow
{
  private Vector2 scrollPos;

  private VRCAvatarDescriptor avatar;

  private string categoryName;
  private int categoryNum;
  private int partsCount = 1;

  private List<string> paramList = new List<string>();
  private List<bool> paramCheckList = new List<bool>();

  private List<GameObject> partsList = new List<GameObject>();
  private List<bool> partsCheckList = new List<bool>();

  private string animationPrefix;

  private UnityEditor.Animations.AnimatorController fxAnimController;

  private string generatedAnimationPath = "Assets/clip/save/path";

  private string errorMsg = "";

  private AnimatorConditionMode GetAnimatorConditionMode(bool b) => b ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;
  private float GetCurveValue(bool b) => b ? 1 : 0;
  private bool IsValidPath() => UnityEditor.AssetDatabase.IsValidFolder(this.generatedAnimationPath);
  private void SetErrorField(string txt) => this.errorMsg += txt + "\n";
  private void ResetErrorField() => this.errorMsg = "";

  // default ui color option
  private Color white = new Color(1, 1, 1, 1);
  private Color red = new Color(1f, .1f, .1f, 1);
  private Color green = new Color(.1f, 1f, .1f, 1);

  // default check options
  private bool avatarCheck;
  private bool categoryParamNameCheck;
  private bool paramListCheck;
  private bool partsListCheck;
  private bool animationPrefixCheck;
  private bool pathCheck;

  // Max count
  private const int MaxCount = 7;

  [MenuItem("coco/BruteForce")]
  private static void Init()
  {
    var window = (BruteForce) EditorWindow.GetWindow(typeof(BruteForce));
    window.Show();
  }

  private void OnGUI()
  {
    // default option, scroll
    GUILayoutOption[] defaultLayoutOption = { GUILayout.Width(position.width - 20) };
    this.scrollPos = EditorGUILayout.BeginScrollView(this.scrollPos, false, true);

    GUILayout.Box("Attach target avatar", defaultLayoutOption);
    this.avatarCheck = this.avatar is VRCAvatarDescriptor;
    GUI.backgroundColor = this.avatarCheck ? green : red;
    this.avatar = EditorGUILayout.ObjectField(avatar, typeof(VRCAvatarDescriptor), true, defaultLayoutOption) as VRCAvatarDescriptor;
    GUI.backgroundColor = white;    
    GUILayout.Space(10);

    GUILayout.Box("Category parameter name", defaultLayoutOption);
    this.categoryParamNameCheck = !string.IsNullOrEmpty(this.categoryName);
    GUI.backgroundColor = this.categoryParamNameCheck ? green : red;
    this.categoryName = EditorGUILayout.TextField(this.categoryName, defaultLayoutOption);
    GUI.backgroundColor = white;
    GUILayout.Space(10);

    GUILayout.Box("Category parameter number", defaultLayoutOption);
    this.categoryNum = EditorGUILayout.IntField(this.categoryNum, defaultLayoutOption);
    GUILayout.Space(10);

    GUILayout.Box($"Parts, Param count (1 ~ ${MaxCount})", defaultLayoutOption);
    this.partsCount = EditorGUILayout.IntField(this.partsCount, defaultLayoutOption);

    this.partsCount = Mathf.Min(this.partsCount, MaxCount);
    this.partsCount = Mathf.Max(this.partsCount, 1);

    RefreshList<List<string>, string>(this.paramList, this.partsCount);
    RefreshList<List<bool>, bool>(this.paramCheckList, this.partsCount);

    GUILayout.Box("Param list", defaultLayoutOption);
    this.paramListCheck = !this.paramCheckList.Any(e => e == false);
    for (int i = 0; i < this.partsCount; ++i)
    {
      GUI.backgroundColor = this.paramCheckList[i] ? green : red;
      this.paramList[i] = EditorGUILayout.TextField($"Param {i}", this.paramList[i], defaultLayoutOption);
      this.paramCheckList[i] = !string.IsNullOrEmpty(this.paramList[i]) && !IsDuplicate(i);
    }

    GUI.backgroundColor = white;

    RefreshList<List<GameObject>, GameObject>(this.partsList, this.partsCount);
    RefreshList<List<bool>, bool>(this.partsCheckList, this.partsCount);

    GUILayout.Box("Part list", defaultLayoutOption);
    this.partsListCheck = !this.partsCheckList.Any(e => e == false);
    for (int i = 0; i < this.partsCount; ++i)
    {
      GUI.backgroundColor = this.partsCheckList[i] ? green : red;
      this.partsList[i] = EditorGUILayout.ObjectField($"Parts {i}", this.partsList[i], typeof(GameObject), true, defaultLayoutOption) as GameObject;
      this.partsCheckList[i] = this.avatar != null && this.partsList[i] != null && IsValidParts(i);
    }

    GUI.backgroundColor = white;

    GUILayout.Box("Animator prefix", defaultLayoutOption);
    this.animationPrefixCheck = !string.IsNullOrEmpty(this.animationPrefix);
    GUI.backgroundColor = this.animationPrefixCheck ? green : red;
    this.animationPrefix = EditorGUILayout.TextField(this.animationPrefix, defaultLayoutOption);
    GUI.backgroundColor = white;

    GUILayout.Box("Path for generated animation clips", defaultLayoutOption);
    this.pathCheck = !string.IsNullOrEmpty(this.generatedAnimationPath) && IsValidPath();
    GUI.backgroundColor = this.pathCheck ? green : red;
    this.generatedAnimationPath = EditorGUILayout.TextField(this.generatedAnimationPath, defaultLayoutOption);
    GUI.backgroundColor = white;

    if (!string.IsNullOrEmpty(this.errorMsg))
    {
      GUILayout.Space(10);
      GUI.backgroundColor = red;
      GUI.contentColor = white;
      GUILayout.Box(this.errorMsg, defaultLayoutOption);
      GUI.backgroundColor = white;
    }   

    EditorGUILayout.EndScrollView();

    var valid = this.avatarCheck && this.categoryParamNameCheck && this.paramListCheck && this.partsListCheck && this.animationPrefixCheck && this.pathCheck;
    GUI.color = valid ? green : red;
    GUI.enabled = valid;
    if (GUILayout.Button("Apply"))
    {
      ResetErrorField();
      if (!CheckAndApplyParam()) return;
      if (!MakeLayer()) return;
    }

    GUI.color = white;
  }

  private bool CheckAndApplyParam()
  {
    UnityEngine.Object original = CreateInstance<VRCExpressionParameters>();
    var exParams = this.avatar.expressionParameters;
    EditorUtility.CopySerialized(exParams, original);
    try
    {
      var categoryParam = exParams.FindParameter(this.categoryName);
      var additionalCategorySize = 0;

      // Check category Parameter in VRC expression parameters
      if (categoryParam == null)
        additionalCategorySize = 4;
      else
      {
        if (categoryParam.valueType != VRCExpressionParameters.ValueType.Int)
          throw new Exception("Category parameter must be Integer.");
      }

      if (exParams.CalcTotalCost() + this.partsCount + additionalCategorySize > VRCExpressionParameters.MAX_PARAMETER_COST)
        throw new Exception("VRC ExpressionParameters' Cost Exceeded");
      
      // Create VRC expression parameter list
      var newParams = new List<VRCExpressionParameters.Parameter>();
      newParams.AddRange(exParams.parameters);

      // Add category parameter in VRC expression parameters
      if (additionalCategorySize == 4)
      {
        newParams.Add(new VRCExpressionParameters.Parameter
        {
          name = this.categoryName,
          defaultValue = 0,
          saved = true,
          valueType = VRCExpressionParameters.ValueType.Int
        });
      }

      for (int i = 0; i < this.partsCount; ++i)
      {
        if (newParams.Any(e => e.name == this.paramList[i]))
          throw new Exception("Duplicate parameter name");

        newParams.Add(new VRCExpressionParameters.Parameter
        {
          name = this.paramList[i],
          defaultValue = 0,
          saved = true,
          valueType = VRCExpressionParameters.ValueType.Bool,
        });
      }

      this.avatar.expressionParameters.parameters = newParams.ToArray();      
      EditorUtility.SetDirty(this.avatar.expressionParameters);

      // find FX animation controller
      var fxAnimationRuntimeController = this.avatar.baseAnimationLayers[4].animatorController;
      if (fxAnimationRuntimeController == null)
        throw new Exception("Invalid FX animation controller in VRCDescriptor");

      var fxAnimationController = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(UnityEditor.AssetDatabase.GetAssetPath(fxAnimationRuntimeController));
      if (fxAnimationController == null)
        throw new Exception("Invalid FX animation controller in AssetDatabase");

      // Add parameters in VRCExpressionParameters and FX animation controller
      if (!AddParametersInFxAnimator(this.avatar.expressionParameters, ref fxAnimationController))
        throw new Exception("Failed to add parameters in FX animation controller");

      this.fxAnimController = fxAnimationController;

      return true;
    } 
    catch (Exception e)
    {
      SetErrorField(e.Message);
      this.avatar.expressionParameters = (VRCExpressionParameters)original;      
      EditorUtility.SetDirty(this.avatar.expressionParameters);
      // TODO : replace to UndoRecord
      return false;
    }    
  }

  private bool AddParametersInFxAnimator(VRCExpressionParameters exParameters, ref UnityEditor.Animations.AnimatorController originalAnimController)
  {
    UnityEngine.Object originalBackup = new UnityEditor.Animations.AnimatorController();
    EditorUtility.CopySerialized(originalAnimController, originalBackup);

    try
    {
      foreach (var exParameter in exParameters.parameters)
      {
        if (!originalAnimController.parameters.Any(e => e.name == exParameter.name))
        {
          var vT = ChangeValueType(exParameter.valueType);
          originalAnimController.AddParameter(new AnimatorControllerParameter
          {
            name = exParameter.name,
            type = ChangeValueType(exParameter.valueType),
          });
        }
      }

      return true;
    } 
    catch (Exception e)
    {
      SetErrorField(e.Message);
      originalAnimController = (UnityEditor.Animations.AnimatorController) originalBackup;
      return false;
    }
  }

  private bool MakeLayer()
  {
    UnityEngine.Object originalBackup = new UnityEditor.Animations.AnimatorController();
    EditorUtility.CopySerialized(this.fxAnimController, originalBackup);

    try
    {
      var stateMachine = new AnimatorStateMachine 
      { 
        name = this.animationPrefix,
      };

      AssetDatabase.AddObjectToAsset(stateMachine, this.fxAnimController);
      stateMachine.hideFlags = HideFlags.HideInHierarchy;

      // Make idle state
      var idleState = stateMachine.AddState("Idle");

      // Make animation clip property path
      var clipPathList = new List<string>();
      foreach (var part in this.partsList)
      {
        var clipPath = part.name;
        var tempPart = part.transform.parent;
        while (tempPart.transform != this.avatar.transform)
        {
          clipPath = tempPart.name + "/" + clipPath;
          tempPart = tempPart.parent;
        }

        clipPathList.Add(clipPath);
      }

      // Make list for created parameters
      var targetParam = new List<string>();
      foreach (var targetParamName in this.paramList)
      {
        var tempParam = this.fxAnimController.parameters.FirstOrDefault(e => e.name == targetParamName);
        if (tempParam == default) continue;

        targetParam.Add(tempParam.name);
      }

      // Make bit flag
      int stateCount = (int)Mathf.Pow(2, this.partsCount);

      // In animation controller layer
      for (int i = 0 ; i < stateCount; ++i)
      {
        var conditions = new List<AnimatorCondition>();

        // Add category condition
        conditions.Add(new AnimatorCondition()
        {
          mode = AnimatorConditionMode.Equals,
          parameter = this.categoryName,
          threshold = this.categoryNum,
        });

        // Idle -> Each state
        for (int k = 0; k < this.partsCount; ++k)
        {
          var checker = 0x01 << k;

          conditions.Add(new AnimatorCondition()
          {
            mode = GetAnimatorConditionMode((i & checker) == checker),
            parameter = targetParam[k],
          });
        }

        var state = stateMachine.AddState(this.animationPrefix + i);

        // Each state -> Exit
        var categoryExitTransition = state.AddExitTransition(false);
        categoryExitTransition.AddCondition(AnimatorConditionMode.NotEqual, this.categoryNum, this.categoryName);
        categoryExitTransition.duration = 0;
        categoryExitTransition.exitTime = 0;
        categoryExitTransition.hasExitTime = false;
        categoryExitTransition.hasFixedDuration = false;

        // Create animation clip
        var clip = new AnimationClip();

        for (int k = 0; k < this.partsCount; ++k)
        {
          var checker = 0x01 << k;

          // Exit transition
          var exitTransition = state.AddExitTransition(false);
          exitTransition.AddCondition(GetAnimatorConditionMode((i & checker) != checker), 0, targetParam[k]);
          exitTransition.duration = 0;
          exitTransition.exitTime = 0;
          exitTransition.hasExitTime = false;
          exitTransition.hasFixedDuration = false;

          // Create and submit animation curve on animation clip
          var curve = new AnimationCurve();
          curve.AddKey(0, GetCurveValue((i & checker) == checker));
          clip.SetCurve(clipPathList[k], typeof(GameObject), "m_IsActive", curve);
        }

        // Submit animation clip
        state.motion = clip;

        // Create animation clip on asset folder
        UnityEditor.AssetDatabase.CreateAsset(clip, this.generatedAnimationPath + $"/{this.animationPrefix}{i}.anim");

        // Attach to idle state in layer
        var animStateTransition = new AnimatorStateTransition()
        {
          destinationState = state,
          conditions = conditions.ToArray(),
          offset = 0,
          hasExitTime = false,
          hasFixedDuration = false,
          exitTime = 0,
          duration = 0
        };

        idleState.AddTransition(animStateTransition);
        AssetDatabase.AddObjectToAsset(animStateTransition, this.fxAnimController);
        animStateTransition.hideFlags = HideFlags.HideInHierarchy;
      }

      var animControllerLayer = new AnimatorControllerLayer
      {
        name = this.animationPrefix,
        stateMachine = stateMachine,
        defaultWeight = 1,
      };

      // Attach layer to animation controller
      this.fxAnimController.AddLayer(animControllerLayer);

      return true;
    }
    catch (Exception e)
    {
      SetErrorField(e.Message);
      this.fxAnimController = (UnityEditor.Animations.AnimatorController)originalBackup;
      return false;
    } 
  }  

  private void RefreshList<T, Type>(T targetList, int targetCount) where T : IList
  {
    if (targetList.Count > targetCount)
      while (targetList.Count > targetCount)
        targetList.RemoveAt(targetList.Count - 1);
    else
      while (targetList.Count < targetCount)
        targetList.Add(default(Type));
  }

  private bool IsDuplicate(int idx)
  {
    var count = 0;
    foreach (var t in this.paramList)
      count = this.paramList[idx].Equals(t) ? count + 1 : count;

    return count > 1;
  }

  private bool IsValidParts(int idx)
  {
    // check root
    var target = this.partsList[idx].transform;

    while (target.parent != null)
      target = target.parent;

    return target == this.avatar.transform;
  }

  private AnimatorControllerParameterType ChangeValueType(VRCExpressionParameters.ValueType vType) 
  {
    switch (vType)
    {
      case VRCExpressionParameters.ValueType.Bool:
        return AnimatorControllerParameterType.Bool;
      case VRCExpressionParameters.ValueType.Int:
        return AnimatorControllerParameterType.Int;
      case VRCExpressionParameters.ValueType.Float:
        return AnimatorControllerParameterType.Float;
      default:
        return AnimatorControllerParameterType.Trigger;
    }
  }
}