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
    using System;
    using System.Net;
    using UnityEngine.Animations;

    public class ExpressionEditor : EditorWindow
    {
        public AudioClip SelectedAudioClip;
        public AnimationClip SelectedAnimationClip;
        public GameObject[] SelectedGameObjects = new GameObject[1];
        public bool IsEnabledByDefault;
        public string ParameterName;

        PageModel currentPage = null;
        string NameFilter = "";
        string PathDances = "Assets/Dances";
        static bool CanUpdate = false;
        static VersionModel CurrentVersion;
        static VersionModel LatestVersion;
        public static VRCAvatarDescriptor CurrentSelectedAvatar = null;
        readonly Dictionary<VRCExpressionsMenu, PageModel> pages = new Dictionary<VRCExpressionsMenu, PageModel>();
        public static Dictionary<int, ParamValue> parameters = new Dictionary<int, ParamValue>();
        public static Dictionary<int, ParamValue> rotationParameters = new Dictionary<int, ParamValue>();
        Dictionary<VRCExpressionsMenu, List<bool>> MainFoldOuts { get; set; } = new Dictionary<VRCExpressionsMenu, List<bool>>();
        public static readonly Dictionary<VRCExpressionsMenu.Control, List<int>> selectedIndexes = new Dictionary<VRCExpressionsMenu.Control, List<int>>();
        readonly Dictionary<VRCExpressionsMenu, Vector2> scrollViews = new Dictionary<VRCExpressionsMenu, Vector2>();
        readonly Dictionary<VRCExpressionsMenu.Control, List<bool>> foldouts = new Dictionary<VRCExpressionsMenu.Control, List<bool>>();

        [MenuItem("ExpressionEditor/Open Editor")]
        static void Init()
        {
            ExpressionEditor window = (ExpressionEditor)EditorWindow.GetWindow(typeof(ExpressionEditor), false, "ExpressionEditor");
            CheckUpdate();
            window.Show();
        }

        static void CheckUpdate()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ExpressionsEditor/version.json");
            CurrentVersion = JsonUtility.FromJson<VersionModel>(asset.text);
            LatestVersion = GetLatestVersion();
            if (LatestVersion.Ver.CompareTo(CurrentVersion.Ver) > 0)
                CanUpdate = true;
            else
                CanUpdate = false;
        }

        static VersionModel GetLatestVersion()
        {
            using(var web = new WebClient())
            {
                var str = web.DownloadString("https://raw.githubusercontent.com/Killers0992/ExpressionsEditor/main/ExpressionsEditor/version.json");
                if (!str.Contains("404"))
                    return JsonUtility.FromJson<VersionModel>(str);
            }
            return new VersionModel();
        }

        void Reset()
        {
            SelectedAudioClip = null;
            SelectedAnimationClip = null;
            SelectedGameObjects = new GameObject[1];
            IsEnabledByDefault = false;
            ParameterName = "";
        }

        void CreateFoldable(VRCExpressionsMenu menu, VRCExpressionsMenu.Control control, int num)
        {
            if (!foldouts.ContainsKey(control))
                foldouts.Add(control, new List<bool>() { false });

            bool isShow = foldouts[control][0];

            var newParams = Extensions.GetParams();
            var newRotParams = Extensions.GetParams(true);

            if (parameters != newParams)
                parameters = newParams;

            if (rotationParameters != newRotParams)
                rotationParameters = newRotParams;

            switch (control.type)
            {
                case VRCExpressionsMenu.Control.ControlType.SubMenu:
                    foldouts[control][0] = EditorGUILayout.Foldout(isShow, $"{control.name} (Submenu)");
                    if (isShow)
                    {
                        control.subMenu = (VRCExpressionsMenu)EditorGUILayout.ObjectField($"Menu object", control.subMenu, typeof(VRCExpressionsMenu), true);
                        if (control.subMenu != null)
                        {
                            if (GUILayout.Button("Open"))
                            {
                                pages.Add(control.subMenu, new PageModel()
                                {
                                    Control = currentPage.Control,
                                    Menu = menu
                                });
                                currentPage = new PageModel() { Menu = control.subMenu, Control = control };
                                foldouts[control][0] = false;
                            }
                        }
                        else
                        {
                            if (GUILayout.Button("Delete"))
                            {
                                menu.controls.Remove(control);
                                EditorUtility.SetDirty(menu);
                                AssetDatabase.SaveAssets();
                            }
                        }
                        EditorGUILayout.Separator();
                    }
                    break;
                case VRCExpressionsMenu.Control.ControlType.Button:
                    foldouts[control][0] = EditorGUILayout.Foldout(isShow, $"{control.name} (Button)");
                    if (isShow)
                    {
                        AddControlButtons(menu, control);
                        if (!selectedIndexes.ContainsKey(control))
                            selectedIndexes.Add(control, new List<int>() { control.parameter.GetIndex() });

                        control.name = EditorGUILayout.TextField($"Name", control.name);
                        control.icon = (Texture2D)EditorGUILayout.ObjectField($"Icon", control.icon, typeof(Texture2D), true);
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label("Parameter");
                        selectedIndexes[control][0] = EditorGUILayout.Popup(selectedIndexes[control][0], parameters.Values.Select(p => p.Name).ToArray());
                        EditorGUILayout.EndHorizontal();

                        var currentParam = Extensions.GetParam(selectedIndexes[control][0]);

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
                    foldouts[control][0] = EditorGUILayout.Foldout(isShow, $"{control.name} (Toggle)");
                    if (isShow)
                    {
                        AddControlButtons(menu, control);

                        EditorGUILayout.BeginVertical();
                        GUILayout.Label("Add premade");

                        if (foldouts[control].Count == 1)
                            foldouts[control].Add(false);

                        if (foldouts[control].Count == 2)
                            foldouts[control].Add(false);

                        if (foldouts[control].Count == 3)
                            foldouts[control].Add(false);

                        foldouts[control][1] = EditorGUILayout.Foldout(foldouts[control][1], "Toggle gameobject");
                        if (foldouts[control][1])
                        {
                            SerializedObject serialObj = new SerializedObject(this);
                            SerializedProperty serialProp = serialObj.FindProperty("SelectedGameObjects");
                            serialProp.isExpanded = true;
                            EditorGUILayout.PropertyField(serialProp, true);
                            serialObj.ApplyModifiedProperties();

                            IsEnabledByDefault = EditorGUILayout.Toggle("Enabled by default", IsEnabledByDefault);
                            ParameterName = EditorGUILayout.TextField("Parameter name", ParameterName);
                            if (GUILayout.Button("Create"))
                            {
                                if (SelectedGameObjects.Length != 0)
                                {
                                    foreach(var gb in SelectedGameObjects)
                                    {
                                        if (gb.TryGetComponent<SkinnedMeshRenderer>(out SkinnedMeshRenderer meshRender))
                                            meshRender.enabled = IsEnabledByDefault;
                                        else
                                            gb.SetActive(IsEnabledByDefault);

                                    }

                                    if (CurrentSelectedAvatar.baseAnimationLayers[4].animatorController == null)
                                    {
                                        if (EditorUtility.DisplayDialog("Missing animator controller in avatar", $"Do you want to create animator controller for FX?", "Create"))
                                        {
                                            if (!AssetDatabase.IsValidFolder("Assets/AutoGenerated"))
                                                AssetDatabase.CreateFolder("Assets", "AutoGenerated");

                                            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{CurrentSelectedAvatar.gameObject.name}"))
                                                AssetDatabase.CreateFolder("Assets/AutoGenerated", $"{CurrentSelectedAvatar.gameObject.name}");

                                            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{CurrentSelectedAvatar.gameObject.name}/Controllers"))
                                                AssetDatabase.CreateFolder($"Assets/AutoGenerated/{CurrentSelectedAvatar.gameObject.name}", "Controllers");

                                            var assetName = AssetDatabase.GenerateUniqueAssetPath($"Assets/AutoGenerated/{CurrentSelectedAvatar.gameObject.name}/Controllers/FX_AnimatorController.controller");

                                            var gestures = new AnimatorController();
                                            AssetDatabase.CreateAsset(gestures, assetName);

                                            CurrentSelectedAvatar.baseAnimationLayers[4].isDefault = false;
                                            CurrentSelectedAvatar.baseAnimationLayers[4].animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetName);
                                        }
                                    }


                                    if (CurrentSelectedAvatar.baseAnimationLayers[4].animatorController is AnimatorController ac)
                                        SelectedGameObjects.CreateGameObjectToggle(CurrentSelectedAvatar, ac, ParameterName, IsEnabledByDefault);

                                    control.parameter = new VRCExpressionsMenu.Control.Parameter()
                                    {
                                        name = $"{ParameterName}T"
                                    };

                                    parameters = Extensions.GetParams();
                                    selectedIndexes[control][0] = control.parameter.GetIndex();

                                    foldouts[control][1] = false;
                                    Reset();
                                }
                                else
                                {
                                    Debug.LogError("Any object is not selected!");
                                }
                            }
                        }

                        foldouts[control][2] = EditorGUILayout.Foldout(foldouts[control][2], "Dance");
                        if (foldouts[control][2])
                        {
                            SelectedAnimationClip = (AnimationClip)EditorGUILayout.ObjectField("Select animation", SelectedAnimationClip, typeof(AnimationClip), true);
                            SelectedAudioClip = (AudioClip)EditorGUILayout.ObjectField("Select song", SelectedAudioClip, typeof(AudioClip), true);
                            if (GUILayout.Button("Create"))
                            {
                                if (control.CreateDanceAnimation(CurrentSelectedAvatar, SelectedAnimationClip, SelectedAudioClip.name,  SelectedAudioClip))
                                {
                                    foldouts[control][2] = false;
                                    Reset();
                                }
                            }
                        }

                        EditorGUILayout.EndVertical();

                        if (!selectedIndexes.ContainsKey(control))
                            selectedIndexes.Add(control, new List<int>() { control.parameter.GetIndex() });

                        control.name = EditorGUILayout.TextField($"Name", control.name);
                        control.icon = (Texture2D)EditorGUILayout.ObjectField($"Icon", control.icon, typeof(Texture2D), true);
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label("Parameter");
                        selectedIndexes[control][0] = EditorGUILayout.Popup(selectedIndexes[control][0], parameters.Values.Select(p => p.Name).ToArray());
                        EditorGUILayout.EndHorizontal();

                        var currentParam = Extensions.GetParam(selectedIndexes[control][0]);

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
                    foldouts[control][0] = EditorGUILayout.Foldout(isShow, $"{control.name} (TwoAxis Puppet)");
                    if (isShow)
                    {
                        AddControlButtons(menu, control);

                        if (control.subParameters.Length == 0)
                            control.subParameters = new VRCExpressionsMenu.Control.Parameter[2];

                        if (!selectedIndexes.ContainsKey(control))
                            selectedIndexes.Add(control, new List<int>()
                        {
                            control.subParameters[0].GetIndex(true),
                            control.subParameters[1].GetIndex(true)
                        });

                        control.name = EditorGUILayout.TextField($"Name", control.name);
                        control.icon = (Texture2D)EditorGUILayout.ObjectField($"Icon", control.icon, typeof(Texture2D), true);
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label("Parameter Horizontal");
                        selectedIndexes[control][0] = EditorGUILayout.Popup(selectedIndexes[control][0], rotationParameters.Values.Select(p => p.Name).ToArray());
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label("Parameter Vertical");
                        selectedIndexes[control][1] = EditorGUILayout.Popup(selectedIndexes[control][1], rotationParameters.Values.Select(p => p.Name).ToArray());
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.Separator();

                        var currentParam = Extensions.GetParam(selectedIndexes[control][0], true);
                        var currentParam2 = Extensions.GetParam(selectedIndexes[control][1], true);
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
                    foldouts[control][0] = EditorGUILayout.Foldout(isShow, $"{control.name} (FourAxis Puppet)");

                    if (isShow)
                    {
                        AddControlButtons(menu, control);
                        control.name = EditorGUILayout.TextField($"Name", control.name);
                        control.icon = (Texture2D)EditorGUILayout.ObjectField($"Icon", control.icon, typeof(Texture2D), true);
                        EditorGUILayout.Separator();
                    }
                    break;
                case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                    foldouts[control][0] = EditorGUILayout.Foldout(isShow, $"{control.name} (Radial Puppet)");
                    if (isShow)
                    {
                        AddControlButtons(menu, control);

                        if (control.subParameters.Length == 0)
                            control.subParameters = new VRCExpressionsMenu.Control.Parameter[1];

                        if (!selectedIndexes.ContainsKey(control))
                            selectedIndexes.Add(control, new List<int>()
                            {
                                control.subParameters[0].GetIndex(true)
                            });

                        control.name = EditorGUILayout.TextField($"Name", control.name);
                        control.icon = (Texture2D)EditorGUILayout.ObjectField($"Icon", control.icon, typeof(Texture2D), true);
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label("Parameter");
                        selectedIndexes[control][0] = EditorGUILayout.Popup(selectedIndexes[control][0], rotationParameters.Values.Select(p => p.Name).ToArray());
                        EditorGUILayout.EndHorizontal();

                        var currentParam = Extensions.GetParam(selectedIndexes[control][0], true);
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

        bool AddControlButtons(VRCExpressionsMenu menu, VRCExpressionsMenu.Control control)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (menu.controls.Count > 1)
            {
                var index = control.type == VRCExpressionsMenu.Control.ControlType.SubMenu ? currentPage.Menu.controls.IndexOf(control) : menu.controls.IndexOf(control);
                if (GUILayout.Button("Up", GUILayout.Width(64)))
                {
                    if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                    {
                        currentPage.Menu.controls.Move(index, index == 0 ? currentPage.Menu.controls.Count - 1 : index - 1);
                        EditorUtility.SetDirty(currentPage.Menu);
                        AssetDatabase.SaveAssets();
                    }
                    else
                    {
                        menu.controls.Move(index, index == 0 ? menu.controls.Count - 1 : index - 1);
                        EditorUtility.SetDirty(menu);
                        AssetDatabase.SaveAssets();
                    }
                }
                if (GUILayout.Button("Down", GUILayout.Width(64)))
                {
                    if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                    {
                        currentPage.Menu.controls.Move(index, index == currentPage.Menu.controls.Count - 1 ? 0 : index + 1);
                        EditorUtility.SetDirty(currentPage.Menu);
                        AssetDatabase.SaveAssets();
                    }
                    else
                    {
                        menu.controls.Move(index, index == menu.controls.Count - 1 ? 0 : index + 1);
                        EditorUtility.SetDirty(menu);
                        AssetDatabase.SaveAssets();
                    }
                }
            }

            if (GUILayout.Button("Delete", GUILayout.Width(64)))
            {
                if (EditorUtility.DisplayDialog("Delete control", $"Do you want to delete control {control.name}?", "Yes"))
                {
                    if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                    {
                        EditorUtility.SetDirty(menu);
                        AssetDatabase.SaveAssets();
                        MoveBack();
                        currentPage.Menu.controls.Remove(control);
                        EditorUtility.SetDirty(currentPage.Menu);
                        AssetDatabase.SaveAssets();
                        return false;
                    }
                    else
                    {
                        menu.controls.Remove(control);
                        EditorUtility.SetDirty(menu);
                        AssetDatabase.SaveAssets();
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            return true;
        }

        void AddButtons(VRCExpressionsMenu menu)
        {
            GUILayout.Label($"Add new");
            EditorGUILayout.BeginHorizontal();
            if (menu.controls.Count < 8)
            {
                if (GUILayout.Button("Button"))
                    menu.AddButton();
                if (GUILayout.Button("Toggle"))
                    menu.AddToggle();
                if (GUILayout.Button("TwoAxis"))
                    menu.AddTwoAxis();
                if (GUILayout.Button("FourAxis"))
                    menu.AddFourAxis();
                if (GUILayout.Button("Radial"))
                    menu.AddRadial();
                if (GUILayout.Button("Submenu"))
                    menu.CreateSubMenu("SubMenu");
            }
            else
            {
                GUILayout.Label("You cant add more than 8 controls per menu.");
            }

            EditorGUILayout.EndHorizontal();
        }

        public int AmountOfClonableAvatars = 0;

        void AddSubmenuPremades(VRCExpressionsMenu menu)
        {
            if (!MainFoldOuts.ContainsKey(menu))
                MainFoldOuts.Add(menu, new List<bool>() { false, false, false });
            GUILayout.BeginHorizontal("box");
            GUILayout.FlexibleSpace();
            GUILayout.Label("Menu");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (currentPage.Menu != CurrentSelectedAvatar.expressionsMenu)
            {
                if (GUILayout.Button("Back"))
                {
                    EditorUtility.SetDirty(currentPage.Menu);
                    AssetDatabase.SaveAssets();
                    MoveBack();
                    return;
                }
                if (AddControlButtons(currentPage.Menu, currentPage.Control))
                {
                    currentPage.Control.name = EditorGUILayout.TextField($"Name", currentPage.Control.name);
                    currentPage.Control.icon = (Texture2D)EditorGUILayout.ObjectField($"Icon", currentPage.Control.icon, typeof(Texture2D), true);
                }
            }

            GUILayout.Label("Add premade");
            MainFoldOuts[menu][0] = EditorGUILayout.Foldout(MainFoldOuts[menu][0], "  Dances folder");
            if (MainFoldOuts[menu][0])
            {
                NameFilter = EditorGUILayout.TextField("  Name filter", NameFilter);
                EditorGUILayout.HelpBox(string.Concat(
                    "Filter name of animationclips and use them as button names",
                    Environment.NewLine,
                    "Example: Animation clip with name Dance1BestDance and using Dance1 as filter",
                    Environment.NewLine,
                    "Will replace that name to just BestDance",
                    Environment.NewLine,
                    "( More filters are seperated by , )"
                ), MessageType.Info, true);
                PathDances = EditorGUILayout.TextField("  Path", PathDances);
                EditorGUILayout.HelpBox(string.Concat(
                    "Path which will contain folders of dances",
                    Environment.NewLine,
                    $"{PathDances}/",
                    Environment.NewLine,
                    "└─ Dance1 /",
                    Environment.NewLine,
                    "     ├─ AnimationClip",
                    Environment.NewLine,
                    "     └─ AudioClip"
                ), MessageType.Info, true);
                if (GUILayout.Button("  Create submenu"))
                {
                    menu.GenerateDancesSubmenu(PathDances, NameFilter);
                    EditorGUILayout.EndVertical();
                    EditorUtility.SetDirty(CurrentSelectedAvatar.expressionsMenu);
                    AssetDatabase.SaveAssets();
                    MainFoldOuts[menu][0] = false;
                }
            }
            MainFoldOuts[menu][1] = EditorGUILayout.Foldout(MainFoldOuts[menu][1], "  Avatar Cloning");
            if (MainFoldOuts[menu][1])
            {
                AmountOfClonableAvatars = EditorGUILayout.IntField("Amount of clonable avatars", AmountOfClonableAvatars);
                if (GUILayout.Button("  Create Avatar Cloning"))
                {
                    if (!AssetDatabase.IsValidFolder("Assets/AutoGenerated"))
                        AssetDatabase.CreateFolder("Assets", "AutoGenerated");

                    var worldTransform = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AutoGenerated");

                    if (worldTransform == null)
                    {
                        var wT = new GameObject("worldTransform");
                        worldTransform = PrefabUtility.SaveAsPrefabAsset(wT, "Assets/AutoGenerated/worldTransform.prefab");
                        DestroyImmediate(wT);
                    }


                    var avatarCloning = CurrentSelectedAvatar.gameObject.GetOrAddGameobject("AvatarCloning");

                    var parentConstraint = avatarCloning.GetOrAddComponent<ParentConstraint>();
                    parentConstraint.constraintActive = true;
                    parentConstraint.AddSource(new ConstraintSource()
                    {
                        weight = 1f,
                        sourceTransform = worldTransform.transform,
                    });

                    var scaleConstraint = avatarCloning.GetOrAddComponent<ScaleConstraint>();
                    scaleConstraint.constraintActive = true;
                    scaleConstraint.AddSource(new ConstraintSource()
                    {
                        weight = 1f,
                        sourceTransform = worldTransform.transform,
                    });

                    var bodySkinned = CurrentSelectedAvatar.VisemeSkinnedMesh;
                    var orginalArmature = bodySkinned.rootBone.parent.gameObject;

                    var avatars = avatarCloning.GetOrAddGameobject("Avatars");


                    if (CurrentSelectedAvatar.baseAnimationLayers[4].animatorController == null)
                    {
                        if (EditorUtility.DisplayDialog("Missing animator controller in avatar", $"Do you want to create animator controller for FX?", "Create"))
                        {
                            if (!AssetDatabase.IsValidFolder("Assets/AutoGenerated"))
                                AssetDatabase.CreateFolder("Assets", "AutoGenerated");

                            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{CurrentSelectedAvatar.gameObject.name}"))
                                AssetDatabase.CreateFolder("Assets/AutoGenerated", $"{CurrentSelectedAvatar.gameObject.name}");

                            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{CurrentSelectedAvatar.gameObject.name}/Controllers"))
                                AssetDatabase.CreateFolder($"Assets/AutoGenerated/{CurrentSelectedAvatar.gameObject.name}", "Controllers");

                            var assetName = AssetDatabase.GenerateUniqueAssetPath($"Assets/AutoGenerated/{CurrentSelectedAvatar.gameObject.name}/Controllers/FX_AnimatorController.controller");

                            var gestures = new AnimatorController();
                            AssetDatabase.CreateAsset(gestures, assetName);

                            CurrentSelectedAvatar.baseAnimationLayers[4].isDefault = false;
                            CurrentSelectedAvatar.baseAnimationLayers[4].animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetName);
                        }
                    }

                    int used = 1;
                    var subMenu = menu.CreateSubMenu("Avatar Cloning");

                    for (int x = 0; x < AmountOfClonableAvatars; x++)
                    {
                        var avatarClone = avatars.GetOrAddGameobject($"Avatar[{x}]");

                        if (used == 8)
                        {
                            subMenu = subMenu.CreateSubMenu("Next Page");
                            used = 1;
                        }

                        var avatarmenu = subMenu.CreateSubMenu($"Avatar {x + 1}");
                        var showCloneToggle = avatarmenu.AddToggle("Show Clone");
                        var avatarInWorld = avatarmenu.AddToggle("In World");
                        var freezeAvatar = avatarmenu.AddToggle("Freeze");

                        avatarClone.SetActive(false);
                        avatarClone.CreateGameObjectToggle(CurrentSelectedAvatar, (AnimatorController)CurrentSelectedAvatar.baseAnimationLayers[4].animatorController, $"AC_ShowClone_{x}", $"ShowAvatar_{x}", "AvatarClone", false);

                        showCloneToggle.parameter = new VRCExpressionsMenu.Control.Parameter()
                        {
                            name = $"AC_ShowClone_{x}T"
                        };


                        var avatarParentConstraint = avatarClone.GetOrAddComponent<ParentConstraint>();
                        avatarParentConstraint.constraintActive = true;

                        var avatarArmature = avatarClone.GetOrAddGameobject("Armature");
                        avatarArmature.transform.localPosition = orginalArmature.transform.localPosition;
                        avatarArmature.transform.localRotation = orginalArmature.transform.localRotation;
                        avatarArmature.transform.localScale = orginalArmature.transform.localScale;

                        GetChildRecursive(orginalArmature, avatarArmature);

                        if (avatarArmature.transform.GetChild(0).TryGetComponent<PositionConstraint>(out PositionConstraint rootPosC))
                        {
                            Extensions.CreateAvatarClonePlaceInWorldAnimation(CurrentSelectedAvatar, (AnimatorController)CurrentSelectedAvatar.baseAnimationLayers[4].animatorController, avatarParentConstraint, rootPosC, x, false);

                            avatarInWorld.parameter = new VRCExpressionsMenu.Control.Parameter()
                            {
                                name = $"AC_WorldAvatar_{x}T"
                            };
                        }

                        var rotationConstraints = avatarArmature.GetComponentsInChildren<RotationConstraint>();
                        rotationConstraints.CreateAvatarFreezeAnimation(CurrentSelectedAvatar, (AnimatorController)CurrentSelectedAvatar.baseAnimationLayers[4].animatorController, x);
                        freezeAvatar.parameter = new VRCExpressionsMenu.Control.Parameter()
                        {
                            name = $"AC_FreezeAvatar_{x}T"
                        };

                        var avatarObjects = avatarClone.GetOrAddGameobject("AvatarObjects");

                        var parObj = UnityEngine.Object.Instantiate(bodySkinned.gameObject, avatarObjects.transform);
                        parObj.name = bodySkinned.gameObject.name;

                        foreach(var skinnedMeshRenderers in parObj.GetComponentsInChildren<SkinnedMeshRenderer>())
                        {
                            skinnedMeshRenderers.rootBone = avatarArmature.transform.GetChild(0);
                            Transform[] newBones = new Transform[skinnedMeshRenderers.bones.Length];

                            for (int i = 0; i < skinnedMeshRenderers.bones.Length; i++)
                            {
                                foreach (var newBone in skinnedMeshRenderers.rootBone.GetComponentsInChildren<Transform>())
                                {
                                    if (newBone.name == skinnedMeshRenderers.bones[i].name)
                                    {
                                        newBones[i] = newBone;
                                        continue;
                                    }
                                }
                            }
                            skinnedMeshRenderers.bones = newBones;
                        }



                        var parentConstraintContainer = avatarClone.GetOrAddComponent<ParentConstraint>();
                        parentConstraintContainer.constraintActive = true;
                        parentConstraintContainer.AddSource(new ConstraintSource()
                        {
                            sourceTransform = CurrentSelectedAvatar.transform,
                            weight = 1f,
                        });
                        used++;

                    }
                    MainFoldOuts[menu][1] = false;
                }
            }
            GUILayout.BeginHorizontal("box");
            GUILayout.FlexibleSpace();
            GUILayout.Label("Controls", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void GetChildRecursive(GameObject obj, GameObject targetParent, int cur =0)
        {
            if (null == obj)
                return;

            foreach (Transform child in obj.transform)
            {
                if (null == child)
                    continue;


                var newObj = new GameObject(child.name);
                newObj.transform.parent = targetParent.transform;
                newObj.transform.localPosition = child.localPosition;
                newObj.transform.localRotation = child.localRotation;
                newObj.transform.localScale = child.localScale;

                if (cur == 0)
                {

                    var pCon = newObj.AddComponent<PositionConstraint>();
                    pCon.constraintActive = true;
                    pCon.AddSource(new ConstraintSource()
                    {
                        sourceTransform = child,
                        weight = 1f,

                    });
                }

                var rCon = newObj.AddComponent<RotationConstraint>();
                rCon.constraintActive = true;
                rCon.AddSource(new ConstraintSource()
                {
                    sourceTransform = child,
                    weight = 1f,
                });
                cur++;
                GetChildRecursive(child.gameObject, newObj, cur);

            }
        }

        void MoveBack()
        {
            if (pages.TryGetValue(currentPage.Menu, out PageModel last))
            {
                var toRemove = currentPage.Menu;
                currentPage = last;
                pages.Remove(toRemove);
            }
        }
                        
        void Footer()
        {
            if (CurrentSelectedAvatar == null)
                GUILayout.FlexibleSpace();
            if (CurrentVersion == null)
            {
                CheckUpdate();
                CurrentSelectedAvatar = null;
            }
            GUILayout.BeginHorizontal("box");
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Current version: {CurrentVersion.Version}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (CanUpdate)
            {
                if (GUILayout.Button("Download Update"))
                {
                    Debug.Log($"Downloading ExpressionEditor version {LatestVersion.Version} !");
                    using (var web = new WebClient())
                    {
                        web.DownloadFile($"https://github.com/Killers0992/ExpressionsEditor/releases/download/{LatestVersion.Version}/ExpressionsEditor.unitypackage", Path.Combine(Application.dataPath, "ExpressionsEditor.unitypackage"));
                    }
                    Debug.Log($"Downloaded ExpressionEditor version {LatestVersion.Version} !");
                    AssetDatabase.ImportPackage(Path.Combine(Application.dataPath, "ExpressionsEditor.unitypackage"), true);
                    AssetDatabase.DeleteAsset("Assets/ExpressionsEditor.unitypackage");
                    CurrentVersion = LatestVersion;
                    CanUpdate = false;
                }
            }
            else
            {
                if (GUILayout.Button("Check Update"))
                    CheckUpdate();
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Latest version: {LatestVersion.Version}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }


        void OnGUI()
        {
            if (Application.isPlaying)
            {
                GUILayout.Label("Stop playmode to use Expression editor.");
                Footer();
                return;
            }
            GUILayout.BeginHorizontal("box");
            GUILayout.FlexibleSpace();
            GUILayout.Label("Avatars", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            EditorGUILayout.BeginVertical();
            foreach (var avatar in GameObject.FindObjectsOfType<VRCAvatarDescriptor>())
            {
                if (avatar == CurrentSelectedAvatar)
                    GUI.color = Color.green;
                if (GUILayout.Button(avatar.name))
                {
                    if (CurrentSelectedAvatar == avatar)
                    {
                        CurrentSelectedAvatar = null;
                        currentPage = null;
                        return;
                    }
                    CurrentSelectedAvatar = avatar;
                    currentPage = new PageModel() { Menu = CurrentSelectedAvatar.expressionsMenu };
                }
                GUI.color = Color.white;
            }
            EditorGUILayout.EndVertical();
            if (CurrentSelectedAvatar != null)
            {
                CurrentSelectedAvatar.expressionsMenu = (VRCExpressionsMenu)EditorGUILayout.ObjectField("Expressions object", CurrentSelectedAvatar.expressionsMenu, typeof(VRCExpressionsMenu), true);
                if (CurrentSelectedAvatar.expressionsMenu == null)
                {
                    GUILayout.Label("Expressions menu on your avatar is missing do you want to create one?");
                    if (GUILayout.Button("Create"))
                    {
                        if (!AssetDatabase.IsValidFolder("Assets/AutoGenerated"))
                            AssetDatabase.CreateFolder("Assets", "AutoGenerated");

                        if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{CurrentSelectedAvatar.gameObject.name}"))
                            AssetDatabase.CreateFolder("Assets/AutoGenerated", $"{CurrentSelectedAvatar.gameObject.name}");

                        if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{CurrentSelectedAvatar.gameObject.name}/MenuComponents"))
                            AssetDatabase.CreateFolder($"Assets/AutoGenerated/{CurrentSelectedAvatar.gameObject.name}", "MenuComponents");

                        string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath($"Assets/AutoGenerated/{CurrentSelectedAvatar.gameObject.name}/MenuComponents/ExpressionsMenu.asset");
                        AssetDatabase.CopyAsset("Assets/VRCSDK/Examples3/Expressions Menu/DefaultExpressionsMenu.asset", assetPathAndName);
                        CurrentSelectedAvatar.customExpressions = true;
                        CurrentSelectedAvatar.expressionsMenu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(assetPathAndName);
                        currentPage = new PageModel() { Menu = CurrentSelectedAvatar.expressionsMenu };
                    }
                    GUILayout.FlexibleSpace();
                    Footer();
                    return;
                }
                CurrentSelectedAvatar.expressionParameters = (VRCExpressionParameters)EditorGUILayout.ObjectField("Parameters object", CurrentSelectedAvatar.expressionParameters, typeof(VRCExpressionParameters), true);
                if (CurrentSelectedAvatar.expressionParameters == null)
                {
                    GUILayout.Label("Expressions parameters on your avatar is missing do you want to create one?");
                    if (GUILayout.Button("Create"))
                    {
                        if (!AssetDatabase.IsValidFolder("Assets/AutoGenerated"))
                            AssetDatabase.CreateFolder("Assets", "AutoGenerated");

                        if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{CurrentSelectedAvatar.gameObject.name}"))
                            AssetDatabase.CreateFolder("Assets/AutoGenerated", $"{CurrentSelectedAvatar.gameObject.name}");

                        if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{CurrentSelectedAvatar.gameObject.name}/MenuComponents"))
                            AssetDatabase.CreateFolder($"Assets/AutoGenerated/{CurrentSelectedAvatar.gameObject.name}", "MenuComponents");

                        string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath($"Assets/AutoGenerated/{CurrentSelectedAvatar.gameObject.name}/MenuComponents/ExpressionsParameters.asset");
                        AssetDatabase.CopyAsset("Assets/VRCSDK/Examples3/Expressions Menu/DefaultExpressionParameters.asset", assetPathAndName);
                        CurrentSelectedAvatar.customExpressions = true;
                        CurrentSelectedAvatar.expressionParameters = AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(assetPathAndName);
                        var newParametersTemplate = CurrentSelectedAvatar.expressionParameters.parameters.Where(p => !string.IsNullOrEmpty(p.name)).ToArray();
                        CurrentSelectedAvatar.expressionParameters.parameters = newParametersTemplate;
                        EditorUtility.SetDirty(CurrentSelectedAvatar.expressionParameters);
                        AssetDatabase.SaveAssets();
                    }
                    GUILayout.FlexibleSpace();
                    Footer();
                    return;
                }

                if (currentPage == null)
                {
                    Footer();
                    return;
                }

                if (!scrollViews.ContainsKey(currentPage.Menu))
                    scrollViews.Add(currentPage.Menu, new Vector2(0f, 0f));
                AddSubmenuPremades(currentPage.Menu);
                scrollViews[currentPage.Menu] = EditorGUILayout.BeginScrollView(scrollViews[currentPage.Menu]);
                foreach (var control in currentPage.Menu.controls.ToArray())
                {
                    CreateFoldable(currentPage.Menu, control, -2);
                }
                EditorGUILayout.EndScrollView();
                EditorGUILayout.Space(10f);
                GUILayout.FlexibleSpace();
                AddButtons(currentPage.Menu);
            }
            Footer();
        }

        public void OnDestroy()
        {
            if (CurrentSelectedAvatar?.expressionParameters != null)
            {
                EditorUtility.SetDirty(CurrentSelectedAvatar.expressionParameters);
                AssetDatabase.SaveAssets();
            }
            if (CurrentSelectedAvatar?.expressionsMenu != null)
            {
                EditorUtility.SetDirty(CurrentSelectedAvatar.expressionsMenu);
                AssetDatabase.SaveAssets();
            }
            if (currentPage?.Menu != null)
            {
                EditorUtility.SetDirty(currentPage.Menu);
                AssetDatabase.SaveAssets();
            }
        }
    }
}