using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Helper class to automagically convert Unity Text and InputFields to TextMeshPro
/// Warning: This script might contain bugs, as it is a result of a community effort
/// Warning: You have to manually set rename all your uGUI Text and InputFields in your script, and reference the new TextMeshPro components
/// This script was originally created by: BRUNO MIKOSKI (http://www.brunomikoski.com/playground/2015/3/31/convert-text-component-to-textmeshprougui-keeping-configurations)
/// The script was then modified by: SIMON TYSLAND (https://tinyurl.com/simtys)
/// </summary>
public class TextMeshProAutoConverter : EditorWindow
{
    public List<ReplaceFont> ReplaceFonts = new List<ReplaceFont>();
    public int HowManyLoops = 10;
    private int _currentSize;

    private const string PROGRESSBAR_TITLE_TEXT = "Converting Text components to TextMeshProUGUI";
    private const string PROGRESSBAR_DESC_TEXT = "Using some heavy black magic here (not), so this might take a while, please hold on";
    private const string PROGRESSBAR_TITLE_INPUTFIELD = "Converting InputField components to TMP_Input";
    private const string PROGRESSBAR_DESC_INPUTFIELD = "Doing the same thing for InputFields. Please hold on";

    [MenuItem("Tools/TextMeshPro AutoConverter (Convert from uGUI to TMP)")]
    public static void ShowWindow()
    {
        GetWindow(typeof(TextMeshProAutoConverter));
    }

