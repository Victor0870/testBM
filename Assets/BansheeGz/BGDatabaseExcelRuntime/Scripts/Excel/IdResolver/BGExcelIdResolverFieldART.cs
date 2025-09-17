/*
<copyright file="BGExcelIdResolverFieldA.cs" company="BansheeGz">
    Copyright (c) 2018-2021 All Rights Reserved
</copyright>
*/

using System;
using System.Collections.Generic;
using NPOI.SS.UserModel;

namespace BansheeGz.BGDatabase
{
    public abstract class BGExcelIdResolverFieldART<T> : BGExcelIdResolverART, BGExcelIdFieldResolverIRT
    {
        private readonly BGField<T> field;
        private readonly Dictionary<T, BGId> value2Id = new Dictionary<T, BGId>();

        public BGField Field => field;
        public bool PrintWarnings;
        public BGExcelIdResolverFieldART(BGLogger logger, BGField<T> field) : base(logger)
        {
            this.field = field;
            var count = field.Meta.CountEntities;
            for (var i = 0; i < count; i++)
            {
                var value = field[i];
                if (value2Id.TryGetValue(value, out var existedId)) 
                    BGSyncUtil.AppendWarning(logger, PrintWarnings,
                        "Field row resolver: row # $ is ignored, cause duplicate ID value is detected! Row ID=$, field=$, duplicate ID value=$", i, existedId, field.FullName, value);
                else value2Id.Add(value, field.Meta.GetEntity(i).Id);
            }
        }

        public override BGId ResolveId(BGExcelSheetReaderEntityRT reader, BGEntitySheetInfo info, IRow row)
        {
            var entityId = BGId.Empty;
            var column = info.GetFieldColumn(field.Id);
            if (column >= 0)
                reader.ReadNotNull(row, column, s =>
                {
                    var value = default(T);
                    try
                    {
                        value = Convert(s);
                    }
                    catch (Exception e)
                    {
                        BGSyncUtil.AppendWarning(logger, PrintWarnings, "Row # " + row.RowNum + " is skipped, cause ID value can not be extracted from value=" + s + ", error=" + e.Message);
                        return;
                    }

                    //here is the logic behind this: if any value present- it means entityId can not be null, even if value2Id does not contains required value 
                    if (!value2Id.TryGetValue(value, out entityId)) entityId = BGId.NewId;
                });

            return entityId;
        }

        protected abstract T Convert(string value);
    }
}