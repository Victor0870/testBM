/*
<copyright file="BGExcelImportGo.cs" company="BansheeGz">
    Copyright (c) 2019-2021 All Rights Reserved
</copyright>
*/

using System;
using System.IO;
using System.Threading;
using NPOI.SS.UserModel;
using UnityEngine;
using UnityEngine.Events;

namespace BansheeGz.BGDatabase
{
    /// <summary>
    /// This class adds runtime support for importing/exporting data from/to Excel file 
    /// </summary>
    [BGPlugin(Version = "1.4")]
    public class BGExcelImportGo : MonoBehaviour
    {
        //================================ serializable
        [Tooltip("If path is rooted, absolute path is used, if path is relative, StreamingAssets folder is used as parent folder")]
        public string ExcelFile = "c:/excel.xls";

        [Tooltip("Coordinates for GUI position")]
        public Vector2 UICoordinates = new Vector2(10, 10);

        [Tooltip("If set to true, data will be imported in Start method")]
        public bool ImportOnStart;

        [Tooltip("If set to true, target xls file will be monitored and it it's changed, data will be imported")]
        public bool MonitorFile;

        [Tooltip("If set to true, export will ignore all tables which does not have corresponding sheet in target xls file")]
        public bool ExportMetaOnlyIfSheetExists;

        [Tooltip("If set to true, custom GUI will not be shown and you will need to call Import/Export methods from your own scripts in case you want to manually trigger import/export function")]
        public bool DisableGUI;

        [HideInInspector]
        [Tooltip("If set to true, no exceptions will be throws in case of any error with individual rows occur while importing. The troubled rows will be skipped. Warnings will be added to the log")]
        public bool IgnoreImportRowsErrors;

        [HideInInspector]
        [Tooltip("If set to true, no exceptions will be throws in case of any error with individual rows occur while exporting. The troubled rows will be skipped. Warnings will be added to the log")]
        public bool IgnoreExportRowsErrors;

        [HideInInspector] public string ImportSettingsAsString;
        [HideInInspector] public string ExportSettingsAsString;

        [HideInInspector] public bool NameMapConfigEnabled;
        [HideInInspector] public string NameMapConfigAsString;
        [HideInInspector] public bool RowsMappingConfigEnabled;
        [HideInInspector] public string RowsMappingConfigAsString;
        [HideInInspector] public bool RelationsConfigEnabled;
        [HideInInspector] public string RelationsConfigAsString;
        
        [Tooltip("If set to true, all warnings are printed directly to Unity console")]
        public bool PrintWarnings;

        [Tooltip("This event is fired after export")]
        public UnityEvent OnExportUnityEvent;
        [Tooltip("This event is fired after import")]
        public UnityEvent OnImportUnityEvent;
        
        //================================  not serializable
        public event Action OnExport;
        public event Action OnImport;
        public Action OnExcelFileChangedCustomAction;
        
        private BGSyncNameMapConfig nameMapConfig;
        private bool nameMapConfigIsInited;

        private BGSyncIdConfig rowsMappingConfig;
        private bool rowsMappingConfigIsInited;

        private BGSyncRelationsConfig relationsConfig;
        private bool relationsConfigIsInited;

        [NonSerialized] private BGLogger logger;
        private bool expanded;
        private string error;
        private long lastRun;
        private long monitoredTime;
        private readonly ThreadMonitor importMonitor = new ThreadMonitor();
        private string lastLog;
        private int warnings;
        private Vector2 scrollPosition;
        private readonly object fileNameLock = new object();
        private FileSystemWatcher fileSystemWatcher;
        private Texture2D blackTexture;
        private Texture2D errorBackground;
        private Texture2D logBackground;

        [NonSerialized] private BGMergeSettingsEntity importSettings;
        [NonSerialized] private BGMergeSettingsEntity exportSettings;

        //==================================================================================================
        //                                Properties
        //==================================================================================================
        public string Error => error;

        public int Warnings => warnings;

        public BGLogger Logger => logger;


        private FileInfo SettingsFile => new FileInfo(Path.Combine(Application.persistentDataPath, "excel_settings.json"));

        public string ExcelFileSynced
        {
            get
            {
                lock (fileNameLock) return ExcelFile;
            }
            set
            {
                lock (fileNameLock) ExcelFile = value;
            }
        }

