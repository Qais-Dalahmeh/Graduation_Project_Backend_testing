using System.ComponentModel.DataAnnotations;

namespace Graduation_Project_Backend.DTOs
{
    public sealed class AddTransactionDto
    {
        [Required(ErrorMessage = "Phone number is required")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Store ID is required")]
        public Guid StoreId { get; set; }

        [Required(ErrorMessage = "Receipt ID is required")]
        public string ReceiptId { get; set; } = string.Empty;

        public string? ReceiptDescription { get; set; }

        [Required(ErrorMessage = "Price is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Price must be non-negative")]
        public decimal Price { get; set; }

        // Extra fields accepted from clients but not required by the backend logic
        public Guid? MallID { get; set; }
        public DateTimeOffset? MadeAt { get; set; }
        public DateTimeOffset? CreatedAt { get; set; }
    }
}
