using System.Text.Json.Serialization;

namespace Comment.Domain.DTOs{
    public class CommentUserDto
    {
        [JsonPropertyName("commenterUserId")]
        public string CommenterUserId { get; set; } = null!;

        [JsonPropertyName("commentername")]
        public string Commentername { get; set; } = null!; // Bu Parents'ten gelecek (maskelenmiş)
    }
}