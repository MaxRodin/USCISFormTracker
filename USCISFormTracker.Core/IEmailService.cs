using USCISFormTracker.Core.Models;

namespace USCISFormTracker.Core;

public interface IEmailService
{
    Task SendChangeNotificationAsync(PdfFormChange change, DiffLines diffLines);
    Task SendAddedFormNotificationAsync(PdfFormRecord newForm);
    Task SendDeletedFormNotificationAsync(string link, string formName, DateTime lastSeen);
}
