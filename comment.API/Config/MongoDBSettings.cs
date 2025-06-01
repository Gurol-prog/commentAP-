namespace Comment.API.Config
{
    public class MongoDBSettings
    {
        public string ConnectionString { get; set; } = null!;
        public string DatabaseName { get; set; } = null!;
        public string ContentCommentsCollectionName { get; set; } = null!;
        public string CommentVotesCollectionName { get; set; } = null!;
        public string CommentReportsCollectionName { get; set; } = null!;
        public string ParentCollectionName { get; set; } = null!;
    }
}
