using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
#endif

namespace Unity.Netcode.Editor
{
    [CustomEditor(typeof(NetworkBehaviour), true)]
    [CanEditMultipleObjects]
#if ODIN_INSPECTOR
    public class NetworkBehaviourEditor : OdinEditor
#else
    public class NetworkBehaviourEditor : UnityEditor.Editor
#endif
    {
        private bool m_Initialized;
        private readonly List<string> m_NetworkVariableNames = new List<string>();
        private readonly Dictionary<string, FieldInfo> m_NetworkVariableFields = new Dictionary<string, FieldInfo>();
        private readonly Dictionary<string, object> m_NetworkVariableObjects = new Dictionary<string, object>();

        private GUIContent m_NetworkVariableLabelGuiContent;

        private void Init(MonoScript script)
        {
            m_Initialized = true;

            m_NetworkVariableNames.Clear();
            m_NetworkVariableFields.Clear();
            m_NetworkVariableObjects.Clear();

            m_NetworkVariableLabelGuiContent = new GUIContent("NetworkVariable", "This variable is a NetworkVariable. It can not be serialized and can only be changed during runtime.");

            var fields = script.GetClass().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            for (int i = 0; i < fields.Length; i++)
            {
                var ft = fields[i].FieldType;
                if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(NetworkVariable<>) && !fields[i].IsDefined(typeof(HideInInspector), true))
                {
                    m_NetworkVariableNames.Add(fields[i].Name);
                    m_NetworkVariableFields.Add(fields[i].Name, fields[i]);
                }
            }
        }

