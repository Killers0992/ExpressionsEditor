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

        AnimationClip CreateAnimationToggle(GameObject selectedObject, string name, bool state)
        {
            AnimationClip clip = new AnimationClip();
            clip.name = $"{name} {(state ? "ON" : "OFF")}";
            clip.wrapMode = WrapMode.Loop;

            var curve = new AnimationCurve(new Keyframe[1] { new Keyframe(0, state ? 1 : 0) });
            clip.SetCurve(selectedObject.transform.GetPath().Replace(vrcAvatar.transform.GetPath(), "").Remove(0, 1), typeof(GameObject), "m_IsActive", curve);
            AssetDatabase.CreateAsset(clip, $"Assets/AutoGen/{$"{name}_{(state ? "ON" : "OFF")}"}.anim");
            return clip;
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
                case VRCExpressionsMenu.Control.ControlType.Toggle:
                    foldouts[control][0] = EditorGUILayout.Foldout(isShow, $"{GetSpaces(num + 2)}Toggle");
                    if (isShow)
                    {
                        AddControlButtons(menu, control);

                        EditorGUILayout.BeginVertical();
                        GUILayout.Label("Add premade");

                        if (foldouts[control].Count == 1)
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

                                        var foundParam = vrcAvatar.expressionParameters.FindParameter($"{outData.ParameterName}T");
                                        if (foundParam == null)
                                        {
                                            vrcAvatar.expressionParameters.parameters = vrcAvatar.expressionParameters.parameters.Append(new Parameter()
                                            {
                                                valueType = ValueType.Bool,
                                                defaultValue = outData.isEnabledByDefault ? 1 : 0,
                                                saved = true,
                                                name = $"{outData.ParameterName}T"
                                            }).ToArray();
                                        }
                                        else
                                        {
                                            for (int x = 0; x < vrcAvatar.expressionParameters.parameters.Length; x++)
                                            {
                                                if (vrcAvatar.expressionParameters.parameters[x].name == $"{outData.ParameterName}T")
                                                {
                                                    vrcAvatar.expressionParameters.parameters[x].valueType = ValueType.Bool;
                                                    vrcAvatar.expressionParameters.parameters[x].defaultValue = outData.isEnabledByDefault ? 1f : 0f;
                                                    vrcAvatar.expressionParameters.parameters[x].saved = true;
                                                }
                                            }
                                        }

                                        control.parameter = new VRCExpressionsMenu.Control.Parameter()
                                        {
                                            name = $"{outData.ParameterName}T"
                                        };
                                        control.value = 1f;
                                        parameters = GetParams();
                                        selectedIndexes[control][0] = GetIndexOfParam(control.parameter);

                                        if (vrcAvatar.baseAnimationLayers[4].animatorController is AnimatorController ac)
                                        {
                                            bool found = false;
                                            for (int x = 0; x < ac.parameters.Length; x++)
                                            {
                                                if (ac.parameters[x].name == $"{outData.ParameterName}T")
                                                {
                                                    ac.parameters[x].type = AnimatorControllerParameterType.Bool;
                                                    ac.parameters[x].defaultBool = outData.isEnabledByDefault;
                                                    found = true;
                                                }
                                            }
                                            if (!found)
                                            {
                                                ac.AddParameter(new AnimatorControllerParameter()
                                                {
                                                    defaultBool = outData.isEnabledByDefault,
                                                    type = AnimatorControllerParameterType.Bool,
                                                    name = $"{outData.ParameterName}T"
                                                });
                                            }

                                            for (int x = 0; x < ac.layers.Length; x++)
                                            {
                                                if (ac.layers[x].name == outData.ParameterName)
                                                {
                                                    ac.RemoveLayer(x);
                                                }
                                            }

                                            ac.AddLayer(new AnimatorControllerLayer()
                                            {
                                                defaultWeight = outData.isEnabledByDefault ? 0f : 1f,
                                                name = outData.ParameterName,
                                                stateMachine = new AnimatorStateMachine()
                                                {
                                                    name = outData.ParameterName
                                                }
                                            });

                                            var stateOff = ac.layers[ac.layers.Length - 1].stateMachine.AddState($"{outData.ParameterName} {(outData.isEnabledByDefault ? "ON" : "OFF")}");
                                            stateOff.motion = CreateAnimationToggle(outData.SelectedGameObject, outData.ParameterName, outData.isEnabledByDefault);

                                            var stateOn = ac.layers[ac.layers.Length - 1].stateMachine.AddState($"{outData.ParameterName} {(!outData.isEnabledByDefault ? "ON" : "OFF")}");
                                            stateOn.motion = CreateAnimationToggle(outData.SelectedGameObject, outData.ParameterName, !outData.isEnabledByDefault);


                                            stateOff.AddTransition(new AnimatorStateTransition()
                                            {
                                                destinationState = stateOn,
                                                conditions = new AnimatorCondition[1]
                                                {
                                                    new AnimatorCondition()
                                                    {
                                                        mode = outData.isEnabledByDefault ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If,
                                                        parameter = $"{outData.ParameterName}T",
                                                        threshold = 1f,
                                                    }
                                                }
                                            });

                                            stateOn.AddTransition(new AnimatorStateTransition()
                                            {
                                                destinationState = stateOff,
                                                conditions = new AnimatorCondition[1]
                                                {
                                                    new AnimatorCondition()
                                                    {
                                                        mode = outData.isEnabledByDefault ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                                                        parameter = $"{outData.ParameterName}T",
                                                        threshold = 1f,
                                                    }
                                                }
                                            });
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
                }
                if (GUILayout.Button("Toggle"))
                {
                    menu.controls.Add(new VRCExpressionsMenu.Control()
                    {
                        type = VRCExpressionsMenu.Control.ControlType.Toggle,
                        name = "Toggle"
                    });
                }
                if (GUILayout.Button("TwoAxis"))
                {
                    menu.controls.Add(new VRCExpressionsMenu.Control()
                    {
                        type = VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet,
                        subParameters = new VRCExpressionsMenu.Control.Parameter[2],
                        name = "TwoAxis"
                    });
                }
                if (GUILayout.Button("FourAxis"))
                {
                    menu.controls.Add(new VRCExpressionsMenu.Control()
                    {
                        type = VRCExpressionsMenu.Control.ControlType.FourAxisPuppet,
                        subParameters = new VRCExpressionsMenu.Control.Parameter[2],
                        name = "FourAxis"
                    });
                }
                if (GUILayout.Button("Radial"))
                {
                    menu.controls.Add(new VRCExpressionsMenu.Control()
                    {
                        type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                        subParameters = new VRCExpressionsMenu.Control.Parameter[1],
                        name = "Radial"
                    });
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
                }
            }
            else
            {
                GUILayout.Label("You cant add more than 8 controls per menu.");
            }

            EditorGUILayout.EndHorizontal();
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