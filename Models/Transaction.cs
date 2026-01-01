using System;
using System.Security.Cryptography;
using System.Text;

namespace Models;

public class Transaction
{
    public string Sender { get; set; }
    public string Recipient { get; set; }
    public double Amount { get; set; }
    public DateTime Timestamp { get; set; }
    public string TransactionId { get; set; }

    public Transaction(string sender, string recipient, double amount)
    {
        Sender = sender;
        Recipient = recipient;
        Amount = amount;
        Timestamp = DateTime.UtcNow;
        TransactionId = CalculateHash();
    }

    public string CalculateHash()
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            string rawData = $"{Sender}{Recipient}{Amount}{Timestamp}";
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            StringBuilder builder = new StringBuilder();
            return Convert.ToHexString(bytes);
        }
        
    }

    public override string ToString()
    {
        return $"{Sender} -> {Recipient}: {Amount} at {Timestamp} (ID: {TransactionId})";
    }
}