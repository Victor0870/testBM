/*
<copyright file="BGExcelExportManager.cs" company="BansheeGz">
    Copyright (c) 2019-2021 All Rights Reserved
</copyright>
*/

using System;
using System.IO;
using NPOI.SS.UserModel;

namespace BansheeGz.BGDatabase
{
    public class BGExcelExportManager
    {
        private BGLogger Logger;

        public BGExcelExportManager()
        {
        }

        public BGExcelExportManager(bool ignoreErrors)
        {
            //ignoreerrors is not yet supported
        }
        
        public BGLogger Export(string path, bool exportMetaOnlyIfSheetExists, BGMergeSettingsEntity settings, BGSyncNameMapConfig NameMapConfig, BGSyncIdConfig idConfig, 
            BGSyncRelationsConfig relationsConfig, bool printWarnings)
        {
            Logger = new BGLogger();
            ExportTo(path, exportMetaOnlyIfSheetExists, settings ?? new BGMergeSettingsEntity {Mode = BGMergeModeEnum.Merge, UpdateMatching = true, AddMissing = true}, NameMapConfig, idConfig, 
                relationsConfig, printWarnings);
            return Logger;
        }

        private void ExportTo(string file, bool exportMetaOnlyIfSheetExists, BGMergeSettingsEntity settings, BGSyncNameMapConfig NameMapConfig, BGSyncIdConfig idConfig, 
            BGSyncRelationsConfig relationsConfig, bool printWarnings)
        {
            var repo = settings.NewRepo(BGRepo.I, false);
            BGExcelReaderRT reader = null;
            if (File.Exists(file))
            {
                Logger.Section("Export: Reading repo", () =>
                {
                    reader = new BGExcelReaderRT(Logger, File.ReadAllBytes(file), BGExcelImportManager.IsUsingXml(file), NameMapConfig, 
                        idConfig == null ? null : new BGExcelIdResolverFactoryRT(BGRepo.I, idConfig), new BGSyncRelationsResolver(relationsConfig, idConfig, BGRepo.I){Repo = repo}, printWarnings);
                    reader.ReadEntities(repo, true);
                    if (exportMetaOnlyIfSheetExists)
                    {
                        settings.AddMissing = false;
                        settings.UpdateMatching = false;
                        repo.ForEachMeta(meta =>
                        {
                            if (!reader.Info.HasEntitySheet(meta.Id)) return;
                            var metaSettings = settings.Ensure(meta.Id);
                            metaSettings.AddMissing = metaSettings.UpdateMatching = true;
                        });
                        if (repo.CountMeta == 0)
                        {
                            throw new Exception("You chose to export only if sheet for meta exists in xls file, " +
                                                "but no sheet for existing meta was found in the file- so no meta to export");
                        }
                    }
                });
            }
            else if (exportMetaOnlyIfSheetExists) throw new Exception("You chose to export only if sheet for meta exists in xls file, but xls file does not exist- so no meta to export");

            new BGMergerEntity(Logger, BGRepo.I, repo, settings).Merge();

            var resolver = new BGSyncRelationsResolver(relationsConfig, idConfig, BGRepo.I) { Repo = repo };
            WriteWorkbook(file, reader == null
                ? new BGExcelWriterRT(Logger, BGRepo.I, repo,  BGExcelImportManager.IsUsingXml(file), false, NameMapConfig, idConfig, resolver, printWarnings).Book
                : new BGExcelWriterRT(Logger, BGRepo.I, repo, settings, reader.Book, reader.Info, false, NameMapConfig, idConfig, resolver, printWarnings).Book);
        }
        
        public static void WriteWorkbook(string path, IWorkbook newBook)
        {
            using (var stream = new MemoryStream())
            {
                newBook.Write(stream);
                File.WriteAllBytes(path, stream.ToArray());
            }
        }
    }
}