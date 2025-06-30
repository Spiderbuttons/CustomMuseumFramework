namespace CustomMuseumFramework.Models;

public class DonationInfo
{
    public bool IsValidDonation = false;
    public bool IsDonated = false;
    
    public DonationInfo(bool isValidDonation, bool isDonated)
    {
        IsValidDonation = isValidDonation;
        IsDonated = isDonated;
    }
}