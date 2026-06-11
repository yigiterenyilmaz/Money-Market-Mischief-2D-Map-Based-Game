using UnityEditor;
using UnityEngine;

/// <summary>
/// DialogueLine inspector çizimi. speakerName alanı sadece isFromPlayer=false iken görünür.
/// </summary>
[CustomPropertyDrawer(typeof(DialogueLine))]
public class DialogueLinePropertyDrawer : PropertyDrawer
{
    private const float SPACING = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty text = property.FindPropertyRelative("text");
        SerializedProperty isFromPlayer = property.FindPropertyRelative("isFromPlayer");

        float lineH = EditorGUIUtility.singleLineHeight;
        float textH = EditorGUI.GetPropertyHeight(text, true);

        float total = textH + SPACING + lineH;
        if (!isFromPlayer.boolValue)
            total += SPACING + lineH;

        return total;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty text = property.FindPropertyRelative("text");
        SerializedProperty isFromPlayer = property.FindPropertyRelative("isFromPlayer");
        SerializedProperty speakerName = property.FindPropertyRelative("speakerName");

        float lineH = EditorGUIUtility.singleLineHeight;
        float textH = EditorGUI.GetPropertyHeight(text, true);

        Rect textRect = new Rect(position.x, position.y, position.width, textH);
        EditorGUI.PropertyField(textRect, text, new GUIContent("Metin"));

        Rect playerRect = new Rect(position.x, position.y + textH + SPACING, position.width, lineH);
        EditorGUI.PropertyField(playerRect, isFromPlayer, new GUIContent("Oyuncu Konuşuyor"));

        if (!isFromPlayer.boolValue)
        {
            Rect speakerRect = new Rect(position.x, position.y + textH + SPACING + lineH + SPACING, position.width, lineH);
            EditorGUI.PropertyField(speakerRect, speakerName, new GUIContent("Konuşmacı İsmi"));
        }

        EditorGUI.EndProperty();
    }
}
