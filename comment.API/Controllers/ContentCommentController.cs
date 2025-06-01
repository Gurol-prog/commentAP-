using Comment.Application.Interfaces;
using Comment.Domain.Entities;
using Comment.Domain.DTOs;
using Comment.Domain.Enums;
using Comment.Domain.Models;
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

        //Contentdeki tüm ana yorumları getir
        [HttpGet("content/{contentId}/comments")]
        public async Task<ActionResult<ApiResponse<IEnumerable<ContentComment>>>> GetCommentsByContentId(string contentId, [FromQuery] string userId)
        {
            try
            {
                var comments = await _commentService.GetCommentsByContentIdAsync(contentId, userId);

                var successResponse = ApiResponse<IEnumerable<ContentComment>>.SuccessResponse(
                    comments,
                    "Yorumlar başarıyla getirildi"
                );
                return Ok(successResponse);
            }
            catch (Exception ex)
            {
                var errorResponse = ApiResponse<IEnumerable<ContentComment>>.ErrorResponse(
                    new List<string> { ex.Message },
                    "Yorumlar getirilirken hata oluştu",
                    500
                );
                return StatusCode(500, errorResponse);
            }
        }

        

        //Bir ana yorumun tüm cevaplarını getir
        [HttpGet("comment/{parentCommentId}/replies")]
        public async Task<ActionResult<ApiResponse<IEnumerable<ContentComment>>>> GetRepliesByParentId(string parentCommentId, [FromQuery] string userId)
        {
            try
            {
                var replies = await _commentService.GetRepliesByParentIdAsync(parentCommentId, userId);

                var successResponse = ApiResponse<IEnumerable<ContentComment>>.SuccessResponse(
                    replies,
                    "Yanıtlar başarıyla getirildi"
                );
                return Ok(successResponse);
            }
            catch (Exception ex)
            {
                var errorResponse = ApiResponse<IEnumerable<ContentComment>>.ErrorResponse(
                    new List<string> { ex.Message },
                    "Yanıtlar getirilirken hata oluştu",
                    500
                );
                return StatusCode(500, errorResponse);
            }
        }
        //Yorum oluşturma
        [HttpPost]
        public async Task<ActionResult<ApiResponse<ContentComment>>> Create([FromBody] CreateCommentRequest request)
        {
            try
            {
                // Validation kontrolü
                if (!ModelState.IsValid)
                {
                    var validationErrors = ModelState
                        .SelectMany(x => x.Value.Errors)
                        .Select(x => x.ErrorMessage)
                        .ToList();

                    var validationResponse = ApiResponse<ContentComment>.ErrorResponse(
                        validationErrors,
                        "Validation hatası",
                        400
                    );
                    return BadRequest(validationResponse);
                }

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

                var successResponse = ApiResponse<ContentComment>.SuccessResponse(
                    created,
                    "Yorum başarıyla oluşturuldu",
                    201
                );
                return StatusCode(201, successResponse);
            }
            catch (Exception ex)
            {
                var errorResponse = ApiResponse<ContentComment>.ErrorResponse(
                    new List<string> { ex.Message },
                    "Yorum oluşturulurken hata oluştu",
                    500
                );
                return StatusCode(500, errorResponse);
            }
        }
        //yorum güncelleme
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<string>>> Update(string id,[FromQuery] string userId, [FromBody] UpdateCommentRequest request)
        {
            try
            {
                // Validation kontrolü
                if (!ModelState.IsValid)
                {
                    var validationErrors = ModelState
                        .SelectMany(x => x.Value.Errors)
                        .Select(x => x.ErrorMessage)
                        .ToList();

                    var validationResponse = ApiResponse<string>.ErrorResponse(
                        validationErrors,
                        "Validation hatası",
                        400
                    );
                    return BadRequest(validationResponse);
                }

                var result = await _commentService.UpdateCommentAsync(id, request.Comment, userId);

                if (result)
                {
                    var successResponse = ApiResponse<string>.SuccessResponse(
                        "Güncelleme başarılı",
                        "Yorum güncellendi"
                    );
                    return Ok(successResponse);
                }
                else
                {
                    var notFoundResponse = ApiResponse<string>.ErrorResponse(
                        new List<string> { "Belirtilen ID ile yorum bulunamadı" },
                        "Yorum bulunamadı",
                        404
                    );
                    return NotFound(notFoundResponse);
                }
            }
            catch (Exception ex)
            {
                var errorResponse = ApiResponse<string>.ErrorResponse(
                    new List<string> { ex.Message },
                    "Yorum güncellenirken hata oluştu",
                    500
                );
                return StatusCode(500, errorResponse);
            }
        }
        // Yorumu soft silme
        [HttpDelete("soft/{id}")]
        public async Task<ActionResult<ApiResponse<string>>> SoftDelete(string id, [FromQuery] string userId)
        {
            try
            {
                var result = await _commentService.SoftDeleteCommentAsync(id, userId);

                if (result)
                {
                    var successResponse = ApiResponse<string>.SuccessResponse(
                        "Silme işlemi başarılı",
                        "Yorum silindi"
                    );
                    return Ok(successResponse);
                }
                else
                {
                    var notFoundResponse = ApiResponse<string>.ErrorResponse(
                        new List<string> { "Belirtilen ID ile yorum bulunamadı veya size ait değil" },
                        "Yorum bulunamadı",
                        404
                    );
                    return NotFound(notFoundResponse);
                }
            }
            catch (Exception ex)
            {
                var errorResponse = ApiResponse<string>.ErrorResponse(
                    new List<string> { ex.Message },
                    "Yorum silinirken hata oluştu",
                    500
                );
                return StatusCode(500, errorResponse);
            }
        }

        //Yoruma dislik/like ekleme
        [HttpPost("comment/{commentId}/like")]
        public async Task<ActionResult<ApiResponse<object>>> ToggleLike(string commentId, [FromQuery] string userId, [FromQuery] VoteType voteType)
        {
            try
            {
                string voteTypeString = voteType switch
                {
                    VoteType.Like => "like",
                    VoteType.Dislike => "dislike",
                    _ => "like"
                };

                var result = await _commentService.ToggleCommentLikeAsync(userId, commentId, voteTypeString);

                // Data kısmı için özel obje
                var responseData = new
                {
                    success = result.success,
                    message = result.message,
                    voteType = voteType.ToString(),
                    likeCount = result.likeCount,
                    dislikeCount = result.dislikeCount
                };

                var successResponse = ApiResponse<object>.SuccessResponse(
                    responseData,
                    "Like/Dislike işlemi tamamlandı"
                );
                return Ok(successResponse);
            }
            catch (Exception ex)
            {
                var errorResponse = ApiResponse<object>.ErrorResponse(
                    new List<string> { ex.Message },
                    "Like/Dislike işleminde hata oluştu",
                    500
                );
                return StatusCode(500, errorResponse);
            }
        }

        //Bir kullanıcı bir yorumu beğenip beğenmediği kontrol et
        [HttpGet("comment/{commentId}/like-status")]
        public async Task<ActionResult<ApiResponse<string>>> GetLikeStatus(string commentId, [FromQuery] string userId)
        {
            try
            {
                var status = await _commentService.GetUserCommentLikeStatusAsync(userId, commentId);

                var likeStatus = status ?? "none";

                var successResponse = ApiResponse<string>.SuccessResponse(
                    likeStatus,
                    "Like durumu başarıyla getirildi"
                );
                return Ok(successResponse);
            }
            catch (Exception ex)
            {
                var errorResponse = ApiResponse<string>.ErrorResponse(
                    new List<string> { ex.Message },
                    "Like durumu getirilirken hata oluştu",
                    500
                );
                return StatusCode(500, errorResponse);
            }
        }

        //bir contente ait tüm yorumların sayısını getirir.
        [HttpGet("content/{contentId}/count")]
        public async Task<ActionResult<ApiResponse<object>>> GetCommentCount(string contentId)
        {
            try
            {
                var count = await _commentService.GetCommentCountByContentIdAsync(contentId);

                var countData = new { count };

                var successResponse = ApiResponse<object>.SuccessResponse(
                    countData,
                    "Yorum sayısı başarıyla getirildi"
                );
                return Ok(successResponse);
            }
            catch (Exception ex)
            {
                var errorResponse = ApiResponse<object>.ErrorResponse(
                    new List<string> { ex.Message },
                    "Yorum sayısı getirilirken hata oluştu",
                    500
                );
                return StatusCode(500, errorResponse);
            }
        }

        //Content'e ait tüm yorumları sil
        [HttpDelete("content/{contentId}/all")]
        public async Task<ActionResult<ApiResponse<string>>> DeleteAllCommentsByContentId(string contentId)
        {
            try
            {
                var result = await _commentService.DeleteAllCommentsByContentIdAsync(contentId);

                if (result)
                {
                    var successResponse = ApiResponse<string>.SuccessResponse(
                        "Toplu silme işlemi başarılı",
                        "Tüm yorumlar silindi"
                    );
                    return Ok(successResponse);
                }
                else
                {
                    var notFoundResponse = ApiResponse<string>.ErrorResponse(
                        new List<string> { "Bu content için silinecek yorum bulunamadı" },
                        "Silinecek yorum bulunamadı",
                        404
                    );
                    return NotFound(notFoundResponse);
                }
            }
            catch (Exception ex)
            {
                var errorResponse = ApiResponse<string>.ErrorResponse(
                    new List<string> { ex.Message },
                    "Yorumlar silinirken hata oluştu",
                    500
                );
                return StatusCode(500, errorResponse);
            }
        }


        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<ContentComment>>> GetById(string id)
        {
            try
            {
                var comment = await _commentService.GetByIdAsync(id);

                if (comment != null)
                {
                    // Başarılı durum
                    var successResponse = ApiResponse<ContentComment>.SuccessResponse(
                        comment,
                        "Yorum başarıyla getirildi"
                    );
                    return Ok(successResponse);
                }
                else
                {
                    // Bulunamadı durumu  
                    var notFoundResponse = ApiResponse<ContentComment>.NoContentResponse(
                        "Yorum bulunamadı",
                        404
                    );
                    return NotFound(notFoundResponse);
                }
            }
            catch (Exception ex)
            {
                // Hata durumu
                var errorResponse = ApiResponse<ContentComment>.ErrorResponse(
                    new List<string> { ex.Message },
                    "Yorum getirilirken hata oluştu",
                    500
                );
                return StatusCode(500, errorResponse);
            }
        }
        
        [HttpPost("filter")]
        public async Task<ActionResult<ApiResponse<IEnumerable<ContentComment>>>> Filter([FromBody] CommentFilterRequest filter)
        {
            try
            {
                var comments = await _commentService.FilterCommentsAsync(filter);

                var response = ApiResponse<IEnumerable<ContentComment>>.SuccessResponse(
                    comments,
                    "Filtrelenmiş yorumlar başarıyla getirildi"
                );
                return Ok(response);
            }
            catch (Exception ex)
            {
                var errorResponse = ApiResponse<IEnumerable<ContentComment>>.ErrorResponse(
                    new List<string> { ex.Message },
                    "Filtreleme sırasında hata oluştu",
                    500
                );
                return StatusCode(500, errorResponse);
            }
        }

    }

    
}