/*
<copyright file="BGExcelSheetReaderEntityRT.cs" company="BansheeGz">
    Copyright (c) 2019-2021 All Rights Reserved
</copyright>
*/

using System;
using System.Collections.Generic;
using NPOI.SS.UserModel;
using UnityEngine;

namespace BansheeGz.BGDatabase
{
    public class BGExcelSheetReaderEntityRT : BGExcelSheetReaderART
    {
        //================================================================================================
        //                                              Static
        //================================================================================================
        public static void ReadEntities(IWorkbook book, BGBookInfo info, BGRepo repo, BGLogger logger, bool ignoreNew, BGSyncNameMapConfig nameMapConfig, BGExcelIdResolverFactoryRT IdResolverFactory,
            BGSyncRelationsResolver relationsResolver, bool printWarnings)
        {
            logger.AppendLine("Reading entities: iterating sheets..");
            var readers = new List<BGExcelSheetReaderEntityRT>();
            for (var i = 0; i < book.NumberOfSheets; i++)
            {
                var sheet = book.GetSheetAt(i);

                if (BGSyncUtil.AppendWarning(logger, printWarnings, string.IsNullOrEmpty(sheet.SheetName), "Sheet with empty name at index $", i)) continue;


                logger.SubSection(() =>
                {
                    var meta = nameMapConfig == null ? repo[sheet.SheetName] : nameMapConfig.Map(repo, sheet.SheetName);
                    if (BGSyncUtil.AppendWarning(logger, printWarnings,meta == null, "Sheet [$] is skipped. No meta with such name found or no proper mapping.", sheet.SheetName)) return;
                    if (BGSyncUtil.AppendWarning(logger, printWarnings,info.HasEntitySheet(meta.Id), "Sheet [$] is skipped. Duplicate name, meta [$] was already been processed.", sheet.SheetName, meta.Name)) return;

                    BGExcelSheetReaderEntityRT reader;
                    if (sheet.PhysicalNumberOfRows == 0)
                    {
                        logger.AppendLine("Sheet [$] is mapped ok, but no rows found.", sheet.SheetName);
                        reader = new BGExcelSheetReaderEntityRT(i, meta, ignoreNew, null, logger, sheet.SheetName, null, null, printWarnings);
                    }
                    else
                    {
                        logger.AppendLine("Sheet [$] is mapped ok to [$] meta. $ rows found.", sheet.SheetName, meta.Name, sheet.LastRowNum + 1);
                        var headersRow = sheet.GetRow(0);
                        reader = new BGExcelSheetReaderEntityRT(i, meta, ignoreNew, headersRow, logger, sheet.SheetName, nameMapConfig,
                            IdResolverFactory == null
                                ? new BGExcelIdResolverIdRT(logger)
                                : IdResolverFactory.Create(meta.Id, logger, printWarnings),
                            printWarnings
                        );
                    }

                    readers.Add(reader);
                    info.AddEntitySheet(meta.Id, reader.Info);

                    if (BGSyncUtil.AppendWarning(logger, printWarnings, !reader.Info.HasAnyData, "No columns found for Sheet [$].", sheet.SheetName)) return;

                    logger.AppendLine("Read $ rows. $ existing entities. $ new entities. $ rows are skipped.",
                        reader.RowsCount, reader.RowsExistingCount, reader.RowsNewCount, reader.RowsCount - reader.RowsExistingCount - reader.RowsNewCount);
                }, "Reading sheet $", sheet.SheetName);
            }

            logger.SubSection(() =>
            {
                foreach (var reader in readers) reader.FlushData();
                if (relationsResolver != null) relationsResolver.Repo = repo;
                foreach (var reader in readers) reader.FlushRelations(relationsResolver);
            }, "Flushing data to database");
        }

        //================================================================================================
        //                                              Fields
        //================================================================================================