        private string ExcelFilePath
        {
            get
            {
                var path = ExcelFileSynced;
                if (string.IsNullOrEmpty(path)) return "";
                if (!Path.IsPathRooted(path)) path = Path.Combine(Application.streamingAssetsPath, path);
                return path;
            }
        }

        private Texture2D ErrorBackground
        {
            get
            {
                if (errorBackground != null) return errorBackground;
                return (errorBackground = Texture1x1(new Color(1, .3f, .3f, .4f)));
            }
        }

        private Texture2D BlackBackground
        {
            get
            {
                if (blackTexture != null) return blackTexture;
                return (blackTexture = Texture1x1(new Color(0, 0, 0, .6f)));
            }
        }

        private Texture2D LogBackground
        {
            get
            {
                if (logBackground != null) return logBackground;
                return (logBackground = Texture1x1(new Color(0.9f, 0.9f, 0.9f, .5f)));
            }
        }

        public BGMergeSettingsEntity ImportSettings => importSettings = InitSettings(importSettings, ImportSettingsAsString, () => new BGMergeSettingsEntity {Mode = BGMergeModeEnum.Merge, UpdateMatching = true});

        public BGMergeSettingsEntity ExportSettings => exportSettings = InitSettings(exportSettings, ExportSettingsAsString, () => new BGMergeSettingsEntity {Mode = BGMergeModeEnum.Merge, UpdateMatching = true, AddMissing = true});

        public BGSyncNameMapConfig NameMapConfig
        {
            get
            {
                if (nameMapConfig == null) nameMapConfig = new BGSyncNameMapConfig();
                if (!nameMapConfigIsInited)
                {
                    nameMapConfigIsInited = true;
                    if (!string.IsNullOrEmpty(NameMapConfigAsString)) nameMapConfig.ConfigFromBytes(new ArraySegment<byte>(Convert.FromBase64String(NameMapConfigAsString)));
                }

                return nameMapConfig;
            }
        }
        
        public BGSyncIdConfig RowsMappingConfig
        {
            get
            {
                if (rowsMappingConfig == null) rowsMappingConfig = new BGSyncIdConfig();
                if (!rowsMappingConfigIsInited)
                {
                    rowsMappingConfigIsInited = true;
                    if (!string.IsNullOrEmpty(RowsMappingConfigAsString)) rowsMappingConfig.ConfigFromBytes(new ArraySegment<byte>(Convert.FromBase64String(RowsMappingConfigAsString)));
                }
                return rowsMappingConfig;
            }
        }

        public BGSyncRelationsConfig RelationsConfig
        {
            get
            {
                if (relationsConfig == null) relationsConfig = new BGSyncRelationsConfig();
                if (!relationsConfigIsInited)
                {
                    relationsConfigIsInited = true;
                    if (!string.IsNullOrEmpty(RelationsConfigAsString)) relationsConfig.ConfigFromBytes(new ArraySegment<byte>(Convert.FromBase64String(RelationsConfigAsString)));
                }
                return relationsConfig;
            }
        }

        //==================================================================================================
        //                                Unity callbacks
        //==================================================================================================
        void Start()
        {
            monitoredTime = DateTime.Now.Ticks;
            LoadSetting();
            if (ImportOnStart) Import();
            CheckMonitor();
        }

        private void Update()
        {
            if (!importMonitor.ActionRequested) return;
            importMonitor.ActionRequested = false;
            if (OnExcelFileChangedCustomAction != null) OnExcelFileChangedCustomAction();
            else Import();
        }

        private void OnDestroy()
        {
            if (fileSystemWatcher != null)
            {
                fileSystemWatcher.Changed -= OnFileChanged; 
                fileSystemWatcher.EnableRaisingEvents = false;
            }
        }

