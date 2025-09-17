/*
<copyright file="BGExcelIdResolverFieldInt.cs" company="BansheeGz">
    Copyright (c) 2018-2021 All Rights Reserved
</copyright>
*/

using System.Globalization;

namespace BansheeGz.BGDatabase
{
    public class BGExcelIdResolverFieldIntRT : BGExcelIdResolverFieldART<int>
    {
        public BGExcelIdResolverFieldIntRT(BGLogger logger, BGFieldInt field) : base(logger, field)
        {
        }

        protected override int Convert(string value)
        {
            return int.Parse(value, CultureInfo.InvariantCulture);
        }
    }
}