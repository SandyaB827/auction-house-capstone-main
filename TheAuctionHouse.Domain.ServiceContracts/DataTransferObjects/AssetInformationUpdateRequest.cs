using System.ComponentModel.DataAnnotations;

    public class AssetInformationUpdateRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int RetailPrice { get; set; }
    }