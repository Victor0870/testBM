/*
<copyright file="BGExcelIdResolverFieldString.cs" company="BansheeGz">
    Copyright (c) 2018-2021 All Rights Reserved
</copyright>
*/

namespace BansheeGz.BGDatabase
{
    public class BGExcelIdResolverFieldStringRT : BGExcelIdResolverFieldART<string>
    {
        public BGExcelIdResolverFieldStringRT(BGLogger logger, BGFieldString field) : base(logger, field)
        {
        }

        protected override string Convert(string value)
        {
            return value;
        }
    }
}