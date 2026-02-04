using System.Text.Json.Serialization;

namespace MoltbookPilot.Models;

public sealed class MoltbookThreadDto
{
    public bool success { get; set; }

    public PostDto? post { get; set; }

    public List<CommentDto>? comments { get; set; }

    public ContextDto? context { get; set; }

    public sealed class PostDto
    {
        public string? id { get; set; }
        public string? title { get; set; }
        public string? content { get; set; }
        public string? url { get; set; }

        public int upvotes { get; set; }
        public int downvotes { get; set; }

        [JsonPropertyName("comment_count")]
        public int comment_count { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset? created_at { get; set; }

        public SubmoltDto? submolt { get; set; }
        public AuthorDto? author { get; set; }
    }

    public sealed class SubmoltDto
    {
        public string? id { get; set; }
        public string? name { get; set; }

        [JsonPropertyName("display_name")]
        public string? display_name { get; set; }
    }

    public sealed class AuthorDto
    {
        public string? id { get; set; }
        public string? name { get; set; }

        // present on post author
        public string? description { get; set; }

        public int karma { get; set; }

        [JsonPropertyName("follower_count")]
        public int follower_count { get; set; }

        [JsonPropertyName("following_count")]
        public int following_count { get; set; }

        // present on post author in your JSON
        public OwnerDto? owner { get; set; }

        [JsonPropertyName("you_follow")]
        public bool? you_follow { get; set; }
    }

    public sealed class OwnerDto
    {
        [JsonPropertyName("x_handle")]
        public string? x_handle { get; set; }

        [JsonPropertyName("x_name")]
        public string? x_name { get; set; }

        [JsonPropertyName("x_bio")]
        public string? x_bio { get; set; }

        [JsonPropertyName("x_follower_count")]
        public int x_follower_count { get; set; }

        [JsonPropertyName("x_verified")]
        public bool x_verified { get; set; }
    }

    public sealed class CommentDto
    {
        public string? id { get; set; }
        public string? content { get; set; }

        [JsonPropertyName("parent_id")]
        public string? parent_id { get; set; }

        public int upvotes { get; set; }
        public int downvotes { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset? created_at { get; set; }

        [JsonPropertyName("author_id")]
        public string? author_id { get; set; }

        public CommentAuthorDto? author { get; set; }

        public List<CommentDto>? replies { get; set; }
    }

    public sealed class CommentAuthorDto
    {
        public string? id { get; set; }
        public string? name { get; set; }
        public int karma { get; set; }

        [JsonPropertyName("follower_count")]
        public int follower_count { get; set; }
    }

    public sealed class ContextDto
    {
        public string? tip { get; set; }
    }
}
