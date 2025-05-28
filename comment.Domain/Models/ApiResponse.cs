namespace Comment.Domain.Models
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }         // İşlem başarılı mı?
        public string Message { get; set; }       // İşlem hakkında bilgilendirme mesajı
        public T Data { get; set; }               // Gerçek veriler (varsa)
        public List<string> Errors { get; set; }  // Hata mesajları (başarısız işlemler için)
        public int StatusCode { get; set; }       // HTTP statü kodu

        public ApiResponse()
        {
            Errors = new List<string>();
        }

        // Genel başarılı yanıt
        public static ApiResponse<T> SuccessResponse(T data, string message = null, int statusCode = 200)
        {
            return new ApiResponse<T>
            {
                Data = data,
                Success = true,
                Message = message ?? "Request successful",
                StatusCode = statusCode
            };
        }

        // Login işlemi için başarılı yanıt
        public static ApiResponse<object> SuccessResponseWithToken(string accessToken, string refreshToken, string message = null, int statusCode = 200)
        {
            return new ApiResponse<object>
            {
                Data = new
                {
                    accessToken,
                    refreshToken
                },
                Success = true,
                Message = message ?? "Login successful",
                StatusCode = statusCode
            };
        }

        // Başarısız yanıt
        public static ApiResponse<T> ErrorResponse(List<string> errors, string message = null, int statusCode = 400)
        {
            return new ApiResponse<T>
            {
                Errors = errors,
                Message = message ?? "Request failed",
                Success = false,
                StatusCode = statusCode
            };
        }

        // Veri bulunamadığında yanıt (örneğin 204 veya 404)
        public static ApiResponse<T> NoContentResponse(string message = null, int statusCode = 204)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message ?? "No content",
                StatusCode = statusCode
            };
    }
    }
}