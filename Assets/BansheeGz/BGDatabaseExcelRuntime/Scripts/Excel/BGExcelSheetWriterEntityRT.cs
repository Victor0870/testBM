/*
<copyright file="BGExcelSheetWriterEntityRT.cs" company="BansheeGz">
    Copyright (c) 2019-2021 All Rights Reserved
</copyright>
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using NPOI.SS.UserModel;

namespace BansheeGz.BGDatabase
{
    public class BGExcelSheetWriterEntityRT : BGExcelSheetWriterART
    {
        private static readonly Dictionary<string, object> CustomConverter2Object = new Dictionary<string, object>();

        private static readonly Dictionary<Type, Func<BGField, int, double>> Type2Getter = new Dictionary<Type, Func<BGField, int, double>>
        {
            { typeof(BGFieldInt), (field, index) => ((BGFieldInt)field)[index] },
            // {typeof(BGFieldLong), (field, index) => ((BGFieldLong) field)[index]},
            // {typeof(BGFieldDouble), (field, index) => ((BGFieldDouble) field)[index]},
            {
                typeof(BGFieldFloat), (field, index) =>
                {
                    var floatValue = ((BGFieldFloat)field)[index];
                    var doubleValue = double.Parse(floatValue.ToString("g7", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                    return doubleValue;
                }
            }
        };

        private readonly BGMergeSettingsEntity settings;
        private readonly bool transferRowsOrder;
        private readonly BGRepo sourceRepo;
        private readonly BGSyncNameMapConfig nameMapConfig;
        private readonly BGSyncIdConfig idConfig;
        private readonly BGSyncRelationsResolver relationsResolver;
        private readonly bool printWarnings;

        public BGExcelSheetWriterEntityRT(BGLogger logger, BGRepo sourceRepo, BGRepo repo, IWorkbook book, BGBookInfo bookInfo, BGMergeSettingsEntity settings, bool transferRowsOrder,
            BGSyncNameMapConfig nameMapConfig, BGSyncIdConfig idConfig, BGSyncRelationsResolver relationsResolver, bool printWarnings) : base(logger, repo, book, bookInfo)
        {
            this.sourceRepo = sourceRepo;
            this.settings = settings;
            this.transferRowsOrder = transferRowsOrder;
            this.nameMapConfig = nameMapConfig;
            this.idConfig = idConfig;
            this.relationsResolver = relationsResolver;
            this.printWarnings = printWarnings;
        }


        public void Write()
        {
            repo.ForEachMeta(meta =>
            {
                var sourceMeta = sourceRepo.GetMeta(meta.Id);
                logger.SubSection(() =>
                {
                    var sheetName = nameMapConfig != null ? nameMapConfig.GetName(meta) : meta.Name;
                    Sheet(sheetName,
                        settings == null || settings.Mode == BGMergeModeEnum.Transfer,
                        () => bookInfo.GetEntitySheet(meta.Id),
                        () =>
                        {
                            var info = new BGEntitySheetInfo(meta.Id, meta.Name, book.NumberOfSheets - 1) { SheetName = sheetName };
                            bookInfo.AddEntitySheet(meta.Id, info);
                            return info;
                        },
                        info =>
                        {
                            logger.SubSection(() =>
                            {
                                //headers
                                Row(0, () =>
                                {
                                    info.IndexId = MapHeader(meta.Id, BGBookInfo.IdHeader, info.IndexId);

                                    meta.ForEachField(field =>
                                    {
                                        if (info.HasField(field.Id))
                                        {
                                            logger.AppendLine("Field $ column found at index $", field.Name, info.GetFieldColumn(field.Id));
                                            return;
                                        }

                                        info.AddField(field.Id, NewCellIndex);
                                        Cell(info.GetFieldColumn(field.Id), nameMapConfig == null ? field.Name : nameMapConfig.GetName(field));

                                        logger.AppendLine("Field $ column not found. Created new column at index $", field.Name, info.GetFieldColumn(field.Id));
                                    });
                                });
                            }, "Mapping for ($) entities.", meta.Name);

                            //Values
                            var isAdding = settings == null || settings.IsAddingMissing(meta.Id);
                            var isUpdating = settings == null || settings.IsUpdatingMatching(meta.Id);
                            var order = transferRowsOrder && sourceMeta != null ? new BGRowsOrder(logger, meta, Swap) : null;

                            //fields
                            var fieldsInfo = new Tuple<BGField, int, Func<BGField, int, double>, BGSyncRelationResolver>[meta.CountFields];
                            for (var i = 0; i < fieldsInfo.Length; i++)
                            {
                                //field
                                var field = meta.GetField(i);
                                //field column
                                var fieldColumn = info.GetFieldColumn(field.Id);
                                //double fields
                                Func<BGField, int, double> getter = null;
                                if (!field.CustomStringFormatSupported) Type2Getter.TryGetValue(field.GetType(), out getter);
                                //relations
                                BGSyncRelationResolver resolver = null;
                                if (field is BGAbstractRelationI) resolver = relationsResolver.GetResolver(field);

                                fieldsInfo[i] = new Tuple<BGField, int, Func<BGField, int, double>, BGSyncRelationResolver>(field, fieldColumn, getter, resolver);
                            }

                            //check for duplicates
                            var duplicatesMonitor = BGSyncDuplicateEntitiesMonitor.Get(idConfig, meta);

                            //rows
                            meta.ForEachEntity(entity =>
                            {
                                if (duplicatesMonitor != null && !duplicatesMonitor.Process(entity, logger, printWarnings)) return;

                                if (!GetRowIndex(info, entity.Id, isAdding, isUpdating, out var rowIndex)) return;

                                Row(rowIndex, () =>
                                {
                                    //id
                                    if (info.IndexId >= 0) Cell(info.IndexId, entity.Id.ToString());

                                    //fields
                                    foreach (var (field, fieldColumn, getter, resolver) in fieldsInfo)
                                        {
                                        if (!field.CustomStringFormatSupported)
                                        {
                                            if (field is BGFieldBool fieldBool)
                                            {
                                                //boolean
                                                Cell(fieldColumn, fieldBool[entity.Index]);
                                                continue;
                                            }

                                            if (getter != null)
                                            {
                                                //numeric
                                                Cell(fieldColumn, getter(field, entity.Index));
                                                continue;
                                            }
                                            }

                                        if (resolver != null)
                                        {
                                            Cell(fieldColumn, resolver.ToExternalFormat(entity.Index));
                                            continue;
                                        }

                                        //default!
                                        var value = BGUtil.ToString(field, entity.Index);
                                        Cell(fieldColumn, value);
                                    }
                                });

                                if (order != null)
                                {
                                    var sourceEntity = sourceMeta.GetEntity(entity.Id);
                                    if (sourceEntity != null) order.Add(new BGRowsOrder.EntityOrderInfo(sourceEntity, entity, rowIndex));
                                }
                            });

                            order?.Complete(CleanUp);

                            if (settings == null || settings.IsRemovingOrphaned(meta.Id)) Remove(info, id => !meta.HasEntity(id));

                            logger.AppendLine("$ entities are processed.", meta.CountEntities);
                        });
                }, "Writing entities for $ meta", meta.Name);
            });
        }

        public static object GetProcessor(string typeName)
        {
            try
            {
                if (CustomConverter2Object.TryGetValue(typeName, out var processor)) return processor;
                var type = BGUtil.GetType(typeName);
                if (type == null) return null;
                processor = Activator.CreateInstance(type);
                CustomConverter2Object[typeName] = processor;
                return processor;
            }
            catch
            {
                return null;
            }
        }

        private void Swap(int index1, int index2)
        {
            if (index1 == index2) return;

            var sourceRow = sheet.GetRow(index1);
            var targetRow = sheet.GetRow(index2);
            Swap(sourceRow, targetRow);
        }

        private void CleanUp()
        {
        }

        protected int MapHeader(BGId metaId, string header, int index)
        {
            var metaConfig = idConfig?.GetMetaConfig(metaId);
            if (metaConfig != null && metaConfig.configType != BGSyncIdConfig.IdConfigEnum.IdColumn) return -1;

            return base.MapHeader(header, index);
        }


        //copied from npoi !!
        private static void Swap(IRow sourceRow, IRow targetRow)
        {
            for (var i = (int)sourceRow.FirstCellNum; i < (int)sourceRow.LastCellNum; ++i)
            {
                var cell1 = sourceRow.GetCell(i);
                var cell2 = targetRow.GetCell(i);
                switch (cell1)
                {
                    case null when cell2 == null:
                        continue;
                    case null:
                    cell1 = sourceRow.CreateCell(i);
                    CopyCell(cell2, cell1);
                    targetRow.RemoveCell(cell2);
                        break;
                    default:
                    {
                        if (cell2 == null)
                {
                    cell2 = targetRow.CreateCell(i);
                    CopyCell(cell1, cell2);
                    sourceRow.RemoveCell(cell1);
                }
                else
                {
                    //swap
                            (cell1.CellStyle, cell2.CellStyle) = (cell2.CellStyle, cell1.CellStyle);

                            (cell1.CellComment, cell2.CellComment) = (cell2.CellComment, cell1.CellComment);

                            (cell1.Hyperlink, cell2.Hyperlink) = (cell2.Hyperlink, cell1.Hyperlink);

                    var cellType1 = cell1.CellType;
                    switch (cellType1)
                    {
                        case CellType.Numeric:
                        {
                            var value1 = cell1.NumericCellValue;
                            MoveValue(cell2, cell1);
                            cell2.SetCellValue(value1);
                            break;
                        }
                        case CellType.String:
                        {
                            var value1 = cell1.RichStringCellValue;
                            MoveValue(cell2, cell1);
                            cell2.SetCellValue(value1);
                            break;
                        }
                        case CellType.Formula:
                        {
                            var value1 = cell1.CellFormula;
                            MoveValue(cell2, cell1);
                            cell2.SetCellFormula(value1);
                            break;
                        }
                        case CellType.Blank:
                        {
                            var value1 = cell1.StringCellValue;
                            MoveValue(cell2, cell1);
                            cell2.SetCellValue(value1);
                            break;
                        }
                        case CellType.Boolean:
                        {
                            var value1 = cell1.BooleanCellValue;
                            MoveValue(cell2, cell1);
                            cell2.SetCellValue(value1);
                            break;
                        }
                        case CellType.Error:
                        {
                            var value1 = cell1.ErrorCellValue;
                            MoveValue(cell2, cell1);
                            cell2.SetCellErrorValue(value1);
                            break;
                        }
                    }
                }

                        break;
                    }
                }
            }
        }

        private static void MoveValue(ICell from, ICell to)
        {
            switch (from.CellType)
            {
                case CellType.Numeric:
                    to.SetCellValue(from.NumericCellValue);
                    break;
                case CellType.String:
                    to.SetCellValue(from.RichStringCellValue);
                    break;
                case CellType.Formula:
                    to.SetCellFormula(from.CellFormula);
                    break;
                case CellType.Blank:
                    to.SetCellValue(from.StringCellValue);
                    break;
                case CellType.Boolean:
                    to.SetCellValue(from.BooleanCellValue);
                    break;
                case CellType.Error:
                    to.SetCellErrorValue(from.ErrorCellValue);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void CopyCell(ICell sourceCell, ICell targetCell)
        {
            if (sourceCell.CellStyle != null) targetCell.CellStyle = sourceCell.CellStyle;
            if (sourceCell.CellComment != null) targetCell.CellComment = sourceCell.CellComment;
            if (sourceCell.Hyperlink != null) targetCell.Hyperlink = sourceCell.Hyperlink;
            targetCell.SetCellType(sourceCell.CellType);
            switch (sourceCell.CellType)
            {
                case CellType.Numeric:
                    targetCell.SetCellValue(sourceCell.NumericCellValue);
                    break;
                case CellType.String:
                    targetCell.SetCellValue(sourceCell.RichStringCellValue);
                    break;
                case CellType.Formula:
                    targetCell.SetCellFormula(sourceCell.CellFormula);
                    break;
                case CellType.Blank:
                    targetCell.SetCellValue(sourceCell.StringCellValue);
                    break;
                case CellType.Boolean:
                    targetCell.SetCellValue(sourceCell.BooleanCellValue);
                    break;
                case CellType.Error:
                    targetCell.SetCellErrorValue(sourceCell.ErrorCellValue);
                    break;
            }
        }


        private void ClearCells(IRow row)
        {
            sheetInfo.ForEachRow((id, index) => ClearCell(row, index));
        }

        private static void ClearCell(IRow row, int index)
        {
            var cell = row.GetCell(index);
            if (cell == null) return;
            switch (cell.CellType)
            {
                case CellType.Numeric:
                    cell.SetCellValue(0);
                    break;
                case CellType.String:
                    cell.SetCellValue((string)null);
                    break;
                case CellType.Formula:
                    break;
                case CellType.Blank:
                    break;
                case CellType.Boolean:
                    cell.SetCellValue(false);
                    break;
                case CellType.Error:
                    break;
            }
        }
    }
}