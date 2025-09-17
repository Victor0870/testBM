/*
<copyright file="BGExcelMergeSettingsWindow.cs" company="BansheeGz">
    Copyright (c) 2019-2021 All Rights Reserved
</copyright>
*/

using System;
using UnityEditor;
using UnityEngine;

namespace BansheeGz.BGDatabase.Editor
{
    public class BGExcelMergeSettingsWindow : EditorWindow
    {
        private BGMergeSettingsEntity settings;
        private SerializedObject serializedObject;
        private string propertyName;
        private BGMergeSettingsEntityEditor settingsEditor;
        private BGScrollView.DefaultScrollView scrollView;

        public static void Open(BGMergeSettingsEntity settings, SerializedObject serializedObject, string propertyName)
        {
            //make sure db is loaded
            var db = BGRepo.I;
            
            var window = EditorWindow.GetWindow(typeof(BGExcelMergeSettingsWindow), true, "Table Settings", true) as BGExcelMergeSettingsWindow;

            window.settings = settings;
            window.serializedObject = serializedObject;
            window.propertyName = propertyName;
            window.settingsEditor = new BGMergeSettingsEntityEditor(settings);

            window.settings.OnChange += window.Save;

            var vector2 = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            window.ShowAsDropDown(new Rect(vector2.x, vector2.y, 2f, 2f), new Vector2(1000, 500));
        }

        private void OnDisable()
        {
            if (settings != null) settings.OnChange -= Save;
        }

        private void Save()
        {
            if (serializedObject == null || serializedObject.targetObject == null) return;
            Undo.RecordObject(serializedObject.targetObject, "Changing config");
            var array = settings.ConfigToBytes();
            var encodedString = array == null ? null : Convert.ToBase64String(array);
            var property = serializedObject.FindProperty(propertyName);
            property.stringValue = encodedString;
            serializedObject.ApplyModifiedProperties();
        }

        private void OnGUI()
        {
            if (settingsEditor == null)
            {
                BGEditorUtility.Label("Please, reopen this window cause context is lost");
                return;
            }

            if (scrollView == null) scrollView = new BGScrollView.DefaultScrollView(settingsEditor.Gui);
            scrollView.Gui();
        }
    }
}