        private void OnGUI()
        {
            if (DisableGUI) return;
            if (GUI.Button(new Rect(UICoordinates, new Vector2(80, 19)), expanded ? "Excel <<" : "Excel >>")) expanded = !expanded;
            if (!expanded) return;


            //-------------- expanded
            var fileExist = File.Exists(ExcelFilePath);
            Area(new Rect(UICoordinates + new Vector2(0, 19), new Vector2(500, 440)), () =>
            {
                Form("File", () =>
                {
                    var oldPath = ExcelFileSynced;
                    var newValue = GUILayout.TextField(oldPath);
                    if (!string.Equals(newValue, oldPath)) ExcelFileSynced = newValue;
                });
                Form("Import OnStart", () => BoolField(ref ImportOnStart));
                Form("Monitor file", () => BoolField(ref MonitorFile, CheckMonitor));
                Form("Export meta only if sheet exists", () => BoolField(ref ExportMetaOnlyIfSheetExists));
                Form("File exist? ", () => Choice(fileExist, "Yes", "No", Color.green, Color.red));
                Form("File modified", () => GUILayout.Label(lastRun == 0 ? "N/A" : new DateTime(lastRun).ToString("MM/dd/yyyy HH:mm:ss")));
                Form("Last run since game started", () => GUILayout.Label(lastRun == 0 ? "N/A" : new DateTime(lastRun).ToString("MM/dd/yyyy HH:mm:ss")));
                Form("Settings file", () => GUILayout.Label(File.Exists(SettingsFile.FullName) ? "exists" : "not exists"));
                Form("Error during run", () =>
                {
                    if (error == null) GUILayout.Label("N/A");
                    else GUILayout.Label(error, new GUIStyle {normal = {textColor = Color.white, background = ErrorBackground}, wordWrap = true}, GUILayout.Height(21 * 3));
                });
                Horizontal(() =>
                {
                    Choice(warnings == 0, "Warnings", "Warnings", Color.white, Color.red);
                    Choice(warnings == 0, "0", "" + warnings, Color.green, Color.red);
                });
                Horizontal(() =>
                {
                    if (GUILayout.Button("Save settings")) SaveSetting();
                    if (GUILayout.Button("Delete settings")) DeleteSetting();
                    if (GUILayout.Button("Export now")) Export();
                    if (GUILayout.Button("Import now")) Import();
                });
                scrollPosition = GUILayout.BeginScrollView(scrollPosition);
                GUILayout.TextArea(lastLog, new GUIStyle {richText = true, normal = {textColor = Color.black, background = LogBackground}});
                GUILayout.EndScrollView();
            });
        }

        //==================================================================================================
        //                                Methods
        //==================================================================================================

        private void CheckMonitor()
        {
            if (fileSystemWatcher != null)
            {
                fileSystemWatcher.Changed -= OnFileChanged;
                fileSystemWatcher.EnableRaisingEvents = false;
            }

            if (!MonitorFile) return;
            var excelFilePath = ExcelFilePath;
            var directoryName = Path.GetDirectoryName(excelFilePath);
            var fileName = Path.GetFileName(excelFilePath);
            fileSystemWatcher = new FileSystemWatcher(directoryName) {Filter = fileName};
            fileSystemWatcher.Changed += OnFileChanged;
            fileSystemWatcher.EnableRaisingEvents = true;
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            var excelFilePath = e.FullPath;
            if (!File.Exists(excelFilePath)) return;
            var lastWritten = File.GetLastWriteTime(excelFilePath).Ticks;
            if (monitoredTime >= lastWritten) return;
            monitoredTime = lastWritten;
            importMonitor.ActionRequested = true;
        }

        private void LoadSetting()
        {
            var file = SettingsFile;
            if (!file.Exists) return;
            var json = JsonUtility.FromJson<JsonSettings>(File.ReadAllText(file.FullName));
            ExcelFileSynced = json.file;
            ImportOnStart = json.autoRun;
            MonitorFile = json.monitorFile;
            ExportMetaOnlyIfSheetExists = json.exportMetaOnlyIfSheetExists;
        }

        private void SaveSetting()
        {
            File.WriteAllText(SettingsFile.FullName, JsonUtility.ToJson(new JsonSettings
            {
                file = ExcelFileSynced,
                monitorFile = MonitorFile,
                autoRun = ImportOnStart,
                exportMetaOnlyIfSheetExists = ExportMetaOnlyIfSheetExists,
            }));
        }

        private void DeleteSetting()
        {
            var file = SettingsFile;
            if (!file.Exists) return;
            File.Delete(file.FullName);
        }

