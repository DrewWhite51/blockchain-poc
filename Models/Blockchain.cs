using System;
using System.Collections.Generic;
using System.Linq;

namespace Models;

public class BlockChain
{
    public List<Block> Chain { get; set; }
    public int Difficulty { get; set; }
    public List<Transaction> PendingTransactions { get; set; }
    public double MiningReward { get; set; }

    public BlockChain(int difficulty = 2, double miningReward = 100.0)
    {
        Chain = new List<Block> ();
        Difficulty = difficulty;
        PendingTransactions = new List<Transaction>();
        MiningReward = miningReward;

        // Create genesis block
        Chain.Add(CreateGenesisBlock());
    }

    private Block CreateGenesisBlock()
    {
        var genesisTransactions = new List<Transaction>
        {
            new Transaction("network", "genesis", 0)
        };
        var genesisBlock = new Block(0, genesisTransactions, "0");
        genesisBlock.MineBlock(Difficulty);
        return genesisBlock;
    }

    public Block GetLatestBlock()
    {
        return Chain[Chain.Count - 1];
    }

    public void MinePendingTransactions(string minerAddress)
    {
        // Add mining reward transaction
        PendingTransactions.Add(new Transaction("network", minerAddress, MiningReward));
        
        // Create new block with pending transactions
        Block newBlock = new Block(Chain.Count, PendingTransactions, GetLatestBlock().Hash);
        newBlock.MineBlock(Difficulty);
        
        Chain.Add(newBlock);
        
        // Clear pending transactions
        PendingTransactions = new List<Transaction>();
        
        Console.WriteLine($"Block successfully mined and added to chain!");
    }

    public void AddTransaction(Transaction transaction)
    {
        // Basic validation
        if (string.IsNullOrEmpty(transaction.Sender) || string.IsNullOrEmpty(transaction.Recipient))
        {
            throw new Exception("Transaction must include sender and recipient");
        }

        if (transaction.Amount <= 0)
        {
            throw new Exception("Transaction amount must be positive");
        }

        PendingTransactions.Add(transaction);
        Console.WriteLine($"Transaction added to pending pool: {transaction}");
    }

    public bool IsChainValid()
    {
        // Check each block (skip genesis)
        for (int i = 1; i < Chain.Count; i++)
        {
            Block currentBlock = Chain[i];
            Block previousBlock = Chain[i - 1];

            // Verify current block's hash is correct
            if (currentBlock.Hash != currentBlock.CalculateHash())
            {
                Console.WriteLine($"Block {i} has invalid hash");
                return false;
            }

            // Verify link to previous block
            if (currentBlock.PreviousHash != previousBlock.Hash)
            {
                Console.WriteLine($"Block {i} has invalid previous hash");
                return false;
            }

            // Verify proof of work
            string target = new string('0', Difficulty);
            if (!currentBlock.Hash.StartsWith(target))
            {
                Console.WriteLine($"Block {i} doesn't meet difficulty requirement");
                return false;
            }
        }

        return true;
    }

    public double GetBalance(string address)
    {
        double balance = 0;

        foreach (var block in Chain)
        {
            foreach (var transaction in block.Transactions)
            {
                if (transaction.Sender == address)
                {
                    balance -= transaction.Amount;
                }
                if (transaction.Recipient == address)
                {
                    balance += transaction.Amount;
                }
            }
        }

        return balance;
    }

    public void PrintChain()
    {
        Console.WriteLine("\n===== BLOCKCHAIN =====");
        foreach (var block in Chain)
        {
            Console.WriteLine($"\n{block}");
            Console.WriteLine($"  Timestamp: {block.Timestamp}");
            Console.WriteLine($"  Previous Hash: {block.PreviousHash.Substring(0, 10)}...");
            Console.WriteLine($"  Nonce: {block.Nonce}");
            Console.WriteLine($"  Transactions:");
            foreach (var tx in block.Transactions)
            {
                Console.WriteLine($"    - {tx}");
            }
        }
        Console.WriteLine("======================\n");
    }

 }