    private void OnGUI()
    {
        int newSize = EditorGUILayout.IntField("Number of Font Assets:", _currentSize);
        if (newSize != _currentSize)
        {
            _currentSize = newSize;
            ReplaceFonts = new List<ReplaceFont>();
            for (int i = 0; i < _currentSize; i++)
            {
                ReplaceFonts.Add(new ReplaceFont());
            }
        }

        foreach (ReplaceFont replaceFont in ReplaceFonts)
        {
            EditorGUILayout.BeginHorizontal();
            replaceFont.OriginalFont = (Font)EditorGUILayout.ObjectField(replaceFont.OriginalFont, typeof(Font), false);
            EditorGUILayout.PrefixLabel(" to: ");
            replaceFont.TargetFont = (TMP_FontAsset)EditorGUILayout.ObjectField(replaceFont.TargetFont, typeof(TMP_FontAsset), false);
            EditorGUILayout.EndHorizontal();
        }

        HowManyLoops = EditorGUILayout.IntField("Loops: ", HowManyLoops);

        if (GUILayout.Button("Execute!"))
        {
            var allTextObjects = Resources.FindObjectsOfTypeAll<Text>();
            List<Text> uniqueTexts = new List<Text>();
            foreach (Text text in allTextObjects)
            {
                if (!uniqueTexts.Contains(text) && !EditorUtility.IsPersistent(text))
                {
                    uniqueTexts.Add(text);
                }
            }

            Debug.Log("Trying to convert Text components. Total text components founds: " + uniqueTexts.Count);

            for (int i = 0; i < uniqueTexts.Count; i++)
            {
                if (i >= HowManyLoops)
                {
                    EditorUtility.ClearProgressBar();
                    break;
                }

                int total = HowManyLoops < uniqueTexts.Count ? HowManyLoops : uniqueTexts.Count;
                if (EditorUtility.DisplayCancelableProgressBar(PROGRESSBAR_TITLE_TEXT, PROGRESSBAR_DESC_TEXT, (float)i / total))
                {
                    EditorUtility.ClearProgressBar();
                    return;
                }

                Text textComponent = uniqueTexts[i];
                if (textComponent.GetComponent<TextMeshProUGUI>() != null)
                {
                    continue;
                }

                GameObject textObject = textComponent.gameObject;

                //If it is not a part of a prefab, we can modify the object directly in the scene.
                if (!PrefabUtility.IsPartOfAnyPrefab(textObject))
                {
                    SwapTextComponents(textComponent, textObject);
                    continue;
                }

                //Else we have to load, modify, save and unload the prefab.
                if (PrefabUtility.IsPartOfPrefabInstance(textObject))
                {
                    bool isImmutablePrefab = PrefabUtility.IsPartOfImmutablePrefab(textObject);
                    bool isVariantPrefab = PrefabUtility.IsPartOfVariantPrefab(textObject);
                    if (isImmutablePrefab || isVariantPrefab)
                    {
                        continue;
                    }

                    GameObject go = PrefabUtility.GetCorrespondingObjectFromSource(textObject);
                    if (go != null)
                    {
                        string path = AssetDatabase.GetAssetPath(go);
                        if (!string.IsNullOrEmpty(path))
                        {
                            var rootGameObject = PrefabUtility.LoadPrefabContents(path);
                            SwapTextComponents(textComponent, textObject);
                            PrefabUtility.SaveAsPrefabAsset(rootGameObject, path);
                            PrefabUtility.UnloadPrefabContents(rootGameObject);
                        }
                    }
                }
                else if (PrefabUtility.IsPartOfPrefabAsset(textObject))
                {
                    bool isImmutablePrefab = PrefabUtility.IsPartOfImmutablePrefab(textObject);
                    bool isVariantPrefab = PrefabUtility.IsPartOfVariantPrefab(textObject);
                    if (isImmutablePrefab || isVariantPrefab)
                    {
                        continue;
                    }

                    string path = AssetDatabase.GetAssetPath(textObject);
                    if (!string.IsNullOrEmpty(path))
                    {
                        var rootGameObject = PrefabUtility.LoadPrefabContents(path);
                        PrefabUtility.SaveAsPrefabAsset(rootGameObject, path);
                        PrefabUtility.UnloadPrefabContents(rootGameObject);
                    }
                }
                else if (PrefabUtility.IsPartOfRegularPrefab(textObject))
                {
                    //This is really wierd! My script says it is a part of a prefab, but in my hierarchy it is not...
                    SwapTextComponents(textComponent, textObject);
                }
                else
                {
                    Debug.LogWarning(
                        "There is no support for this type of prefab. Text was not changed to TextMeshPro component. I suggest doing this manually" +
                        textObject.name + " (" + textObject.GetHashCode() + ")");
                }
            }

            var allInputFieldObjects = Resources.FindObjectsOfTypeAll<InputField>();
            List<InputField> uniqueInputFields = new List<InputField>();
            foreach (InputField text in allInputFieldObjects)
            {
                if (!uniqueInputFields.Contains(text) && !EditorUtility.IsPersistent(text))
                {
                    uniqueInputFields.Add(text);
                }
            }

            EditorUtility.ClearProgressBar();

            Debug.Log("Trying to convert InputField components. Total InputField components founds: " + uniqueInputFields.Count);

            for (int i = 0; i < uniqueInputFields.Count; i++)
            {
                if (i >= HowManyLoops)
                {
                    EditorUtility.ClearProgressBar();
                    break;
                }

                int total = HowManyLoops < uniqueTexts.Count ? HowManyLoops : uniqueTexts.Count;
                if (EditorUtility.DisplayCancelableProgressBar(PROGRESSBAR_TITLE_INPUTFIELD, PROGRESSBAR_DESC_INPUTFIELD, (float)i / total))
                {
                    break;
                }

                InputField inputField = uniqueInputFields[i];
                if (inputField.GetComponent<TMP_InputField>() != null)
                {
                    continue;
                }

                GameObject textObject = inputField.gameObject;

                //If it is not a part of a prefab, we can modify the object directly in the scene.
                if (!PrefabUtility.IsPartOfAnyPrefab(textObject))
                {
                    SwapInputFieldComponents(inputField);
                    continue;
                }

                //Else we have to load, modify, save and unload the prefab.
                if (PrefabUtility.IsPartOfPrefabInstance(textObject))
                {
                    bool isImmutablePrefab = PrefabUtility.IsPartOfImmutablePrefab(textObject);
                    bool isVariantPrefab = PrefabUtility.IsPartOfVariantPrefab(textObject);
                    if (isImmutablePrefab || isVariantPrefab)
                    {
                        continue;
                    }

                    GameObject go = PrefabUtility.GetCorrespondingObjectFromSource(textObject);
                    if (go != null)
                    {
                        string path = AssetDatabase.GetAssetPath(go);
                        if (!string.IsNullOrEmpty(path))
                        {
                            var rootGameObject = PrefabUtility.LoadPrefabContents(path);
                            SwapInputFieldComponents(inputField);
                            PrefabUtility.SaveAsPrefabAsset(rootGameObject, path);
                            PrefabUtility.UnloadPrefabContents(rootGameObject);
                        }
                    }
                }
                else if (PrefabUtility.IsPartOfPrefabAsset(textObject))
                {
                    bool isImmutablePrefab = PrefabUtility.IsPartOfImmutablePrefab(textObject);
                    bool isVariantPrefab = PrefabUtility.IsPartOfVariantPrefab(textObject);
                    if (isImmutablePrefab || isVariantPrefab)
                    {
                        continue;
                    }

                    string path = AssetDatabase.GetAssetPath(textObject);
                    if (!string.IsNullOrEmpty(path))
                    {
                        var rootGameObject = PrefabUtility.LoadPrefabContents(path);
                        PrefabUtility.SaveAsPrefabAsset(rootGameObject, path);
                        PrefabUtility.UnloadPrefabContents(rootGameObject);
                    }
                }
                else if (PrefabUtility.IsPartOfRegularPrefab(textObject))
                {
                    //This is really wierd! My script says it is a part of a prefab, but in my hierarchy it is not...
                    SwapInputFieldComponents(inputField);
                }
                else
                {
                    Debug.LogWarning(
                        "There is no support for this type of prefab. Text was not changed to TextMeshPro component. I suggest doing this manually" +
                        textObject.name + " (" + textObject.GetHashCode() + ")");
                }
            }
        }

        EditorUtility.ClearProgressBar();

        if (GUILayout.Button("Find missing references in scene"))
        {
            FindMissingReferencesInCurrentScene();
        }
    }

