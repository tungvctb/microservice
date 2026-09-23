namespace Notification.Domain.Enums;

public enum NotificationSeverity { Info = 0, Success = 1, Warning = 2, Error = 3 }

/// <summary>Kênh nhận. Bản demo chỉ đẩy realtime; Email/Sms để sẵn chỗ mở rộng.</summary>
public enum NotificationChannel { InApp = 0, Email = 1, Sms = 2 }
