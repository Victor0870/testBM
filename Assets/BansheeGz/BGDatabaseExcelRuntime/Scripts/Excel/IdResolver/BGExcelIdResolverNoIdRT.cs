/*
<copyright file="BGExcelIdResolverNoId.cs" company="BansheeGz">
    Copyright (c) 2018-2021 All Rights Reserved
</copyright>
*/

using NPOI.SS.UserModel;

namespace BansheeGz.BGDatabase
{
    public class BGExcelIdResolverNoIdRT : BGExcelIdResolverART
    {
        public BGExcelIdResolverNoIdRT(BGLogger logger) : base(logger)
        {
        }

        public override BGId ResolveId(BGExcelSheetReaderEntityRT reader, BGEntitySheetInfo info, IRow row)
        {
            return BGId.Empty;
        }
    }
}