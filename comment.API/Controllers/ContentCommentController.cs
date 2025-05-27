using Comment.Application.Interfaces;
using Comment.Domain.Entities;
using Comment.Domain.DTOs;
using Comment.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Comment.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")] 
    public class ContentCommentController : ControllerBase
    {
        private readonly IContentCommentService _commentService;

        public ContentCommentController(IContentCommentService commentService)
        {
            _commentService = commentService;
        }

        
        [HttpGet("content/{contentId}/comments")]
        public async Task<IActionResult> GetCommentsByContentId(string contentId, [FromQuery] string userId)
        {
            var comments = await _commentService.GetCommentsByContentIdAsync(contentId, userId);
            return Ok(comments);
        }

        
        [HttpGet("comment/{parentCommentId}/replies")]
        public async Task<IActionResult> GetRepliesByParentId(string parentCommentId, [FromQuery] string userId)
        {
            var replies = await _commentService.GetRepliesByParentIdAsync(parentCommentId, userId);
            return Ok(replies);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateCommentRequest request)
        {
            var comment = new ContentComment
            {
                
                ContentId = request.ContentId,
                CommenterUserId = request.CommenterUserId,
                Comment = request.Comment,
                ParentCommentId = request.ParentCommentId,
                LikeCount = 0,
                DislikeCount = 0,
                InsertTime = DateTime.UtcNow,
                LastUpdateTime = null,
                DeleteTime = null
            };
            
            var created = await _commentService.CreateCommentAsync(comment);
            return Ok(created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateCommentRequest request)
        {
            var result = await _commentService.UpdateCommentAsync(id, request.Comment);
            return result ? Ok("Yorum güncellendi.") : NotFound("Yorum bulunamadı.");
        }

        [HttpDelete("soft/{id}")]
        public async Task<IActionResult> SoftDelete(string id)
        {
            var result = await _commentService.SoftDeleteCommentAsync(id);
            return result ? Ok("Yorum silindi.") : NotFound("Yorum bulunamadı.");
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> HardDelete(string id)
        {
            var result = await _commentService.DeleteCommentAsync(id);
            return result ? Ok("Yorum tamamen silindi.") : NotFound("Yorum bulunamadı.");
        }

        
       [HttpPost("comment/{commentId}/like")]
        public async Task<IActionResult> ToggleLike(string commentId, [FromQuery] string userId, [FromQuery] VoteType voteType)
        {
           
            string voteTypeString = voteType switch
            {
                VoteType.Like => "like",
                VoteType.Dislike => "dislike",
                _ => "like"
            };
            
            var result = await _commentService.ToggleCommentLikeAsync(userId, commentId, voteTypeString);
            
            //  Response formatı
            return Ok(new {
                success = result.success,
                message = result.message,
                voteType = voteType.ToString(),
                likeCount = result.likeCount,
                dislikeCount = result.dislikeCount
            });
        }

        
        [HttpGet("comment/{commentId}/like-status")]
        public async Task<IActionResult> GetLikeStatus(string commentId, [FromQuery] string userId)
        {
            var status = await _commentService.GetUserCommentLikeStatusAsync(userId, commentId);
            return Ok(status ?? "none");
        }

        
        [HttpGet("content/{contentId}/count")]
        public async Task<IActionResult> GetCommentCount(string contentId)
        {
            var count = await _commentService.GetCommentCountByContentIdAsync(contentId);
            return Ok(new { count });
        }

        //Content'e ait tüm yorumları sil
        [HttpDelete("content/{contentId}/all")]
        public async Task<IActionResult> DeleteAllCommentsByContentId(string contentId)
        {
            var result = await _commentService.DeleteAllCommentsByContentIdAsync(contentId);
            return result ? Ok("Tüm yorumlar silindi.") : NotFound("Silinecek yorum bulunamadı.");
        }

        
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var comment = await _commentService.GetByIdAsync(id);
            return comment != null ? Ok(comment) : NotFound("Yorum bulunamadı.");
        }
    }

    
}