        private void RenderNetworkVariable(int index)
        {
            if (!m_NetworkVariableFields.ContainsKey(m_NetworkVariableNames[index]))
            {
                serializedObject.Update();
                var scriptProperty = serializedObject.FindProperty("m_Script");
                if (scriptProperty == null)
                {
                    return;
                }

                var targetScript = scriptProperty.objectReferenceValue as MonoScript;
                Init(targetScript);
            }

            object value = m_NetworkVariableFields[m_NetworkVariableNames[index]].GetValue(target);
            if (value == null)
            {
                var fieldType = m_NetworkVariableFields[m_NetworkVariableNames[index]].FieldType;
                var networkVariable = (NetworkVariableBase)Activator.CreateInstance(fieldType, true);
                m_NetworkVariableFields[m_NetworkVariableNames[index]].SetValue(target, networkVariable);
            }

            var type = m_NetworkVariableFields[m_NetworkVariableNames[index]].GetValue(target).GetType();
            var genericType = type.GetGenericArguments()[0];

            EditorGUILayout.BeginHorizontal();
            bool disabled = m_NetworkVariableFields[m_NetworkVariableNames[index]].IsPrivate && !EditorApplication.isPlaying;
            EditorGUI.BeginDisabledGroup(disabled);
            if (genericType.IsValueType)
            {
                var method = typeof(NetworkBehaviourEditor).GetMethod("RenderNetworkVariableValueType", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.NonPublic);
                var genericMethod = method.MakeGenericMethod(genericType);
                genericMethod.Invoke(this, new[] { (object)index });
            }
            else
            {
                EditorGUILayout.LabelField("Type not renderable");
            }

            GUILayout.Label(m_NetworkVariableLabelGuiContent, EditorStyles.miniLabel, GUILayout.Width(EditorStyles.miniLabel.CalcSize(m_NetworkVariableLabelGuiContent).x));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private void RenderNetworkVariableValueType<T>(int index) where T : unmanaged
        {
            var networkVariable = (NetworkVariable<T>)m_NetworkVariableFields[m_NetworkVariableNames[index]].GetValue(target);
            var type = typeof(T);
            object val = networkVariable.Value;
            string name = m_NetworkVariableNames[index];

            var behaviour = (NetworkBehaviour)target;

            // Only server can MODIFY. So allow modification if network is either not running or we are server
            if (behaviour.IsBehaviourEditable())
            {
                EditorGUI.BeginChangeCheck();

                if (type == typeof(int))
                {
                    val = EditorGUILayout.IntField(name, (int)val);
                }
                else if (type == typeof(uint))
                {
                    val = (uint)EditorGUILayout.LongField(name, (long)((uint)val));
                }
                else if (type == typeof(short))
                {
                    val = (short)EditorGUILayout.IntField(name, (int)((short)val));
                }
                else if (type == typeof(ushort))
                {
                    val = (ushort)EditorGUILayout.IntField(name, (int)((ushort)val));
                }
                else if (type == typeof(sbyte))
                {
                    val = (sbyte)EditorGUILayout.IntField(name, (int)((sbyte)val));
                }
                else if (type == typeof(byte))
                {
                    val = (byte)EditorGUILayout.IntField(name, (int)((byte)val));
                }
                else if (type == typeof(long))
                {
                    val = EditorGUILayout.LongField(name, (long)val);
                }
                else if (type == typeof(ulong))
                {
                    val = (ulong)EditorGUILayout.LongField(name, (long)((ulong)val));
                }
                else if (type == typeof(bool))
                {
                    val = EditorGUILayout.Toggle(name, (bool)val);
                }
                else if (type == typeof(string))
                {
                    val = EditorGUILayout.TextField(name, (string)val);
                }
                else if (type.IsEnum)
                {
                    val = EditorGUILayout.EnumPopup(name, (Enum)val);
                }
                else
                {
                    EditorGUILayout.LabelField("Type not renderable");
                }

                if (EditorGUI.EndChangeCheck())
                {
                    networkVariable.Value = (T)val;
                }
            }
            else
            {
                EditorGUILayout.LabelField(name, EditorStyles.wordWrappedLabel);
                EditorGUILayout.SelectableLabel(val.ToString(), EditorStyles.wordWrappedLabel);
            }
        }

        #if ODIN_INSPECTOR
        public override void OnInspectorGUI()
        {
            // init
            if (!m_Initialized)
            {
                serializedObject.Update();
                var scriptProperty = serializedObject.FindProperty("m_Script");
                if (scriptProperty == null)
                {
                    return;
                }

                var targetScript = scriptProperty.objectReferenceValue as MonoScript;
                Init(targetScript);
            }

            // render regular serialized fields
            InspectorUtilities.BeginDrawPropertyTree(Tree, true);
            foreach (InspectorProperty property in Tree.EnumerateTree(false))
            {
                if (m_NetworkVariableNames.Contains(property.Name))
                    continue;

                property.Draw(property.Label);
            }
            InspectorUtilities.EndDrawPropertyTree(Tree);

            // render network vars
            EditorGUI.BeginChangeCheck();
            serializedObject.Update();
            for (int i = 0; i < m_NetworkVariableNames.Count; i++)
            {
                RenderNetworkVariable(i);
            }
            serializedObject.ApplyModifiedProperties();
            EditorGUI.EndChangeCheck();
        }
        #else
        public override void OnInspectorGUI()
        {
            if (!m_Initialized)
            {
                serializedObject.Update();
                var scriptProperty = serializedObject.FindProperty("m_Script");
                if (scriptProperty == null)
                {
                    return;
                }

                var targetScript = scriptProperty.objectReferenceValue as MonoScript;
                Init(targetScript);
            }

            EditorGUI.BeginChangeCheck();
            serializedObject.Update();

            for (int i = 0; i < m_NetworkVariableNames.Count; i++)
            {
                RenderNetworkVariable(i);
            }

            var property = serializedObject.GetIterator();
            bool expanded = true;
            while (property.NextVisible(expanded))
            {
                if (m_NetworkVariableNames.Contains(property.name))
                {
                    // Skip rendering of NetworkVars, they have special rendering
                    continue;
                }

                if (property.propertyType == SerializedPropertyType.ObjectReference)
                {
                    if (property.name == "m_Script")
                    {
                        EditorGUI.BeginDisabledGroup(true);
                    }

                    EditorGUILayout.PropertyField(property, true);

                    if (property.name == "m_Script")
                    {
                        EditorGUI.EndDisabledGroup();
                    }
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(property, true);
                    EditorGUILayout.EndHorizontal();
                }

                expanded = false;
            }

            serializedObject.ApplyModifiedProperties();
            EditorGUI.EndChangeCheck();
        }
        #endif
    }
}
