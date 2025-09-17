/*
<copyright file="BGExcelIdResolverIndex.cs" company="BansheeGz">
    Copyright (c) 2018-2021 All Rights Reserved
</copyright>
*/

using NPOI.SS.UserModel;

namespace BansheeGz.BGDatabase
{
    public class BGExcelIdResolverIndexRT : BGExcelIdResolverART
    {
        private readonly BGMetaEntity mainMeta;

        public BGExcelIdResolverIndexRT(BGLogger logger, BGMetaEntity mainMeta) : base(logger)
        {
            this.mainMeta = mainMeta;
        }

        public override BGId ResolveId(BGExcelSheetReaderEntityRT reader, BGEntitySheetInfo info, IRow row)
        {
            if (mainMeta == null) return BGId.Empty;
            var targetIndex = row.RowNum - 1;
            //here is the logic behind this: each row, if present, has some ID, even if there is no mapping row in the other repo
            if (targetIndex < 0 || targetIndex >= mainMeta.CountEntities) return BGId.NewId;
            return mainMeta.GetEntity(targetIndex).Id;
        }
    }
}