        public void Export()
        {
            lastRun = DateTime.Now.Ticks;
            error = null;
            warnings = 0;
            lastLog = null;

            try
            {
                logger = new BGExcelExportManager().Export(ExcelFilePath, ExportMetaOnlyIfSheetExists, ExportSettings, NameMapConfigEnabled ? NameMapConfig : null, 
                    RowsMappingConfigEnabled ? RowsMappingConfig : null, RelationsConfigEnabled ? RelationsConfig : null, PrintWarnings);
                warnings = logger.Warnings;
                lastLog = logger.Log;
                OnExportUnityEvent?.Invoke();
                OnExport?.Invoke();
            }
            catch (Exception e)
            {
                logger = new BGLogger();
                logger.AppendWarning(e.Message);
                Debug.LogException(e);
                error = e.Message;
                lastLog = e.ToString();
            }
        }

        public void Import()
        {
            lastRun = DateTime.Now.Ticks;
            error = null;
            warnings = 0;
            lastLog = null;

            try
            {
                logger = new BGExcelImportManager().Import(ExcelFilePath, ImportSettings, NameMapConfigEnabled ? NameMapConfig : null, RowsMappingConfigEnabled ? RowsMappingConfig : null,
                    RelationsConfigEnabled ? RelationsConfig : null, PrintWarnings);
                warnings = logger.Warnings;
                lastLog = logger.Log;
                var binders = FindObjectsOfType<BGDataBinderGoA>();
                if (binders != null)
                {
                    foreach (var binder in binders) binder.Bind();
                }
                OnImportUnityEvent?.Invoke();
                OnImport?.Invoke();
            }
            catch (Exception e)
            {
                logger = new BGLogger();
                logger.AppendWarning(e.Message);
                Debug.LogException(e);
                error = e.Message;
                lastLog = e.ToString();
            }
        }

        private void Horizontal(Action action)
        {
            GUILayout.BeginHorizontal();
            action();
            GUILayout.EndHorizontal();
        }

        private void Area(Rect rect, Action action)
        {
            GUI.DrawTexture(rect, BlackBackground);
            GUILayout.BeginArea(rect);
            action();
            GUILayout.EndArea();
        }

        private void Form(string label, Action action)
        {
            Horizontal(() =>
            {
                GUILayout.Label(label, GUILayout.Width(200));
                action();
            });
        }

        private void BoolField(ref bool value, Action action = null)
        {
            var newValue = GUILayout.Toggle(value, "");
            if (newValue == value) return;

            value = newValue;
            action?.Invoke();
        }

        private void Choice(bool condition, string option1, string option2, Color color1, Color color2)
        {
            if (condition) GUILayout.Label(option1, new GUIStyle {normal = {textColor = color1}});
            else GUILayout.Label(option2, new GUIStyle {normal = {textColor = color2}});
        }

        private static BGMergeSettingsEntity InitSettings(BGMergeSettingsEntity settings, string config, Func<BGMergeSettingsEntity> defaultProvider)
        {
            if (settings != null) return settings;
            BGMergeSettingsEntity result;
            if (string.IsNullOrEmpty(config)) result = defaultProvider();
            else
            {
                result = new BGMergeSettingsEntity();
                var configArray = Convert.FromBase64String(config);
                result.ConfigFromBytes(new ArraySegment<byte>(configArray));
            }

            return result;
        }


        private static Texture2D Texture1x1(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }


        [Serializable]
        private class JsonSettings
        {
            public string file;
            public bool autoRun;
            public bool monitorFile;
            public bool exportMetaOnlyIfSheetExists;
        }

        private class ThreadMonitor
        {
            private int actionRequested;

            public bool ActionRequested
            {
                get => Interlocked.CompareExchange(ref actionRequested, 1, 1) == 1;
                set
                {
                    if (value) Interlocked.CompareExchange(ref actionRequested, 1, 0);
                    else Interlocked.CompareExchange(ref actionRequested, 0, 1);
                }
            }
        }
    }

    public interface BGExcelCellWriteProcessorRT
    {
        void OnWrite(ICell cell, BGField field, BGEntity entity);
    }
    public interface BGExcelCellReadProcessorRT
    {
        void OnRead(ICell cell, BGField field, BGEntity entity);
    }
}