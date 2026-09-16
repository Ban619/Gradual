using ClosedXML.Excel;
using Gradual.Models;

namespace Gradual.Services;

public static class ProjectExporter
{
    public static void ExportToExcel(IEnumerable<ProjectRecord> projects, string destinationPath)
    {
        var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Projects");

        worksheet.Cell(1, 1).Value = "Project Name";
        worksheet.Cell(1, 2).Value = "Client";
        worksheet.Cell(1, 3).Value = "Status";
        worksheet.Cell(1, 4).Value = "Priority";
        worksheet.Cell(1, 5).Value = "Folder Path";
        worksheet.Cell(1, 6).Value = "Notes";
        worksheet.Cell(1, 7).Value = "Project Phases";
        worksheet.Cell(1, 8).Value = "Attachments";
        worksheet.Cell(1, 9).Value = "Updated At";

        var row = 2;
        foreach (var project in projects)
        {
            worksheet.Cell(row, 1).Value = project.ProjectName;
            worksheet.Cell(row, 2).Value = project.Client;
            worksheet.Cell(row, 3).Value = project.Status;
            worksheet.Cell(row, 4).Value = project.Priority;
            worksheet.Cell(row, 5).Value = project.FolderPath;
            worksheet.Cell(row, 6).Value = project.Notes;
            worksheet.Cell(row, 7).Value = string.Join(" | ", project.ProjectPhases);
            worksheet.Cell(row, 8).Value = string.Join(" | ", project.AttachmentPaths);
            worksheet.Cell(row, 9).Value = project.UpdatedAt;
            row++;
        }

        worksheet.Columns().AdjustToContents();
        workbook.SaveAs(destinationPath);
    }
}
