namespace CustomMuseumFramework.Models;

public class DonationInfo(bool isValidDonation, bool isDonated)
{
    public bool IsValidDonation = isValidDonation;
    public bool IsDonated = isDonated;
}