    private void SwapTextComponents(Text textComponent, GameObject textObject)
    {
        TextMeshProUGUI textMeshPro = textObject.GetComponent<TextMeshProUGUI>();

        if (textMeshPro != null || textComponent == null)
        {
            return;
        }

        TextHolder textHolderObject = new TextHolder(textComponent);

        TMP_FontAsset textMeshProFont = GetTextMeshProFont(textHolderObject.Font);
        if (textMeshProFont == null)
        {
            if (textHolderObject.Font != null)
            {
                Debug.LogWarning(
                    "Aborted. Could not find the proper TextMeshPro Font Asset for this font: " + textHolderObject.Font +
                    ", please create a TMP font asset for this font, in order to convert Text comp to TextMeshPro Text (Click to see the GameObject that produced this error)",
                    textObject);
            }
            else
            {
                Debug.LogWarning(
                    "Aborted. Font was null on original Text component. Could not convert to TextMeshPro. (Click to see the GameObject that produced this error)",
                    textObject);
            }

            return;
        }

        Text textToBeDestroyed = textComponent;
        Outline outlineToBeDestoryed = textObject.GetComponent<Outline>();
        Shadow shadowToBeDestroyed = textObject.GetComponent<Shadow>();

        DestroyImmediate(textToBeDestroyed, true);

        if (outlineToBeDestoryed != null)
        {
            DestroyImmediate(outlineToBeDestoryed, true);
        }

        if (shadowToBeDestroyed != null)
        {
            DestroyImmediate(shadowToBeDestroyed, true);
        }

        textMeshPro = textObject.AddComponent<TextMeshProUGUI>();

        if (textMeshPro == null)
        {
            Debug.LogWarning("TextMeshProUGUI comp was null, aborted");
            return;
        }

        textMeshPro.font = textMeshProFont;

        textMeshPro.fontSize = textHolderObject.FontSize;
        textMeshPro.fontSizeMax = textHolderObject.ResizeTextMaxSize;
        textMeshPro.enableAutoSizing = textHolderObject.ResizeTextForBestFit;

        textMeshPro.alignment = GetAligmentFromTextObject(textHolderObject.Alignment);

        textMeshPro.color = textHolderObject.Color;

        textMeshPro.enableWordWrapping = textHolderObject.HorizontalOverflow != HorizontalWrapMode.Wrap;

        textMeshPro.overflowMode = GetOverflowMode(textHolderObject.VerticalOverflow);

        textMeshPro.text = textHolderObject.Text;

        textMeshPro.richText = textHolderObject.SupportRichText;

        textMeshPro.rectTransform.position = textHolderObject.Rect.position;
        textMeshPro.rectTransform.sizeDelta = textHolderObject.SizeDelta;
        textMeshPro.rectTransform.anchorMin = textHolderObject.Rect.anchorMin;
        textMeshPro.rectTransform.anchorMax = textHolderObject.Rect.anchorMax;
        textMeshPro.rectTransform.pivot = textHolderObject.Rect.pivot;
        textMeshPro.rectTransform.localScale = textHolderObject.Rect.localScale;
        textMeshPro.rectTransform.localRotation = textHolderObject.Rect.localRotation;

        textMeshPro.raycastTarget = textHolderObject.RaycastTarget;
        switch (textHolderObject.FontStyle)
        {
            case FontStyle.Normal:
                textMeshPro.fontStyle = FontStyles.Normal;
                break;
            case FontStyle.Bold:
                textMeshPro.fontStyle = FontStyles.Bold;
                break;
            case FontStyle.Italic:
                textMeshPro.fontStyle = FontStyles.Italic;
                break;
            case FontStyle.BoldAndItalic:
                textMeshPro.fontStyle = FontStyles.Bold;
                textMeshPro.fontStyle = FontStyles.Italic;
                break;
        }

        textMeshPro.fontSizeMin = textHolderObject.ResizeTextMinSize;
    }