        private readonly BGMetaEntity meta;
        private readonly BGEntitySheetInfo info;
        private readonly BGLogger logger;
        private readonly bool ignoreNew;
        private readonly BGExcelIdResolverART idResolver;
        private readonly bool printWarnings;

        private readonly BGEntitySheetDataInfo data;
        public int RowsCount;
        public int RowsNewCount;
        public int RowsExistingCount;

        public BGEntitySheetInfo Info => info;

        //================================================================================================
        //                                              Constructors
        //================================================================================================

        public BGExcelSheetReaderEntityRT(int sheetNumber, BGMetaEntity meta, bool ignoreNew, IRow headersRow, BGLogger logger, string sheetName, BGSyncNameMapConfig nameMapConfig,
            BGExcelIdResolverART idResolver, bool printWarnings)
        {
            this.meta = meta;
            this.ignoreNew = ignoreNew;
            info = new BGEntitySheetInfo(meta.Id, meta.Name, sheetNumber) { SheetName = sheetName ?? meta.Name };
            this.idResolver = idResolver ?? new BGExcelIdResolverIdRT(logger);
            this.logger = logger;
            this.printWarnings = printWarnings;

            if (headersRow == null) return;

            logger.SubSection(() =>
            {
                info.PhysicalColumnCount = headersRow.Cells.Count;

                ForEachCell(headersRow, (i, cell) =>
                {
                    string name;
                    if (cell.CellType == CellType.Formula)
                    {
                        if (BGSyncUtil.AppendWarning(logger, printWarnings,cell.CachedFormulaResultType != CellType.String, "[$]->[error:header is formula, but formula type is not a string (type=$)],",
                            i, cell.CachedFormulaResultType.ToString())) return;
                        name = cell.StringCellValue;
                    }
                    else
                    {
                        if (BGSyncUtil.AppendWarning(logger, printWarnings,cell.CellType != CellType.String, "[$]->[error:not a string and not a formula],", i)) return;
                        name = cell.StringCellValue;
                    }

                    var index = cell.ColumnIndex;

                    if (BGSyncUtil.AppendWarning(logger, printWarnings,string.IsNullOrEmpty(name), "[$]->[error:empty string],", i)) return;

                    switch (name)
                    {
                        case BGBookInfo.IdHeader:
                            //id
                            logger.AppendLine("[column #$ $]->[_id],", i, BGBookInfo.IdHeader);
                            info.IndexId = index;
                            break;
                        default:

                            var field = nameMapConfig == null ? meta.GetField(name, false) : nameMapConfig.Map(meta, name);

                            if (BGSyncUtil.AppendWarning(logger, printWarnings,field == null, "[column #$ $]->[warning: no field with such name or no proper mapping- skipping,", i, name)) return;

                            logger.AppendLine("[column #$ $]->[$],", i, name, field.Name);
                            info.AddField(field.Id, index);
                            break;
                    }
                });
            }, "Mapping for [$]", meta.Name);

            //did not manage to map a single field
            if (!Info.HasAnyData) return;

            //read data
            var columns = Info.GetFieldsInfo(meta);
            data = new BGEntitySheetDataInfo(BGEntitySheetInfo.GetFieldsArray(columns));
            ForEachRowNoHeader(headersRow.Sheet, row =>
            {
                if (row == null) return;
                if (row.RowNum == 0) return;

                RowsCount++;

                //----------- id
                BGId entityId;
                try
                {
                    entityId = idResolver.ResolveId(this, info, row);
                }
                catch (Exception e)
                {
                    BGSyncUtil.AppendWarning(logger, printWarnings,"Exception while trying to fetch entity's id, row number=$. Error=$", row.RowNum, e.Message);
                    throw new ExitException();
                }

                if (!entityId.IsEmpty)
                {
                    //entity id is found
                    if (info.HasRow(entityId))
                    {
                        //duplicate entity
                        BGSyncUtil.AppendWarning(logger, printWarnings,"Duplicate entity found. id=$", entityId);
                        throw new ExitException();
                    }

                    info.AddRow(entityId, row.RowNum);
                }
                else
                {
                    //entity id is not set- we assume it's a new row
                    if (ignoreNew) return;

                    // do not remove It! this branch is executed if field ID cell value is empty- we need to ignore such rows! 
                    if (idResolver is BGExcelIdFieldResolverIRT) return;

                    // we need to find at least one non empty cell- otherwise ignore it
                    if (IsRowEmpty(row)) return;
                }

                //counters
                if (entityId.IsEmpty) RowsNewCount++;
                else RowsExistingCount++;

                //------ fields
                var rowsData = new string[columns.Count];
                for (var i = 0; i < columns.Count; i++)
                {
                    var (field, column) = columns[i];
                    rowsData[i] = ReadAsString(row.GetCell(column));
                }

                data.AddRow(new BGEntitySheetDataInfo.RowData(entityId, rowsData, row));
            });
        }

