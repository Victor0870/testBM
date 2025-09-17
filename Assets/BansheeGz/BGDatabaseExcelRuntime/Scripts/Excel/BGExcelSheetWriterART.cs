/*
<copyright file="BGExcelSheetWriterART.cs" company="BansheeGz">
    Copyright (c) 2019-2021 All Rights Reserved
</copyright>
*/

using System;
using System.Collections.Generic;
using NPOI.SS.UserModel;

namespace BansheeGz.BGDatabase
{
    public  abstract partial class BGExcelSheetWriterART
    {
        protected readonly BGLogger logger;
        protected readonly BGRepo repo;
        protected readonly IWorkbook book;
        protected readonly BGBookInfo bookInfo;
        private readonly HashSet<int> usedRows = new HashSet<int>();

        protected int currentRow;
        protected ISheet sheet;

        protected IRow row;
        protected BGSheetInfoA sheetInfo;

        public IRow IRow => row;

        public int CurrentRow
        {
            get => currentRow;
            set => currentRow = value;
        }

        public int NewRow
        {
            get
            {
                for (var i = 1; i < 1048575; i++)
                {
                    if (usedRows.Contains(i)) continue;

                    var row = sheet.GetRow(i);
                    if (row == null)
                    {
                        usedRows.Add(i);
                        return i;
                    }

                    if (IsRowEmpty(row))
                    {
                        usedRows.Add(i);
                        return i;
                    }
                }

                throw new Exception("It looks like sheet " + sheet.SheetName + " reached the maximum number of rows = 1048575!");
                // return sheet.LastRowNum + 1;
            }
        }

        public int NewCellIndex
        {
            get
            {
                var max = -1;
                foreach (var cell in row.Cells)
                    if (max < cell.ColumnIndex)
                        max = cell.ColumnIndex;
                return max + 1;
            }
        }


        protected BGExcelSheetWriterART(BGLogger logger, BGRepo repo, IWorkbook book, BGBookInfo bookInfo)
        {
            this.logger = logger;
            this.repo = repo;
            this.book = book;
            this.bookInfo = bookInfo;
        }


        public void Row(Action action)
        {
            Row(currentRow, action);
        }

        public void Row(int index, Action action)
        {
            row = GetRow(index) ?? sheet.CreateRow(index);
            currentRow = index;
            action();
            currentRow++;
            row.Height = -1;
        }

        public IRow GetRow(int index)
        {
            return sheet.GetRow(index);
        }

        protected void Delete(List<int> rows)
        {
            if (BGUtil.IsEmpty(rows)) return;
            rows.Sort();
            var lastRow = sheet.LastRowNum;
            for (var i = 0; i < rows.Count; i++)
            {
                var rowIndex = rows[i];
                var rownum = rowIndex - i;
//                var row = sheet.GetRow(rownum);
//                sheet.RemoveRow(row);
//                sheet.ShiftRows(rownum + 1, sheet.LastRowNum, -1);
                if (rownum < lastRow) sheet.ShiftRows(rownum + 1, lastRow, -1);
            }

            for (var i = lastRow; i > lastRow - rows.Count; i--)
            {
                var removingRow = sheet.GetRow(i);
                if (removingRow != null) sheet.RemoveRow(removingRow);
            }
        }

        protected void Cell(int index, Action<ICell> cellAction)
        {
            var cell = row.GetCell(index) ?? row.CreateCell(index);
            cellAction(cell);
        }

        protected void Cell(int index, bool value) => Cell(index, cell => cell.SetCellValue(value));

        protected void Cell(int index, double value) => Cell(index, cell => cell.SetCellValue(value));

        protected void Cell(int index, string value) => Cell(index, cell => cell.SetCellValue(value));

        protected static void Clear(ISheet sheet)
        {
            var list = new List<IRow>();
            var rowEnumerator = sheet.GetRowEnumerator();
            while (rowEnumerator.MoveNext()) list.Add((IRow)rowEnumerator.Current);

            foreach (var row in list) sheet.RemoveRow(row);
        }

        protected int MapHeader(string header, int index)
        {
            //find index
            if (index < 0)
            {
                index = NewCellIndex;
                logger.AppendLine("$ column not found. Created new column at index $", header, index);
            }
            else logger.AppendLine("$ column found at index $", header, index);

            //set value
            Cell(index, header);

            return index;
        }

        protected void Sheet<T>(string name, bool @override, Func<T> provider, Func<T> factory, Action<T> action) where T : BGSheetInfoA
        {
            currentRow = 0;

            var mySheetInfo = provider();
            if (mySheetInfo == null)
            {
                logger.AppendLine("Sheet with name $ not found. Creating a new sheet..", name);
                var duplicateName = GetDuplicateSheetName(name);
                if (duplicateName != null)
                    throw new BGException("Can not create an Excel sheet with name=$, " +
                                          "cause a sheet with the same name=$ already exists (comparison is case insensitive)", name, duplicateName);
                sheet = book.CreateSheet(name);
                mySheetInfo = factory();
            }
            else
            {
                logger.AppendLine("Found existing sheet with name $", name);
                sheet = book.GetSheetAt(mySheetInfo.SheetNumber);

                if (@override)
                {
                    Clear(sheet);
                    mySheetInfo.Clear();
                }
            }

            sheetInfo = mySheetInfo;
            usedRows.Clear();
            mySheetInfo.ForEachRow((id, index) => usedRows.Add(index));
            action(mySheetInfo);
        }

        protected void Remove(BGSheetInfoA info, Predicate<BGId> predicate)
        {
            var rowsToRemove = new List<int>();
            info.ForEachRow((id, rowIndex) =>
            {
                if (!predicate(id)) return;
                rowsToRemove.Add(rowIndex);
            });
            Delete(rowsToRemove);
        }

        protected bool GetRowIndex(BGSheetInfoA info, BGId id, bool isAdding, bool isUpdating, out int rowIndex)
        {
            rowIndex = info.GetRow(id);
            if (rowIndex == -1)
            {
                if (isAdding) rowIndex = NewRow;
                else return false;
            }
            else if (!isUpdating) return false;

            return true;
        }

        private string GetDuplicateSheetName(string name)
        {
            for (var i = 0; i < book.NumberOfSheets; i++)
            {
                var sheetName = book.GetSheetName(i);
                // if (name.Equals(sheetName, StringComparison.OrdinalIgnoreCase)) return true;
                if (name.Equals(sheetName, StringComparison.InvariantCultureIgnoreCase)) return sheetName;
            }

            return null;
        }

        public static bool IsCellEmpty(IRow row, int index)
        {
            return IsCellEmpty(row.GetCell(index));
        }

        public static bool IsCellEmpty(ICell cell)
        {
            if (cell == null) return true;
            var hasValue = false;
            switch (cell.CellType)
            {
                case CellType.Unknown:
                    break;
                case CellType.Numeric:
                    hasValue = cell.NumericCellValue != 0;
                    break;
                case CellType.String:
                    hasValue = !string.IsNullOrEmpty(cell.StringCellValue);
                    break;
                case CellType.Formula:
                    hasValue = true;
                    break;
                case CellType.Blank:
                    break;
                case CellType.Boolean:
                    hasValue = cell.BooleanCellValue;
                    break;
                case CellType.Error:
                    hasValue = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return !hasValue;
        }

        private static bool IsRowEmpty(IRow row)
        {
            var cells = row.Cells;
            if (cells == null || cells.Count == 0) return true;
            foreach (var cell in cells)
                if (!IsCellEmpty(cell))
                    return false;
            return true;
        }
    }
}