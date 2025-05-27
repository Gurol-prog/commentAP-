namespace Comment.Domain.DTOs
{
    public class CreateCommentRequest
    {
        public string ContentId { get; set; } = null!;
        public string CommenterUserId { get; set; } = null!;
        public string Comment { get; set; } = null!;
        public string? ParentCommentId { get; set; }
    }
}