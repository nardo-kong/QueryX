using System.Collections.Generic; // For IEnumerable
using System.Data;
using System.Globalization; // For CsvHelper CultureInfo
using System.IO; // For File operations
using System.Threading.Tasks; // For async
using CsvHelper; // CsvHelper library
using CsvHelper.Configuration; // For CsvConfiguration
using ClosedXML.Excel; // ClosedXML library

namespace QueryX.Services // Ensure namespace matches
{
    public class ExportService
    {
        // Method to export DataTable to CSV asynchronously
        public async Task ExportDataTableToCsvAsync(DataTable dataTable, string filePath)
        {
            if (dataTable == null) throw new ArgumentNullException(nameof(dataTable));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            var config = new CsvConfiguration(CultureInfo.CurrentCulture) // Use current culture settings
            {
                // Delimiter = ",", // Default is comma, change if needed
                // HasHeaderRecord = true, // Default is true
            };

            try
            {
                // Use StreamWriter with async writing
                using (var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8)) // Use UTF8 for broader compatibility
                using (var csv = new CsvWriter(writer, config))
                {
                    // Write header row
                    foreach (DataColumn column in dataTable.Columns)
                    {
                        csv.WriteField(column.ColumnName);
                    }
                    await csv.NextRecordAsync(); // End header row

                    // Write data rows
                    foreach (DataRow row in dataTable.Rows)
                    {
                        for (var i = 0; i < dataTable.Columns.Count; i++)
                        {
                            // Convert DBNull to empty string or handle as needed
                            object? value = row[i];
                            csv.WriteField(value is DBNull ? "" : value?.ToString() ?? "");
                        }
                        await csv.NextRecordAsync(); // End data row
                    }
                    await writer.FlushAsync(); // Ensure all data is written
                }
            }
            catch (Exception ex)
            {
                // Log the error or rethrow a more specific exception
                System.Diagnostics.Debug.WriteLine($"Error exporting to CSV: {ex}");
                throw new IOException($"Failed to export data to CSV file '{filePath}'. Reason: {ex.Message}", ex);
            }
        }

        // Method to export DataTable to Excel (.xlsx) asynchronously
        public async Task ExportDataTableToExcelAsync(DataTable dataTable, string filePath)
        {
            if (dataTable == null) throw new ArgumentNullException(nameof(dataTable));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            try
            {
                // Use Task.Run for potentially CPU-bound ClosedXML operations if needed,
                // though saving might be IO-bound depending on complexity.
                await Task.Run(() =>
                {
                    using (var workbook = new XLWorkbook())
                    {
                        // Add DataTable as a worksheet
                        // Use the DataTable's name or provide a default sheet name
                        var worksheet = workbook.Worksheets.Add(dataTable, "Results");

                        // Optional: Adjust column widths
                        worksheet.Columns().AdjustToContents();

                        // Optional: Add table styles or formatting
                        // var table = worksheet.Table(0); // Get table from DataTable insertion
                        // table.Theme = XLTableTheme.TableStyleMedium2;

                        workbook.SaveAs(filePath);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error exporting to Excel: {ex}");
                throw new IOException($"Failed to export data to Excel file '{filePath}'. Reason: {ex.Message}", ex);
            }
        }

        // --- NEW METHOD for exporting multiple tables to one Excel file ---
        public async Task ExportDataTablesToExcelAsync(List<DataTable> dataTables, string filePath)
        {
            if (dataTables == null || !dataTables.Any()) throw new ArgumentNullException(nameof(dataTables));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            try
            {
                await Task.Run(() =>
                {
                    using (var workbook = new XLWorkbook())
                    {
                        int sheetNumber = 1;
                        foreach (var dataTable in dataTables)
                        {
                            // Use the DataTable's assigned name or create a default one
                            string sheetName = string.IsNullOrWhiteSpace(dataTable.TableName)
                                ? $"Sheet{sheetNumber++}"
                                : dataTable.TableName;

                            // Truncate sheet name if too long (Excel limit is 31 chars) and ensure it's unique
                            if (sheetName.Length > 31) sheetName = sheetName.Substring(0, 31);
                            if (workbook.Worksheets.Any(w => w.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase)))
                            {
                                sheetName = $"{sheetName.Substring(0, Math.Min(28, sheetName.Length))}_{sheetNumber++}";
                            }

                            var worksheet = workbook.Worksheets.Add(dataTable, sheetName);
                            worksheet.Columns().AdjustToContents(); // Adjust column widths
                        }
                        workbook.SaveAs(filePath);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error exporting to Excel: {ex}");
                throw new IOException($"Failed to export data to Excel file '{filePath}'. Reason: {ex.Message}", ex);
            }
        }
    }
}