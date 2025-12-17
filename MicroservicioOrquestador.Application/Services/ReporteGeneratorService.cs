using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.Extensions.Logging;
using MicroservicioReportes.Application.Interfaces;

namespace MicroservicioReportes.Application.Services
{
    public class ReporteGeneratorService
    {
        private readonly IUsuarioServiceExternal _usuarioService;
        private readonly ITareaServiceExternal _tareaService;
        private readonly ILogger<ReporteGeneratorService> _logger;

        public ReporteGeneratorService(
            IUsuarioServiceExternal usuarioService,
            ITareaServiceExternal tareaService,
            ILogger<ReporteGeneratorService> logger)
        {
            _usuarioService = usuarioService;
            _tareaService = tareaService;
            _logger = logger;
        }

        public async Task<(byte[] pdfBytes, byte[] excelBytes)> GenerarReportesTareaAsync(
            int tareaId,
            string tareaTitulo,
            List<int> empleadosIds,
            string usuarioNombre)
        {
            _logger.LogInformation($"Generando reportes para tarea {tareaId}");

            var empleados = new List<dynamic>();
            foreach (var empId in empleadosIds)
            {
                try
                {
                    var emp = await _usuarioService.ObtenerUsuarioPorId(empId);
                    if (emp != null)
                    {
                        empleados.Add(emp);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"No se pudo obtener empleado {empId}: {ex.Message}");
                }
            }

            var pdfBytes = await GenerarPDFAsync(tareaId, tareaTitulo, empleados, usuarioNombre);
            var excelBytes = await GenerarExcelAsync(tareaId, tareaTitulo, empleados, usuarioNombre);

            return (pdfBytes, excelBytes);
        }

        private async Task<byte[]> GenerarPDFAsync(int tareaId, string tareaTitulo, List<dynamic> empleados, string usuarioNombre)
        {
            return await Task.Run(() =>
            {
                using (var memoryStream = new MemoryStream())
                {
                    var document = new Document(PageSize.A4, 50, 50, 50, 50);
                    var writer = PdfWriter.GetInstance(document, memoryStream);

                    document.Open();

                    var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, new BaseColor(64, 64, 64));
                    var title = new Paragraph("REPORTE DE TAREA\n\n", titleFont);
                    title.Alignment = Element.ALIGN_CENTER;
                    document.Add(title);

                    var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);
                    var boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);

                    document.Add(new Paragraph($"Tarea ID: {tareaId}", boldFont));
                    document.Add(new Paragraph($"Título: {tareaTitulo}", boldFont));
                    document.Add(new Paragraph($"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}", normalFont));
                    document.Add(new Paragraph("\n"));

                    document.Add(new Paragraph("EMPLEADOS ASIGNADOS:", boldFont));
                    document.Add(new Paragraph("\n"));

                    if (empleados.Any())
                    {
                        foreach (var emp in empleados)
                        {
                            var nombres = emp.GetType().GetProperty("Nombres")?.GetValue(emp)?.ToString() ?? "";
                            var apellido = emp.GetType().GetProperty("PrimerApellido")?.GetValue(emp)?.ToString() ?? "";
                            var email = emp.GetType().GetProperty("Email")?.GetValue(emp)?.ToString() ?? "";

                            document.Add(new Paragraph($"• {nombres} {apellido} ({email})", normalFont));
                        }
                    }
                    else
                    {
                        document.Add(new Paragraph("No hay empleados asignados.", normalFont));
                    }

                    document.Add(new Paragraph("\n"));
                    document.Add(new Paragraph($"Total de empleados: {empleados.Count}", boldFont));

                    document.Add(new Paragraph("\n\n"));
                    var footerFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, new BaseColor(128, 128, 128));
                    var footer = new Paragraph($"Generado por: {usuarioNombre}\n{DateTime.Now:dd/MM/yyyy HH:mm}", footerFont);
                    footer.Alignment = Element.ALIGN_RIGHT;
                    document.Add(footer);

                    document.Close();
                    writer.Close();

                    return memoryStream.ToArray();
                }
            });
        }

        private async Task<byte[]> GenerarExcelAsync(int tareaId, string tareaTitulo, List<dynamic> empleados, string usuarioNombre)
        {
            return await Task.Run(() =>
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Reporte de Tarea");

                    worksheet.Cell(1, 1).Value = "REPORTE DE TAREA";
                    worksheet.Cell(1, 1).Style.Font.Bold = true;
                    worksheet.Cell(1, 1).Style.Font.FontSize = 16;
                    worksheet.Range(1, 1, 1, 4).Merge();

                    int row = 3;
                    worksheet.Cell(row, 1).Value = "Tarea ID:";
                    worksheet.Cell(row, 1).Style.Font.Bold = true;
                    worksheet.Cell(row, 2).Value = tareaId;
                    row++;

                    worksheet.Cell(row, 1).Value = "Título:";
                    worksheet.Cell(row, 1).Style.Font.Bold = true;
                    worksheet.Cell(row, 2).Value = tareaTitulo;
                    row++;

                    worksheet.Cell(row, 1).Value = "Fecha:";
                    worksheet.Cell(row, 1).Style.Font.Bold = true;
                    worksheet.Cell(row, 2).Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                    row += 2;

                    worksheet.Cell(row, 1).Value = "EMPLEADOS ASIGNADOS:";
                    worksheet.Cell(row, 1).Style.Font.Bold = true;
                    worksheet.Cell(row, 1).Style.Font.FontSize = 14;
                    row++;

                    worksheet.Cell(row, 1).Value = "Nombre";
                    worksheet.Cell(row, 2).Value = "Apellido";
                    worksheet.Cell(row, 3).Value = "Email";
                    worksheet.Range(row, 1, row, 3).Style.Font.Bold = true;
                    worksheet.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.LightGray;
                    row++;

                    if (empleados.Any())
                    {
                        foreach (var emp in empleados)
                        {
                            var nombres = emp.GetType().GetProperty("Nombres")?.GetValue(emp)?.ToString() ?? "";
                            var apellido = emp.GetType().GetProperty("PrimerApellido")?.GetValue(emp)?.ToString() ?? "";
                            var email = emp.GetType().GetProperty("Email")?.GetValue(emp)?.ToString() ?? "";

                            worksheet.Cell(row, 1).Value = nombres;
                            worksheet.Cell(row, 2).Value = apellido;
                            worksheet.Cell(row, 3).Value = email;
                            row++;
                        }
                    }
                    else
                    {
                        worksheet.Cell(row, 1).Value = "No hay empleados asignados";
                        worksheet.Range(row, 1, row, 3).Merge();
                        row++;
                    }

                    row++;
                    worksheet.Cell(row, 1).Value = $"Total de empleados: {empleados.Count}";
                    worksheet.Cell(row, 1).Style.Font.Bold = true;

                    row += 2;
                    worksheet.Cell(row, 1).Value = $"Generado por: {usuarioNombre}";
                    worksheet.Cell(row, 1).Style.Font.Italic = true;
                    row++;
                    worksheet.Cell(row, 1).Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                    worksheet.Cell(row, 1).Style.Font.Italic = true;

                    worksheet.Columns().AdjustToContents();

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        return stream.ToArray();
                    }
                }
            });
        }
    }
}