    private void SwapInputFieldComponents(InputField uInput)
    {
        if (uInput != null)
        {
            GameObject tempInputFieldObj = new GameObject("tempObj");
            TMP_InputField tempInput = tempInputFieldObj.AddComponent<TMP_InputField>();
            tempInput.interactable = uInput.interactable;
            tempInput.targetGraphic = uInput.targetGraphic;
            tempInput.transition = uInput.transition;
            switch (tempInput.transition)
            {
                case Selectable.Transition.None:
                    break;
                case Selectable.Transition.ColorTint:
                    tempInput.colors = tempInput.colors;
                    break;
                case Selectable.Transition.SpriteSwap:
                    tempInput.spriteState = uInput.spriteState;
                    break;
                case Selectable.Transition.Animation:
                    tempInput.animationTriggers.normalTrigger = tempInput.animationTriggers.normalTrigger;
                    tempInput.animationTriggers.highlightedTrigger = tempInput.animationTriggers.highlightedTrigger;
                    tempInput.animationTriggers.pressedTrigger = tempInput.animationTriggers.pressedTrigger;
                    tempInput.animationTriggers.disabledTrigger = tempInput.animationTriggers.disabledTrigger;
                    break;
            }

            TextMeshProUGUI tempPlaceHolderText =
                uInput.transform.childCount > 0 ? uInput.transform.GetComponentInChildren<TextMeshProUGUI>() : null;

            TextMeshProUGUI[] tempTextComponentTexts =
                uInput.transform.childCount > 0 ? uInput.transform.GetComponentsInChildren<TextMeshProUGUI>() : null;

            TextMeshProUGUI tempTextComponentText = null;
            if (tempTextComponentTexts != null && tempTextComponentTexts.Length > 0)
            {
                tempTextComponentText = tempTextComponentTexts.FirstOrDefault(x => x != tempPlaceHolderText);
            }

            tempInput.navigation = uInput.navigation;
            tempInput.textViewport = null;
            tempInput.textComponent = tempTextComponentText;
            tempInput.placeholder = tempPlaceHolderText;
            tempInput.text = uInput.text;
            tempInput.characterLimit = uInput.characterLimit;
            tempInput.caretBlinkRate = uInput.caretBlinkRate;
            tempInput.caretWidth = uInput.caretWidth;
            tempInput.customCaretColor = uInput.customCaretColor;
            if (uInput.customCaretColor)
            {
                tempInput.caretColor = uInput.caretColor;
            }

            tempInput.selectionColor = uInput.selectionColor;
            tempInput.shouldHideMobileInput = uInput.shouldHideMobileInput;
            tempInput.readOnly = uInput.readOnly;
            switch (uInput.contentType)
            {
                case InputField.ContentType.Standard:
                    tempInput.contentType = TMP_InputField.ContentType.Standard;
                    break;
                case InputField.ContentType.Autocorrected:
                    tempInput.contentType = TMP_InputField.ContentType.Autocorrected;
                    break;
                case InputField.ContentType.IntegerNumber:
                    tempInput.contentType = TMP_InputField.ContentType.IntegerNumber;
                    break;
                case InputField.ContentType.DecimalNumber:
                    tempInput.contentType = TMP_InputField.ContentType.DecimalNumber;
                    break;
                case InputField.ContentType.Alphanumeric:
                    tempInput.contentType = TMP_InputField.ContentType.Alphanumeric;
                    break;
                case InputField.ContentType.Name:
                    tempInput.contentType = TMP_InputField.ContentType.Name;
                    break;
                case InputField.ContentType.EmailAddress:
                    tempInput.contentType = TMP_InputField.ContentType.EmailAddress;
                    break;
                case InputField.ContentType.Password:
                    tempInput.contentType = TMP_InputField.ContentType.Password;
                    break;
                case InputField.ContentType.Pin:
                    tempInput.contentType = TMP_InputField.ContentType.Pin;
                    break;
                case InputField.ContentType.Custom:
                    tempInput.contentType = TMP_InputField.ContentType.Custom;
                    break;
            }

            switch (uInput.lineType)
            {
                case InputField.LineType.SingleLine:
                    tempInput.lineType = TMP_InputField.LineType.SingleLine;
                    break;
                case InputField.LineType.MultiLineSubmit:
                    tempInput.lineType = TMP_InputField.LineType.MultiLineSubmit;
                    break;
                case InputField.LineType.MultiLineNewline:
                    tempInput.lineType = TMP_InputField.LineType.MultiLineNewline;
                    break;
            }

            TMP_FontAsset temptFontOfChild = uInput.transform.childCount > 0 && uInput.transform.GetComponentInChildren<TextMeshProUGUI>() != null
                ? uInput.transform.GetComponentInChildren<TextMeshProUGUI>().font
                : null;

            if (temptFontOfChild != null && tempInput.fontAsset != null)
            {
                tempInput.fontAsset = temptFontOfChild;
            }

            GameObject originalObject = uInput.gameObject;

            DestroyImmediate(uInput, true);

            var newInputField = originalObject.AddComponent<TMP_InputField>();

            newInputField.interactable = tempInput.interactable;
            newInputField.targetGraphic = tempInput.targetGraphic;
            newInputField.transition = tempInput.transition;
            switch (newInputField.transition)
            {
                case Selectable.Transition.None:
                    break;
                case Selectable.Transition.ColorTint:
                    newInputField.colors = newInputField.colors;
                    break;
                case Selectable.Transition.SpriteSwap:
                    newInputField.spriteState = tempInput.spriteState;
                    break;
                case Selectable.Transition.Animation:
                    newInputField.animationTriggers.normalTrigger = newInputField.animationTriggers.normalTrigger;
                    newInputField.animationTriggers.highlightedTrigger = newInputField.animationTriggers.highlightedTrigger;
                    newInputField.animationTriggers.pressedTrigger = newInputField.animationTriggers.pressedTrigger;
                    newInputField.animationTriggers.disabledTrigger = newInputField.animationTriggers.disabledTrigger;
                    break;
            }

            newInputField.navigation = tempInput.navigation;
            newInputField.textViewport = null;
            newInputField.textComponent = tempInput.textComponent;
            newInputField.text = tempInput.text;

            newInputField.characterLimit = tempInput.characterLimit;
            newInputField.caretBlinkRate = tempInput.caretBlinkRate;
            newInputField.caretWidth = tempInput.caretWidth;
            newInputField.customCaretColor = tempInput.customCaretColor;
            if (tempInput.customCaretColor)
            {
                newInputField.caretColor = tempInput.caretColor;
            }

            newInputField.selectionColor = tempInput.selectionColor;
            newInputField.shouldHideMobileInput = tempInput.shouldHideMobileInput;
            newInputField.readOnly = tempInput.readOnly;
            newInputField.contentType = tempInput.contentType;
            newInputField.lineType = tempInput.lineType;

            if (tempInput.fontAsset != null)
            {
                newInputField.fontAsset = tempInput.fontAsset;
            }

            newInputField.textComponent = tempTextComponentText;
            newInputField.placeholder = tempPlaceHolderText;

            StringBuilder sb = new StringBuilder(1000);
            sb.Append("Success! Converted InputField to TMP_Input. However, something might have gone wrong. Click here to see the list of warnings");
            sb.Append(
                "Tried to auto-assign the correct text components to TextComponent and Placeholder fields. This might have gone wrong. Please check each individual input field!");
            sb.Append(
                "TextMeshPro input field could not copy uGUI Input Field's Action callbacks (Like OnValueChanged, OnSelect, etc). This is not possible as far as I know? ;)");
            sb.Append("ContentType field might be wrong, please double check it.");
            Debug.LogWarning(sb.ToString(), newInputField.gameObject);

            DestroyImmediate(tempInputFieldObj, true);
        }
        else
        {
            Debug.Log("Original uGUI InputField was null");
        }
    }

