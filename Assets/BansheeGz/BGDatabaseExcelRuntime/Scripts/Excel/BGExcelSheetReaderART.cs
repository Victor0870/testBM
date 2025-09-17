/*
<copyright file="BGExcelSheetReaderART.cs" company="BansheeGz">
    Copyright (c) 2019-2021 All Rights Reserved
</copyright>
*/

using System;
using System.Globalization;
using NPOI.SS.UserModel;

namespace BansheeGz.BGDatabase
{
    public class BGExcelSheetReaderART
    {
        protected static int ForEachRowNoHeader(ISheet sheet, Action<IRow> action)
        {
            var rowEnumerator = sheet.GetEnumerator();
            var count = 0;
            while (rowEnumerator.MoveNext())
            {
                count++;
                //skip headers
                if (count == 1) continue;

                try
                {
                    action((IRow)rowEnumerator.Current);
                }
                catch (ExitException)
                {
                    //ignore
                }
            }

            return count;
        }

        protected static void ForEachCell(IRow row, Action<int, ICell> action)
        {
            var cells = row?.Cells;
            if (cells == null) return;

            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                action(i, cell);
            }
        }

        public void ReadNotNull(IRow row, int index, Action<string> action)
        {
            var cell = row.GetCell(index);
            if (cell == null) return;

            var value = ReadAsString(cell);
            if (!string.IsNullOrEmpty(value)) action(value);
        }

        protected void Read(IRow row, int index, Action<string> action)
        {
            var cell = row.GetCell(index);
            if (cell != null) action(ReadAsString(cell));
        }

        protected static string ReadAsString(ICell cell) => cell == null ? null : ReadCell(cell.CellType, cell, true);

        private static string ReadCell(CellType cellType, ICell cell, bool allowFormula)
        {
            string result;
            switch (cellType)
            {
                case CellType.Numeric:
                    result = cell.NumericCellValue.ToString(CultureInfo.InvariantCulture);
                    break;
                case CellType.String:
                    result = cell.StringCellValue;
                    result = result?.Trim();
                    break;
                case CellType.Boolean:
                    result = cell.BooleanCellValue ? "1" : "0";
                    break;
                case CellType.Formula:
                    if (!allowFormula) throw new Exception("Formulas not allowed at this point");
                    if (string.IsNullOrEmpty(cell.CellFormula)) return null;
                    if (cell.IsPartOfArrayFormulaGroup)
                    {
                        var evaluator = cell.Sheet.Workbook.GetCreationHelper().CreateFormulaEvaluator();
                        var val = evaluator.Evaluate(cell);
                        result = ReadCell(val.CellType, cell, false);
                    }
                    else result = ReadCell(cell.CachedFormulaResultType, cell, false);

                    break;
                default:
                    return null;
            }

            return result;
        }

        public class ExitException : Exception
        {
        }
    }
}