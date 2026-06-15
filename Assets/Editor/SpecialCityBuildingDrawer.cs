using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SpecialCityBuilding))]
public class SpecialCityBuildingDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        float lineHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        float y = position.y + lineHeight;

        SerializedProperty daySprite       = property.FindPropertyRelative("daySprite");
        SerializedProperty nightSprite     = property.FindPropertyRelative("nightSprite");
        SerializedProperty count           = property.FindPropertyRelative("count");
        SerializedProperty targetLayer      = property.FindPropertyRelative("targetLayer");
        SerializedProperty connectToRoad   = property.FindPropertyRelative("connectToRoad");
        SerializedProperty clearingRadius  = property.FindPropertyRelative("clearingRadius");
        SerializedProperty isIsometric     = property.FindPropertyRelative("isIsometric");
        SerializedProperty isAnimated      = property.FindPropertyRelative("isAnimated");
        SerializedProperty animationFrames = property.FindPropertyRelative("animationFrames");
        SerializedProperty frameRate       = property.FindPropertyRelative("frameRate");

        y = DrawProperty(position, y, daySprite);
        y = DrawProperty(position, y, nightSprite);
        y = DrawProperty(position, y, count);
        y = DrawProperty(position, y, targetLayer);
        y = DrawProperty(position, y, connectToRoad);
        y = DrawProperty(position, y, clearingRadius);
        y = DrawProperty(position, y, isIsometric);
        y = DrawProperty(position, y, isAnimated);

        // isAnimated tikli ise animasyon alanlari
        if (isAnimated.boolValue)
        {
            y = DrawProperty(position, y, animationFrames, true);
            y = DrawProperty(position, y, frameRate);
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    private float DrawProperty(Rect position, float y, SerializedProperty prop, bool includeChildren = false)
    {
        float height = EditorGUI.GetPropertyHeight(prop, includeChildren);
        Rect rect = new Rect(position.x, y, position.width, height);
        EditorGUI.PropertyField(rect, prop, includeChildren);
        return y + height + EditorGUIUtility.standardVerticalSpacing;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        float lineHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        // Foldout + 8 sabit alan
        float total = lineHeight * 9;

        SerializedProperty isAnimated = property.FindPropertyRelative("isAnimated");
        if (isAnimated != null && isAnimated.boolValue)
        {
            SerializedProperty animationFrames = property.FindPropertyRelative("animationFrames");
            float framesHeight = EditorGUI.GetPropertyHeight(animationFrames, true);
            total += framesHeight + EditorGUIUtility.standardVerticalSpacing; // animationFrames (degisken yukseklik)
            total += lineHeight; // frameRate
        }

        return total;
    }
}
