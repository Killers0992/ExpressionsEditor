namespace ExpressionsEditor
{
    using ExpressionsEditor.Models;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.Animations;
    using UnityEditor.Sprites;
    using UnityEngine;
    using UnityEngine.Animations;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Avatars.ScriptableObjects;
    using static VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters;
    using static VRC.SDKBase.VRC_AnimatorTrackingControl;

    public static class Extensions
    {
        public static string GetPath(this Transform current)
        {
            if (current.parent == null)
                return "/" + current.name;
            return current.parent.GetPath() + "/" + current.name;
        }

        public static string GetPath(this Component component)
        {
            return component.transform.GetPath() + "/" + component.GetType().ToString();
        }

        public static void RemoveLayerIfExists(this AnimatorController animator, string name)
        {
            for (int x = 0; x < animator.layers.Length; x++)
            {
                if (animator.layers[x].name == name)
                {
                    animator.RemoveLayer(x);
                }
            }
        }

        public static string ReplaceAll(this string str, string[] strings, string replaceWith)
        {

            foreach (var st in strings)
                if (!string.IsNullOrEmpty(st))
                    str = str.Replace(st, replaceWith);
            return str;
        }


        public static Dictionary<int, ParamValue> GetParams(bool rotationOnly = false)
        {
            if (ExpressionEditor.CurrentSelectedAvatar == null)
                return new Dictionary<int, ParamValue>();
            int curIndex = 0;
            Dictionary<int, ParamValue> values = new Dictionary<int, ParamValue>() { { -1, new ParamValue() { Name = "None", Index = curIndex } } };
            for (int x = 0; x < ExpressionEditor.CurrentSelectedAvatar.expressionParameters.parameters.Length; x++)
            {
                if (ExpressionEditor.CurrentSelectedAvatar.expressionParameters.parameters[x].valueType != VRCExpressionParameters.ValueType.Float && rotationOnly)
                    continue;
                curIndex++;
                values.Add(x, new ParamValue()
                {
                    Name = ExpressionEditor.CurrentSelectedAvatar.expressionParameters.parameters[x].name,
                    Index = curIndex
                });
            }
            return values;
        }

        public static Parameter GetParam(int selectedIndex, bool rotationOnly = false)
        {
            if (selectedIndex == 0)
                return null;

            var data = rotationOnly ? ExpressionEditor.rotationParameters : ExpressionEditor.parameters;

            foreach (var param in data)
            {
                if (selectedIndex == param.Value.Index)
                    return ExpressionEditor.CurrentSelectedAvatar.expressionParameters.GetParameter(param.Key);
            }
            return null;
        }

        public static int GetIndex(this VRCExpressionsMenu.Control.Parameter parameter, bool rotationOnly = false)
        {
            if (parameter == null)
                return 0;

            var data = rotationOnly ? ExpressionEditor.rotationParameters : ExpressionEditor.parameters;

            foreach (var param in data)
            {
                if (param.Value.Name == parameter.name)
                    return param.Value.Index;
            }
            return 0;
        }

        public static AnimatorControllerParameter GetOrAddParameter(this AnimatorController animator, string parameterName, object defaultValue)
        {
            if (parameterName.ToLower() == "vrcemote")
                defaultValue = 0;

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

        public static AnimationClip CreateAnimationToggle(this GameObject gameObject, VRCAvatarDescriptor vrcAvatar, string name, string category, bool state)
        {
            AnimationClip clip = new AnimationClip();
            clip.name = $"{name} {(state ? "ON" : "OFF")}";
            clip.wrapMode = WrapMode.Loop;

            var curve = new AnimationCurve(new Keyframe[1] { new Keyframe(0, state ? 1 : 0) });
            string path = gameObject.transform.GetPath().Replace(vrcAvatar.transform.GetPath(), "").Remove(0, 1);

            if (gameObject.TryGetComponent<SkinnedMeshRenderer>(out _))
                clip.SetCurve(path, typeof(SkinnedMeshRenderer), "m_Enabled", curve);
            else
                clip.SetCurve(path, typeof(GameObject), "m_IsActive", curve);

            if (!AssetDatabase.IsValidFolder("Assets/AutoGenerated"))
                AssetDatabase.CreateFolder("Assets", "AutoGenerated");

            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}"))
                AssetDatabase.CreateFolder("Assets/AutoGenerated", $"{vrcAvatar.gameObject.name}");

            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations"))
                AssetDatabase.CreateFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}", "Animations");

            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations/{category}"))
                AssetDatabase.CreateFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations", category);

            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations/{category}/{name}"))
                AssetDatabase.CreateFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations/{category}", name);

            AssetDatabase.CreateAsset(clip, $"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations/{category}/{name}/{$"Animation_{(state ? "ON" : "OFF")}"}.anim");
            return clip;
        }

        public static AnimationClip CreateAnimationToggle(this SkinnedMeshRenderer renderer, VRCAvatarDescriptor vrcAvatar, string name, string category, Material targetTexture, int index, int materialIndex)
        {
            AnimationClip clip = new AnimationClip();
            clip.name = $"{name} Material";
            clip.wrapMode = WrapMode.Loop;


            string path = renderer.gameObject.transform.GetPath().Replace(vrcAvatar.transform.GetPath(), "").Remove(0, 1);

            var pptrCurve = EditorCurveBinding.PPtrCurve(path, typeof(SkinnedMeshRenderer), $"m_Materials.Array.data[{materialIndex}]");


            ObjectReferenceKeyframe frames = new ObjectReferenceKeyframe()
            {
                time = 0f,
                value = targetTexture
            };
            AnimationUtility.SetObjectReferenceCurve(clip, pptrCurve, new ObjectReferenceKeyframe[] { frames });

            if (!AssetDatabase.IsValidFolder("Assets/AutoGenerated"))
                AssetDatabase.CreateFolder("Assets", "AutoGenerated");

            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}"))
                AssetDatabase.CreateFolder("Assets/AutoGenerated", $"{vrcAvatar.gameObject.name}");

            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations"))
                AssetDatabase.CreateFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}", "Animations");

            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations/{category}"))
                AssetDatabase.CreateFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations", category);

            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations/{category}/{name}"))
                AssetDatabase.CreateFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations/{category}", name);
            AssetDatabase.CreateAsset(clip, $"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations/{category}/{name}/Material_{(index == 0 ? "Default" : $"{index}")}.anim");

            return clip;
        }


        public static AnimationClip CreateAnimationToggle(ParentConstraint avatarParent, PositionConstraint avatarRoot, VRCAvatarDescriptor vrcAvatar, string name, string category, bool state)
        {
            AnimationClip clip = new AnimationClip();
            clip.name = $"{name} {(state ? "ON" : "OFF")}";
            clip.wrapMode = WrapMode.Loop;

            var curve = new AnimationCurve(new Keyframe[1] { new Keyframe(0, state ? 1 : 0) });
            string path = avatarParent.transform.GetPath().Replace(vrcAvatar.transform.GetPath(), "").Remove(0, 1);

            clip.SetCurve(path, typeof(ParentConstraint), "m_Active", curve);
            path = avatarRoot.transform.GetPath().Replace(vrcAvatar.transform.GetPath(), "").Remove(0, 1);
            clip.SetCurve(path, typeof(PositionConstraint), "m_Active", curve);

            if (!AssetDatabase.IsValidFolder("Assets/AutoGenerated"))
                AssetDatabase.CreateFolder("Assets", "AutoGenerated");

            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}"))
                AssetDatabase.CreateFolder("Assets/AutoGenerated", $"{vrcAvatar.gameObject.name}");

            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations"))
                AssetDatabase.CreateFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}", "Animations");


            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations/{category}"))
                AssetDatabase.CreateFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations", category);

            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations/{category}/{name}"))
                AssetDatabase.CreateFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations/{category}", name);

            AssetDatabase.CreateAsset(clip, $"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations/{category}/{name}/{$"Animation_{(state ? "ON" : "OFF")}"}.anim"); return clip;
        }

        public static AnimationClip CreateAnimationToggle(this RotationConstraint[] rotationConstraints, VRCAvatarDescriptor vrcAvatar, string name, string category, bool state)
        {
            AnimationClip clip = new AnimationClip();
            clip.name = $"{name} {(state ? "ON" : "OFF")}";
            clip.wrapMode = WrapMode.Loop;

            var curve = new AnimationCurve(new Keyframe[1] { new Keyframe(0, state ? 1 : 0) });

            foreach(var constraint in rotationConstraints)
            {
                string path = constraint.transform.GetPath().Replace(vrcAvatar.transform.GetPath(), "").Remove(0, 1);

                clip.SetCurve(path, typeof(RotationConstraint), "m_Active", curve);
            }

            if (!AssetDatabase.IsValidFolder("Assets/AutoGenerated"))
                AssetDatabase.CreateFolder("Assets", "AutoGenerated");

            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}"))
                AssetDatabase.CreateFolder("Assets/AutoGenerated", $"{vrcAvatar.gameObject.name}");

            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations"))
                AssetDatabase.CreateFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}", "Animations");


            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations/{category}"))
                AssetDatabase.CreateFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations", category);

            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations/{category}/{name}"))
                AssetDatabase.CreateFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations/{category}", name);

            AssetDatabase.CreateAsset(clip, $"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations/{category}/{name}/{$"Animation_{(state ? "ON" : "OFF")}"}.anim"); return clip;
        }

        public static AnimationClip CreateAnimationToggle(this GameObject[] gameObjects, VRCAvatarDescriptor vrcAvatar, string name, string category, bool state)
        {
            AnimationClip clip = new AnimationClip();
            clip.name = $"{name} {(state ? "ON" : "OFF")}";
            clip.wrapMode = WrapMode.Loop;
            var curve = new AnimationCurve(new Keyframe[1] { new Keyframe(0, state ? 1 : 0) });

            foreach (var gb in gameObjects)
            {
                string path = gb.transform.GetPath().Replace(vrcAvatar.transform.GetPath(), "").Remove(0, 1);

                if (gb.TryGetComponent<SkinnedMeshRenderer>(out _))
                    clip.SetCurve(path, typeof(SkinnedMeshRenderer), "m_Enabled", curve);
                else
                    clip.SetCurve(path, typeof(GameObject), "m_IsActive", curve);
            }

            if (!AssetDatabase.IsValidFolder("Assets/AutoGenerated"))
                AssetDatabase.CreateFolder("Assets", "AutoGenerated");

            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}"))
                AssetDatabase.CreateFolder("Assets/AutoGenerated", $"{vrcAvatar.gameObject.name}");

            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations"))
                AssetDatabase.CreateFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}", "Animations");

            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations/{category}"))
                AssetDatabase.CreateFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations", category);

            if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations/{category}/{name}"))
                AssetDatabase.CreateFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations/{category}", name);

            AssetDatabase.CreateAsset(clip, $"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Animations/{category}/{name}/{$"Animation_{(state ? "ON" : "OFF")}"}.anim"); return clip;
        }

        public static Parameter GetOrAddParameter(VRCExpressionParameters expressionParameters, string parameterName, float defaultValue, VRCExpressionParameters.ValueType type)
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

            EditorUtility.SetDirty(expressionParameters);
            AssetDatabase.SaveAssets();
            return foundParam;
        }

        public static bool CreateGameObjectToggle(this GameObject gameObject, VRCAvatarDescriptor vrcAvatar, AnimatorController animator,  string parameterName, string name, string category, object defaultValue)
        {
            animator.GetOrAddParameter(defaultValue is bool ? $"{parameterName}T" : parameterName, defaultValue);

            animator.RemoveLayerIfExists(defaultValue is bool ? parameterName : gameObject.name);

            var stateMachine = new AnimatorStateMachine()
            {
                name = defaultValue is bool ? parameterName : gameObject.name
            };
            AssetDatabase.AddObjectToAsset(stateMachine, animator);

            float defaultValueFloat = 0f;
            VRCExpressionParameters.ValueType valueType = VRCExpressionParameters.ValueType.Bool;
            switch (defaultValue)
            {
                case float f:
                    valueType = VRCExpressionParameters.ValueType.Float;
                    defaultValueFloat = f;
                    break;
                case bool b:
                    defaultValueFloat = b ? 1f : 0f;
                    break;
                case int i:
                    valueType = VRCExpressionParameters.ValueType.Int;
                    defaultValueFloat = i;
                    break;
            }

            GetOrAddParameter(vrcAvatar.expressionParameters, defaultValue is bool ? $"{parameterName}T" : parameterName, defaultValueFloat, valueType);

            animator.AddLayer(new AnimatorControllerLayer()
            {
                defaultWeight = 1f,
                name = defaultValue is bool ? parameterName : gameObject.name,
                stateMachine = stateMachine
            });

            var lastLayer = animator.layers[animator.layers.Length - 1];

            var state1 = lastLayer.stateMachine.AddState($"Animation {(defaultValueFloat == 1f ? "ON" : "OFF")}");
            state1.motion = gameObject.CreateAnimationToggle(vrcAvatar, name, category, defaultValueFloat == 1f);

            var state2 = lastLayer.stateMachine.AddState($"Animation {(defaultValueFloat != 1f ? "ON" : "OFF")}");
            state2.motion = gameObject.CreateAnimationToggle(vrcAvatar, name, category, defaultValueFloat != 1f);

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

        public static bool CreateGameObjectToggle(this GameObject[] gameObjects, VRCAvatarDescriptor vrcAvatar, AnimatorController animator, string parameterName, object defaultValue)
        {
            animator.GetOrAddParameter($"{parameterName}T", defaultValue);

            animator.RemoveLayerIfExists(parameterName);

            var stateMachine = new AnimatorStateMachine()
            {
                name = parameterName
            };
            AssetDatabase.AddObjectToAsset(stateMachine, animator);

            float defaultValueFloat = 0f;
            VRCExpressionParameters.ValueType valueType = VRCExpressionParameters.ValueType.Bool;
            switch (defaultValue)
            {
                case float f:
                    valueType = VRCExpressionParameters.ValueType.Float;
                    defaultValueFloat = f;
                    break;
                case bool b:
                    defaultValueFloat = b ? 1f : 0f;
                    break;
                case int i:
                    valueType = VRCExpressionParameters.ValueType.Int;
                    defaultValueFloat = i;
                    break;
            }

            GetOrAddParameter(vrcAvatar.expressionParameters, $"{parameterName}T", defaultValueFloat, valueType);

            animator.AddLayer(new AnimatorControllerLayer()
            {
                defaultWeight = 1f,
                name = $"{parameterName}",
                stateMachine = stateMachine
            });

            var lastLayer = animator.layers[animator.layers.Length - 1];

            var state1 = lastLayer.stateMachine.AddState($"Animation {(defaultValueFloat == 1f ? "ON" : "OFF")}");
            state1.motion = gameObjects.CreateAnimationToggle(vrcAvatar, parameterName, "ObjectToggle", defaultValueFloat == 1f);

            var state2 = lastLayer.stateMachine.AddState($"Animation {(defaultValueFloat != 1f ? "ON" : "OFF")}");
            state2.motion = gameObjects.CreateAnimationToggle(vrcAvatar, parameterName, "ObjectToggle", defaultValueFloat != 1f);

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
                        parameter = $"{parameterName}T",
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
                        parameter = $"{parameterName}T",
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

        public static void CreateAvatarFreezeAnimation(this RotationConstraint[] rotationconstraints, VRCAvatarDescriptor vrcAvatar, AnimatorController animator, int avatarId)
        {
            animator.GetOrAddParameter($"AC_FreezeAvatar_{avatarId}T", false);

            animator.RemoveLayerIfExists($"AC_FreezeAvatar_{avatarId}");

            var stateMachine = new AnimatorStateMachine()
            {
                name = $"AC_FreezeAvatar_{avatarId}"
            };
            AssetDatabase.AddObjectToAsset(stateMachine, animator);

            GetOrAddParameter(vrcAvatar.expressionParameters, $"AC_FreezeAvatar_{avatarId}T", 0f, ValueType.Bool);

            animator.AddLayer(new AnimatorControllerLayer()
            {
                defaultWeight = 1f,
                name = $"AC_FreezeAvatar_{avatarId}",
                stateMachine = stateMachine
            });

            var lastLayer = animator.layers[animator.layers.Length - 1];

            var state1 = lastLayer.stateMachine.AddState($"Animation OFF");
            state1.motion = rotationconstraints.CreateAnimationToggle(vrcAvatar, $"FreezeAvatar_{avatarId}", "AvatarClone", true);

            var state2 = lastLayer.stateMachine.AddState($"Animation ON");
            state2.motion = rotationconstraints.CreateAnimationToggle(vrcAvatar, $"FreezeAvatar_{avatarId}", "AvatarClone", false);

            AnimatorConditionMode condition1 = AnimatorConditionMode.If;
            AnimatorConditionMode condition2 = AnimatorConditionMode.IfNot;

            var newTransition = new AnimatorStateTransition()
            {
                destinationState = state2,
                conditions = new AnimatorCondition[1]
                {
                    new AnimatorCondition()
                    {
                        mode = condition1,
                        parameter = $"AC_FreezeAvatar_{avatarId}T",
                        threshold = 1f,
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
                        parameter = $"AC_FreezeAvatar_{avatarId}T",
                        threshold = 0f,
                    }
                }
            };
            state2.AddTransition(newTransition);
            AssetDatabase.AddObjectToAsset(newTransition, state2);
            EditorUtility.SetDirty(animator);
            AssetDatabase.SaveAssets();
        }


        public static void CreateAvatarClonePlaceInWorldAnimation(VRCAvatarDescriptor vrcAvatar, AnimatorController animator, ParentConstraint avatarConstraint, PositionConstraint avatarHipsConstraint, int avatarId, bool inWorldByDefault = false)
        {
            animator.GetOrAddParameter($"AC_WorldAvatar_{avatarId}T", inWorldByDefault);

            animator.RemoveLayerIfExists($"AC_WorldAvatar_{avatarId}");

            var stateMachine = new AnimatorStateMachine()
            {
                name = $"AC_WorldAvatar_{avatarId}"
            };
            AssetDatabase.AddObjectToAsset(stateMachine, animator);

            GetOrAddParameter(vrcAvatar.expressionParameters, $"AC_WorldAvatar_{avatarId}T", inWorldByDefault ? 1f : 0f, ValueType.Bool);

            animator.AddLayer(new AnimatorControllerLayer()
            {
                defaultWeight = 1f,
                name = $"AC_WorldAvatar_{avatarId}",
                stateMachine = stateMachine
            });

            var lastLayer = animator.layers[animator.layers.Length - 1];

            var state1 = lastLayer.stateMachine.AddState($"Animation {(inWorldByDefault ? "ON" : "OFF")}");
            state1.motion = CreateAnimationToggle(avatarConstraint, avatarHipsConstraint, vrcAvatar, $"WorldAvatar_{avatarId}", "AvatarClone", !inWorldByDefault);

            var state2 = lastLayer.stateMachine.AddState($"Animation {(!inWorldByDefault ? "ON" : "OFF")}");
            state2.motion = CreateAnimationToggle(avatarConstraint, avatarHipsConstraint, vrcAvatar, $"WorldAvatar_{avatarId}", "AvatarClone", inWorldByDefault);

            AnimatorConditionMode condition1 = AnimatorConditionMode.If;
            AnimatorConditionMode condition2 = AnimatorConditionMode.IfNot;
            if (inWorldByDefault)
            {
                condition1 = AnimatorConditionMode.IfNot;
                condition2 = AnimatorConditionMode.If;
            }

            var newTransition = new AnimatorStateTransition()
            {
                destinationState = state2,
                conditions = new AnimatorCondition[1]
                {
                    new AnimatorCondition()
                    {
                        mode = condition1,
                        parameter = $"AC_WorldAvatar_{avatarId}T",
                        threshold = inWorldByDefault ? 1f : 0f,
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
                        parameter = $"AC_WorldAvatar_{avatarId}T",
                        threshold = !inWorldByDefault ? 1f : 0f,
                    }
                }
            };
            state2.AddTransition(newTransition);
            AssetDatabase.AddObjectToAsset(newTransition, state2);
            EditorUtility.SetDirty(animator);
            AssetDatabase.SaveAssets();
        }

        public static bool CreateDanceAnimation(this VRCExpressionsMenu.Control control, VRCAvatarDescriptor vrcAvatar, AnimationClip animationClip, string danceName, AudioClip audioClip = null, bool raw = false)
        {
            if (animationClip == null)
            {
                Debug.LogError($"Animation clip is null!");
                return false;
            }

            if (vrcAvatar.baseAnimationLayers[3].animatorController == null)
            {
                if (EditorUtility.DisplayDialog("Missing animator controller in avatar", $"Do you want to create default animator controller for actions?", "Create"))
                {
                    if (!AssetDatabase.IsValidFolder("Assets/AutoGenerated"))
                        AssetDatabase.CreateFolder("Assets", "AutoGenerated");

                    if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}"))
                        AssetDatabase.CreateFolder("Assets/AutoGenerated", $"{vrcAvatar.gameObject.name}");

                    if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Controllers"))
                        AssetDatabase.CreateFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}", "Controllers");

                    var assetName = AssetDatabase.GenerateUniqueAssetPath($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Controllers/Action_AnimatorController.controller");
                    AssetDatabase.CopyAsset("Assets/VRCSDK/Examples3/Animation/Controllers/vrc_AvatarV3ActionLayer.controller", assetName);
                    
                    vrcAvatar.baseAnimationLayers[3].isDefault = false;
                    vrcAvatar.baseAnimationLayers[3].animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetName);
                }
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

                var proxyStandingMotion = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/VRCSDK/Examples3/Animation/ProxyAnim/proxy_stand_still.anim");
                                            
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
                        destinationState = eeState,
                        hasExitTime = false,
                        hasFixedDuration = true,
                    };
                    aaState.AddTransition(newTransition);
                    AssetDatabase.AddObjectToAsset(newTransition, aaState);
                }
                int freeVRCEmote = -1;
                AnimatorState state = null;
                List<int> usedVRCEmotes = new List<int>();

                List<AnimatorStateTransition> transitions = new List<AnimatorStateTransition>();

                foreach (var sstate in actionLayer.stateMachine.states)
                {
                    transitions.AddRange(sstate.state.transitions);
                }

                int lowestValue = -1;
                int highestValue = -1;

                foreach (var transitionx in transitions)
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
                        foreach (var trans in transitionx.destinationState.transitions)
                        {
                            foreach (var condition in trans.conditions)
                            {
                                if (condition.parameter != "VRCEmote")
                                    continue;
                                if (condition.mode != AnimatorConditionMode.Equals)
                                    continue;
                                ids.Add((int)condition.threshold);
                            }
                        }

                        int realGreaterValue = ids.Count == 0 ? -1 : ids.OrderBy(p => p).First() - 1;
                        int realLessValue = ids.Count == 0 ? -1 : ids.OrderByDescending(p => p).First() + 1;

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

                        lowestValue = greaterValue;
                        highestValue = lessValue + 1;
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
                        if (tr.destinationState.name == $"{danceName}")
                        {
                            state = tr.destinationState;
                            freeVRCEmote = (int)condition.threshold;
                        }
                    }
                }

                if (lowestValue == -1)
                    lowestValue = usedVRCEmotes.OrderByDescending(p => p).First();

                if (highestValue == -1)
                    highestValue = lowestValue + 2;

                if (freeVRCEmote == -1)
                {
                    freeVRCEmote = highestValue - 1;
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
                                threshold = lowestValue,
                            },
                            new AnimatorCondition()
                            {
                                mode = AnimatorConditionMode.Less,
                                parameter = "VRCEmote",
                                threshold = highestValue
                            }
                        };
                        newAnim = actionLayer.stateMachine.AddState($"{danceName}", new Vector3(-1000f, 200f, 0f));
                    }

                    newAnim.motion = animationClip;

                    if (state == null)
                    {
                        var newTransition = new AnimatorStateTransition()
                        {
                            destinationState = newAnim,
                            hasExitTime = false,
                            hasFixedDuration = true,
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
                            hasFixedDuration = true,
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
                        hasFixedDuration = true,
                        exitTime = 0.1f,
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
                if (!raw)
                {
                    ExpressionEditor.parameters = GetParams();
                    ExpressionEditor.selectedIndexes[control][0] = control.parameter.GetIndex();
                }


                if (audioClip == null)
                    return true;


                var animations = vrcAvatar.gameObject.GetOrAddGameobject("Animations");

                var existingAnimation = animations.GetOrAddGameobject(danceName);

                var audioSource = existingAnimation.GetOrAddComponent<AudioSource>();
                audioSource.clip = audioClip;
                audioSource.spatialBlend = 1f;
                audioSource.maxDistance = 50f;
                audioSource.minDistance = 2f;
                audioSource.bypassEffects = true;
                audioSource.bypassListenerEffects = true;
                audioSource.bypassReverbZones = true;
                audioSource.priority = 256;
                existingAnimation.SetActive(false);

                if (vrcAvatar.baseAnimationLayers[4].animatorController == null)
                {
                    if (EditorUtility.DisplayDialog("Missing animator controller in avatar", $"Do you want to create animator controller for FX?", "Create"))
                    {
                        if (!AssetDatabase.IsValidFolder("Assets/AutoGenerated"))
                            AssetDatabase.CreateFolder("Assets", "AutoGenerated");

                        if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}"))
                            AssetDatabase.CreateFolder("Assets/AutoGenerated", $"{vrcAvatar.gameObject.name}");

                        if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Controllers"))
                            AssetDatabase.CreateFolder($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}", "Controllers");


                        var assetName = AssetDatabase.GenerateUniqueAssetPath($"Assets/AutoGenerated/{vrcAvatar.gameObject.name}/Controllers/FX_AnimatorController.controller");

                        var gestures = new AnimatorController();
                        AssetDatabase.CreateAsset(gestures, assetName);

                        vrcAvatar.baseAnimationLayers[4].isDefault = false;
                        vrcAvatar.baseAnimationLayers[4].animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetName);
                    }
                }

                if (vrcAvatar.baseAnimationLayers[4].animatorController is AnimatorController ac2)
                {
                    existingAnimation.CreateGameObjectToggle(vrcAvatar, ac2, "VRCEmote", danceName, "Dances", freeVRCEmote);
                }
            }
            return true;
        }

        public static GameObject GetOrAddGameobject(this GameObject go, string name)
        {
            GameObject targetG = null;
            for (int x = 0; x < go.transform.childCount; x++)
            {
                if (go.transform.GetChild(x).name == name)
                {
                    targetG = go.transform.GetChild(x).gameObject;
                }
            }
            if (targetG == null)
            {
                targetG = new GameObject(name);
                targetG.transform.parent = go.transform;
                targetG.transform.localPosition = Vector3.zero;
            }
            return targetG;
        }

        public static GameObject GetGameobject(this GameObject go, string name)
        {
            GameObject targetG = null;
            for (int x = 0; x < go.transform.childCount; x++)
            {
                if (go.transform.GetChild(x).name == name)
                {
                    targetG = go.transform.GetChild(x).gameObject;
                }
            }
            return targetG;
        }

        public static AnimatorState CreateSwitch(this VRCExpressionsMenu.Control toggle, AnimatorControllerLayer layer, VRCAvatarDescriptor vrcAvatar, AnimatorController animator, string name, SkinnedMeshRenderer renderer, Material targetTexture, int id, int materialIndex, AnimatorState targetState = null)
        {

            if (id != 0)
            {
                animator.GetOrAddParameter($"MS_{name}_{id}T", false);
                GetOrAddParameter(vrcAvatar.expressionParameters, $"MS_{name}_{id}T", 0f, ValueType.Bool);
                toggle.parameter = new VRCExpressionsMenu.Control.Parameter()
                {
                    name = $"MS_{name}_{id}T"
                };
                toggle.value = 0f;
            }


            var animation = renderer.CreateAnimationToggle(vrcAvatar, $"{name}", "MaterialSwitch", targetTexture, id, materialIndex);

            var state = layer.stateMachine.AddState($"Material {(id == 0 ? "Default" : $"{id}")}");
            state.motion = animation;

            if (targetState == null)
                return state;


            AnimatorConditionMode condition1 = AnimatorConditionMode.IfNot;
            AnimatorConditionMode condition2 = AnimatorConditionMode.If;

            var newTransition = new AnimatorStateTransition()
            {
                destinationState = targetState,
                conditions = new AnimatorCondition[1]
                {
                    new AnimatorCondition()
                    {
                        mode = condition1,
                        parameter = $"MS_{name}_{id}T",
                        threshold = 0f,
                    }
                }
            };
            state.AddTransition(newTransition);
            AssetDatabase.AddObjectToAsset(newTransition, state);

            newTransition = new AnimatorStateTransition()
            {
                destinationState = state,
                conditions = new AnimatorCondition[1]
                {
                    new AnimatorCondition()
                    {
                        mode = condition2,
                        parameter = $"MS_{name}_{id}T",
                        threshold = 1f,
                    }
                }
            };
            targetState.AddTransition(newTransition);
            AssetDatabase.AddObjectToAsset(newTransition, state);
            EditorUtility.SetDirty(animator);
            AssetDatabase.SaveAssets();
            return state;
        }


        public static void GenerateTextureSwitch(this VRCExpressionsMenu menu, string name, SkinnedMeshRenderer renderer, int selectedMaterialIndex, Material[] textures)
        {
            var subMenu = menu.CreateSubMenu($"{name}");

            if (ExpressionEditor.CurrentSelectedAvatar.baseAnimationLayers[4].animatorController == null)
            {
                if (EditorUtility.DisplayDialog("Missing animator controller in avatar", $"Do you want to create animator controller for FX?", "Create"))
                {
                    if (!AssetDatabase.IsValidFolder("Assets/AutoGenerated"))
                        AssetDatabase.CreateFolder("Assets", "AutoGenerated");

                    if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{ExpressionEditor.CurrentSelectedAvatar.gameObject.name}"))
                        AssetDatabase.CreateFolder("Assets/AutoGenerated", $"{ExpressionEditor.CurrentSelectedAvatar.gameObject.name}");

                    if (!AssetDatabase.IsValidFolder($"Assets/AutoGenerated/{ExpressionEditor.CurrentSelectedAvatar.gameObject.name}/Controllers"))
                        AssetDatabase.CreateFolder($"Assets/AutoGenerated/{ExpressionEditor.CurrentSelectedAvatar.gameObject.name}", "Controllers");


                    var assetName = AssetDatabase.GenerateUniqueAssetPath($"Assets/AutoGenerated/{ExpressionEditor.CurrentSelectedAvatar.gameObject.name}/Controllers/FX_AnimatorController.controller");

                    var gestures = new AnimatorController();
                    AssetDatabase.CreateAsset(gestures, assetName);

                    ExpressionEditor.CurrentSelectedAvatar.baseAnimationLayers[4].isDefault = false;
                    ExpressionEditor.CurrentSelectedAvatar.baseAnimationLayers[4].animatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetName);
                }
            }

            var animator = (AnimatorController)ExpressionEditor.CurrentSelectedAvatar.baseAnimationLayers[4].animatorController;

            animator.RemoveLayerIfExists($"MS_{name}");

            var stateMachine = new AnimatorStateMachine()
            {
                name = $"MS_{name}"
            };

            AssetDatabase.AddObjectToAsset(stateMachine, animator);

            animator.AddLayer(new AnimatorControllerLayer()
            {
                defaultWeight = 1f,
                name = $"MS_{name}",
                stateMachine = stateMachine
            });

            var lastLayer = animator.layers[animator.layers.Length - 1];

            AnimatorState defaultState = null;

            int used = 1;
            string toggleName = "Default";
            for(int x= 0; x< textures.Length; x++)
            {
                toggleName = $"Material {x}";

                if (used != 8)
                {
                    VRCExpressionsMenu.Control toggle = null;
                    if (x != 0)
                        toggle = subMenu.AddToggle(toggleName);


                    var state = toggle.CreateSwitch(lastLayer, ExpressionEditor.CurrentSelectedAvatar, animator, name, renderer, textures[x], x, selectedMaterialIndex, defaultState);

                    if (defaultState == null)
                        defaultState = state;
                }
                else
                {
                    subMenu = subMenu.CreateSubMenu("Next Page");
                    used = 1;
                    var toggle = subMenu.AddToggle(toggleName);
                    var state = toggle.CreateSwitch(lastLayer, ExpressionEditor.CurrentSelectedAvatar, animator, name, renderer, textures[x], x, selectedMaterialIndex, defaultState);

                    if (defaultState == null)
                        defaultState = state;
                }
                used++;
            }
            EditorUtility.SetDirty(animator);
            AssetDatabase.SaveAssets();
        }


        public static void GenerateDancesSubmenu(this VRCExpressionsMenu menu, string dancesPath, string danceFilter)
        {
            Dictionary<AnimationClip, AudioClip> dances = new Dictionary<AnimationClip, AudioClip>();
            foreach (var danceFolders in AssetDatabase.GetSubFolders(dancesPath)) 
            {
                AnimationClip clip = null;
                AudioClip aclip = null;
                foreach (var asset in AssetDatabase.FindAssets("t:audioclip t:animationclip", new string[] { danceFolders }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(asset);
                    if (path.EndsWith(".anim"))
                        clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    else
                        aclip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }

                if (clip == null)
                    continue;

                dances.Add(clip, aclip);
            }
            int used = 1;
            var sub = CreateSubMenu(menu, "Dances");
            sub.name = "Dances";

            string[] filter = danceFilter.Split(',');
            foreach (var dance in dances)
            {
                var danceName = dance.Key.name.ReplaceAll(filter, "");
                if (used != 8)
                {
                    var toggle = sub.AddToggle(danceName);
                    toggle.CreateDanceAnimation(ExpressionEditor.CurrentSelectedAvatar, dance.Key, danceName, dance.Value, true);
                }
                else
                {
                    sub = CreateSubMenu(sub, "Next Page");
                    used = 1;
                    var toggle = sub.AddToggle(danceName);
                    toggle.CreateDanceAnimation(ExpressionEditor.CurrentSelectedAvatar, dance.Key, danceName, dance.Value, true);
                }
                used++;
            }
        }

        public static VRCExpressionsMenu.Control AddToggle(this VRCExpressionsMenu menu, string name = "Toggle")
        {
            var toggle = new VRCExpressionsMenu.Control()
            {
                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                name = name     
            };
            menu.controls.Add(toggle);
            EditorUtility.SetDirty(menu);
            AssetDatabase.SaveAssets();
            return toggle;
        }

        public static VRCExpressionsMenu.Control AddButton(this VRCExpressionsMenu menu, string name = "Button")
        {
            var toggle = new VRCExpressionsMenu.Control()
            {
                type = VRCExpressionsMenu.Control.ControlType.Button,
                name = name
            };
            menu.controls.Add(toggle);
            EditorUtility.SetDirty(menu);
            AssetDatabase.SaveAssets();
            return toggle;
        }

        public static VRCExpressionsMenu.Control AddTwoAxis(this VRCExpressionsMenu menu, string name = "TwoAxis")
        {
            var twoAxis = new VRCExpressionsMenu.Control()
            {
                type = VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet,
                subParameters = new VRCExpressionsMenu.Control.Parameter[2],
                name = name
            };
            menu.controls.Add(twoAxis);
            EditorUtility.SetDirty(menu);
            AssetDatabase.SaveAssets();
            return twoAxis;
        }

        public static VRCExpressionsMenu.Control AddFourAxis(this VRCExpressionsMenu menu, string name = "FourAxis")
        {
            var fourAxis = new VRCExpressionsMenu.Control()
            {
                type = VRCExpressionsMenu.Control.ControlType.FourAxisPuppet,
                subParameters = new VRCExpressionsMenu.Control.Parameter[2],
                name = name
            };
            menu.controls.Add(fourAxis);
            EditorUtility.SetDirty(menu);
            AssetDatabase.SaveAssets();
            return fourAxis;
        }

        public static VRCExpressionsMenu.Control AddRadial(this VRCExpressionsMenu menu, string name = "Radial")
        {
            var fourAxis = new VRCExpressionsMenu.Control()
            {
                type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                subParameters = new VRCExpressionsMenu.Control.Parameter[1],
                name = name
            };
            menu.controls.Add(fourAxis);
            EditorUtility.SetDirty(menu);
            AssetDatabase.SaveAssets();
            return fourAxis;
        }

        public static VRCExpressionsMenu CreateSubMenu(this VRCExpressionsMenu menu, string name)
        {
            VRCExpressionsMenu eMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();

            var assetPath = menu.GetAssetDirectory();

            if (!AssetDatabase.IsValidFolder(Path.Combine(assetPath, name)))
                AssetDatabase.CreateFolder(assetPath, name);

            string assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(assetPath, name, "SubMenu.asset"));
            AssetDatabase.CreateAsset(eMenu, assetPathAndName);

            menu.controls.Add(new VRCExpressionsMenu.Control()
            {
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                name = name,
                subMenu = eMenu
            });


            EditorUtility.SetDirty(menu);
            AssetDatabase.SaveAssets();
            return eMenu;
        }

        public static string GetAssetDirectory(this Object obj)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (System.IO.File.GetAttributes(path) == FileAttributes.Directory)
                return path;
            else
                return Path.GetDirectoryName(path);
        }

        public static void Move<T>(this List<T> list, int oldIndex, int newIndex)
        {
            if ((oldIndex == newIndex) || (0 > oldIndex) || (oldIndex >= list.Count) || (0 > newIndex) ||
                (newIndex >= list.Count)) return;
            var i = 0;
            T tmp = list[oldIndex];
            if (oldIndex < newIndex)
            {
                for (i = oldIndex; i < newIndex; i++)
                {
                    list[i] = list[i + 1];
                }
            }
            else
            {
                for (i = oldIndex; i > newIndex; i--)
                {
                    list[i] = list[i - 1];
                }
            }
            list[newIndex] = tmp;
        }
    }
}