    private TextOverflowModes GetOverflowMode(VerticalWrapMode verticalOverflow)
    {
        if (verticalOverflow == VerticalWrapMode.Truncate)
        {
            return TextOverflowModes.Truncate;
        }

        return TextOverflowModes.Overflow;
    }

    private TextAlignmentOptions GetAligmentFromTextObject(TextAnchor alignment)
    {
        if (alignment == TextAnchor.LowerCenter)
        {
            return TextAlignmentOptions.Bottom;
        }

        if (alignment == TextAnchor.LowerLeft)
        {
            return TextAlignmentOptions.BottomLeft;
        }

        if (alignment == TextAnchor.LowerRight)
        {
            return TextAlignmentOptions.BottomRight;
        }

        if (alignment == TextAnchor.MiddleCenter)
        {
            return TextAlignmentOptions.Midline;
        }

        if (alignment == TextAnchor.MiddleLeft)
        {
            return TextAlignmentOptions.MidlineLeft;
        }

        if (alignment == TextAnchor.MiddleRight)
        {
            return TextAlignmentOptions.MidlineRight;
        }

        if (alignment == TextAnchor.UpperCenter)
        {
            return TextAlignmentOptions.Top;
        }

        if (alignment == TextAnchor.UpperLeft)
        {
            return TextAlignmentOptions.TopLeft;
        }

        if (alignment == TextAnchor.UpperRight)
        {
            return TextAlignmentOptions.TopRight;
        }

        return TextAlignmentOptions.Center;
    }

