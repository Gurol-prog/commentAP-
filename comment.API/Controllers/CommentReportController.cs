using Comment.Application.Interfaces;
using Comment.Domain.Entities;
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
        public async Task<IActionResult> SubmitReport([FromQuery] string userId, [FromQuery] string commentId, [FromQuery] string reason, [FromQuery] string? description = null)
        {
            try
            {
                var report = new CommentReport
                {
                    ReporterUserId = userId,
                    CommentId = commentId,
                    Reason = reason,
                    Description = description
                };

                var result = await _reportService.CreateReportAsync(report);
                return Ok(new { message = "Şikayet alındı.", reportId = result.Id });
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000) // Duplicate key error
            {
                return BadRequest("Bu yorumu zaten şikayet ettiniz.");
            }
            catch (Exception)
            {
                return StatusCode(500, "Şikayet gönderilirken bir hata oluştu.");
            }
        }

        // Admin: Bekleyen şikayetleri listele (incelenmemiş olanlar)
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingReports()
        {
            var reports = await _reportService.GetUnreviewedReportsAsync();
            return Ok(reports);
        }

        // Admin: Belirli bir yoruma ait tüm şikayetleri getir
        [HttpGet("by-comment/{commentId}")]
        public async Task<IActionResult> GetReportsByCommentId(string commentId)
        {
            var reports = await _reportService.GetReportsByCommentIdAsync(commentId);
            return Ok(reports);
        }

        // Admin: Şikayet detaylarını getir
        [HttpGet("{reportId}")]
        public async Task<IActionResult> GetReportById(string reportId)
        {
            var report = await _reportService.GetReportByIdAsync(reportId);
            return report is not null ? Ok(report) : NotFound("Şikayet bulunamadı.");
        }

        // Admin: Şikayete cevap ver ve incelendi olarak işaretle
        [HttpPut("review")]
        public async Task<IActionResult> ReviewReport([FromQuery] string reportId, [FromQuery] string adminResponse)
        {
            var success = await _reportService.ReviewReportAsync(reportId, adminResponse);
            return success ? Ok("Şikayet incelendi ve yanıt gönderildi.") : NotFound("Şikayet bulunamadı.");
        }

        // Admin: Şikayeti deaktif et (kapat)
        [HttpPut("deactivate/{reportId}")]
        public async Task<IActionResult> DeactivateReport(string reportId)
        {
            var success = await _reportService.DeactivateReportAsync(reportId);
            return success ? Ok("Şikayet kapatıldı.") : NotFound("Şikayet bulunamadı.");
        }

        // Kullanıcının şikayet ettiği yorumları getir
        [HttpGet("user-reports/{userId}")]
        public async Task<IActionResult> GetUserReportedComments(string userId)
        {
            var commentIds = await _reportService.GetReportedCommentIdsByUserAsync(userId);
            return Ok(commentIds);
        }
    }
}