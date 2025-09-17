/*
<copyright file="BGExcelImportGoEditor.cs" company="BansheeGz">
    Copyright (c) 2019-2021 All Rights Reserved
</copyright>
*/

using System;
using BansheeGz.BGDatabase;
using UnityEditor;

namespace BansheeGz.BGDatabase.Editor
{
    [CustomEditor(typeof(BGExcelImportGo))]
    public class BGExcelImportGoEditor : UnityEditor.Editor
    {
        private BGExcelImportGo importer;

        protected virtual void OnEnable()
        {
            importer = (BGExcelImportGo) this.target;
        }


        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            //merge settings
            BGEditorUtility.Horizontal(() =>
            {
                if (BGEditorUtility.Button("Export settings")) BGExcelMergeSettingsWindow.Open(importer.ExportSettings, serializedObject, "ExportSettingsAsString");
                if (BGEditorUtility.Button("Import settings")) BGExcelMergeSettingsWindow.Open(importer.ImportSettings, serializedObject, "ImportSettingsAsString");
            });
            
            //names map config
            BGEditorUtility.Horizontal(() =>
            {
                EditorGUILayout.PrefixLabel("Names map config");
                if (!BGEditorUtility.Button("Edit")) return;
                
                var nameMapEditor = new BGSyncNameMapConfigEditor(false);
                nameMapEditor.OnChange += () =>
                {
                    Change("Name Map Config Changed", () => 
                        serializedObject.FindProperty("NameMapConfigAsString").stringValue = Convert.ToBase64String(importer.NameMapConfig.ConfigToBytes()));
                };
                var scrollView = new BGScrollView.DefaultScrollView(() =>
                {
                    nameMapEditor.Gui(importer.NameMapConfig, importer.NameMapConfigEnabled, b =>
                    {
                        Change("Name Map Config Enabled", () => serializedObject.FindProperty("NameMapConfigEnabled").boolValue = b);
                    });
                }, false, false);
                BGPopup.Popup("Names map config editor", 800, 600, popup =>
                {
                    scrollView.Gui();
                });
            });
            
            //rows mapping settings
            BGEditorUtility.Horizontal(() =>
            {
                EditorGUILayout.PrefixLabel("Rows mapping config");
                if (!BGEditorUtility.Button("Edit")) return;
                
                var idConfigEditor = new BGSyncIdConfigEditor(false);
                idConfigEditor.OnChange += () =>
                {
                    Change("Rows Map Config Changed", () => 
                        serializedObject.FindProperty("RowsMappingConfigAsString").stringValue = Convert.ToBase64String(importer.RowsMappingConfig.ConfigToBytes()));
                };
                var scrollView = new BGScrollView.DefaultScrollView(() =>
                {
                    idConfigEditor.Gui(importer.RowsMappingConfig, importer.RowsMappingConfigEnabled, b =>
                    {
                        Change("Rows Map Config Enabled", () => serializedObject.FindProperty("RowsMappingConfigEnabled").boolValue = b);
                    });
                }, false, false);
                BGPopup.Popup("Rows map config editor", 800, 600, popup =>
                {
                    scrollView.Gui();
                });
            });
            //relations settings
            BGEditorUtility.Horizontal(() =>
            {
                EditorGUILayout.PrefixLabel("References config");
                if (!BGEditorUtility.Button("Edit")) return;
                
                var relationsEditor = new BGSyncRelationsConfigEditor(false);
                relationsEditor.OnChange += () =>
                {
                    Change("References Config Changed", () => 
                        serializedObject.FindProperty("RelationsConfigAsString").stringValue = Convert.ToBase64String(importer.RelationsConfig.ConfigToBytes()));
                };
                var scrollView = new BGScrollView.DefaultScrollView(() =>
                {
                    relationsEditor.Gui(importer.RelationsConfig, importer.RowsMappingConfig, importer.RelationsConfigEnabled, b =>
                    {
                        Change("References Config Enabled", () => serializedObject.FindProperty("RelationsConfigEnabled").boolValue = b);
                    });
                }, false, false);
                BGPopup.Popup("References config editor", 800, 600, popup =>
                {
                    scrollView.Gui();
                });
            });
        }

        private void Change(string message, Action callback)
        {
            Undo.RecordObject(serializedObject.targetObject, message);
            callback();
            serializedObject.ApplyModifiedProperties();
        }
    }
}