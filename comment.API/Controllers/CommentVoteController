using Comment.Application.Interfaces;
using Comment.Domain.Enums;
using Comment.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace Comment.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommentVoteController : ControllerBase
    {
        private readonly ICommentVoteService _voteService;

        public CommentVoteController(ICommentVoteService voteService)
        {
            _voteService = voteService;
        }

        
        [HttpPost("toggle")]
        public async Task<IActionResult> ToggleVote([FromQuery] string userId, [FromQuery] string commentId, [FromQuery] VoteType voteType)
        {
            try
            {
                var voteTypeString = voteType.ToString().ToLowerInvariant(); // "like" veya "dislike"
                var existingVote = await _voteService.GetUserCommentVoteAsync(userId, commentId);
                
                if (existingVote == null)
                {
                    // Kullanıcının oyu yok, yeni oy ekle
                    var success = await _voteService.AddCommentVoteAsync(userId, commentId, voteTypeString);
                    if (success)
                    {
                        var stats = await _voteService.GetCommentVoteStatsAsync(commentId);
                        var data = new { 
                            message = $"{voteType} eklendi", 
                            currentVote = voteType,
                            likeCount = stats.likeCount, 
                            dislikeCount = stats.dislikeCount 
                        };
                        var response = ApiResponse<object>.SuccessResponse(data, "Oy başarıyla eklendi");
                        return Ok(response);
                    }
                    else
                    {
                        var response = ApiResponse<object>.ErrorResponse(
                            new List<string> { "Oy eklenirken hata oluştu." },
                            "Oy ekleme başarısız",
                            400
                        );
                        return BadRequest(response);
                    }
                }
                
                if (existingVote.VoteType == voteTypeString)
                {
                    // Aynı oy tipi, oyu kaldır
                    var removed = await _voteService.RemoveCommentVoteAsync(userId, commentId);
                    if (removed)
                    {
                        var stats = await _voteService.GetCommentVoteStatsAsync(commentId);
                        var data = new { 
                            message = $"{voteType} kaldırıldı", 
                            currentVote = (VoteType?)null,
                            likeCount = stats.likeCount, 
                            dislikeCount = stats.dislikeCount 
                        };
                        var response = ApiResponse<object>.SuccessResponse(data, "Oy başarıyla kaldırıldı");
                        return Ok(response);
                    }
                    else
                    {
                        var response = ApiResponse<object>.ErrorResponse(
                            new List<string> { "Oy kaldırılırken hata oluştu." },
                            "Oy kaldırma başarısız",
                            400
                        );
                        return BadRequest(response);
                    }
                }
                
                // Farklı oy tipi, oyu güncelle
                var updated = await _voteService.UpdateCommentVoteAsync(userId, commentId, voteTypeString);
                if (updated)
                {
                    var stats = await _voteService.GetCommentVoteStatsAsync(commentId);
                    var data = new { 
                        message = $"{voteType} olarak güncellendi", 
                        currentVote = voteType,
                        likeCount = stats.likeCount, 
                        dislikeCount = stats.dislikeCount 
                    };
                    var response = ApiResponse<object>.SuccessResponse(data, "Oy başarıyla güncellendi");
                    return Ok(response);
                }
                else
                {
                    var response = ApiResponse<object>.ErrorResponse(
                        new List<string> { "Oy güncellenirken hata oluştu." },
                        "Oy güncelleme başarısız",
                        400
                    );
                    return BadRequest(response);
                }
            }
            catch (Exception)
            {
                var response = ApiResponse<object>.ErrorResponse(
                    new List<string> { "Oy işlemi sırasında beklenmeyen bir hata oluştu." },
                    "İşlem başarısız",
                    500
                );
                return StatusCode(500, response);
            }
        }

        [HttpPut("update")]
        public async Task<IActionResult> UpdateVote([FromQuery] string userId, [FromQuery] string commentId, [FromQuery] VoteType newVoteType)
        {
            try
            {
                var voteTypeString = newVoteType.ToString().ToLowerInvariant();
                var result = await _voteService.UpdateCommentVoteAsync(userId, commentId, voteTypeString);
                
                if (result)
                {
                    var response = ApiResponse<string>.SuccessResponse(
                        $"Oy {newVoteType} olarak güncellendi.",
                        "Oy başarıyla güncellendi"
                    );
                    return Ok(response);
                }
                else
                {
                    var response = ApiResponse<string>.ErrorResponse(
                        new List<string> { "Oy bulunamadı." },
                        "Oy güncelleme başarısız",
                        404
                    );
                    return NotFound(response);
                }
            }
            catch (Exception)
            {
                var response = ApiResponse<string>.ErrorResponse(
                    new List<string> { "Oy güncellenirken beklenmeyen bir hata oluştu." },
                    "İşlem başarısız",
                    500
                );
                return StatusCode(500, response);
            }
        }

        [HttpDelete("remove")]
        public async Task<IActionResult> RemoveVote([FromQuery] string userId, [FromQuery] string commentId)
        {
            try
            {
                var result = await _voteService.RemoveCommentVoteAsync(userId, commentId);
                
                if (result)
                {
                    var response = ApiResponse<string>.SuccessResponse(
                        "Oy kaldırıldı.",
                        "Oy başarıyla kaldırıldı"
                    );
                    return Ok(response);
                }
                else
                {
                    var response = ApiResponse<string>.ErrorResponse(
                        new List<string> { "Oy bulunamadı." },
                        "Oy kaldırma başarısız",
                        404
                    );
                    return NotFound(response);
                }
            }
            catch (Exception)
            {
                var response = ApiResponse<string>.ErrorResponse(
                    new List<string> { "Oy kaldırılırken beklenmeyen bir hata oluştu." },
                    "İşlem başarısız",
                    500
                );
                return StatusCode(500, response);
            }
        }

        [HttpGet("stats/{commentId}")]
        public async Task<IActionResult> GetStats(string commentId)
        {
            try
            {
                var stats = await _voteService.GetCommentVoteStatsAsync(commentId);
                var data = new { commentId, likeCount = stats.likeCount, dislikeCount = stats.dislikeCount };
                
                var response = ApiResponse<object>.SuccessResponse(
                    data,
                    "İstatistikler başarıyla getirildi"
                );
                return Ok(response);
            }
            catch (Exception)
            {
                var response = ApiResponse<object>.ErrorResponse(
                    new List<string> { "İstatistikler getirilirken beklenmeyen bir hata oluştu." },
                    "İşlem başarısız",
                    500
                );
                return StatusCode(500, response);
            }
        }

        // ✅ Kullanıcının oy durumunu getir - Enum ile response
        [HttpGet("user-vote")]
        public async Task<IActionResult> GetUserVote([FromQuery] string userId, [FromQuery] string commentId)
        {
            try
            {
                var vote = await _voteService.GetUserCommentVoteAsync(userId, commentId);
                VoteType? currentVote = null;
                
                if (vote != null && Enum.TryParse<VoteType>(vote.VoteType, true, out var parsedVote))
                {
                    currentVote = parsedVote;
                }
                
                var data = new { 
                    hasVoted = vote != null, 
                    currentVote = currentVote,
                    voteTime = vote?.InsertTime
                };
                
                var response = ApiResponse<object>.SuccessResponse(
                    data,
                    "Kullanıcı oy durumu başarıyla getirildi"
                );
                return Ok(response);
            }
            catch (Exception)
            {
                var response = ApiResponse<object>.ErrorResponse(
                    new List<string> { "Kullanıcı oy durumu getirilirken beklenmeyen bir hata oluştu." },
                    "İşlem başarısız",
                    500
                );
                return StatusCode(500, response);
            }
        }

        // ✅ Admin için - yoruma ait tüm oyları temizle
        [HttpDelete("comment/{commentId}/all")]
        public async Task<IActionResult> RemoveAllVotesForComment(string commentId)
        {
            try
            {
                var result = await _voteService.RemoveAllVotesForCommentAsync(commentId);
                
                if (result)
                {
                    var response = ApiResponse<string>.SuccessResponse(
                        "Yoruma ait tüm oylar kaldırıldı.",
                        "Toplu oy silme başarılı"
                    );
                    return Ok(response);
                }
                else
                {
                    var response = ApiResponse<string>.ErrorResponse(
                        new List<string> { "Silinecek oy bulunamadı." },
                        "Toplu oy silme başarısız",
                        404
                    );
                    return NotFound(response);
                }
            }
            catch (Exception)
            {
                var response = ApiResponse<string>.ErrorResponse(
                    new List<string> { "Oylar silinirken beklenmeyen bir hata oluştu." },
                    "İşlem başarısız",
                    500
                );
                return StatusCode(500, response);
            }
        }
    }
}