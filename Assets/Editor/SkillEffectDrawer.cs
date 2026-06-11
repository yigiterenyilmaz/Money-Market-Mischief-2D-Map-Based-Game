using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Skill ve Event editorlerinde effect listesi çizmek için ortak yardımcı sınıf.
/// [SerializeReference] ile PropertyField düzgün çalışmadığı için reflection ile çizim yapılır.
/// </summary>
public static class SkillEffectDrawer
{
    private static Type[] effectTypes;
    private static string[] effectTypeNames;

    private static void CacheTypes()
    {
        if (effectTypes != null) return;

        effectTypes = TypeCache.GetTypesDerivedFrom<SkillEffect>()
            .Where(t => !t.IsAbstract)
            .OrderBy(t => t.Name)
            .ToArray();

        effectTypeNames = effectTypes.Select(t => FormatTypeName(t.Name)).ToArray();
    }

    private static string FormatTypeName(string name)
    {
        // "PassiveIncomeEffect" → "Passive Income"
        name = name.Replace("Effect", "");
        var result = "";
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
                result += " ";
            result += name[i];
        }
        return result;
    }

    /// <summary>
    /// [SerializeReference] List<SkillEffect> alanını dropdown ile çizer.
    /// </summary>
    public static void DrawEffectList(SerializedProperty listProperty)
    {
        CacheTypes();

        for (int i = 0; i < listProperty.arraySize; i++)
        {
            var element = listProperty.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Başlık ve sil butonu
            EditorGUILayout.BeginHorizontal();
            string typeName = element.managedReferenceValue != null
                ? FormatTypeName(element.managedReferenceValue.GetType().Name)
                : "Null";
            EditorGUILayout.LabelField(typeName, EditorStyles.boldLabel);
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                listProperty.DeleteArrayElementAtIndex(i);
                listProperty.serializedObject.ApplyModifiedProperties();
                break;
            }
            EditorGUILayout.EndHorizontal();

            // Effect alanlarını reflection ile çiz
            // Not: reflection ile doğrudan obje üzerinde değişiklik yapıyoruz,
            // SerializedProperty sistemi bypass ediliyor. SkillEditor tarafında
            // ApplyModifiedProperties/Update sıralaması buna göre ayarlanmalı.
            if (element.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++;
                var obj = element.managedReferenceValue;
                bool changed = DrawFieldsWithReflection(obj);
                if (changed)
                {
                    Undo.RecordObject(listProperty.serializedObject.targetObject, "Modify Effect");
                    EditorUtility.SetDirty(listProperty.serializedObject.targetObject);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        // Effect ekle dropdown
        if (EditorGUILayout.DropdownButton(new GUIContent("Effect Ekle"), FocusType.Keyboard))
        {
            var menu = new GenericMenu();
            for (int i = 0; i < effectTypes.Length; i++)
            {
                var type = effectTypes[i];
                var displayName = effectTypeNames[i];
                menu.AddItem(new GUIContent(displayName), false, () =>
                {
                    listProperty.serializedObject.Update();
                    listProperty.arraySize++;
                    var newElement = listProperty.GetArrayElementAtIndex(listProperty.arraySize - 1);
                    newElement.managedReferenceValue = Activator.CreateInstance(type);
                    listProperty.serializedObject.ApplyModifiedProperties();
                });
            }
            menu.ShowAsContext();
        }
    }

    /// <summary>
    /// Bir objenin tüm public instance alanlarını reflection ile çizer.
    /// SerializedProperty'ye bağımlı olmadan doğrudan okuma/yazma yapar.
    /// </summary>
    private static bool DrawFieldsWithReflection(object obj)
    {
        bool changed = false;
        var fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var field in fields)
        {
            string label = ObjectNames.NicifyVariableName(field.Name);
            var fieldType = field.FieldType;
            object value = field.GetValue(obj);

            EditorGUI.BeginChangeCheck();

            if (fieldType == typeof(float))
            {
                float newVal = EditorGUILayout.FloatField(label, (float)value);
                if (EditorGUI.EndChangeCheck()) { field.SetValue(obj, newVal); changed = true; }
            }
            else if (fieldType == typeof(int))
            {
                int newVal = EditorGUILayout.IntField(label, (int)value);
                if (EditorGUI.EndChangeCheck()) { field.SetValue(obj, newVal); changed = true; }
            }
            else if (fieldType == typeof(string))
            {
                string newVal = EditorGUILayout.TextField(label, (string)value ?? "");
                if (EditorGUI.EndChangeCheck()) { field.SetValue(obj, newVal); changed = true; }
            }
            else if (fieldType == typeof(bool))
            {
                bool newVal = EditorGUILayout.Toggle(label, (bool)value);
                if (EditorGUI.EndChangeCheck()) { field.SetValue(obj, newVal); changed = true; }
            }
            else if (fieldType.IsEnum)
            {
                Enum newVal = EditorGUILayout.EnumPopup(label, (Enum)value);
                if (EditorGUI.EndChangeCheck()) { field.SetValue(obj, newVal); changed = true; }
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(fieldType))
            {
                var newVal = EditorGUILayout.ObjectField(label, (UnityEngine.Object)value, fieldType, false);
                if (EditorGUI.EndChangeCheck()) { field.SetValue(obj, newVal); changed = true; }
            }
            else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                EditorGUI.EndChangeCheck();
                var elementType = fieldType.GetGenericArguments()[0];

                //sadece UnityEngine.Object listelerini destekle (ScriptableObject vs.)
                if (typeof(UnityEngine.Object).IsAssignableFrom(elementType))
                {
                    var list = (System.Collections.IList)value;
                    if (list == null)
                    {
                        list = (System.Collections.IList)Activator.CreateInstance(fieldType);
                        field.SetValue(obj, list);
                        changed = true;
                    }

                    EditorGUILayout.LabelField(label);
                    EditorGUI.indentLevel++;

                    for (int li = 0; li < list.Count; li++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        var oldItem = list[li] as UnityEngine.Object;
                        var newItem = EditorGUILayout.ObjectField(oldItem, elementType, false);
                        if (newItem != oldItem) { list[li] = newItem; changed = true; }
                        if (GUILayout.Button("-", GUILayout.Width(20)))
                        {
                            list.RemoveAt(li);
                            changed = true;
                            EditorGUILayout.EndHorizontal();
                            break;
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    if (GUILayout.Button("+", GUILayout.Width(20)))
                    {
                        list.Add(null);
                        changed = true;
                    }

                    EditorGUI.indentLevel--;
                }
                else
                {
                    EditorGUILayout.LabelField(label, "(Desteklenmeyen liste tipi)");
                }
            }
            else
            {
                EditorGUI.EndChangeCheck();
                EditorGUILayout.LabelField(label, fieldType.Name);
            }
        }

        return changed;
    }
}
