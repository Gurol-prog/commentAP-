using Comment.Application.Interfaces;
using Comment.Domain.Enums; 
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
            var voteTypeString = voteType.ToString().ToLowerInvariant(); // "like" veya "dislike"
            var existingVote = await _voteService.GetUserCommentVoteAsync(userId, commentId);
            
            if (existingVote == null)
            {
                // Kullanıcının oyu yok, yeni oy ekle
                var success = await _voteService.AddCommentVoteAsync(userId, commentId, voteTypeString);
                if (success)
                {
                    var stats = await _voteService.GetCommentVoteStatsAsync(commentId);
                    return Ok(new { 
                        message = $"{voteType} eklendi", 
                        currentVote = voteType,
                        likeCount = stats.likeCount, 
                        dislikeCount = stats.dislikeCount 
                    });
                }
                return BadRequest("Oy eklenirken hata oluştu.");
            }
            
            if (existingVote.VoteType == voteTypeString)
            {
                // Aynı oy tipi, oyu kaldır
                var removed = await _voteService.RemoveCommentVoteAsync(userId, commentId);
                if (removed)
                {
                    var stats = await _voteService.GetCommentVoteStatsAsync(commentId);
                    return Ok(new { 
                        message = $"{voteType} kaldırıldı", 
                        currentVote = (VoteType?)null,
                        likeCount = stats.likeCount, 
                        dislikeCount = stats.dislikeCount 
                    });
                }
                return BadRequest("Oy kaldırılırken hata oluştu.");
            }
            
            // Farklı oy tipi, oyu güncelle
            var updated = await _voteService.UpdateCommentVoteAsync(userId, commentId, voteTypeString);
            if (updated)
            {
                var stats = await _voteService.GetCommentVoteStatsAsync(commentId);
                return Ok(new { 
                    message = $"{voteType} olarak güncellendi", 
                    currentVote = voteType,
                    likeCount = stats.likeCount, 
                    dislikeCount = stats.dislikeCount 
                });
            }
            return BadRequest("Oy güncellenirken hata oluştu.");
        }

        [HttpPut("update")]
        public async Task<IActionResult> UpdateVote([FromQuery] string userId, [FromQuery] string commentId, [FromQuery] VoteType newVoteType)
        {
            var voteTypeString = newVoteType.ToString().ToLowerInvariant();
            var result = await _voteService.UpdateCommentVoteAsync(userId, commentId, voteTypeString);
            return result ? Ok($"Oy {newVoteType} olarak güncellendi.") : NotFound("Oy bulunamadı.");
        }

        [HttpDelete("remove")]
        public async Task<IActionResult> RemoveVote([FromQuery] string userId, [FromQuery] string commentId)
        {
            var result = await _voteService.RemoveCommentVoteAsync(userId, commentId);
            return result ? Ok("Oy kaldırıldı.") : NotFound("Oy bulunamadı.");
        }

        [HttpGet("stats/{commentId}")]
        public async Task<IActionResult> GetStats(string commentId)
        {
            var stats = await _voteService.GetCommentVoteStatsAsync(commentId);
            return Ok(new { commentId, likeCount = stats.likeCount, dislikeCount = stats.dislikeCount });
        }

        // ✅ Kullanıcının oy durumunu getir - Enum ile response
        [HttpGet("user-vote")]
        public async Task<IActionResult> GetUserVote([FromQuery] string userId, [FromQuery] string commentId)
        {
            var vote = await _voteService.GetUserCommentVoteAsync(userId, commentId);
            VoteType? currentVote = null;
            
            if (vote != null && Enum.TryParse<VoteType>(vote.VoteType, true, out var parsedVote))
            {
                currentVote = parsedVote;
            }
            
            return Ok(new { 
                hasVoted = vote != null, 
                currentVote = currentVote,
                voteTime = vote?.InsertTime
            });
        }

        // ✅ Admin için - yoruma ait tüm oyları temizle
        [HttpDelete("comment/{commentId}/all")]
        public async Task<IActionResult> RemoveAllVotesForComment(string commentId)
        {
            var result = await _voteService.RemoveAllVotesForCommentAsync(commentId);
            return result ? Ok("Yoruma ait tüm oylar kaldırıldı.") : NotFound("Silinecek oy bulunamadı.");
        }
    }
}