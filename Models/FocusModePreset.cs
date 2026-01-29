namespace DongNoti.Models
{
    public class FocusModePreset
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Minutes { get; set; }

        public FocusModePreset()
        {
        }

        public FocusModePreset(string id, string displayName, int minutes)
        {
            Id = id;
            DisplayName = displayName;
            Minutes = minutes;
        }
    }
}
