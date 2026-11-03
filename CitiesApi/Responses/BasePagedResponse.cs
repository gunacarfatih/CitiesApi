namespace CitiesApi.Responses
{
    public class BasePagedResponse<T> : BaseResponse<T>
    {
        public PaginationMetadata? Pagination { get; set; }
    }
}