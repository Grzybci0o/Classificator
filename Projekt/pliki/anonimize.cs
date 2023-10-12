using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using OfficeOpenXml;

namespace Projekt.pliki;

public class anonimize
{
    private static int _dataWide = 14;
    private static List<string> _codesColumns = new List<string> { "nazwisko", "pacjentId", "pid" };
    private static List<string> _fcodesColumn = new List<string> { "file_name", "pacjentId", "fid" };
    private static string _kodeFilePath = "kody.xlsx";
    private static string _fcodesFilePath = "kody_plikow.xlsx";
    private static string _dataInputDirectory = "dataIn";
    private static string _dataOutputDirectory = "dataAn";
    private static int _maxPid;
    private static int _maxFid;

    static (Dictionary<string, List<object>>, Dictionary<string, List<object>>) TryReadCodes()
    {
        _maxPid = 0;
        var kodesDict = GetEmptyCodes();
        try
        {
            using (var package = new ExcelPackage(new FileInfo(_kodeFilePath)))
            {
                var kodesWorksheet = package.Workbook.Worksheets[0];
                Console.WriteLine(">>>> Kody wczytane z " + _kodeFilePath);
    
                for (int row = 2; row <= kodesWorksheet.Dimension.End.Row; row++)
                {
                    int pid = int.Parse(kodesWorksheet.Cells[row, 3].Text);
                    if (pid > _maxPid) _maxPid = pid;
    
                    foreach (var colName in _codesColumns)
                    {
                        kodesDict[colName].Add(kodesWorksheet.Cells[row, _codesColumns.IndexOf(colName) + 1].Text);
                    }
                }
            }
        }
        catch
        {
            Console.WriteLine("Problem z czytaniem pliku z kodami " + _kodeFilePath);
        }
    
        _maxFid = 0;
        var fCodesDict = new Dictionary<string, List<object>>();
        foreach (var c in _fcodesColumn) fCodesDict[c] = new List<object>();
    
        try
        {
            using (var package = new ExcelPackage(new FileInfo(_fcodesFilePath)))
            {
                var fCodesWorksheet = package.Workbook.Worksheets[0];
    
                for (int row = 2; row <= fCodesWorksheet.Dimension.End.Row; row++)
                {
                    int fid = int.Parse(fCodesWorksheet.Cells[row, 3].Text);
                    if (fid > _maxFid) _maxFid = fid;
    
                    foreach (var c in _fcodesColumn)
                    {
                        fCodesDict[c].Add(fCodesWorksheet.Cells[row, _fcodesColumn.IndexOf(c) + 1].Text);
                    }
                }
            }
        }
        catch
        {
            Console.WriteLine("Problem z czytaniem pliku z kodami " + _fcodesFilePath);
        }
    
        return (kodesDict, fCodesDict);
    }
    
    static void SaveKodes(Dictionary<string, List<object>> kodesDict, Dictionary<string, List<object>> fCodesDict)
    {
        using (var package = new ExcelPackage())
        {
            var kodesWorksheet = package.Workbook.Worksheets.Add("Kody");
            kodesWorksheet.Cells["A1"].LoadFromArrays(new List<string[]> { _codesColumns.ToArray() });
    
            for (int i = 0; i < kodesDict[_codesColumns[0]].Count; i++)
            {
                for (int j = 0; j < _codesColumns.Count; j++)
                {
                    kodesWorksheet.Cells[i + 2, j + 1].Value = kodesDict[_codesColumns[j]][i];
                }
            }
    
            package.SaveAs(new FileInfo(_kodeFilePath));
        }
    
        using (var package = new ExcelPackage())
        {
            var fCodesWorksheet = package.Workbook.Worksheets.Add("KodyPlikow");
            fCodesWorksheet.Cells["A1"].LoadFromArrays(new List<string[]> { _fcodesColumn.ToArray() });
    
            for (int i = 0; i < fCodesDict[_fcodesColumn[0]].Count; i++)
            {
                for (int j = 0; j < _fcodesColumn.Count; j++)
                {
                    fCodesWorksheet.Cells[i + 2, j + 1].Value = fCodesDict[_fcodesColumn[j]][i];
                }
            }
    
            package.SaveAs(new FileInfo(_fcodesFilePath));
        }
    }
    
