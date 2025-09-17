/*
<copyright file="BGExcelIdResolverId.cs" company="BansheeGz">
    Copyright (c) 2018-2021 All Rights Reserved
</copyright>
*/

using NPOI.SS.UserModel;

namespace BansheeGz.BGDatabase
{
    public class BGExcelIdResolverIdRT : BGExcelIdResolverART
    {
        public BGExcelIdResolverIdRT(BGLogger logger) : base(logger)
        {
        }

        public override BGId ResolveId(BGExcelSheetReaderEntityRT reader, BGEntitySheetInfo info, IRow row)
        {
            var entityId = BGId.Empty;
            if (info.IndexId >= 0)
                reader.ReadNotNull(row, info.IndexId, s =>
                {
                    entityId = new BGId(s);
                });

            return entityId;
        }
    }
}