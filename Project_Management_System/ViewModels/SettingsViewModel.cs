namespace Project_Management_System.ViewModels
{
    public class SettingsViewModel
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
        public string? ProfilePictureUrl { get; set; }
        public string Phone { get; set; } = "";

        public bool HasCustomPicture =>
            !string.IsNullOrWhiteSpace(ProfilePictureUrl);
    }
}