    private TMP_FontAsset GetTextMeshProFont(Font font)
    {
        if (font == null)
        {
            return null;
        }

        foreach (ReplaceFont replaceFont in ReplaceFonts)
        {
            if (replaceFont.OriginalFont == font)
            {
                return replaceFont.TargetFont;
            }
        }

        return null;
    }

//    [MenuItem("Tools/Show Missing Object References in scene", false, 50)]
//    public static void FindMissingReferencesInCurrentScene()
//    {
//        var objects = GetSceneObjects();
//        FindMissingReferences(SceneManager.GetActiveScene().name, objects);
//    }
//
//    private static GameObject[] GetSceneObjects()
//    {
//        return Resources.FindObjectsOfTypeAll<GameObject>()
//                        .Where(go => string.IsNullOrEmpty(AssetDatabase.GetAssetPath(go))
//                                     && go.hideFlags == HideFlags.None).ToArray();
//    }

    private const string ERROR_STRING = "Missing Ref in: [{3}]{0}. Component: {1}, Property: {2}";
    private const string CAUTION_STRING = "We can't update: [{3}]{0}. Component: {1}, Property: {2}";

    private static void ShowError(string context, GameObject go, string c, string property)
    {
        Debug.LogError(string.Format(ERROR_STRING, FullPath(go), c, property, context), go);
    }

    private static string FullPath(GameObject go)
    {
        return go.transform.parent == null
            ? go.name
            : FullPath(go.transform.parent.gameObject) + "/" + go.name;
    }

    public static void FindMissingReferencesInCurrentScene()
    {
        var objects = Object.FindObjectsOfType<GameObject>();
        FindMissingReferences(SceneManager.GetActiveScene().name, objects);
    }