        private void FlushData()
        {
            if (data == null) return;

            //rows
            for (var i = 0; i < data.RowsCount; i++)
            {
                var rowsData = data.GetRow(i);
                var entity = EnsureEntity((IRow)rowsData.ExtraData, rowsData);

                //fields
                for (var j = 0; j < data.FieldsCount; j++)
                {
                    var field = data.GetField(j);
                    if (field is BGAbstractRelationI) continue;
                    var value = rowsData.GetValue(j);
                    if (string.IsNullOrEmpty(value)) continue;
                    try
                    {
                        BGUtil.FromString(field, entity.Index, value);
                    }
                    catch (Exception e)
                    {
                        BGSyncUtil.AppendWarning(logger, printWarnings, "Can not fetch field $ value for entity with id=$. Value=$. Error=$", field.Name, rowsData.EntityId, value, e.Message);
                    }
                }
            }
        }

        private void FlushRelations(BGSyncRelationsResolver relationsResolver)
        {
            if (data == null) return;

            //fields
            for (var j = 0; j < data.FieldsCount; j++)
            {
                var field = data.GetField(j);
                if (!(field is BGAbstractRelationI)) continue;

                var resolver = relationsResolver.GetResolver(field);

                //rows
                for (var i = 0; i < data.RowsCount; i++)
                {
                    var rowsData = data.GetRow(i);
                    var value = rowsData.GetValue(j);
                    if (string.IsNullOrEmpty(value)) continue;
                    try
                    {
                        resolver.ToDatabase(i, value);
                    }
                    catch (Exception e)
                    {
                        BGSyncUtil.AppendWarning(logger, printWarnings,"Can not fetch field $ value for entity with id=$. Value=$. Error=$", field.Name, rowsData.EntityId, value, e.Message);
                    }
                }
            }
        }

        //================================================================================================
        //                                              utilities
        //================================================================================================

        private bool IsRowEmpty(IRow row)
        {
            //this is not optimal - but we can not change it without changing BGDatabase package
            var hasValue = false;
            info.ForEachField((id, index) =>
            {
                if (hasValue) return;
                hasValue = !BGExcelSheetWriterART.IsCellEmpty(row, index);
            });
            return !hasValue;
        }

        private BGEntity EnsureEntity(IRow row, BGEntitySheetDataInfo.RowData rowsData)
        {
            // create an entity if required
            BGEntity entity;
            //--------------------  existing entity
            if (!rowsData.EntityId.IsEmpty) entity = meta.NewEntity(rowsData.EntityId);
            else
            {
                //-------------------- new entity
                entity = meta.NewEntity();
                rowsData.EntityId = entity.Id;
                if (info.IndexId >= 0)
                {
                    //update id if id column exists
                    var idCell = row.GetCell(info.IndexId) ?? row.CreateCell(info.IndexId);
                    idCell.SetCellType(CellType.String);
                    idCell.SetCellValue(entity.Id.ToString());
                }
            }

            rowsData.Entity = entity;
            return entity;
        }
        

    }
}