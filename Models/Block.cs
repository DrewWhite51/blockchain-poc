using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Models;

public class Block
{
    public int Index {get; set;}
    public DateTime Timestamp {get; set;}
    public List<Transaction> Transactions {get; set;}
    public string PreviousHash {get; set;}
    public string Hash {get; set;}
    public int Nonce {get; set;}

    public Block(int index,  List<Transaction> transactions, string previousHash)
    {
        Index = index;
        Timestamp = DateTime.Now;
        Transactions = transactions;
        PreviousHash = previousHash;
        Hash = "";
        Nonce = 0;
    }

    public string CalculateHash()
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            string transactionData = string.Join("", Transactions.Select(t => t.TransactionId));
            string rawData = $"{Index}{Timestamp}{transactionData}{PreviousHash}{Nonce}";
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            return Convert.ToHexString(bytes);
        }
    }

    public void MineBlock(int difficulty)
    {
        string target = new string('0', difficulty);
        Console.WriteLine($"Mining block {Index}...");

        // Keep trying different nonces until we find a hash that starts with target
        do
        {
            Nonce++;
            Hash = CalculateHash();
        } while (!Hash.StartsWith(target));

        Console.WriteLine($"Block mined! Hash: {Hash}");
        Console.WriteLine($"Nonce: {Nonce}");
    }

    public override string ToString()
    {
        return $"Block #{Index} [{Hash.Substring(0, 10)}...] - {Transactions.Count} transactions";    }
}