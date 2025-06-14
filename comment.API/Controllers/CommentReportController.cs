using Comment.Application.Interfaces;
using Comment.Domain.DTOs;
using Comment.Domain.Entities;
using Comment.Domain.Enums;
using Comment.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace Comment.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommentReportController : ControllerBase
    {
        private readonly ICommentReportService _reportService;

        public CommentReportController(ICommentReportService reportService)
        {
            _reportService = reportService;
        }

        // Kullanıcı bir yorumu şikayet eder
        [HttpPost("submit")]
        public async Task<IActionResult> SubmitReport([FromQuery] string userId, [FromQuery] string commentId, [FromQuery] ReportReason reason, [FromQuery] string? description = null)
        {
            try
            {
                var report = new CommentReport
                {
                    ReporterUserId = userId,
                    CommentId = commentId,
                    Reason = reason.ToString(),
                    Description = description
                };

                var result = await _reportService.CreateReportAsync(report);
                var response = ApiResponse<object>.SuccessResponse(
                    new { message = "Şikayet alındı.", reportId = result.Id },
                    "Şikayet başarıyla gönderildi."
                );
                return Ok(response);
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
            {
                var response = ApiResponse<object>.ErrorResponse(
                    new List<string> { "Bu yorumu zaten şikayet ettiniz." },
                    "Şikayet gönderilemedi.",
                    400
                );
                return BadRequest(response);
            }
            catch (Exception)
            {
                var response = ApiResponse<object>.ErrorResponse(
                    new List<string> { "Şikayet gönderilirken bir hata oluştu." },
                    "İşlem başarısız.",
                    500
                );
                return StatusCode(500, response);
            }
        }


        // Admin: Bekleyen şikayetleri listele (incelenmemiş olanlar)
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingReports()
        {
            try
            {
                var reports = await _reportService.GetUnreviewedReportsAsync();
                var response = ApiResponse<object>.SuccessResponse(
                    reports,
                    "Bekleyen şikayetler başarıyla getirildi."
                );
                return Ok(response);
            }
            catch (Exception)
            {
                var response = ApiResponse<object>.ErrorResponse(
                    new List<string> { "Bekleyen şikayetler getirilirken bir hata oluştu." },
                    "İşlem başarısız.",
                    500
                );
                return StatusCode(500, response);
            }
        }

        [HttpGet("pending-with-details")]
        public async Task<IActionResult> GetPendingReportsWithDetails()
        {
            try
            {
                var reports = await _reportService.GetUnreviewedReportsWithDetails();
                var response = ApiResponse<IEnumerable<object>>.SuccessResponse(
                    reports,
                    "Bekleyen şikayetler detaylı bilgilerle başarıyla getirildi."
                );
                return Ok(response);
            }
            catch (Exception)
            {
                var response = ApiResponse<IEnumerable<object>>.ErrorResponse(
                    new List<string> { "Bekleyen şikayetler getirilirken bir hata oluştu." },
                    "İşlem başarısız.",
                    500
                );
                return StatusCode(500, response);
            }
        }
        // Admin: Belirli bir yoruma ait tüm şikayetleri getir detaylı

        [HttpGet("by-comment/{commentId}/details")]
        public async Task<IActionResult> GetReportsByCommentIdWithDetails(string commentId)
        {
            try
            {
                var reports = await _reportService.GetReportsByCommentIdWithDetails(commentId);
                var response = ApiResponse<IEnumerable<object>>.SuccessResponse(
                    reports,
                    "Yoruma ait şikayetler detaylı bilgilerle başarıyla getirildi."
                );
                return Ok(response);
            }
            catch (Exception)
            {
                var response = ApiResponse<IEnumerable<object>>.ErrorResponse(
                    new List<string> { "Yoruma ait şikayetler getirilirken bir hata oluştu." },
                    "İşlem başarısız.",
                    500
                );
                return StatusCode(500, response);
            }
        }


        // Admin: Belirli bir yoruma ait tüm şikayetleri getir
        [HttpGet("by-comment/{commentId}")]
        public async Task<IActionResult> GetReportsByCommentId(string commentId)
        {
            try
            {
                var reports = await _reportService.GetReportsByCommentIdAsync(commentId);
                var response = ApiResponse<object>.SuccessResponse(
                    reports,
                    "Yoruma ait şikayetler başarıyla getirildi."
                );
                return Ok(response);
            }
            catch (Exception)
            {
                var response = ApiResponse<object>.ErrorResponse(
                    new List<string> { "Yoruma ait şikayetler getirilirken bir hata oluştu." },
                    "İşlem başarısız.",
                    500
                );
                return StatusCode(500, response);
            }
        }

        [HttpGet("{reportId}/details")]
        public async Task<IActionResult> GetReportByIdWithDetails(string reportId)
        {
            try
            {
                var report = await _reportService.GetReportByIdWithDetails(reportId);
                if (report != null)
                {
                    var response = ApiResponse<object>.SuccessResponse(
                        report,
                        "Şikayet detayları başarıyla getirildi."
                    );
                    return Ok(response);
                }
                else
                {
                    var response = ApiResponse<object>.ErrorResponse(
                        new List<string> { "Şikayet bulunamadı." },
                        "Şikayet bulunamadı.",
                        404
                    );
                    return NotFound(response);
                }
            }
            catch (Exception)
            {
                var response = ApiResponse<object>.ErrorResponse(
                    new List<string> { "Şikayet detayları getirilirken bir hata oluştu." },
                    "İşlem başarısız.",
                    500
                );
                return StatusCode(500, response);
            }
        }


        // Admin: Şikayet detaylarını getir
        [HttpGet("{reportId}")]
        public async Task<IActionResult> GetReportById(string reportId)
        {
            try
            {
                var report = await _reportService.GetReportByIdAsync(reportId);
                if (report is not null)
                {
                    var response = ApiResponse<object>.SuccessResponse(
                        report,
                        "Şikayet detayları başarıyla getirildi."
                    );
                    return Ok(response);
                }
                else
                {
                    var response = ApiResponse<object>.ErrorResponse(
                        new List<string> { "Şikayet bulunamadı." },
                        "Şikayet bulunamadı.",
                        404
                    );
                    return NotFound(response);
                }
            }
            catch (Exception)
            {
                var response = ApiResponse<object>.ErrorResponse(
                    new List<string> { "Şikayet detayları getirilirken bir hata oluştu." },
                    "İşlem başarısız.",
                    500
                );
                return StatusCode(500, response);
            }
        }

        // Admin: Şikayete cevap ver ve incelendi olarak işaretle
        [HttpPut("review")]
        public async Task<IActionResult> ReviewReport([FromQuery] string reportId, [FromQuery] string adminResponse)
        {
            try
            {
                var success = await _reportService.ReviewReportAsync(reportId, adminResponse);
                if (success)
                {
                    var response = ApiResponse<object>.SuccessResponse(
                        null,
                        "Şikayet incelendi ve yanıt gönderildi."
                    );
                    return Ok(response);
                }
                else
                {
                    var response = ApiResponse<object>.ErrorResponse(
                        new List<string> { "Şikayet bulunamadı." },
                        "Şikayet bulunamadı.",
                        404
                    );
                    return NotFound(response);
                }
            }
            catch (Exception)
            {
                var response = ApiResponse<object>.ErrorResponse(
                    new List<string> { "Şikayet incelenirken bir hata oluştu." },
                    "İşlem başarısız.",
                    500
                );
                return StatusCode(500, response);
            }
        }

        // Admin: Şikayeti deaktif et (kapat)
        [HttpPut("deactivate/{reportId}")]
        public async Task<IActionResult> DeactivateReport(string reportId)
        {
            try
            {
                var success = await _reportService.DeactivateReportAsync(reportId);
                if (success)
                {
                    var response = ApiResponse<object>.SuccessResponse(
                        null,
                        "Şikayet kapatıldı."
                    );
                    return Ok(response);
                }
                else
                {
                    var response = ApiResponse<object>.ErrorResponse(
                        new List<string> { "Şikayet bulunamadı." },
                        "Şikayet bulunamadı.",
                        404
                    );
                    return NotFound(response);
                }
            }
            catch (Exception)
            {
                var response = ApiResponse<object>.ErrorResponse(
                    new List<string> { "Şikayet kapatılırken bir hata oluştu." },
                    "İşlem başarısız.",
                    500
                );
                return StatusCode(500, response);
            }
        }


        // Admin: Şikayetleri filtrele
        [HttpPost("filter")]
        public async Task<IActionResult> FilterReports([FromBody] FilterCommentReportsRequest request)
        {
            try
            {
                var result = await _reportService.FilterCommentReportsAsync(request);
                var response = ApiResponse<object>.SuccessResponse(
                    result,
                    "Şikayetler başarıyla filtrelendi."
                );
                return Ok(response);
            }
            catch (Exception)
            {
                var response = ApiResponse<object>.ErrorResponse(
                    new List<string> { "Şikayetler filtrelenirken bir hata oluştu." },
                    "İşlem başarısız.",
                    500
                );
                return StatusCode(500, response);
            }


        }

        [HttpPost("filter-with-details")]
        public async Task<IActionResult> FilterReportsWithDetails([FromBody] FilterCommentReportsRequest request)
        {
            try
            {
                var result = await _reportService.FilterCommentReportsWithDetailsAsync(request);
                var response = ApiResponse<object>.SuccessResponse(
                    result,
                    "Şikayetler detaylı bilgilerle başarıyla filtrelendi."
                );
                return Ok(response);
            }
            catch (Exception)
            {
                var response = ApiResponse<object>.ErrorResponse(
                    new List<string> { "Şikayetler filtrelenirken bir hata oluştu." },
                    "İşlem başarısız.",
                    500
                );
                return StatusCode(500, response);
            }
        }


        [HttpGet("reports-with-detailsz")]
        public async Task<IActionResult> GetReportsWithDetails()
        {
            try
            {
                var reports = await _reportService.GetReportsWithDetails();

                var response = ApiResponse<IEnumerable<object>>.SuccessResponse(
                    reports,
                    "Şikayetler detaylı bilgilerle başarıyla getirildi."
                );
                return Ok(response);
            }
            catch (Exception)
            {
                var response = ApiResponse<IEnumerable<object>>.ErrorResponse(
                    new List<string> { "Şikayetler getirilirken hata oluştu." },
                    "İşlem başarısız.",
                    500
                );
                return StatusCode(500, response);
            }
        }

        [HttpGet("against-user/{userId}")]
        public async Task<IActionResult> GetCommentsReportedAgainstUser(string userId)
        {
            try
            {
                var reportedComments = await _reportService.GetCommentsReportedAgainstUser(userId);
                var response = ApiResponse<IEnumerable<object>>.SuccessResponse(
                    reportedComments,
                    "Kullanıcıya karşı yapılan şikayetler başarıyla getirildi."
                );
                return Ok(response);
            }
            catch (Exception)
            {
                var response = ApiResponse<IEnumerable<object>>.ErrorResponse(
                    new List<string> { "Kullanıcıya karşı şikayetler getirilirken bir hata oluştu." },
                    "İşlem başarısız.",
                    500
                );
                return StatusCode(500, response);
            }
        }

    }
}