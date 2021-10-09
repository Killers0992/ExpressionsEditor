namespace ExpressionsEditor
{
    using System.IO;
    using System.Linq;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;
    using UnityEditor.Animations;
    using ExpressionsEditor.Models;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Avatars.ScriptableObjects;
    using static VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters;
    using static VRC.SDKBase.VRC_AnimatorTrackingControl;


    public class ExpressionEditor : EditorWindow
    {
        VRCAvatarDescriptor vrcAvatar = null;
        Dictionary<int, ParamValue> parameters = new Dictionary<int, ParamValue>();
        Dictionary<int, ParamValue> rotationParameters = new Dictionary<int, ParamValue>();
        Dictionary<VRCExpressionsMenu.Control, List<int>> selectedIndexes = new Dictionary<VRCExpressionsMenu.Control, List<int>>();
        Dictionary<VRCExpressionsMenu, Vector2> scrollViews = new Dictionary<VRCExpressionsMenu, Vector2>();
        Dictionary<VRCExpressionsMenu.Control, List<bool>> foldouts = new Dictionary<VRCExpressionsMenu.Control, List<bool>>();
        Dictionary<VRCExpressionsMenu.Control, Dictionary<int, TempData>> tempdatas { get; set; } = new Dictionary<VRCExpressionsMenu.Control, Dictionary<int, TempData>>();

        [MenuItem("ExpressionEditor/Open Editor")]
        static void Init()
        {
            ExpressionEditor window = (ExpressionEditor)EditorWindow.GetWindow(typeof(ExpressionEditor), false, "ExpressionEditor");
            window.Show();
        }

        string GetSpaces(int num)
        {
            string str = string.Empty;
            for (int x = 0; x < num; x++)
            {
                str += " ";
            }
            return str;
        }

        Dictionary<int, ParamValue> GetParams(bool rotationOnly = false)
        {
            if (vrcAvatar == null)
                return new Dictionary<int, ParamValue>();
            int curIndex = 0;
            Dictionary<int, ParamValue> values = new Dictionary<int, ParamValue>() { { -1, new ParamValue() { Name = "None", Index = curIndex } } };
            for (int x = 0; x < vrcAvatar.expressionParameters.parameters.Length; x++)
            {
                if (vrcAvatar.expressionParameters.parameters[x].valueType != ValueType.Float && rotationOnly)
                    continue;
                curIndex++;
                values.Add(x, new ParamValue()
                {
                    Name = vrcAvatar.expressionParameters.parameters[x].name,
                    Index = curIndex
                });
            }
            return values;
        }

        Parameter GetParam(int selectedIndex, bool rotationOnly = false)
        {
            if (selectedIndex == 0)
                return null;

            var data = rotationOnly ? rotationParameters : parameters;

            foreach (var param in data)
            {
                if (selectedIndex == param.Value.Index)
                    return vrcAvatar.expressionParameters.GetParameter(param.Key);
            }
            return null;
        }

        int GetIndexOfParam(VRCExpressionsMenu.Control.Parameter parameter, bool rotationOnly = false)
        {
            if (parameter == null)
                return 0;

            var data = rotationOnly ? rotationParameters : parameters;

            foreach (var param in data)
            {
                if (param.Value.Name == parameter.name)
                    return param.Value.Index;
            }
            return 0;
        }

        AnimatorControllerParameter GetOrAddParameter(AnimatorController animator, string parameterName, object defaultValue)
        {
            var foundParam = animator.parameters.FirstOrDefault(p => p.name == parameterName);
            AnimatorControllerParameterType type = AnimatorControllerParameterType.Bool;
            switch (defaultValue)
            {
                case float _:
                    type = AnimatorControllerParameterType.Float;
                    break;
                case int _:
                    type = AnimatorControllerParameterType.Int;
                    break;
            }

            if (foundParam == null)
            {
                foundParam = new AnimatorControllerParameter()
                {
                    defaultBool = defaultValue is bool ? (bool)defaultValue : default(bool),
                    defaultFloat = defaultValue is float ? (float)defaultValue : default(float),
                    defaultInt = defaultValue is int ? (int)defaultValue : default(int),
                    name = parameterName,
                    type = type
                };
                animator.AddParameter(foundParam);
            }
            else
            {
                switch (defaultValue)
                {
                    case float f:
                        foundParam.defaultFloat = f;
                        break;
                    case bool b:
                        foundParam.defaultBool = b;
                        break;
                    case int i:
                        foundParam.defaultInt = i;
                        break;
                }
                foundParam.type = type;
            }
            return foundParam;
        }

        bool CreateGameObjectToggle(VRCExpressionParameters parameters, AnimatorController animator, GameObject selectedObject, string parameterName, object defaultValue)
        {
            GetOrAddParameter(animator, defaultValue is bool ? $"{parameterName}T" : parameterName, defaultValue);

            animator.RemoveLayerIfExists(selectedObject.name);

            var stateMachine = new AnimatorStateMachine()
            {
                name = selectedObject.name
            };
            AssetDatabase.AddObjectToAsset(stateMachine, animator);

            float defaultValueFloat = 0f;
            ValueType valueType = ValueType.Bool;
            switch (defaultValue)
            {
                case float f:
                    valueType = ValueType.Float;
                    defaultValueFloat = f;
                    break;
                case bool b:
                    defaultValueFloat = b ? 1f : 0f;
                    break;
                case int i:
                    valueType = ValueType.Int;
                    defaultValueFloat = i;
                    break;
            }

            GetOrAddParameter(parameters, defaultValue is bool ? $"{parameterName}T" : parameterName, defaultValueFloat, valueType);

            animator.AddLayer(new AnimatorControllerLayer()
            {
                defaultWeight = 1f,
                name = selectedObject.name,
                stateMachine = stateMachine
            });

            var lastLayer = animator.layers[animator.layers.Length - 1];

            var state1 = lastLayer.stateMachine.AddState($"Animation {(defaultValueFloat == 1f ? "ON" : "OFF")}");
            state1.motion = CreateAnimationToggle(selectedObject, parameterName, defaultValueFloat == 1f);

            var state2 = lastLayer.stateMachine.AddState($"Animation {(defaultValueFloat != 1f ? "ON" : "OFF")}");
            state2.motion = CreateAnimationToggle(selectedObject, parameterName, defaultValueFloat != 1f);

            AnimatorConditionMode condition1 = AnimatorConditionMode.If;
            AnimatorConditionMode condition2 = AnimatorConditionMode.IfNot;
            if (defaultValue is bool && defaultValueFloat == 1)
            {
                condition1 = AnimatorConditionMode.IfNot;
                condition2 = AnimatorConditionMode.If;
            }
            else if (defaultValue is int)
            {
                condition1 = AnimatorConditionMode.Equals;
                condition2 = AnimatorConditionMode.NotEqual;
            }

            var newTransition = new AnimatorStateTransition()
            {
                destinationState = state2,
                conditions = new AnimatorCondition[1]
                {
                    new AnimatorCondition()
                    {
                        mode = condition1,
                        parameter = defaultValue is bool ? $"{parameterName}T" : parameterName,
                        threshold = defaultValueFloat,
                    }
                }
            };
            state1.AddTransition(newTransition);
            AssetDatabase.AddObjectToAsset(newTransition, state1);

            newTransition = new AnimatorStateTransition()
            {
                destinationState = state1,
                conditions = new AnimatorCondition[1]
                {
                    new AnimatorCondition()
                    {
                        mode = condition2,
                        parameter = defaultValue is bool ? $"{parameterName}T" : parameterName,
                        threshold = defaultValueFloat,
                    }
                }
            };
            state2.AddTransition(newTransition);
            AssetDatabase.AddObjectToAsset(newTransition, state2);
            EditorUtility.SetDirty(animator);
            AssetDatabase.SaveAssets();
            return true;
        }

        AnimationClip CreateAnimationToggle(GameObject selectedObject, string name, bool state)
        {
            AnimationClip clip = new AnimationClip();
            clip.name = $"{name} {(state ? "ON" : "OFF")}";
            clip.wrapMode = WrapMode.Loop;

            var curve = new AnimationCurve(new Keyframe[1] { new Keyframe(0, state ? 1 : 0) });
            var path = selectedObject.transform.GetPath().Replace(vrcAvatar.transform.GetPath(), "").Remove(0, 1);
            if (selectedObject.TryGetComponent<SkinnedMeshRenderer>(out _))
                clip.SetCurve(path, typeof(SkinnedMeshRenderer), "m_Enabled", curve);
            else
                clip.SetCurve(path, typeof(GameObject), "m_IsActive", curve);
            AssetDatabase.CreateAsset(clip, $"Assets/AutoGen/{$"{name}_{(state ? "ON" : "OFF")}"}.anim");
            return clip;
        }     

        bool CreateDanceAnimation(VRCExpressionsMenu.Control control, AnimationClip animationClip, AudioClip audioClip = null)
        {
            if (animationClip == null)
            {
                Debug.LogError($"Animation clip is null!");
                return false;
            }

            if (vrcAvatar.baseAnimationLayers[3].animatorController is AnimatorController ac)
            {
                var actionLayer = ac.layers.FirstOrDefault(p => p.name == "Action");
                if (actionLayer == null)
                {
                    Debug.LogError("Use default Action controller for your avatar! ( Action layer not found )");
                    return false;
                }

                var aaState = actionLayer.stateMachine.states.FirstOrDefault(p => p.state.name == "WaitForActionOrAFK").state;
                if (aaState == null)
                {
                    Debug.LogError("Use default Action controller for your avatar! ( WaitForActionOrAFK state not found )");
                    return false;
                }

                var proxyStandingMotion = Resources.FindObjectsOfTypeAll<AnimationClip>().FirstOrDefault(p => p.name == "proxy_stand_still");

                if (proxyStandingMotion == null)
                {
                    Debug.LogError("Proxy standing motion not found!");
                    return false;
                }

                var eeState = actionLayer.stateMachine.states.FirstOrDefault(p => p.state.name == "ExpressionEditor").state;
                if (eeState == null)
                {
                    eeState = actionLayer.stateMachine.AddState("ExpressionEditor", new Vector3(-1000f, 50f, 0f));

                    eeState.motion = proxyStandingMotion;

                    var layerControl = eeState.AddStateMachineBehaviour<VRCPlayableLayerControl>();
                    layerControl.blendDuration = 0.5f;
                    layerControl.goalWeight = 1f;

                    var trackingControl = eeState.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
                    trackingControl.trackingHead = TrackingType.Animation;
                    trackingControl.trackingLeftHand = TrackingType.Animation;
                    trackingControl.trackingRightHand = TrackingType.Animation;
                    trackingControl.trackingHip = TrackingType.Animation;
                    trackingControl.trackingLeftFoot = TrackingType.Animation;
                    trackingControl.trackingRightFoot = TrackingType.Animation;
                    trackingControl.trackingLeftFingers = TrackingType.Animation;
                    trackingControl.trackingRightFingers = TrackingType.Animation;

                    var newTransition = new AnimatorStateTransition()
                    {
                        destinationState = eeState
                    };
                    aaState.AddTransition(newTransition);
                    AssetDatabase.AddObjectToAsset(newTransition, aaState);
                }
                int freeVRCEmote = -1;
                AnimatorState state = null;
                List<int> usedVRCEmotes = new List<int>();

                List<AnimatorStateTransition> transitions = new List<AnimatorStateTransition>();

                foreach(var sstate in actionLayer.stateMachine.states)
                {
                    transitions.AddRange(sstate.state.transitions);
                }

                foreach(var transitionx in transitions)
                {
                    int greaterValue = -1;
                    int lessValue = -1;
                    foreach (var cond in transitionx.conditions)
                    {
                        if (cond.parameter != "VRCEmote")
                            continue;
                        switch (cond.mode)
                        {
                            case AnimatorConditionMode.Greater:
                                greaterValue = (int)cond.threshold;
                                break;
                            case AnimatorConditionMode.Less:
                                lessValue = (int)cond.threshold;
                                break;
                        }
                    }
                    if (greaterValue != -1 && lessValue != -1)
                    {
                        List<int> ids = new List<int>();
                        if (transitionx.destinationState != eeState)
                            goto skip;
                        foreach(var trans in transitionx.destinationState.transitions)
                        {
                            foreach(var condition in trans.conditions)
                            {
                                if (condition.parameter != "VRCEmote")
                                    continue;
                                if (condition.mode != AnimatorConditionMode.Equals)
                                    continue;
                                ids.Add((int)condition.threshold);
                            }
                        }

                        int realGreaterValue = ids.Count == 0 ? -1 : ids.OrderBy(p => p).First();
                        int realLessValue = ids.Count == 0 ? -1 : ids.OrderByDescending(p => p).First();

                        if (realGreaterValue == realLessValue)
                        {
                            if (realGreaterValue == -1)
                                continue;
                            if (!usedVRCEmotes.Contains(realGreaterValue))
                                usedVRCEmotes.Add(realGreaterValue);
                            continue;
                        }

                        if (greaterValue != realGreaterValue)
                            greaterValue = realGreaterValue;

                        if (lessValue != realLessValue)
                            lessValue = realLessValue;

                        skip:
                        for (int x = greaterValue + 1; x < lessValue; x++)
                        {
                            if (!usedVRCEmotes.Contains(x))
                                usedVRCEmotes.Add(x);
                        }
                    }
                }



                foreach (var tr in eeState.transitions)
                {
                    foreach (var condition in tr.conditions)
                    {
                        if (tr.destinationState.name == $"{animationClip.name}")
                        {
                            state = tr.destinationState;
                            freeVRCEmote = (int)condition.threshold;
                        }
                    }
                }

                int higestValue = usedVRCEmotes.OrderByDescending(p => p).First();

                if (freeVRCEmote == -1)
                {
                    freeVRCEmote = higestValue + 1;
                }

                var blendout = actionLayer.stateMachine.states.FirstOrDefault(p => p.state.name == "EE BlendOut").state;
                if (blendout == null)
                {
                    blendout = actionLayer.stateMachine.AddState("EE BlendOut", new Vector3(-1000f, 350f, 0f));
                    blendout.motion = proxyStandingMotion;
                    var layerControl = blendout.AddStateMachineBehaviour<VRCPlayableLayerControl>();
                    layerControl.goalWeight = 0f;
                    layerControl.blendDuration = 0.25f;
                }

                var transition = aaState.transitions.FirstOrDefault(p => p.destinationState == eeState);
                if (transition != null)
                {
                    AnimatorState newAnim = null;
                    if (state != null)
                        newAnim = state;
                    else
                    {
                        transition.conditions = new AnimatorCondition[]
                        {
                            new AnimatorCondition()
                            {
                                mode = AnimatorConditionMode.Greater,
                                parameter = "VRCEmote",
                                threshold = higestValue,
                            },
                            new AnimatorCondition()
                            {
                                mode = AnimatorConditionMode.Less,
                                parameter = "VRCEmote",
                                threshold = higestValue + 2
                            }
                        };
                        newAnim = actionLayer.stateMachine.AddState($"{animationClip.name}", new Vector3(-1000f, 200f, 0f));
                    }

                    newAnim.motion = animationClip;

                    if (state == null)
                    {
                        var newTransition = new AnimatorStateTransition()
                        {
                            destinationState = newAnim,
                            hasExitTime = false,
                            conditions = new AnimatorCondition[]
                            {
                                new AnimatorCondition()
                                {
                                    mode = AnimatorConditionMode.Equals,
                                    parameter = "VRCEmote",
                                    threshold = freeVRCEmote
                                }
                            }
                        };
                        eeState.AddTransition(newTransition);
                        AssetDatabase.AddObjectToAsset(newTransition, eeState);

                        newTransition = new AnimatorStateTransition()
                        {
                            destinationState = blendout,
                            hasExitTime = false,
                            conditions = new AnimatorCondition[]
                            {
                                new AnimatorCondition()
                                {
                                    mode = AnimatorConditionMode.NotEqual,
                                    parameter = "VRCEmote",
                                    threshold = freeVRCEmote
                                }
                            }
                        };
                        newAnim.AddTransition(newTransition);
                        AssetDatabase.AddObjectToAsset(newTransition, eeState);
                    }
                }

                var resetTrack = actionLayer.stateMachine.states.FirstOrDefault(p => p.state.name == "EE Reset Tracking").state;
                if (resetTrack == null)
                {
                    resetTrack = actionLayer.stateMachine.AddState("EE Reset Tracking", new Vector3(-1000f, 500f, 0f));
                    resetTrack.motion = proxyStandingMotion;
                    var trackingControl = resetTrack.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
                    trackingControl.trackingHead = TrackingType.Tracking;
                    trackingControl.trackingLeftHand = TrackingType.Tracking;
                    trackingControl.trackingRightHand = TrackingType.Tracking;
                    trackingControl.trackingHip = TrackingType.Tracking;
                    trackingControl.trackingLeftFoot = TrackingType.Tracking;
                    trackingControl.trackingRightFoot = TrackingType.Tracking;
                    trackingControl.trackingLeftFingers = TrackingType.Tracking;
                    trackingControl.trackingRightFingers = TrackingType.Tracking;

                    var newTransition = new AnimatorStateTransition()
                    {
                        destinationState = resetTrack,
                    };
                    blendout.AddTransition(newTransition);
                    AssetDatabase.AddObjectToAsset(newTransition, blendout);

                    resetTrack.AddExitTransition(true);
                }

                EditorUtility.SetDirty(ac);
                AssetDatabase.SaveAssets();

                control.parameter = new VRCExpressionsMenu.Control.Parameter()
                {
                    name = $"VRCEmote"
                };
                control.value = freeVRCEmote;
                parameters = GetParams();
                selectedIndexes[control][0] = GetIndexOfParam(control.parameter);

                if (audioClip == null)
                    return true;

                GameObject animations = null;
                for (int x = 0; x < vrcAvatar.transform.childCount; x++)
                {
                    if (vrcAvatar.transform.GetChild(x).name == "Animations")
                    {
                        animations = vrcAvatar.transform.GetChild(x).gameObject;
                    }
                }
                if (animations == null)
                {
                    animations = new GameObject("Animations");
                    animations.transform.parent = vrcAvatar.transform;
                    animations.transform.localPosition = Vector3.zero;
                }
                GameObject existingAnimation = null;
                for (int x = 0; x < animations.transform.childCount; x++)
                {
                    if (animations.transform.GetChild(x).name == $"A {animationClip.name}")
                    {
                        existingAnimation = animations.transform.GetChild(x).gameObject;
                    }
                }
                if (existingAnimation == null)
                {
                    existingAnimation = new GameObject($"A {animationClip.name}");
                    existingAnimation.transform.parent = animations.transform;
                    existingAnimation.transform.localPosition = Vector3.zero;
                    var audioSource = existingAnimation.AddComponent<AudioSource>();
                    audioSource.clip = audioClip;
                    audioSource.spatialBlend = 1f;
                    audioSource.maxDistance = 50f;
                    audioSource.minDistance = 2f;
                    audioSource.bypassEffects = true;
                    audioSource.bypassListenerEffects = true;
                    audioSource.bypassReverbZones = true;
                    audioSource.priority = 256;
                }
                else
                {
                    var audioSource = existingAnimation.GetComponent<AudioSource>();
                    audioSource.clip = audioClip;
                    audioSource.spatialBlend = 1f;
                    audioSource.maxDistance = 50f;
                    audioSource.minDistance = 2f;
                    audioSource.bypassEffects = true;
                    audioSource.bypassListenerEffects = true;
                    audioSource.bypassReverbZones = true;
                    audioSource.priority = 256;
                }
                existingAnimation.SetActive(false);
                if (vrcAvatar.baseAnimationLayers[4].animatorController is AnimatorController ac2)
                {
                    CreateGameObjectToggle(vrcAvatar.expressionParameters, ac2, existingAnimation, "VRCEmote", freeVRCEmote);
                }
            }
            return true;
        }


        Parameter GetOrAddParameter(VRCExpressionParameters expressionParameters, string parameterName, float defaultValue, ValueType type)
        {
            if (defaultValue > 1f)
                defaultValue = 0f;

            var foundParam = expressionParameters.FindParameter(parameterName);
            if (foundParam == null)
            {
                foundParam = new Parameter()
                {
                    valueType = type,
                    defaultValue = defaultValue,
                    saved = true,
                    name = parameterName
                };
                expressionParameters.parameters = expressionParameters.parameters.Append(foundParam).ToArray();
            }
            else
            {
                foundParam.valueType = type;
                foundParam.defaultValue = defaultValue;
                foundParam.saved = true;
            }
            return foundParam;
        }

        void CreateFoldable(VRCExpressionsMenu menu, VRCExpressionsMenu.Control control, int num)
        {
            if (!foldouts.ContainsKey(control))
                foldouts.Add(control, new List<bool>() { false });

            bool isShow = foldouts[control][0];

            var newParams = GetParams();
            var newRotParams = GetParams(true);

            if (parameters != newParams)
                parameters = newParams;

            if (rotationParameters != newRotParams)
                rotationParameters = newRotParams;

            switch (control.type)
            {
                case VRCExpressionsMenu.Control.ControlType.SubMenu:
                    foldouts[control][0] = EditorGUILayout.Foldout(isShow, $"{GetSpaces(num + 2)}Submenu");
                    if (isShow)
                    {
                        AddControlButtons(menu, control);
                        control.name = EditorGUILayout.TextField($"{GetSpaces(num)}Name", control.name);
                        control.icon = (Texture2D)EditorGUILayout.ObjectField($"{GetSpaces(num + 2)}Icon", control.icon, typeof(Texture2D), true);
                        control.subMenu = (VRCExpressionsMenu)EditorGUILayout.ObjectField($"{GetSpaces(num + 2)}Menu object", control.subMenu, typeof(VRCExpressionsMenu), true);
                        if (!scrollViews.ContainsKey(control.subMenu))
                            scrollViews.Add(control.subMenu, new Vector2(0f, 0f));

                        scrollViews[control.subMenu] = EditorGUILayout.BeginScrollView(scrollViews[control.subMenu]);
                        foreach (var subMenuControl in control.subMenu.controls.ToArray())
                        {
                            CreateFoldable(control.subMenu, subMenuControl, num + 2);
                        }
                        EditorGUILayout.EndScrollView();
                        AddButtons(control.subMenu);
                        EditorGUILayout.Separator();
                    }
                    break;
                case VRCExpressionsMenu.Control.ControlType.Button:
                    foldouts[control][0] = EditorGUILayout.Foldout(isShow, $"{GetSpaces(num + 2)}Button");
                    if (isShow)
                    {
                        AddControlButtons(menu, control);
                        if (!selectedIndexes.ContainsKey(control))
                            selectedIndexes.Add(control, new List<int>() { GetIndexOfParam(control.parameter) });

                        control.name = EditorGUILayout.TextField($"{GetSpaces(num + 2)}Name", control.name);
                        control.icon = (Texture2D)EditorGUILayout.ObjectField($"{GetSpaces(num + 2)}Icon", control.icon, typeof(Texture2D), true);
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label("Parameter");
                        selectedIndexes[control][0] = EditorGUILayout.Popup(selectedIndexes[control][0], parameters.Values.Select(p => p.Name).ToArray());
                        EditorGUILayout.EndHorizontal();

                        var currentParam = GetParam(selectedIndexes[control][0]);

                        if (currentParam != null)
                        {
                            if (control.parameter?.name != currentParam.name)
                                control.parameter = new VRCExpressionsMenu.Control.Parameter()
                                {
                                    name = currentParam.name
                                };

                            switch (currentParam.valueType)
                            {
                                case ValueType.Float:
                                    control.value = EditorGUILayout.FloatField("Value", control.value);
                                    break;
                                case ValueType.Int:
                                    control.value = EditorGUILayout.IntField("Value", (int)control.value);
                                    break;
                            }
                        }
                        else if (control.parameter != null)
                        {
                            control.parameter = null;
                        }

                        EditorGUILayout.Separator();
                    }
                    break;
                case VRCExpressionsMenu.Control.ControlType.Toggle:
                    foldouts[control][0] = EditorGUILayout.Foldout(isShow, $"{GetSpaces(num + 2)}Toggle");
                    if (isShow)
                    {
                        AddControlButtons(menu, control);

                        EditorGUILayout.BeginVertical();
                        GUILayout.Label("Add premade");

                        if (foldouts[control].Count == 1)
                            foldouts[control].Add(false);

                        if (foldouts[control].Count == 2)
                            foldouts[control].Add(false);

                        foldouts[control][1] = EditorGUILayout.Foldout(foldouts[control][1], "Toggle gameobject");
                        if (foldouts[control][1])
                        {
                            if (!tempdatas.ContainsKey(control))
                                tempdatas.Add(control, new Dictionary<int, TempData>());

                            if (!tempdatas[control].ContainsKey(0))
                                tempdatas[control].Add(0, new TempData());

                            if (tempdatas[control].TryGetValue(0, out TempData outData))
                            {
                                outData.SelectedGameObject = (GameObject)EditorGUILayout.ObjectField("Select gameobject", outData.SelectedGameObject, typeof(GameObject), true);
                                outData.isEnabledByDefault = EditorGUILayout.Toggle("Enabled by default", outData.isEnabledByDefault);
                                outData.ParameterName = EditorGUILayout.TextField("Parameter name", outData.ParameterName);
                                if (GUILayout.Button("Create"))
                                {
                                    if (outData.SelectedGameObject != null)
                                    {
                                        outData.SelectedGameObject.SetActive(outData.isEnabledByDefault);

                                        GetOrAddParameter(vrcAvatar.expressionParameters, $"{outData.ParameterName}T", outData.isEnabledByDefault ? 1 : 0, ValueType.Bool);

                                        control.parameter = new VRCExpressionsMenu.Control.Parameter()
                                        {
                                            name = $"{outData.ParameterName}T"
                                        };

                                        parameters = GetParams();
                                        selectedIndexes[control][0] = GetIndexOfParam(control.parameter);
                                        
                                        if (vrcAvatar.baseAnimationLayers[4].animatorController is AnimatorController ac)
                                        {
                                            CreateGameObjectToggle(vrcAvatar.expressionParameters, ac, outData.SelectedGameObject, outData.ParameterName, outData.isEnabledByDefault);
                                        }
                                        foldouts[control][1] = false;
                                        tempdatas[control].Remove(0);
                                    }
                                    else
                                    {
                                        Debug.Log("OBJECT IS NULL");
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (tempdatas.TryGetValue(control, out Dictionary<int, TempData> dataOut))
                            {
                                if (dataOut.ContainsKey(0))
                                    dataOut.Remove(0);
                            }
                        }

                        foldouts[control][2] = EditorGUILayout.Foldout(foldouts[control][2], "Dance");
                        if (foldouts[control][2])
                        {
                            if (!tempdatas.ContainsKey(control))
                                tempdatas.Add(control, new Dictionary<int, TempData>());

                            if (!tempdatas[control].ContainsKey(1))
                                tempdatas[control].Add(1, new TempData());

                            if (tempdatas[control].TryGetValue(1, out TempData outData))
                            {
                                outData.SelectedAnimationClip = (AnimationClip)EditorGUILayout.ObjectField("Select animation", outData.SelectedAnimationClip, typeof(AnimationClip), true);
                                outData.SelectedAudioClip = (AudioClip)EditorGUILayout.ObjectField("Select song", outData.SelectedAudioClip, typeof(AudioClip), true);
                                if (GUILayout.Button("Create"))
                                {
                                    if (CreateDanceAnimation(control, outData.SelectedAnimationClip, outData.SelectedAudioClip))
                                    {
                                        foldouts[control][2] = false;
                                        tempdatas[control].Remove(1);
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (tempdatas.TryGetValue(control, out Dictionary<int, TempData> dataOut))
                            {
                                if (dataOut.ContainsKey(1))
                                    dataOut.Remove(1);
                            }
                        }
                        EditorGUILayout.EndVertical();

                        if (!selectedIndexes.ContainsKey(control))
                            selectedIndexes.Add(control, new List<int>() { GetIndexOfParam(control.parameter) });

                        control.name = EditorGUILayout.TextField($"{GetSpaces(num + 2)}Name", control.name);
                        control.icon = (Texture2D)EditorGUILayout.ObjectField($"{GetSpaces(num + 2)}Icon", control.icon, typeof(Texture2D), true);
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label("Parameter");
                        selectedIndexes[control][0] = EditorGUILayout.Popup(selectedIndexes[control][0], parameters.Values.Select(p => p.Name).ToArray());
                        EditorGUILayout.EndHorizontal();

                        var currentParam = GetParam(selectedIndexes[control][0]);

                        if (currentParam != null)
                        {
                            if (control.parameter?.name != currentParam.name)
                                control.parameter = new VRCExpressionsMenu.Control.Parameter()
                                {
                                    name = currentParam.name
                                };

                            switch (currentParam.valueType)
                            {
                                case VRCExpressionParameters.ValueType.Float:
                                    control.value = EditorGUILayout.FloatField("Value", control.value);
                                    break;
                                case VRCExpressionParameters.ValueType.Int:
                                    control.value = EditorGUILayout.IntField("Value", (int)control.value);
                                    break;
                            }
                        }
                        else if (control.parameter != null)
                        {
                            control.parameter = null;
                        }

                        EditorGUILayout.Separator();
                    }
                    break;
                case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                    foldouts[control][0] = EditorGUILayout.Foldout(isShow, $"{GetSpaces(num + 2)}TwoAxis Puppet");
                    if (isShow)
                    {
                        AddControlButtons(menu, control);

                        if (control.subParameters.Length == 0)
                            control.subParameters = new VRCExpressionsMenu.Control.Parameter[2];

                        if (!selectedIndexes.ContainsKey(control))
                            selectedIndexes.Add(control, new List<int>()
                        {
                            GetIndexOfParam(control.subParameters[0], true),
                            GetIndexOfParam(control.subParameters[1], true)
                        });

                        control.name = EditorGUILayout.TextField($"{GetSpaces(num + 2)}Name", control.name);
                        control.icon = (Texture2D)EditorGUILayout.ObjectField($"{GetSpaces(num + 2)}Icon", control.icon, typeof(Texture2D), true);
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label("Parameter Horizontal");
                        selectedIndexes[control][0] = EditorGUILayout.Popup(selectedIndexes[control][0], rotationParameters.Values.Select(p => p.Name).ToArray());
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label("Parameter Vertical");
                        selectedIndexes[control][1] = EditorGUILayout.Popup(selectedIndexes[control][1], rotationParameters.Values.Select(p => p.Name).ToArray());
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.Separator();

                        var currentParam = GetParam(selectedIndexes[control][0], true);
                        var currentParam2 = GetParam(selectedIndexes[control][1], true);
                        var subParam = control.GetSubParameter(0);
                        var subParam2 = control.GetSubParameter(1);

                        if (currentParam != null)
                        {
                            if (subParam == null)
                            {
                                control.subParameters[0] = new VRCExpressionsMenu.Control.Parameter()
                                {
                                    name = currentParam.name
                                };
                            }
                            else
                            {
                                if (subParam.name != currentParam.name)
                                {
                                    control.subParameters[0] = new VRCExpressionsMenu.Control.Parameter()
                                    {
                                        name = currentParam.name
                                    };
                                }
                            }
                        }
                        else if (subParam != null)
                        {
                            control.subParameters[0] = null;
                        }


                        if (currentParam2 != null)
                        {
                            if (subParam2 == null)
                            {
                                control.subParameters[1] = new VRCExpressionsMenu.Control.Parameter()
                                {
                                    name = currentParam.name
                                };
                            }
                            else
                            {
                                if (subParam2.name != currentParam2.name)
                                {
                                    control.subParameters[1] = new VRCExpressionsMenu.Control.Parameter()
                                    {
                                        name = currentParam2.name
                                    };
                                }
                            }
                        }
                        else if (subParam2 != null)
                        {
                            control.subParameters[1] = null;
                        }
                    }
                    break;
                case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                    foldouts[control][0] = EditorGUILayout.Foldout(isShow, $"{GetSpaces(num + 2)}FourAxis Puppet");

                    if (isShow)
                    {
                        AddControlButtons(menu, control);
                        control.name = EditorGUILayout.TextField($"{GetSpaces(num + 2)}Name", control.name);
                        control.icon = (Texture2D)EditorGUILayout.ObjectField($"{GetSpaces(num + 2)}Icon", control.icon, typeof(Texture2D), true);
                        EditorGUILayout.Separator();
                    }
                    break;
                case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                    foldouts[control][0] = EditorGUILayout.Foldout(isShow, $"{GetSpaces(num + 2)}Radial Puppet");
                    if (isShow)
                    {
                        AddControlButtons(menu, control);

                        if (control.subParameters.Length == 0)
                            control.subParameters = new VRCExpressionsMenu.Control.Parameter[1];

                        if (!selectedIndexes.ContainsKey(control))
                            selectedIndexes.Add(control, new List<int>()
                        {
                            GetIndexOfParam(control.subParameters[0], true)
                        });

                        control.name = EditorGUILayout.TextField($"{GetSpaces(num + 2)}Name", control.name);
                        control.icon = (Texture2D)EditorGUILayout.ObjectField($"{GetSpaces(num + 2)}Icon", control.icon, typeof(Texture2D), true);
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label("Parameter");
                        selectedIndexes[control][0] = EditorGUILayout.Popup(selectedIndexes[control][0], rotationParameters.Values.Select(p => p.Name).ToArray());
                        EditorGUILayout.EndHorizontal();

                        var currentParam = GetParam(selectedIndexes[control][0], true);
                        var subParam = control.GetSubParameter(0);

                        if (currentParam != null)
                        {
                            if (subParam == null)
                            {
                                control.subParameters[0] = new VRCExpressionsMenu.Control.Parameter()
                                {
                                    name = currentParam.name
                                };
                            }
                            else
                            {
                                if (subParam.name != currentParam.name)
                                    control.subParameters[0] = new VRCExpressionsMenu.Control.Parameter()
                                    {
                                        name = currentParam.name
                                    };
                            }
                        }
                        else if (subParam != null)
                        {
                            control.subParameters[0] = null;
                        }
                        EditorGUILayout.Separator();
                    }
                    break;
            }
        }

        void AddControlButtons(VRCExpressionsMenu menu, VRCExpressionsMenu.Control control)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Delete"))
            {
                menu.controls.Remove(control);
            }
            EditorGUILayout.EndHorizontal();
        }

        void AddButtons(VRCExpressionsMenu menu)
        {
            GUILayout.Label($"Add new");
            EditorGUILayout.BeginHorizontal();
            if (menu.controls.Count <= 8)
            {
                if (GUILayout.Button("Button"))
                {
                    menu.controls.Add(new VRCExpressionsMenu.Control()
                    {
                        type = VRCExpressionsMenu.Control.ControlType.Button,
                        name = "Button"
                    });
                    EditorUtility.SetDirty(menu);
                    AssetDatabase.SaveAssets();
                }
                if (GUILayout.Button("Toggle"))
                {
                    menu.controls.Add(new VRCExpressionsMenu.Control()
                    {
                        type = VRCExpressionsMenu.Control.ControlType.Toggle,
                        name = "Toggle"
                    });
                    EditorUtility.SetDirty(menu);
                    AssetDatabase.SaveAssets();
                }
                if (GUILayout.Button("TwoAxis"))
                {
                    menu.controls.Add(new VRCExpressionsMenu.Control()
                    {
                        type = VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet,
                        subParameters = new VRCExpressionsMenu.Control.Parameter[2],
                        name = "TwoAxis"
                    });
                    EditorUtility.SetDirty(menu);
                    AssetDatabase.SaveAssets();
                }
                if (GUILayout.Button("FourAxis"))
                {
                    menu.controls.Add(new VRCExpressionsMenu.Control()
                    {
                        type = VRCExpressionsMenu.Control.ControlType.FourAxisPuppet,
                        subParameters = new VRCExpressionsMenu.Control.Parameter[2],
                        name = "FourAxis"
                    });
                    EditorUtility.SetDirty(menu);
                    AssetDatabase.SaveAssets();
                }
                if (GUILayout.Button("Radial"))
                {
                    menu.controls.Add(new VRCExpressionsMenu.Control()
                    {
                        type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                        subParameters = new VRCExpressionsMenu.Control.Parameter[1],
                        name = "Radial"
                    });
                    EditorUtility.SetDirty(menu);
                    AssetDatabase.SaveAssets();
                }
                if (GUILayout.Button("Submenu"))
                {
                    VRCExpressionsMenu eMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();

                    if (!AssetDatabase.IsValidFolder(Path.Combine("Assets", "AutoGen")))
                        AssetDatabase.CreateFolder("Assets", "AutoGen");

                    string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath($"Assets/AutoGen/SubMenu.asset");
                    AssetDatabase.CreateAsset(eMenu, assetPathAndName);

                    menu.controls.Add(new VRCExpressionsMenu.Control()
                    {
                        type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                        name = "SubMenu",
                        subMenu = eMenu
                    });
                    EditorUtility.SetDirty(menu);
                    AssetDatabase.SaveAssets();
                }
            }
            else
            {
                GUILayout.Label("You cant add more than 8 controls per menu.");
            }

            EditorGUILayout.EndHorizontal();
        }

        public void OnDestroy()
        {
            if (vrcAvatar?.expressionParameters != null)
            {
                EditorUtility.SetDirty(vrcAvatar.expressionParameters);
                AssetDatabase.SaveAssets();
            }
            if (vrcAvatar?.expressionsMenu != null)
            {
                EditorUtility.SetDirty(vrcAvatar.expressionsMenu);
                AssetDatabase.SaveAssets();
            }
        }

        void OnGUI()
        {
            if (!AssetDatabase.IsValidFolder("Assets/AutoGen"))
                AssetDatabase.CreateFolder("Assets", "AutoGen");
            if (Application.isPlaying)
            {
                GUILayout.Label("Stop playmode to use Expression editor.");
                return;
            }
            GUILayout.Label("Avatar", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical();
            foreach (var avatar in GameObject.FindObjectsOfType<VRCAvatarDescriptor>())
            {
                if (GUILayout.Button(avatar.name))
                {
                    vrcAvatar = avatar;
                }
            }
            GUILayout.Label($"Selected {vrcAvatar?.name ?? "NONE"}");
            EditorGUILayout.EndVertical();
            GUILayout.Space(35f);
            GUILayout.Label("Expressions menu");
            if (vrcAvatar != null)
            {
                vrcAvatar.expressionsMenu = (VRCExpressionsMenu)EditorGUILayout.ObjectField("Expressions object", vrcAvatar.expressionsMenu, typeof(VRCExpressionsMenu), true);
                if (vrcAvatar.expressionsMenu == null)
                {
                    GUILayout.Label("Expressions menu on your avatar is null do you want to create one?");
                    if (GUILayout.Button("Create"))
                    {
                        VRCExpressionsMenu menu = new VRCExpressionsMenu();
                        string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath($"Assets/AutoGen/ExpressionsMenu.asset");
                        AssetDatabase.CreateAsset(menu, assetPathAndName);
                        vrcAvatar.expressionsMenu = menu;
                    }
                    return;
                }
                vrcAvatar.expressionParameters = (VRCExpressionParameters)EditorGUILayout.ObjectField("Parameters object", vrcAvatar.expressionParameters, typeof(VRCExpressionParameters), true);
                if (vrcAvatar.expressionParameters == null)
                {
                    GUILayout.Label("Expressions parameters on your avatar is null do you want to create one?");
                    if (GUILayout.Button("Create"))
                    {
                        VRCExpressionParameters menu = new VRCExpressionParameters();
                        string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath($"Assets/AutoGen/ExpressionsParameters.asset");
                        AssetDatabase.CreateAsset(menu, assetPathAndName);
                        vrcAvatar.expressionParameters = menu;
                    }
                    return;
                }
                if (!scrollViews.ContainsKey(vrcAvatar.expressionsMenu))
                    scrollViews.Add(vrcAvatar.expressionsMenu, new Vector2(0f, 0f));

                scrollViews[vrcAvatar.expressionsMenu] = EditorGUILayout.BeginScrollView(scrollViews[vrcAvatar.expressionsMenu]);
                foreach (var control in vrcAvatar.expressionsMenu.controls.ToArray())
                {
                    CreateFoldable(vrcAvatar.expressionsMenu, control, -2);
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.Space(10f);
                AddButtons(vrcAvatar.expressionsMenu);
            }
        }
    }
}