using System.Text;
using ClosedXML.Excel;

namespace Leno.SellerShop.Infrastructure.Export;

/// <summary>
/// 导出文件生成器，将数据行渲染为 Excel（ClosedXML）或 CSV。
/// </summary>
public sealed class ExportFileGenerator
{
    /// <summary>生成 Excel 字节流。</summary>
    public byte[] GenerateExcel(string sheetName, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(sheetName);

        for (var i = 0; i < headers.Count; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
        }
        ws.Row(1).Style.Font.Bold = true;

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            for (var c = 0; c < headers.Count; c++)
            {
                var key = headers[c];
                var value = row.TryGetValue(key, out var v) ? v : null;
                ws.Cell(r + 2, c + 1).Value = ToCellValue(value);
            }
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>生成 CSV 字节流（UTF-8 with BOM）。</summary>
    public byte[] GenerateCsv(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, new UTF8Encoding(true));
        writer.WriteLine(string.Join(",", headers.Select(EscapeCsvField)));
        foreach (var row in rows)
        {
            var values = headers.Select(h => row.TryGetValue(h, out var v) ? v?.ToString() ?? string.Empty : string.Empty);
            writer.WriteLine(string.Join(",", values.Select(EscapeCsvField)));
        }
        writer.Flush();
        return ms.ToArray();
    }

    private static XLCellValue ToCellValue(object? value) => value switch
    {
        null => string.Empty,
        int iv => iv,
        long lv => lv,
        decimal dv => dv,
        double dv2 => dv2,
        DateTime dt => dt,
        bool bv => bv,
        _ => value.ToString() ?? string.Empty
    };

    private static string EscapeCsvField(string? value)
    {
        if (value is null) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
