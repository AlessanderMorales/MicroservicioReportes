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

        private readonly BaseColor _colorPrimario = new BaseColor(211, 47, 47);
        private readonly BaseColor _colorSecundario = new BaseColor(255, 87, 34);
        private readonly BaseColor _colorTexto = new BaseColor(64, 64, 64);

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
                    var document = new Document(PageSize.A4, 40, 40, 40, 40);
                    var writer = PdfWriter.GetInstance(document, memoryStream);

                    document.Open();

                    try
                    {
                        // Buscar logo en ubicaciones estándar
                        string logoPath = null;
                        var possiblePaths = new[]
                        {
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "images", "Logo.png"),
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "Logo.png"),
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logo.png"),
                            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "Logo.png"),
                            Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Logo.png"),
                            Path.Combine(Directory.GetCurrentDirectory(), "Logo.png")
                        };

                        foreach (var path in possiblePaths)
                        {
                            if (File.Exists(path))
                            {
                                logoPath = path;
                                _logger.LogInformation($"✅ Logo encontrado en: {logoPath}");
                                break;
                            }
                        }

                        if (!string.IsNullOrEmpty(logoPath))
                        {
                            var logo = Image.GetInstance(logoPath);
                            logo.ScaleToFit(80f, 80f);
                            logo.Alignment = Image.ALIGN_LEFT;
                            
                            var headerTable = new PdfPTable(2);
                            headerTable.WidthPercentage = 100;
                            headerTable.SetWidths(new float[] { 1f, 4f });
                            
                            var logoCell = new PdfPCell(logo);
                            logoCell.Border = Rectangle.NO_BORDER;
                            logoCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                            logoCell.HorizontalAlignment = Element.ALIGN_CENTER;
                            headerTable.AddCell(logoCell);
                            
                            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 20, _colorPrimario);
                            var subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 12, _colorSecundario);
                            
                            var titlePhrase = new Phrase();
                            titlePhrase.Add(new Chunk("SISTEMA DE GESTIÓN\n", titleFont));
                            titlePhrase.Add(new Chunk("Reporte de Asignación de Tarea", subtitleFont));
                            
                            var titleCell = new PdfPCell(titlePhrase);
                            titleCell.Border = Rectangle.NO_BORDER;
                            titleCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                            titleCell.PaddingLeft = 10;
                            headerTable.AddCell(titleCell);
                            
                            document.Add(headerTable);
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ Logo no encontrado. Usando encabezado sin logo.");
                            
                            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 20, _colorPrimario);
                            var title = new Paragraph("SISTEMA DE GESTIÓN DE PROYECTOS Y TAREAS\n", titleFont);
                            title.Alignment = Element.ALIGN_CENTER;
                            document.Add(title);
                            
                            var subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 14, _colorSecundario);
                            var subtitle = new Paragraph("Reporte de Asignación de Tarea\n", subtitleFont);
                            subtitle.Alignment = Element.ALIGN_CENTER;
                            document.Add(subtitle);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"❌ Error al cargar logo: {ex.Message}");
                        
                        var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, _colorPrimario);
                        var title = new Paragraph("REPORTE DE ASIGNACIÓN DE TAREA\n\n", titleFont);
                        title.Alignment = Element.ALIGN_CENTER;
                        document.Add(title);
                    }

                    var lineSeparator = new Paragraph(new Chunk(new iTextSharp.text.pdf.draw.LineSeparator(2f, 100f, _colorSecundario, Element.ALIGN_CENTER, -2)));
                    document.Add(lineSeparator);
                    document.Add(new Paragraph("\n"));

                    var headerInfoFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, _colorPrimario);
                    var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 11, _colorTexto);

                    var infoTable = new PdfPTable(2);
                    infoTable.WidthPercentage = 100;
                    infoTable.SetWidths(new float[] { 1f, 2f });
                    infoTable.SpacingBefore = 10f;
                    infoTable.SpacingAfter = 15f;

                    AddInfoRow(infoTable, "Tarea ID:", tareaId.ToString(), headerInfoFont, normalFont);
                    AddInfoRow(infoTable, "Título:", tareaTitulo, headerInfoFont, normalFont);
                    AddInfoRow(infoTable, "Fecha:", DateTime.Now.ToString("dd/MM/yyyy"), headerInfoFont, normalFont);
                    AddInfoRow(infoTable, "Hora:", DateTime.Now.ToString("HH:mm:ss"), headerInfoFont, normalFont);
                    AddInfoRow(infoTable, "Generado por:", usuarioNombre, headerInfoFont, normalFont);

                    document.Add(infoTable);

                    var sectionTitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.WHITE);
                    
                    var sectionHeader = new PdfPTable(1);
                    sectionHeader.WidthPercentage = 100;
                    var sectionCell = new PdfPCell(new Phrase("EMPLEADOS ASIGNADOS", sectionTitleFont));
                    sectionCell.BackgroundColor = _colorPrimario;
                    sectionCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    sectionCell.Padding = 8;
                    sectionCell.Border = Rectangle.NO_BORDER;
                    sectionHeader.AddCell(sectionCell);
                    document.Add(sectionHeader);

                    var empleadosTable = new PdfPTable(4);
                    empleadosTable.WidthPercentage = 100;
                    empleadosTable.SetWidths(new float[] { 0.5f, 2f, 2f, 2.5f });

                    var tableHeaderFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
                    AddTableHeader(empleadosTable, "#", tableHeaderFont, _colorSecundario);
                    AddTableHeader(empleadosTable, "Nombre", tableHeaderFont, _colorSecundario);
                    AddTableHeader(empleadosTable, "Apellido", tableHeaderFont, _colorSecundario);
                    AddTableHeader(empleadosTable, "Email", tableHeaderFont, _colorSecundario);

                    var tableDataFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, _colorTexto);
                    if (empleados.Any())
                    {
                        int contador = 1;
                        foreach (var emp in empleados)
                        {
                            var nombres = emp.GetType().GetProperty("Nombres")?.GetValue(emp)?.ToString() ?? "";
                            var apellido = emp.GetType().GetProperty("PrimerApellido")?.GetValue(emp)?.ToString() ?? "";
                            var email = emp.GetType().GetProperty("Email")?.GetValue(emp)?.ToString() ?? "";

                            var bgColor = contador % 2 == 0 ? new BaseColor(245, 245, 245) : BaseColor.WHITE;
                            
                            AddTableCell(empleadosTable, contador.ToString(), tableDataFont, bgColor, Element.ALIGN_CENTER);
                            AddTableCell(empleadosTable, nombres, tableDataFont, bgColor, Element.ALIGN_LEFT);
                            AddTableCell(empleadosTable, apellido, tableDataFont, bgColor, Element.ALIGN_LEFT);
                            AddTableCell(empleadosTable, email, tableDataFont, bgColor, Element.ALIGN_LEFT);
                            contador++;
                        }
                    }
                    else
                    {
                        var emptyCell = new PdfPCell(new Phrase("No hay empleados asignados", tableDataFont));
                        emptyCell.Colspan = 4;
                        emptyCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        emptyCell.Padding = 15;
                        empleadosTable.AddCell(emptyCell);
                    }

                    document.Add(empleadosTable);

                    document.Add(new Paragraph("\n"));
                    
                    var summaryTable = new PdfPTable(2);
                    summaryTable.WidthPercentage = 50;
                    summaryTable.HorizontalAlignment = Element.ALIGN_RIGHT;
                    
                    var summaryLabelFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, _colorTexto);
                    var summaryValueFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, _colorPrimario);
                    
                    var labelCell = new PdfPCell(new Phrase("Total Empleados:", summaryLabelFont));
                    labelCell.Border = Rectangle.TOP_BORDER;
                    labelCell.BorderColor = _colorSecundario;
                    labelCell.BorderWidth = 2;
                    labelCell.Padding = 10;
                    labelCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                    summaryTable.AddCell(labelCell);
                    
                    var valueCell = new PdfPCell(new Phrase(empleados.Count.ToString(), summaryValueFont));
                    valueCell.Border = Rectangle.TOP_BORDER;
                    valueCell.BorderColor = _colorSecundario;
                    valueCell.BorderWidth = 2;
                    valueCell.Padding = 10;
                    valueCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    valueCell.BackgroundColor = new BaseColor(255, 243, 224);
                    summaryTable.AddCell(valueCell);
                    
                    document.Add(summaryTable);

                    document.Add(new Paragraph("\n\n"));
                    var footerFont = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 9, new BaseColor(128, 128, 128));
                    var footer = new Paragraph();
                    footer.Add(new Chunk("─────────────────────────────────────────────────────────────\n", footerFont));
                    footer.Add(new Chunk($"Generado por Sistema de Gestión | {DateTime.Now:dd/MM/yyyy HH:mm:ss} | Usuario: {usuarioNombre}", footerFont));
                    footer.Alignment = Element.ALIGN_CENTER;
                    document.Add(footer);

                    document.Close();
                    writer.Close();

                    return memoryStream.ToArray();
                }
            });
        }

        private void AddInfoRow(PdfPTable table, string label, string value, Font labelFont, Font valueFont)
        {
            var labelCell = new PdfPCell(new Phrase(label, labelFont));
            labelCell.Border = Rectangle.NO_BORDER;
            labelCell.PaddingBottom = 5;
            table.AddCell(labelCell);
            
            var valueCell = new PdfPCell(new Phrase(value, valueFont));
            valueCell.Border = Rectangle.NO_BORDER;
            valueCell.PaddingBottom = 5;
            table.AddCell(valueCell);
        }

        private void AddTableHeader(PdfPTable table, string text, Font font, BaseColor bgColor)
        {
            var cell = new PdfPCell(new Phrase(text, font));
            cell.BackgroundColor = bgColor;
            cell.HorizontalAlignment = Element.ALIGN_CENTER;
            cell.Padding = 8;
            cell.BorderColor = BaseColor.WHITE;
            table.AddCell(cell);
        }

        private void AddTableCell(PdfPTable table, string text, Font font, BaseColor bgColor, int alignment)
        {
            var cell = new PdfPCell(new Phrase(text, font));
            cell.BackgroundColor = bgColor;
            cell.HorizontalAlignment = alignment;
            cell.Padding = 6;
            cell.BorderColor = new BaseColor(220, 220, 220);
            table.AddCell(cell);
        }

        private async Task<byte[]> GenerarExcelAsync(int tareaId, string tareaTitulo, List<dynamic> empleados, string usuarioNombre)
        {
            return await Task.Run(() =>
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Reporte de Tarea");

                    worksheet.Cell(1, 1).Value = "SISTEMA DE GESTIÓN DE PROYECTOS Y TAREAS";
                    worksheet.Cell(1, 1).Style.Font.Bold = true;
                    worksheet.Cell(1, 1).Style.Font.FontSize = 16;
                    worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#D32F2F");
                    worksheet.Range(1, 1, 1, 4).Merge();

                    worksheet.Cell(2, 1).Value = "Reporte de Asignación de Tarea";
                    worksheet.Cell(2, 1).Style.Font.FontSize = 12;
                    worksheet.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#FF5722");
                    worksheet.Range(2, 1, 2, 4).Merge();

                    int row = 4;
                    
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
                    row++;

                    worksheet.Cell(row, 1).Value = "Generado por:";
                    worksheet.Cell(row, 1).Style.Font.Bold = true;
                    worksheet.Cell(row, 2).Value = usuarioNombre;
                    row += 2;

                    worksheet.Cell(row, 1).Value = "EMPLEADOS ASIGNADOS";
                    worksheet.Cell(row, 1).Style.Font.Bold = true;
                    worksheet.Cell(row, 1).Style.Font.FontSize = 14;
                    worksheet.Cell(row, 1).Style.Font.FontColor = XLColor.White;
                    worksheet.Range(row, 1, row, 4).Merge();
                    worksheet.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#D32F2F");
                    row++;

                    worksheet.Cell(row, 1).Value = "#";
                    worksheet.Cell(row, 2).Value = "Nombre";
                    worksheet.Cell(row, 3).Value = "Apellido";
                    worksheet.Cell(row, 4).Value = "Email";
                    worksheet.Range(row, 1, row, 4).Style.Font.Bold = true;
                    worksheet.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#FF5722");
                    worksheet.Range(row, 1, row, 4).Style.Font.FontColor = XLColor.White;
                    row++;

                    if (empleados.Any())
                    {
                        int contador = 1;
                        foreach (var emp in empleados)
                        {
                            var nombres = emp.GetType().GetProperty("Nombres")?.GetValue(emp)?.ToString() ?? "";
                            var apellido = emp.GetType().GetProperty("PrimerApellido")?.GetValue(emp)?.ToString() ?? "";
                            var email = emp.GetType().GetProperty("Email")?.GetValue(emp)?.ToString() ?? "";

                            worksheet.Cell(row, 1).Value = contador;
                            worksheet.Cell(row, 2).Value = nombres;
                            worksheet.Cell(row, 3).Value = apellido;
                            worksheet.Cell(row, 4).Value = email;
                            
                            if (contador % 2 == 0)
                                worksheet.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF3E0");
                            
                            contador++;
                            row++;
                        }
                    }
                    else
                    {
                        worksheet.Cell(row, 1).Value = "No hay empleados asignados";
                        worksheet.Range(row, 1, row, 4).Merge();
                        row++;
                    }

                    row++;
                    worksheet.Cell(row, 3).Value = "Total Empleados:";
                    worksheet.Cell(row, 3).Style.Font.Bold = true;
                    worksheet.Cell(row, 4).Value = empleados.Count;
                    worksheet.Cell(row, 4).Style.Font.Bold = true;
                    worksheet.Cell(row, 4).Style.Font.FontColor = XLColor.FromHtml("#D32F2F");
                    worksheet.Cell(row, 4).Style.Font.FontSize = 14;

                    row += 2;
                    worksheet.Cell(row, 1).Value = $"Generado el {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                    worksheet.Cell(row, 1).Style.Font.Italic = true;
                    worksheet.Cell(row, 1).Style.Font.FontColor = XLColor.Gray;

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
