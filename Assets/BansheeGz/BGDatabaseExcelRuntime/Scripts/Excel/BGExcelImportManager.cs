/*
<copyright file="BGExcelImportManager.cs" company="BansheeGz">
    Copyright (c) 2019-2021 All Rights Reserved
</copyright>
*/

using System;
using System.IO;

namespace BansheeGz.BGDatabase
{
    public class BGExcelImportManager
    {
        private BGLogger Logger;
        private BGMergeSettingsEntity EntitySettings;


        private BGMergeSettingsEntity NewEntitySettings => new BGMergeSettingsEntity { Mode = BGMergeModeEnum.Merge, UpdateMatching = true };

        public BGExcelImportManager()
        {
        }

        public BGExcelImportManager(bool ignoreErrors)
        {
            //ignoreerrors is not yet supported
        }


        public BGLogger Import(string path, BGMergeSettingsEntity settings, BGSyncNameMapConfig NameMapConfig, BGSyncIdConfig idConfig, BGSyncRelationsConfig relationsConfig, bool printWarnings)
        {
            if (!File.Exists(path)) throw new Exception("File does not exists: " + path);

            BGExcelReaderRT reader = null;
            Logger = new BGLogger();
            EntitySettings = settings ?? NewEntitySettings;
            ReadFile(Logger, path,
                content => reader = new BGExcelReaderRT(Logger, content, IsUsingXml(path), NameMapConfig,
                    idConfig == null ? null : new BGExcelIdResolverFactoryRT(BGRepo.I, idConfig),
                    new BGSyncRelationsResolver(relationsConfig, idConfig, BGRepo.I),
                    printWarnings
                ));

            Import(repo => reader.ReadEntities(repo, false), repo => reader.Info);
            return Logger;
        }

        private void Import(Action<BGRepo> readEntity, Func<BGRepo, BGBookInfo> readerInfo)
        {
            var repo = EntitySettings.NewRepo(BGRepo.I, false);

            //---- entities
            Logger.Section("Reading entities", () => readEntity(repo));

            //merge
            //we should exclude non existing metas and fields from settings
            var settingsClone = (BGMergeSettingsEntity)EntitySettings.Clone();
            settingsClone.RemoveNotExistent(repo, readerInfo(repo));
            new BGMergerEntity(Logger, repo, BGRepo.I, settingsClone).Merge();
        }

        public static void ReadFile(BGLogger logger, string path, Action<byte[]> action)
        {
            logger.AppendLine("Trying to read file at ($)..", path);
//            var content = File.ReadAllBytes(path);
            byte[] content;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                content = new byte[stream.Length];
                stream.Read(content, 0, content.Length);
            }

            if (logger.AppendLine(content.Length == 0, "Content of file is empty")) return;
            logger.AppendLine("File is read successfully. ($) bytes", content.Length);
            action(content);
        }

        public static bool IsUsingXml(string path)
        {
            return path != null && path.Trim().EndsWith(".xlsx");
        }
    }
}