    private static void FindMissingReferences(string context, GameObject[] objects)
    {
        foreach (var go in objects)
        {
            var components = go.GetComponents<Component>();

            foreach (var c in components)
            {
                if (!c)
                {
                    Debug.LogError("Missing Component in GO: " + FullPath(go), go);
                    continue;
                }

                SerializedObject so = new SerializedObject(c);
                var sp = so.GetIterator();

                while (sp.NextVisible(true))
                {
                    if (sp.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        if (sp.objectReferenceValue == null)
                        {
                            object spObj = sp.serializedObject.targetObject;
                            if (spObj != null)
                            {
                                Type type = spObj.GetType();
                                Debug.Log(spObj + " | " + type);
                                if (type == typeof(Text) || type == typeof(InputField) ||
                                    type == typeof(TextMeshProUGUI) || type == typeof(TMP_InputField))
                                {
                                    ShowError(context, go, c.GetType().Name, ObjectNames.NicifyVariableName(sp.name));
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    public static object GetTargetObjectOfProperty(SerializedProperty prop)
    {
        var path = prop.propertyPath.Replace(".Array.data[", "[");
        object obj = prop.serializedObject.targetObject;
        var elements = path.Split('.');
        foreach (var element in elements)
        {
            if (element.Contains("["))
            {
                var elementName = element.Substring(0, element.IndexOf("["));
                var index = System.Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                obj = GetValue_Imp(obj, elementName, index);
            }
            else
            {
                obj = GetValue_Imp(obj, element);
            }
        }
        return obj;
    }

            private static object GetValue_Imp(object source, string name, int index)
        {
            var enumerable = GetValue_Imp(source, name) as System.Collections.IEnumerable;
            if (enumerable == null) return null;
            var enm = enumerable.GetEnumerator();
            //while (index-- >= 0)
            //    enm.MoveNext();
            //return enm.Current;

            for (int i = 0; i <= index; i++)
            {
                if (!enm.MoveNext()) return null;
            }
            return enm.Current;
        }


    private static object GetValue_Imp(object source, string name)
    {
        if (source == null)
            return null;
        var type = source.GetType();

        while (type != null)
        {
            var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (f != null)
                return f.GetValue(source);

            var p = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p != null)
                return p.GetValue(source, null);

            type = type.BaseType;
        }
        return null;
    }

    public static string GetPropertyType(SerializedProperty property)
    {
        var type = property.type;
        var match = Regex.Match(type, @"PPtr<\$(.*?)>");
        if (match.Success)
            type = match.Groups[1].Value;
        return type;
    }

    public static Type GetPropertyObjectType(SerializedProperty property)
    {
        return typeof(UnityEngine.Object).Assembly.GetType("UnityEngine." + GetPropertyType(property));
    }
}

public class TextHolder
{
    public float FontSize;
    public float ResizeTextMaxSize;
    public bool ResizeTextForBestFit;
    public TextAnchor Alignment;
    public Color Color;
    public HorizontalWrapMode HorizontalOverflow;
    public VerticalWrapMode VerticalOverflow;
    public string Text;
    public bool SupportRichText;
    public Font Font;
    public RectTransform Rect;
    public bool RaycastTarget;
    public FontStyle FontStyle;
    public float ResizeTextMinSize;
    public Vector2 SizeDelta;

    public TextHolder(Text uGuiText)
    {
        FontSize = uGuiText.fontSize;
        ResizeTextMaxSize = uGuiText.resizeTextMaxSize;
        ResizeTextForBestFit = uGuiText.resizeTextForBestFit;
        Alignment = uGuiText.alignment;
        Color = uGuiText.color;
        HorizontalOverflow = uGuiText.horizontalOverflow;
        VerticalOverflow = uGuiText.verticalOverflow;
        Text = uGuiText.text;
        SupportRichText = uGuiText.supportRichText;
        Font = uGuiText.font;
        Rect = uGuiText.rectTransform;
        RaycastTarget = uGuiText.raycastTarget;
        FontStyle = uGuiText.fontStyle;
        ResizeTextMinSize = uGuiText.resizeTextMinSize;
        SizeDelta = uGuiText.rectTransform.sizeDelta;
    }
}

public class ReplaceFont
{
    public Font OriginalFont;
    public TMP_FontAsset TargetFont;
}