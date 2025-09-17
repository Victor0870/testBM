/*
<copyright file="BGExcelIdResolverA.cs" company="BansheeGz">
    Copyright (c) 2018-2021 All Rights Reserved
</copyright>
*/

using NPOI.SS.UserModel;

namespace BansheeGz.BGDatabase
{
    public abstract class BGExcelIdResolverART
    {
        protected readonly BGLogger logger;

        protected BGExcelIdResolverART(BGLogger logger)
        {
            this.logger = logger;
        }

        public abstract BGId ResolveId(BGExcelSheetReaderEntityRT reader, BGEntitySheetInfo info, IRow row);
    }
}