    static Dictionary<string, List<object>> GetEmptyCodes()
    {
        var kodesDict = new Dictionary<string, List<object>>();
        foreach (var col in _codesColumns) kodesDict[col] = new List<object>();
        return kodesDict;
    }
    
    static void ProcessFile(string path, int fnumber, Dictionary<string, List<object>> kodesDict, Dictionary<string, List<object>> fCodesDict)
    {
        var fileName = Path.GetFileName(path);
        var (d, pid, fid) = AnonimizeFile(path, kodesDict, fileName);
        var anonimizedDf = new List<object[]>();
    
        anonimizedDf.Add(new object[] { "CentSek", d.Keys.ToArray() });
    
        foreach (var row in Enumerable.Range(0, d.Values.First().Count))
        {
            var rowData = new List<object> { d["CentSek"][row] };
            foreach (var col in d.Keys.Where(col => col != "CentSek"))
            {
                rowData.Add(d[col][row]);
            }
            anonimizedDf.Add(rowData.ToArray());
        }
    
        Thread.Sleep(1);
        var outExcelPath = Path.Combine(_dataOutputDirectory, $"{pid:D6}-{fnumber:D4}.xlsx");
        Console.WriteLine(">>>>> Zanonimizowane dane zapisane w " + outExcelPath);
    
        using (var package = new ExcelPackage())
        {
            var worksheet = package.Workbook.Worksheets.Add("Sheet1");
            worksheet.Cells["A1"].LoadFromArrays(anonimizedDf);
            package.SaveAs(new FileInfo(outExcelPath));
        }
    }
    
       static (Dictionary<string, List<object>>, int, int) AnonimizeFile(string path, Dictionary<string, List<object>> kodesDict, string fileName)
    {
        var originalDf = new List<object[]>();
        using (var package = new ExcelPackage(new FileInfo(path)))
        {
            var worksheet = package.Workbook.Worksheets[0];
            for (int row = 1; row <= worksheet.Dimension.End.Row; row++)
            {
                var rowValues = new List<object>();
                for (int col = 1; col <= worksheet.Dimension.End.Column; col++)
                {
                    rowValues.Add(worksheet.Cells[row, col].Text);
                }
                originalDf.Add(rowValues.ToArray());
            }
        }
    
        var age = 0;
        var pid = 0;
        var sex = "unrecognized";
        var fid = 0;
    
        var (d, newPid, newFid) = AnonimizeRowList(age, pid, originalDf, sex, fid);
    
        return (d, newPid, newFid);
    }
    
    static (Dictionary<string, List<object>>, int, int) AnonimizeRowList(int age, int pid, List<object[]> rowList, string sex, int fid)
    {
        var d = new Dictionary<string, List<object>>();
        var originColList = rowList[0].Skip(1).Take(_dataWide).Cast<string>().ToList();
        var colList = new List<string> { originColList[0], "CentSek" };
        colList.AddRange(originColList.Skip(1));
        string tp = "";
        
        foreach (var colName in colList)
        {
            d[colName] = new List<object>();
        }
    
        var metaDataList = new List<object>();
        d["metaDane"] = metaDataList;
    
        foreach (var row in rowList.Skip(1))
        {
            var t = (string)row[0];
            
            foreach (var word in row.Skip(1).Cast<string>())
            {
                if (word.ToLower().Contains("pocz"))
                {
                    tp = t;
                }
            }
    
            d[colList[0]].Add(row[0]);
            d[colList[1]].Add(tools.ParseTime(row[0].ToString()));
    
            for (int i = 1; i <= _dataWide; i++)
            {
                d[colList[i + 1]].Add(row[i]);
            }
    
            metaDataList.Add("");
        }
    
        metaDataList[0] = pid;
        metaDataList[1] = tp;
        metaDataList[2] = "age:";
        metaDataList[3] = age;
        metaDataList[4] = "sex:";
        metaDataList[5] = sex;
        metaDataList[6] = "fid:";
        metaDataList[7] = fid;
    
        return (d, pid, fid);
    }
}